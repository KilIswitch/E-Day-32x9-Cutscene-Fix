using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

// Contains object identity and gameplay watcher support only. No cinematic writes.
internal static class GameplayRuntime
{
    const long ObjectManagerRva=0xe830dc0;
    const ulong ObjectManagerKey=0x5f85652ab88d6999;
    [DllImport("kernel32", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32", SetLastError=true)] static extern bool WriteProcessMemory(IntPtr h, IntPtr address, byte[] data, UIntPtr size, out UIntPtr count);
    internal static byte[] Read(IntPtr h, long address, int size) { return GameplayFovFix.Read(h, new IntPtr(address), size); }
    internal static long Q(IntPtr h, long address) { return BitConverter.ToInt64(Read(h,address,8),0); }
    internal static int I(IntPtr h, long address) { return BitConverter.ToInt32(Read(h,address,4),0); }
    internal static void Write(IntPtr h, long address, byte[] data)
    {
        UIntPtr count;
        if (!WriteProcessMemory(h,new IntPtr(address),data,(UIntPtr)data.Length,out count) || count.ToUInt64()!=(ulong)data.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not update gameplay camera data.");
        if (!Read(h,address,data.Length).SequenceEqual(data)) throw new InvalidOperationException("Camera data readback failed.");
    }
    public static void ValidateMarkers(IntPtr h,long basis)
    {
        // Object array decoding and the gameplay camera manager class reference.
        if (!Read(h,basis+0x3cd33ad,17).SequenceEqual(new byte[]{0x4c,0x8b,0x15,0x0c,0xda,0xb5,0x0a,0x48,0xbe,0x99,0x69,0x8d,0xb8,0x2a,0x65,0x85,0x5f}) ||
            !Read(h,basis+0x162360f,7).SequenceEqual(new byte[]{0x48,0x8b,0x05,0x6a,0x6a,0x74,0x0d}))
            throw new GameplayFovFix.NotReadyException("Gameplay object signatures differ. No changes made.");
    }
    static string SharedName(Process game) { return "Local\\EDay32x9_"+game.Id+"_"+game.StartTime.ToUniversalTime().Ticks+"_Camera"; }
    static string Name(Process game, string kind) { return "Local\\EDayGameplayFov_"+game.Id+"_"+game.StartTime.ToUniversalTime().Ticks+"_"+kind; }
    public static bool Running(Process game)
    {
        try { using (Mutex.OpenExisting(Name(game,"Camera"))) { return true; } }
        catch (WaitHandleCannotBeOpenedException) { return false; }
    }
    public static void Start(Process game)
    {
        if (Running(game)) { Console.WriteLine("Gameplay FOV adjustment is already running."); return; }
        try { using (Mutex.OpenExisting(SharedName(game))) throw new InvalidOperationException("Another E-Day camera helper is active. Restore it or close the game before using this version."); }
        catch (WaitHandleCannotBeOpenedException) { }
        using (var ready=new EventWaitHandle(false,EventResetMode.ManualReset,Name(game,"Ready")))
        {
            var info=new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--pid "+game.Id+" --watch"+GameplayFovFix.WatcherBuildArgument(game));
            info.UseShellExecute=true; info.WindowStyle=ProcessWindowStyle.Hidden; info.WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory;
            using (Process child=Process.Start(info))
            {
                if (!ready.WaitOne(10000) || child.HasExited) throw new InvalidOperationException("Could not start the gameplay FOV helper.");
            }
        }
        Console.WriteLine("Gameplay FOV adjustment is running in the background until game exit.");
    }
    public static void Stop(Process game)
    {
        try
        {
            using (var signal=EventWaitHandle.OpenExisting(Name(game,"Stop"))) signal.Set();
            using (var mutex=Mutex.OpenExisting(Name(game,"Camera")))
            {
                try { if (!mutex.WaitOne(10000)) throw new InvalidOperationException("Gameplay FOV helper did not stop; restore was cancelled."); }
                catch (AbandonedMutexException) { }
                mutex.ReleaseMutex();
            }
            Console.WriteLine("Gameplay FOV setting restored.");
        }
        catch (WaitHandleCannotBeOpenedException) { }
    }
    internal class Ref { public long Address; public int Index,Serial; }
    internal sealed class Objects
    {
        public IntPtr H; public long Chunks; public int Count;
        public Objects(IntPtr h,long basis)
        {
            H=h; long manager=unchecked((long)((ulong)Q(h,basis+ObjectManagerRva)^ObjectManagerKey));
            byte[] header=Read(h,manager,40); Chunks=BitConverter.ToInt64(header,16); Count=BitConverter.ToInt32(header,36);
            if (Count<0 || Count>4000000 || Chunks<0x10000) throw new InvalidOperationException("Invalid game object array.");
        }
        public byte[] Item(int index)
        {
            if (index<0 || index>=Count) return null;
            return Read(H,Q(H,Chunks+(index>>16)*8)+(index&65535)*24,24);
        }
        public bool Valid(Ref value, bool requireSerial)
        {
            byte[] item=Item(value.Index);
            if (item!=null && value.Serial==0 && BitConverter.ToInt64(item,0)==value.Address && (BitConverter.ToUInt32(item,8)&0x10200000)==0)
                value.Serial=BitConverter.ToInt32(item,16);
            return item!=null && BitConverter.ToInt64(item,0)==value.Address && (BitConverter.ToUInt32(item,8)&0x10200000)==0 &&
                (!requireSerial || BitConverter.ToInt32(item,16)==value.Serial);
        }
        public List<Ref> FindNative(long cls, bool derived=false)
        {
            var found=new List<Ref>();
            var classes=new Dictionary<long,bool>(); int depth=derived ? I(H,cls+0x38) : 0;
            for (int c=0;c<(Count+65535)/65536;c++)
            {
                int n=Math.Min(65536,Count-c*65536); byte[] chunk=Read(H,Q(H,Chunks+c*8),n*24);
                for (int j=0;j<n;j++)
                {
                    long obj=BitConverter.ToInt64(chunk,j*24);
                    if (obj==0 || (BitConverter.ToUInt32(chunk,j*24+8)&0x10200000)!=0) continue;
                    byte[] head;
                    try { head=Read(H,obj,24); } catch (Win32Exception) { continue; }
                    long actual=BitConverter.ToInt64(head,16); bool match=actual==cls;
                    if (derived && !classes.TryGetValue(actual,out match))
                    {
                        try { match=I(H,actual+0x38)>=depth && Q(H,Q(H,actual+0x30)+depth*8)==cls+0x30; }
                        catch (Win32Exception) { match=false; }
                        classes[actual]=match;
                    }
                    if (match && (BitConverter.ToUInt32(head,8)&0x30)==0 && BitConverter.ToInt32(head,12)==c*65536+j)
                        found.Add(new Ref{Address=obj,Index=c*65536+j,Serial=BitConverter.ToInt32(chunk,j*24+16)});
                }
            }
            return found;
        }
    }
    public static void Watch(Process game)
    {
        using (var shared=new Mutex(false,SharedName(game)))
        using (var mutex=new Mutex(false,Name(game,"Camera")))
        {
            bool owned;
            try { owned=shared.WaitOne(0); } catch (AbandonedMutexException) { owned=true; }
            if (!owned) throw new InvalidOperationException("Another E-Day camera helper is active.");
            IntPtr h=IntPtr.Zero; GameplayFov mod=null;
            using (var stop=new EventWaitHandle(false,EventResetMode.ManualReset,Name(game,"Stop")))
            using (var ready=new EventWaitHandle(false,EventResetMode.ManualReset,Name(game,"Ready")))
            try
            {
                mutex.WaitOne();
                h=OpenProcess(0x438,false,game.Id); if (h==IntPtr.Zero) throw new Win32Exception();
                long basis=game.MainModule.BaseAddress.ToInt64(); ValidateMarkers(h,basis);
                mod=new GameplayFov(h,basis);
                var managers=new List<Ref>(); var search=Stopwatch.StartNew(); bool searched=false;
                ready.Set();
                while (!stop.WaitOne(20) && !game.HasExited)
                {
                    try
                    {
                        var objects=new Objects(h,basis);
                        // Use serial checks so recycled objects are not mistaken for prior managers.
                        managers.RemoveAll(value=>!objects.Valid(value,true));
                        if (managers.Count==0 && (!searched || search.ElapsedMilliseconds>=3000))
                        {
                            long cls=Q(h,basis+0xed6a080);
                            if (cls!=0) managers=objects.FindNative(cls,true);
                            searched=true; search.Restart();
                        }
                        mod.Update(objects,managers);
                    }
                    catch (Win32Exception) { if (game.HasExited) break; }
                }
            }
            finally
            {
                try
                {
                    if (h!=IntPtr.Zero && mod!=null && !game.HasExited)
                        mod.Restore(new Objects(h,game.MainModule.BaseAddress.ToInt64()));
                }
                finally
                {
                    if (h!=IntPtr.Zero) CloseHandle(h);
                    mutex.ReleaseMutex(); shared.ReleaseMutex();
                }
            }
        }
    }
    internal static bool Same(float a,float b) { return Math.Abs(a-b)<0.00001f; }
}
