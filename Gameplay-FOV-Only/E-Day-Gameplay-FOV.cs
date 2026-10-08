using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Security.Cryptography;
using System.Text;

// Gameplay-only runtime data adjustment for the Steam full game, CL 4894958.
// No executable files are modified. Closing the game resets this change.
public static class GameplayFovFix
{
    internal sealed class NotReadyException : InvalidOperationException
    {
        public NotReadyException(string message) : base(message) { }
    }
    internal sealed class BuildDeclinedException : InvalidOperationException { }
    static string approvedBuild;
    static bool hiddenWatcher;
    internal static bool AskDifferentBuild(string version,int size,TextReader input,TextWriter output)
    {
        output.WriteLine("This game build has not been tested with the gameplay FOV fix.");
        output.WriteLine("Detected: "+version+" (module size "+size+")");
        output.WriteLine("Tested: "+SupportedVersion+" (module size 322289664)");
        output.WriteLine("Continuing may fail or cause incorrect framing or a game crash. Compatibility checks remain enabled.");
        while (true)
        {
            output.Write("Continue trying the mod for this game session? [Yes/No]: "); output.Flush();
            string answer=input.ReadLine();
            if (answer==null) return false;
            answer=answer.Trim();
            if (String.Equals(answer,"yes",StringComparison.OrdinalIgnoreCase) || String.Equals(answer,"y",StringComparison.OrdinalIgnoreCase)) return true;
            if (answer.Length==0 || String.Equals(answer,"no",StringComparison.OrdinalIgnoreCase) || String.Equals(answer,"n",StringComparison.OrdinalIgnoreCase)) return false;
            output.WriteLine("Please enter Yes or No.");
        }
    }
    static string BuildToken(Process game,ProcessModule main)
    {
        string identity=game.Id+"|"+game.StartTime.ToUniversalTime().Ticks+"|"+main.FileVersionInfo.FileVersion+"|"+main.ModuleMemorySize;
        using (var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-","");
    }
    internal static string WatcherBuildArgument(Process game)
    {
        string token=BuildToken(game,game.MainModule);
        return approvedBuild==token ? " --build-consent "+token : "";
    }
    const string SupportedVersion = "+++fenix2+fairlight-omega-release-CL-4894958";
    [DllImport("kernel32", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32", SetLastError=true)] static extern bool ReadProcessMemory(IntPtr h, IntPtr address, byte[] data, UIntPtr size, out UIntPtr count);
    [DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
    internal static byte[] Read(IntPtr h, IntPtr address, int length)
    {
        byte[] data = new byte[length]; UIntPtr count;
        if (!ReadProcessMemory(h, address, data, (UIntPtr)length, out count) || count.ToUInt64() != (ulong)length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read game memory.");
        return data;
    }
    internal static void Validate(Process game)
    {
        if (game.ProcessName != "GoWEDay-Steam") throw new InvalidOperationException("Target is not GoWEDay-Steam.exe.");
        if (Process.GetProcessesByName("EasyAntiCheat_EOS").Length != 0)
            throw new InvalidOperationException("Close the protected game and use the offline gameplay FOV launcher.");
        ProcessModule main = game.MainModule;
        string directory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        if (!String.Equals(Path.GetDirectoryName(main.FileName), directory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Place this helper beside the running GoWEDay-Steam.exe.");
        foreach (ProcessModule module in game.Modules)
            if (module.ModuleName.IndexOf("EasyAntiCheat", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("The game has anti-cheat loaded. Use the offline gameplay FOV launcher.");
        bool differentBuild=main.FileVersionInfo.FileVersion!=SupportedVersion || main.ModuleMemorySize!=322289664;
        if (differentBuild && approvedBuild!=BuildToken(game,main))
        {
            if (hiddenWatcher) throw new InvalidOperationException("Different game build has no approval for this session. No changes made.");
            if (!AskDifferentBuild(main.FileVersionInfo.FileVersion,main.ModuleMemorySize,Console.In,Console.Out)) throw new BuildDeclinedException();
            approvedBuild=BuildToken(game,main);
        }
        IntPtr h = OpenProcess(0x410u, false, game.Id);
        if (h == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot access the game process.");
        try
        {
            long basis = main.BaseAddress.ToInt64();
            try
            {
                GameplayRuntime.ValidateMarkers(h, basis);
                GameplayFov.ValidateMarkers(h, basis);
            }
            catch (NotReadyException ex)
            {
                if (differentBuild) throw new InvalidOperationException("This build's memory layout is incompatible: "+ex.Message+" An updated mod is required.");
                throw;
            }
            catch (Win32Exception ex)
            {
                if (differentBuild) throw new InvalidOperationException("This build's memory layout could not be validated. No changes made. An updated mod may be required. "+ex.Message);
                throw;
            }
            long managerClass=GameplayRuntime.Q(h,basis+0xed6a080);
            if (managerClass==0) throw new NotReadyException("Gameplay camera class is not initialized yet.");
            new GameplayRuntime.Objects(h,basis);
        }
        finally { CloseHandle(h); }
    }
    // Supported builds may initialize their data after the process and code are readable.
    // Readiness failures never write memory. A wait of -1 retries until game exit.
    internal static void UntilReady(Process game,int wait,Action attempt)
    {
        Stopwatch timer=Stopwatch.StartNew(); bool announced=false;
        while (true)
        {
            try { attempt(); return; }
            catch (Win32Exception) { if (wait==0 || (wait>0 && timer.Elapsed.TotalSeconds>=wait) || game.HasExited) throw; }
            catch (NotReadyException) { if (wait==0 || (wait>0 && timer.Elapsed.TotalSeconds>=wait) || game.HasExited) throw; }
            if (!announced) { Console.WriteLine("Waiting for gameplay initialization"+(wait<0 ? " until ready or game exit." : ".")); announced=true; }
            Thread.Sleep(500);
        }
    }
    public static int Main(string[] args)
    {
        try
        {
            int pid = 0, wait = 0; bool check = false, restore = false, watch = false; string consent=null;
            approvedBuild=null; hiddenWatcher=false;
            for (int i=0; i<args.Length; i++)
            {
                if (args[i] == "--pid" && i+1<args.Length) pid = int.Parse(args[++i]);
                else if (args[i] == "--wait" && i+1<args.Length)
                { string value=args[++i]; wait=String.Equals(value,"forever",StringComparison.OrdinalIgnoreCase) ? -1 : int.Parse(value); }
                else if (args[i] == "--check") check = true;
                else if (args[i] == "--restore") restore = true;
                else if (args[i] == "--watch") watch = true;
                else if (args[i] == "--build-consent" && i+1<args.Length) consent=args[++i];
                else throw new ArgumentException("Usage: E-Day-Gameplay-FOV.exe [--pid ID] [--wait SECONDS|forever] [--check | --restore]");
            }
            if (wait < -1 || wait > 180 || (check && restore) || (watch && (check || restore)) || (consent!=null && (!watch || consent.Length!=64 || !consent.All(Uri.IsHexDigit)))) throw new ArgumentException("Invalid arguments.");
            hiddenWatcher=watch; approvedBuild=consent;
            Process game;
            if (pid != 0) game = Process.GetProcessById(pid);
            else
            {
                Process[] games = Process.GetProcessesByName("GoWEDay-Steam");
                if (games.Length != 1) throw new InvalidOperationException("Exactly one GoWEDay-Steam.exe must be running.");
                game = games[0];
            }
            using (game)
            {
                if (restore)
                {
                    if (game.ProcessName!="GoWEDay-Steam") throw new InvalidOperationException("Target is not GoWEDay-Steam.exe.");
                    GameplayRuntime.Stop(game);
                }
                else UntilReady(game,wait,delegate
                {
                    game.Refresh(); Validate(game);
                    if (watch) GameplayRuntime.Watch(game);
                    else if (check) Console.WriteLine(GameplayRuntime.Running(game) ? "Gameplay FOV helper is active." : "Gameplay FOV helper is not running.");
                    else GameplayRuntime.Start(game);
                });
                return 0;
            }
        }
        catch (BuildDeclinedException) { Console.WriteLine("gameplay FOV fix skipped: unsupported build declined. No changes made. The game can continue without the mod."); return 2; }
        catch (Exception ex) { Console.Error.WriteLine("gameplay FOV fix: " + ex.Message); return 1; }
    }
}
