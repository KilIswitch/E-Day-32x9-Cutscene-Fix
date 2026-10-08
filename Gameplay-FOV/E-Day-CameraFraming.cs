using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

// Follows the cinematic subsystem's weak camera reference. It changes only
// camera data, never instructions, and never scans or adjusts gameplay cameras.
internal static class CameraFraming
{
    internal sealed class FastWait : WaitHandle
    {
        [DllImport("kernel32",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateWaitableTimerExW(IntPtr attributes,string name,uint flags,uint access);
        [DllImport("kernel32",SetLastError=true)] static extern bool SetWaitableTimer(IntPtr timer,ref long due,int period,IntPtr callback,IntPtr argument,bool resume);
        public FastWait()
        {
            IntPtr timer=CreateWaitableTimerExW(IntPtr.Zero,null,2,0x1f0003);
            if (timer==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not create the camera transition timer.");
            SafeWaitHandle=new Microsoft.Win32.SafeHandles.SafeWaitHandle(timer,true);
        }
        public bool Wait(EventWaitHandle stop,int milliseconds)
        {
            long due=-10000L*milliseconds;
            if (!SetWaitableTimer(SafeWaitHandle.DangerousGetHandle(),ref due,0,IntPtr.Zero,IntPtr.Zero,false)) throw new Win32Exception();
            return WaitHandle.WaitAny(new WaitHandle[]{stop,this})==1;
        }
    }
    const long ObjectManagerRva = 0xe830dc0, ClassRva = 0xeba4dc0, CineVtableRva = 0xd365980;
    const ulong ObjectManagerKey = 0x5f85652ab88d6999;
    [DllImport("kernel32", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32", SetLastError=true)] static extern bool WriteProcessMemory(IntPtr h, IntPtr address, byte[] data, UIntPtr size, out UIntPtr count);
    internal static byte[] Read(IntPtr h, long address, int size) { return CinematicFix.Read(h, new IntPtr(address), size); }
    internal static long Q(IntPtr h, long address) { return BitConverter.ToInt64(Read(h,address,8),0); }
    internal static int I(IntPtr h, long address) { return BitConverter.ToInt32(Read(h,address,4),0); }
    internal static void Write(IntPtr h, long address, byte[] data)
    {
        UIntPtr count;
        if (!WriteProcessMemory(h,new IntPtr(address),data,(UIntPtr)data.Length,out count) || count.ToUInt64()!=(ulong)data.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not update cinematic camera data.");
        if (!Read(h,address,data.Length).SequenceEqual(data)) throw new InvalidOperationException("Camera data readback failed.");
    }
    public static void ValidateMarkers(IntPtr h, long basis)
    {
        if (!Read(h,basis+0x1601beb,7).SequenceEqual(new byte[]{0x48,0x8b,0x05,0xce,0x31,0x5a,0x0d}) ||
            !Read(h,basis+0x3cd33ad,17).SequenceEqual(new byte[]{0x4c,0x8b,0x15,0x0c,0xda,0xb5,0x0a,0x48,0xbe,0x99,0x69,0x8d,0xb8,0x2a,0x65,0x85,0x5f}) ||
            !Read(h,basis+0x162360f,7).SequenceEqual(new byte[]{0x48,0x8b,0x05,0x6a,0x6a,0x74,0x0d}) ||
            !Read(h,basis+0x17114c3,7).SequenceEqual(new byte[]{0x48,0x8b,0x05,0x4e,0xc9,0x63,0x0d}) ||
            !Read(h,basis+0x8d6f3fc,7).SequenceEqual(new byte[]{0x44,0x89,0xaf,0x04,0x3a,0x00,0x00}) ||
            !Read(h,basis+0x5518dc6,13).SequenceEqual(new byte[]{0xc5,0xf8,0x28,0xd3,0xc4,0xe2,0x61,0x99,0x91,0xbc,0x02,0x00,0x00}) ||
            !Read(h,basis+0x551887a,28).SequenceEqual(new byte[]{0xc5,0xfa,0x10,0x91,0x38,0x0d,0x00,0x00,0xc5,0xf8,0x2f,0xd1,0xc5,0xfa,0x10,0x81,0xcc,0x0c,0x00,0x00,0xc5,0xfa,0x59,0x99,0xa4,0x0c,0x00,0x00}))
            throw new CinematicFix.NotReadyException("Camera framing signatures differ. No changes made.");
    }
    static string Name(Process game, string kind) { return "Local\\EDay32x9_"+game.Id+"_"+game.StartTime.ToUniversalTime().Ticks+"_"+kind; }
    public static bool Running(Process game)
    {
        try { using (Mutex.OpenExisting(Name(game,"Camera"))) { return true; } }
        catch (WaitHandleCannotBeOpenedException) { return false; }
    }
    public static void Start(Process game)
    {
        if (Running(game)) { Console.WriteLine("Cinematic framing correction is already running."); return; }
        using (var ready=new EventWaitHandle(false,EventResetMode.ManualReset,Name(game,"Ready")))
        {
            var info=new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--pid "+game.Id+" --watch"+CinematicFix.WatcherBuildArgument(game));
            info.UseShellExecute=true; info.WindowStyle=ProcessWindowStyle.Hidden; info.WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory;
            using (Process child=Process.Start(info))
            {
                if (!ready.WaitOne(10000) || child.HasExited) throw new InvalidOperationException("Could not start the cinematic framing helper.");
            }
        }
        Console.WriteLine("Cinematic framing correction is running in the background until game exit.");
    }
    public static void Stop(Process game)
    {
        try
        {
            using (var signal=EventWaitHandle.OpenExisting(Name(game,"Stop"))) signal.Set();
            using (var mutex=Mutex.OpenExisting(Name(game,"Camera")))
            {
                try { if (!mutex.WaitOne(10000)) throw new InvalidOperationException("Framing helper did not stop; restore was cancelled."); }
                catch (AbandonedMutexException) { }
                mutex.ReleaseMutex();
            }
            Console.WriteLine("Cinematic camera data restored.");
        }
        catch (WaitHandleCannotBeOpenedException) { }
    }
    internal class Ref { public long Address; public int Index,Serial; }
    internal sealed class Camera : Ref { public float Original,Applied,Aspect; public byte Scale,Crop; }
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
        using (var mutex=new Mutex(false,Name(game,"Camera")))
        {
            bool owned;
            try { owned=mutex.WaitOne(0); } catch (AbandonedMutexException) { owned=true; }
            if (!owned) return;
            IntPtr h=IntPtr.Zero; var changed=new Dictionary<string,Camera>(); GameplayFov gameplayFov=null;
            using (var stop=new EventWaitHandle(false,EventResetMode.ManualReset,Name(game,"Stop")))
            using (var ready=new EventWaitHandle(false,EventResetMode.ManualReset,Name(game,"Ready")))
            try
            {
                h=OpenProcess(0x438,false,game.Id); if (h==IntPtr.Zero) throw new Win32Exception();
                long basis=game.MainModule.BaseAddress.ToInt64(); ValidateMarkers(h,basis);
                gameplayFov=new GameplayFov(h,basis);
                var natives=new List<Ref>(); var managers=new List<Ref>(); var search=Stopwatch.StartNew(); bool searched=false;
                var config=Stopwatch.StartNew(); var sourceTimer=Stopwatch.StartNew();
                var sourceCameras=new CameraSources(h,basis); bool sourcesPrepared=false;
                float extra=ReadExtraView(); int delay=1;
                using (var tick=new FastWait())
                {
                ready.Set();
                while (tick.Wait(stop,delay) && !game.HasExited)
                {
                    try
                    {
                    Objects objects;
                    try { objects=new Objects(h,basis); } catch (Win32Exception) { continue; }
                    long cls=Q(h,basis+ClassRva);
                    natives.RemoveAll(value=>!objects.Valid(value,false) || Q(h,value.Address+16)!=cls);
                    managers.RemoveAll(value=>!objects.Valid(value,false));
                    long managerClass=Q(h,basis+0xed6a080);
                    if ((natives.Count==0 || managers.Count==0) && (!searched || search.ElapsedMilliseconds>3000))
                    {
                        if (cls!=0 && natives.Count==0) natives=objects.FindNative(cls);
                        if (managerClass!=0 && managers.Count==0) managers=objects.FindNative(managerClass,true);
                        searched=true; search.Restart();
                    }
                    gameplayFov.Update(objects,managers);
                    if (!Read(h,basis+0xe2189dc,4).SequenceEqual(new byte[]{0x39,0x8e,0x63,0x40})) break;
                    if (config.ElapsedMilliseconds>=250) { extra=ReadExtraView(); config.Restart(); }
                    bool cinematicFillActive=false;
                    var targetsByActor=new Dictionary<long,float>();
                    foreach (Ref manager in managers)
                    {
                        float fill=BitConverter.ToSingle(Read(h,manager.Address+0x3a04,4),0);
                        if (!(fill>=0 && fill<=5120f/1440f+0.001f)) continue;
                        long targets=Q(h,manager.Address+0x3e10); if (targets==0) continue;
                        byte[] target=Read(h,targets+24,8); int ti=BitConverter.ToInt32(target,0), ts=BitConverter.ToInt32(target,4);
                        byte[] targetItem=objects.Item(ti);
                        if (ts==0 || targetItem==null || BitConverter.ToInt32(targetItem,16)!=ts || (BitConverter.ToUInt32(targetItem,8)&0x10200000)!=0) continue;
                        long actor=BitConverter.ToInt64(targetItem,0); if (actor==0) continue;
                        targetsByActor[actor]=fill; if (fill>1) cinematicFillActive=true;
                    }
                    delay=cinematicFillActive ? 1 : 20;
                    if (cinematicFillActive && (!sourcesPrepared || sourceTimer.ElapsedMilliseconds>=250))
                    {
                        sourceCameras.Refresh(objects);
                        sourceCameras.Prepare(objects,targetsByActor.Values.Max(),extra,changed);
                        sourcesPrepared=true; sourceTimer.Restart();
                    }
                    var weakCameras=new List<byte[]>();
                    foreach (Ref native in natives)
                        weakCameras.Add(Read(h,native.Address+0x214,8));
                    foreach (long actor in targetsByActor.Keys)
                    {
                        if (Q(h,actor)!=basis+0xd365158) continue;
                        long component=Q(h,actor+0xce0); if (component==0) continue;
                        int componentIndex=I(h,component+12); byte[] componentItem=objects.Item(componentIndex);
                        if (componentItem==null || BitConverter.ToInt64(componentItem,0)!=component) continue;
                        byte[] weak=new byte[8]; Array.Copy(BitConverter.GetBytes(componentIndex),weak,4); Array.Copy(componentItem,16,weak,4,4);
                        weakCameras.Add(weak);
                    }
                    var visited=new HashSet<string>();
                    foreach (byte[] weak in weakCameras)
                    {
                        int index=BitConverter.ToInt32(weak,0), serial=BitConverter.ToInt32(weak,4);
                        if (index<0 || !visited.Add(index+":"+serial)) continue;
                        byte[] item=objects.Item(index); if (item==null || BitConverter.ToInt32(item,16)!=serial || (BitConverter.ToUInt32(item,8)&0x10200000)!=0) continue;
                        long addr=BitConverter.ToInt64(item,0); if (addr==0) continue;
                        byte[] body=Read(h,addr,0x2d2);
                        if (BitConverter.ToInt64(body,0)!=basis+CineVtableRva || BitConverter.ToInt32(body,12)!=index || (BitConverter.ToUInt32(body,8)&0x30)!=0) continue;
                        float viewport; long outer=BitConverter.ToInt64(body,32);
                        if (!targetsByActor.TryGetValue(outer,out viewport)) continue;
                        float aspect=BitConverter.ToSingle(body,0x2b4), current=BitConverter.ToSingle(body,0x2bc);
                        if (!(aspect>0.5f && aspect<8 && current>=0 && current<=64) || body[0x2d0]>1 || body[0x2d1]>1) continue;
                        Camera inherited=sourceCameras.Inherited(outer,current);
                        if (viewport<=1)
                        {
                            if (inherited!=null)
                            {
                                var inheritedCamera=new Camera {Address=addr,Index=index,Serial=serial,Original=inherited.Original,Applied=current,Scale=inherited.Scale,Crop=inherited.Crop};
                                RestoreCamera(h,objects,inheritedCamera);
                            }
                            continue;
                        }
                        string key=index+":"+serial; Camera saved;
                        if (!changed.TryGetValue(key,out saved))
                        {
                            if (viewport<=aspect+0.001f) continue;
                            saved=new Camera{Address=addr,Index=index,Serial=serial,Original=inherited==null ? current : inherited.Original,Applied=current,Aspect=aspect,Scale=inherited==null ? body[0x2d0] : inherited.Scale,Crop=inherited==null ? body[0x2d1] : inherited.Crop};
                            changed.Add(key,saved);
                        }
                        else
                        {
                            // Preserve new overscan values authored by cinematic animation.
                            if (!Same(current,saved.Applied)) saved.Original=current;
                            saved.Aspect=aspect;
                        }
                        float desired=(1+saved.Original)*Math.Max(1,viewport/saved.Aspect)*extra-1;
                        if (!(desired>=0 && desired<=64)) continue;
                        if (!objects.Valid(saved,true)) continue;
                        if (!Same(current,desired)) Write(h,addr+0x2bc,BitConverter.GetBytes(desired));
                        if (body[0x2d0]!=0 || body[0x2d1]!=0) Write(h,addr+0x2d0,new byte[]{0,0});
                        saved.Applied=desired;
                    }
                    // Keep outgoing cinematic cameras ready for a returning shot.
                    // Restore all tracked cameras when cinematic fill turns off or ends.
                    foreach (string key in changed.Keys.Where(key=>!cinematicFillActive || !objects.Valid(changed[key],true)).ToArray())
                    { RestoreCamera(h,objects,changed[key]); changed.Remove(key); }
                    }
                    catch (Win32Exception) { if (game.HasExited) break; }
                }
                }
            }
            finally
            {
                try
                {
                if (h!=IntPtr.Zero)
                {
                    if (!game.HasExited)
                    {
                        var objects=new Objects(h,game.MainModule.BaseAddress.ToInt64());
                        foreach (Camera camera in changed.Values) RestoreCamera(h,objects,camera);
                        if (gameplayFov!=null) gameplayFov.Restore(objects);
                    }
                }
                }
                finally
                {
                    if (h!=IntPtr.Zero) CloseHandle(h);
                    try { if (!game.HasExited) CinematicFix.Apply(game,false,true); }
                    finally { mutex.ReleaseMutex(); }
                }
            }
        }
    }
    internal static bool Same(float a,float b) { return Math.Abs(a-b)<0.00001f; }
    static void RestoreCamera(IntPtr h,Objects objects,Camera camera)
    {
        if (!objects.Valid(camera,true)) return;
        if (Same(BitConverter.ToSingle(Read(h,camera.Address+0x2bc,4),0),camera.Applied))
            Write(h,camera.Address+0x2bc,BitConverter.GetBytes(camera.Original));
        byte[] flags=Read(h,camera.Address+0x2d0,2);
        if (flags[0]==0 && flags[1]==0) Write(h,camera.Address+0x2d0,new byte[]{camera.Scale,camera.Crop});
    }
    static float ReadExtraView()
    {
        float value;
        try
        {
            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"E-Day-32x9-framing.txt");
            if (File.Exists(path) && float.TryParse(File.ReadAllText(path).Trim(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value) && value>=1 && value<=8) return value;
        }
        catch (IOException) { }
        return 1.50f;
    }
}
