using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

// Gameplay-only runtime data adjustment for the Steam full game, CL 4927986, with validated dynamic address discovery.
// No executable files are modified. Closing the game resets this change.
public static class GameplayFovFix
{
    internal sealed class NotReadyException : InvalidOperationException
    {
        public NotReadyException(string message) : base(message) { }
    }
    static string lastBuildAnnouncement;
    internal static void AnnounceBuild(string version,int size,System.IO.TextWriter output)
    {
        if(version==SupportedVersion && size==323710976) return;
        string identity=version+"|"+size;
        if(lastBuildAnnouncement==identity) return;
        output.WriteLine("Untested game build detected: "+version+" (module size "+size+"). Continuing with required camera layout validation.");
        lastBuildAnnouncement=identity;
    }
    const string SupportedVersion = "+++fenix2+fairlight-omega-release-CL-4927986";
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
        AnnounceBuild(main.FileVersionInfo.FileVersion,main.ModuleMemorySize,Console.Out);
        IntPtr h = OpenProcess(0x410u, false, game.Id);
        if (h == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot access the game process.");
        try
        {
            long basis = main.BaseAddress.ToInt64();
            GameLayout.Resolve(h,basis,main.ModuleMemorySize,false,true);
            GameplayRuntime.ValidateMarkers(h,basis);
            GameplayFov.ValidateMarkers(h,basis);
            long managerClass=GameplayRuntime.Q(h,basis+GameLayout.Required.ManagerClass);
            if (managerClass==0) throw new NotReadyException("Gameplay camera class is not initialized yet.");
            new GameplayRuntime.Objects(h,basis);
        }
        finally { CloseHandle(h); }
    }
    // Supported builds may initialize their data after the process and code are readable.
    // Readiness failures never write memory. A wait of -1 retries until game exit.
    internal static void UntilReady(Process game,int wait,Action attempt)
    {
        Stopwatch timer=Stopwatch.StartNew(); bool announced=false; string previousReason=null;
        while (true)
        {
            try { attempt(); return; }
            catch (Win32Exception) { if (wait==0 || (wait>0 && timer.Elapsed.TotalSeconds>=wait) || game.HasExited) throw; }
            catch (GameLayout.InitializingException ex)
            {
                if (wait==0 || (wait>0 && timer.Elapsed.TotalSeconds>=wait) || game.HasExited) throw;
                if(previousReason!=ex.Message) { Console.WriteLine(ex.Message); previousReason=ex.Message; }
            }
            catch (NotReadyException) { if (wait==0 || (wait>0 && timer.Elapsed.TotalSeconds>=wait) || game.HasExited) throw; }
            if (!announced) { Console.WriteLine("Waiting for gameplay initialization"+(wait<0 ? " until ready or game exit." : ".")); announced=true; }
            Thread.Sleep(500);
        }
    }
    public static int Main(string[] args)
    {
        try
        {
            int pid = 0, wait = 0; bool check = false, restore = false, watch = false;
            lastBuildAnnouncement=null;
            for (int i=0; i<args.Length; i++)
            {
                if (args[i] == "--pid" && i+1<args.Length) pid = int.Parse(args[++i]);
                else if (args[i] == "--wait" && i+1<args.Length)
                { string value=args[++i]; wait=String.Equals(value,"forever",StringComparison.OrdinalIgnoreCase) ? -1 : int.Parse(value); }
                else if (args[i] == "--check") check = true;
                else if (args[i] == "--restore") restore = true;
                else if (args[i] == "--watch") watch = true;
                else throw new ArgumentException("Usage: E-Day-Gameplay-FOV.exe [--pid ID] [--wait SECONDS|forever] [--check | --restore]");
            }
            if (wait < -1 || wait > 180 || (check && restore) || (watch && (check || restore))) throw new ArgumentException("Invalid arguments.");
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
        catch (Exception ex) { Console.Error.WriteLine("gameplay FOV fix: " + ex.Message); return 1; }
    }
}
