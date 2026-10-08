using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Security.Cryptography;
using System.Text;

// Runtime cinematic fix for the Steam full game, CL 4894958.
// No executable files are modified. Closing the game resets this change.
public static class CinematicFix
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
        output.WriteLine("This game build has not been tested with the 32:9 fix.");
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
    const long ContextRva = 0x08d6f491, LimitRva = 0x0e2189dc;
    static readonly byte[] Context = { 0x41,0xc6,0x86,0x06,0x02,0x00,0x00,0x01,
        0xc4,0xc1,0x7a,0x10,0x86,0x08,0x02,0x00,0x00,
        0xc5,0xfa,0x5d,0x0d,0x32,0x95,0x4a,0x05,
        0xc5,0xf8,0x29,0x74,0x24,0x30,
        0xc5,0xf2,0x5f,0x35,0xe8,0xa9,0x07,0x04 };
    static readonly byte[] Original = { 0x8e,0xe3,0x18,0x40 };
    static readonly byte[] Wide = { 0x39,0x8e,0x63,0x40 };
    [DllImport("kernel32", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32", SetLastError=true)] static extern bool ReadProcessMemory(IntPtr h, IntPtr address, byte[] data, UIntPtr size, out UIntPtr count);
    [DllImport("kernel32", SetLastError=true)] static extern bool WriteProcessMemory(IntPtr h, IntPtr address, byte[] data, UIntPtr size, out UIntPtr count);
    [DllImport("kernel32", SetLastError=true)] static extern bool VirtualProtectEx(IntPtr h, IntPtr address, UIntPtr size, uint protection, out uint previous);
    [DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
    internal static byte[] Read(IntPtr h, IntPtr address, int length)
    {
        byte[] data = new byte[length]; UIntPtr count;
        if (!ReadProcessMemory(h, address, data, (UIntPtr)length, out count) || count.ToUInt64() != (ulong)length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read game memory.");
        return data;
    }
    internal static void Apply(Process game, bool check, bool restore)
    {
        if (game.ProcessName != "GoWEDay-Steam") throw new InvalidOperationException("Target is not GoWEDay-Steam.exe.");
        if (Process.GetProcessesByName("EasyAntiCheat_EOS").Length != 0)
            throw new InvalidOperationException("Close the protected game and use the offline 32:9 launcher.");
        ProcessModule main = game.MainModule;
        string directory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        if (!String.Equals(Path.GetDirectoryName(main.FileName), directory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Place this helper beside the running GoWEDay-Steam.exe.");
        foreach (ProcessModule module in game.Modules)
            if (module.ModuleName.IndexOf("EasyAntiCheat", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("The game has anti-cheat loaded. Use the offline 32:9 launcher.");
        bool differentBuild=main.FileVersionInfo.FileVersion!=SupportedVersion || main.ModuleMemorySize!=322289664;
        if (differentBuild && approvedBuild!=BuildToken(game,main))
        {
            if (hiddenWatcher) throw new InvalidOperationException("Different game build has no approval for this session. No changes made.");
            if (!AskDifferentBuild(main.FileVersionInfo.FileVersion,main.ModuleMemorySize,Console.In,Console.Out)) throw new BuildDeclinedException();
            approvedBuild=BuildToken(game,main);
        }
        IntPtr h = OpenProcess(check ? 0x410u : 0x438u, false, game.Id);
        if (h == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot access the game process.");
        try
        {
            long basis = main.BaseAddress.ToInt64();
            try
            {
                if (!Read(h, new IntPtr(basis + ContextRva), Context.Length).SequenceEqual(Context))
                    throw new NotReadyException("Cinematic function signature differs. No changes made.");
                CameraFraming.ValidateMarkers(h, basis);
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
            // The vminss instruction in Context resolves exactly to this data constant.
            long resolved = basis + ContextRva + 17 + 8 + BitConverter.ToInt32(Context, 21);
            if (resolved != basis + LimitRva) throw new InvalidOperationException("Unexpected cinematic limit address.");
            IntPtr address = new IntPtr(resolved);
            byte[] before = Read(h, address, 4);
            if (!before.SequenceEqual(Original) && !before.SequenceEqual(Wide))
                throw new NotReadyException("Cinematic limit contains an unexpected value. No changes made.");
            if (check)
            {
                Console.WriteLine(before.SequenceEqual(Wide) ? "32:9 cinematic limit is active." : "Original cinematic limit is active.");
                return;
            }
            byte[] desired = restore ? Original : Wide;
            if (!before.SequenceEqual(desired))
            {
                uint previous, ignored; UIntPtr count;
                if (!VirtualProtectEx(h, address, (UIntPtr)4, 4, out previous)) throw new Win32Exception();
                try
                {
                    if (!WriteProcessMemory(h, address, desired, (UIntPtr)4, out count) || count.ToUInt64() != 4)
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not update cinematic limit.");
                }
                finally
                {
                    if (!VirtualProtectEx(h, address, (UIntPtr)4, previous, out ignored))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not restore memory protection.");
                }
                if (!Read(h, address, 4).SequenceEqual(desired)) throw new InvalidOperationException("Readback verification failed.");
            }
            Console.WriteLine(restore ? "Original cinematic limit restored." : "32:9 cinematic limit applied (5120 x 1440). Keep Cinematic Ultrawide Support enabled.");
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
            if (!announced) { Console.WriteLine("Waiting for cinematic initialization"+(wait<0 ? " until ready or game exit." : ".")); announced=true; }
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
                else throw new ArgumentException("Usage: E-Day-32x9.exe [--pid ID] [--wait SECONDS|forever] [--check | --restore]");
            }
            if (wait < -1 || wait > 180 || (check && restore) || (consent!=null && (!watch || consent.Length!=64 || !consent.All(Uri.IsHexDigit)))) throw new ArgumentException("Invalid arguments.");
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
                UntilReady(game,wait,delegate
                {
                        game.Refresh();
                        if (restore) { Apply(game,true,false); CameraFraming.Stop(game); }
                        Apply(game, check || watch, restore);
                        if (watch) CameraFraming.Watch(game);
                        else if (check) Console.WriteLine(CameraFraming.Running(game) ? "Framing helper is active." : "Framing helper is not running.");
                        else if (!restore) CameraFraming.Start(game);
                });
                return 0;
            }
        }
        catch (BuildDeclinedException) { Console.WriteLine("32:9 fix skipped: unsupported build declined. No changes made. The game can continue without the mod."); return 2; }
        catch (Exception ex) { Console.Error.WriteLine("32:9 fix: " + ex.Message); return 1; }
    }
}
