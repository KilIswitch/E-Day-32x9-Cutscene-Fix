using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

internal static class GameplayFovTests
{
    [DllImport("kernel32")] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32",SetLastError=true)] static extern IntPtr VirtualAlloc(IntPtr address,UIntPtr size,uint type,uint protect);
    [DllImport("kernel32")] static extern bool VirtualFree(IntPtr address,UIntPtr size,uint type);
    static int passes;
    static void Assert(bool condition,string name) { if (!condition) throw new Exception(name); passes++; Console.WriteLine("PASS "+name); }
    static void Float(long address,float value) { Marshal.Copy(BitConverter.GetBytes(value),0,new IntPtr(address),4); }
    static float Float(long address) { var b=new byte[4]; Marshal.Copy(new IntPtr(address),b,0,4); return BitConverter.ToSingle(b,0); }
    static void Bytes(long address,byte[] data) { Marshal.Copy(data,0,new IntPtr(address),data.Length); }
    static void Config(string value) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"E-Day-gameplay-fov.txt"),value); Thread.Sleep(280); }
    public static int Main()
    {
        float value;
        Assert(GameplayFov.ParseSetting("120",out value) && value==120,"default 120 accepted");
        Assert(GameplayFov.ParseSetting(" 135.5 ",out value) && value==135.5f,"custom decimal accepted");
        Assert(GameplayFov.ParseSetting("0",out value) && value==0,"disable setting accepted");
        Assert(!GameplayFov.ParseSetting("NaN",out value) && !GameplayFov.ParseSetting("Infinity",out value),"nonfinite values rejected");
        Assert(!GameplayFov.ParseSetting("59",out value) && !GameplayFov.ParseSetting("151",out value),"unsupported extremes rejected");
        Assert(!GameplayFov.ParseSetting("",out value) && !GameplayFov.ParseSetting("120,5",out value),"blank and locale ambiguity rejected");
        IntPtr reserve=VirtualAlloc(IntPtr.Zero,(UIntPtr)0xf000000,0x2000,4); if (reserve==IntPtr.Zero) throw new Exception("reserve");
        long basis=reserve.ToInt64(); IntPtr handle=OpenProcess(0x438,false,Process.GetCurrentProcess().Id);
        var allocations=new List<IntPtr>();
        Func<int,long> allocate=delegate(int size) { var ptr=Marshal.AllocHGlobal(size); allocations.Add(ptr); Bytes(ptr.ToInt64(),new byte[size]); return ptr.ToInt64(); };
        try
        {
            foreach (long rva in new long[]{0x7cc4750,0x7cb48b2,0xe830dc0})
                if (VirtualAlloc(new IntPtr((basis+rva)&~4095L),(UIntPtr)4096,0x1000,4)==IntPtr.Zero) throw new Exception("commit");
            Bytes(basis+0x7cc4750,new byte[]{0xc5,0xf2,0x5d,0x05,0xd8,0x8a,0x1c,0x04,0xc5,0xfa,0x5f,0x05,0x0c,0x92,0x1c,0x04,0xc5,0xfa,0x5e,0x89,0x40,0x03,0,0,0xc5,0xfa,0x11,0x89,0xa8,0x5e,0,0,0xc3});
            Bytes(basis+0x7cb48b2,new byte[]{0xc5,0xfa,0x10,0x83,0xa8,0x5e,0,0});
            long manager=allocate(0x6200), controller=allocate(40), gameActor=allocate(40), cineActor=allocate(40), targets=allocate(32);
            long objectManager=allocate(40),chunks=allocate(8),items=allocate(72);
            Marshal.WriteInt64(new IntPtr(controller),basis+8);
            Marshal.WriteInt64(new IntPtr(manager),basis+0xd8a6b30);
            Marshal.WriteInt64(new IntPtr(manager+0x328),controller);
            Marshal.WriteInt64(new IntPtr(manager+0x3e10),targets);
            Marshal.WriteInt32(new IntPtr(manager+0x3e18),4); Marshal.WriteInt32(new IntPtr(manager+0x3e1c),4);
            Float(manager+0x340,80); Float(manager+0x5ea8,1.125f);
            Float(manager+0x371c,90); // Final-view cache sentinel must not be touched.
            Marshal.WriteInt64(new IntPtr(objectManager+16),chunks); Marshal.WriteInt32(new IntPtr(objectManager+36),3);
            Marshal.WriteInt64(new IntPtr(chunks),items);
            Marshal.WriteInt64(new IntPtr(basis+0xe830dc0),unchecked((long)((ulong)objectManager^0x5f85652ab88d6999)));
            long[] objs={manager,gameActor,cineActor};
            for (int i=0;i<3;i++) { Marshal.WriteInt64(new IntPtr(items+i*24),objs[i]); Marshal.WriteInt32(new IntPtr(items+i*24+16),101+i); Marshal.WriteInt32(new IntPtr(objs[i]+12),i); }
            Marshal.WriteInt32(new IntPtr(targets),1); Marshal.WriteInt32(new IntPtr(targets+4),102);
            Config("120"); var engineObjects=new CameraFraming.Objects(handle,basis); var mod=new GameplayFov(handle,basis);
            var managers=new List<CameraFraming.Ref>{new CameraFraming.Ref{Address=manager,Index=0,Serial=101}};
            mod.Update(engineObjects,managers);
            Assert(Float(manager+0x5ea8)==1.5f,"120 uses validated gameplay multiplier");
            Assert(Float(manager+0x371c)==90,"final-view cache left untouched");
            Config("130"); mod.Update(engineObjects,managers); Assert(Float(manager+0x5ea8)==1.625f,"live text change to 130 applied");
            Config("NaN"); mod.Update(engineObjects,managers); Assert(Float(manager+0x5ea8)==1.625f,"invalid text retains last valid setting");
            Marshal.WriteInt32(new IntPtr(targets+24),2); Marshal.WriteInt32(new IntPtr(targets+28),103);
            mod.Update(engineObjects,managers); Assert(Float(manager+0x5ea8)==1.125f,"cinematic target restores gameplay baseline");
            Bytes(targets+24,new byte[8]); mod.Update(engineObjects,managers);
            Assert(Float(manager+0x5ea8)==1.625f,"gameplay resumes after cinematic target ends");
            Config("0"); mod.Update(engineObjects,managers); Assert(Float(manager+0x5ea8)==1.125f,"zero disables and restores gameplay FOV");
            Config("120"); mod.Update(engineObjects,managers); Float(manager+0x5ea8,1.0f); mod.Update(engineObjects,managers); mod.Restore(engineObjects);
            Assert(Float(manager+0x5ea8)==1.0f,"undo preserves later in-game setting change");
            mod.Update(engineObjects,managers); Marshal.WriteInt32(new IntPtr(items+16),999); mod.Restore(engineObjects);
            Assert(Float(manager+0x5ea8)==1.5f,"recycled object serial prevents restoration write");
            Marshal.WriteInt32(new IntPtr(items+16),101); Float(manager+0x5ea8,1.125f); Marshal.WriteInt32(new IntPtr(targets+4),999);
            mod.Update(engineObjects,managers); Assert(Float(manager+0x5ea8)==1.125f,"invalid gameplay target prevents writes");
            Console.WriteLine(passes+" fixtures passed; all memory belonged to this test process."); return 0;
        }
        finally { CloseHandle(handle); foreach(var ptr in allocations) Marshal.FreeHGlobal(ptr); VirtualFree(reserve,UIntPtr.Zero,0x8000); }
    }
}
