using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

// Installs a validated cinematic view wrapper for this session. Framing
// is corrected before the original view calculation; gameplay is separate.
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
            !Read(h,basis+0x5518d58,8).SequenceEqual(new byte[]{0xc5,0xfa,0x10,0xa9,0x3c,0x0d,0x00,0x00}) ||
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
        using(var mutex=new Mutex(false,Name(game,"Camera")))
        {
            bool owned;
            try { owned=mutex.WaitOne(0); } catch(AbandonedMutexException) { owned=true; }
            if(!owned) return;
            IntPtr h=IntPtr.Zero; CameraFrameGate gate=null;
            using(var stop=new EventWaitHandle(false,EventResetMode.ManualReset,Name(game,"Stop")))
            using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,Name(game,"Ready")))
            try
            {
                h=OpenProcess(0xc38,false,game.Id); if(h==IntPtr.Zero) throw new Win32Exception();
                long basis=game.MainModule.BaseAddress.ToInt64(); ValidateMarkers(h,basis);
                gate=new CameraFrameGate(h,basis);
                var managers=new List<Ref>(); var search=Stopwatch.StartNew(); bool searched=false;
                var config=Stopwatch.StartNew(); float extra=ReadExtraView(); bool configured=false;
                using(var tick=new FastWait())
                {
                    ready.Set();
                    while(tick.Wait(stop,20) && !game.HasExited)
                    {
                        try
                        {
                            var objects=new Objects(h,basis);
                            managers.RemoveAll(value=>!objects.Valid(value,true));
                            if(managers.Count==0 && (!searched || search.ElapsedMilliseconds>=3000))
                            {
                                long cls=Q(h,basis+0xed6a080);
                                if(cls!=0) managers=objects.FindNative(cls,true);
                                searched=true; search.Restart(); configured=false;
                            }
                            if(!Read(h,basis+0xe2189dc,4).SequenceEqual(new byte[]{0x39,0x8e,0x63,0x40})) break;
                            if(!configured || config.ElapsedMilliseconds>=250)
                            {
                                extra=ReadExtraView(); gate.Update(objects,managers,extra);
                                configured=true; config.Restart();
                            }
                        }
                        catch(Win32Exception) { if(game.HasExited) break; }
                    }
                }
            }
            finally
            {
                try
                {
                    if(h!=IntPtr.Zero && !game.HasExited)
                    {
                        if(gate!=null) gate.Dispose();
                    }
                }
                finally
                {
                    if(h!=IntPtr.Zero) CloseHandle(h);
                    try { if(!game.HasExited) CinematicFix.Apply(game,false,true); }
                    finally { mutex.ReleaseMutex(); }
                }
            }
        }
    }
    // Keep the existing setting for normal/tight lenses. On wider lenses,
    // limit the added expansion to a 35 mm lens on this game's 24.892 mm sensor.
    // This adjusts overscan only; authored focal length and filmback stay intact.
    internal static float LensExtra(float extra,float sensorWidth,float focalLength)
    {
        if (!(sensorWidth>0 && sensorWidth<=200 && focalLength>0 && focalLength<=1000)) return extra;
        double referenceRatio=24.892/35.0;
        double lensRatio=(double)sensorWidth/focalLength;
        return (float)Math.Max(1,Math.Min(extra,extra*referenceRatio/lensRatio));
    }
    internal static float EffectiveAspect(float aspect,float cropAspect)
    {
        // UCineCameraComponent applies the crop after refreshing sensor aspect.
        // Use the final crop to avoid chasing the intermediate sensor value.
        return cropAspect>0.5f && cropAspect<8 ? cropAspect : aspect;
    }
    internal static bool Same(float a,float b) { return Math.Abs(a-b)<0.00001f; }
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
