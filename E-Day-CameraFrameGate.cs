using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

// Replaces a validated vtable data pointer for this session. The game image's
// instructions and disk files are untouched. The wrapper tail-calls the original
// view function, with unchanged arguments, after updating cinematic overscan.
internal sealed class CameraFrameGate : IDisposable
{
    internal const int Capacity=32768, EntrySize=32;
    const long VtableRva=0xd365980, ViewRva=0x551829c, SlotOffset=0x6c8;
    readonly IntPtr h;
    readonly long basis,slot,original;
    long config,code,entries;
    bool installed,everInstalled,disposed;
    CameraFraming.Ref manager;
    [DllImport("kernel32",SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr address,UIntPtr size,uint type,uint protection);
    [DllImport("kernel32",SetLastError=true)] static extern bool VirtualFreeEx(IntPtr h,IntPtr address,UIntPtr size,uint type);
    [DllImport("kernel32",SetLastError=true)] static extern bool VirtualProtectEx(IntPtr h,IntPtr address,UIntPtr size,uint protection,out uint previous);
    [DllImport("kernel32",SetLastError=true)] static extern bool FlushInstructionCache(IntPtr h,IntPtr address,UIntPtr size);
    [DllImport("kernel32")] static extern uint GetProcessId(IntPtr h);
    [DllImport("ntdll")] static extern int NtSuspendProcess(IntPtr h);
    [DllImport("ntdll")] static extern int NtResumeProcess(IntPtr h);

    internal static byte[] Wrapper(long configuration,long target)
    {
        // Generated from camera-frame-gate.asm. Rebuilding the helper needs only
        // csc.exe; the assembly source and disassembly accompany the full source.
        byte[] value=Hex("534883ec704889cb4c8944242848895424304c894c2438f30f7f4c244049ba8877665544332211f049ff424041833a000f842f040000498b42304839030f8522040000f74308300000000f8515040000488b43204885c00f8408040000488b40204885c00f84fb030000498b92b80000004839100f84eb030000498b42084c8b184d335a104d85db0f84d70300008b4b0c413b4b240f83ca030000894c246c89cac1ea10498b43104885c00f84b4030000488b04d081e1ffff0000486bc9184801c84839180f859a030000f74008000020100f858d0300008b5010895424688b44246c4123829800000048c1e0054d8b82900000004901c0458b8a9c000000498b004885c00f848c0000004839d8750e8b442468413b400c0f84e5000000418b4808413b4b24736f89cac1ea10498b4310488b04d081e1ffff0000486bc9184801c8418b480c394810754c498b084839087544f7400800002010753b4983c020498b8290000000418b8a9c00000048c1e1054801c84939c072074d8b829000000041ffc90f8575fffffff049ff8288000000e9ce020000f30f1083bc0200000f57e40f2fc40f8aba0200000f82b4020000410f2f427c0f87a90200000fb783d0020000a9fefe00000f859702000049c700000000006641894018f3410f114010f3410f1140148b44246c418940088b4424684189400c41c7401c00000000498918eb47f30f1083bc0200000f57e40f2fc40f8a4e0200000f8248020000410f2f427c0f873d020000f3410f104814f30f5cc8410f548aa0000000410f2f8ab00000007606f3410f1140104c89442460418b4a20413b4b240f83b801000089cac1ea10498b4310488b04d081e1ffff0000486bc9184801c8418b4a243948100f8591010000f74008000020100f8584010000498b4a184839080f8577010000498b52284839110f856a010000f30f1091043a0000410f2f52600f8657010000410f2f52780f874c010000488b43204885c00f843f010000498b52384839100f8532010000f30f1083380d0000410f2f426c7607410f2f42687218f30f1083a40c0000f30f5983cc0c0000f30f5e83a80c0000410f2f426c0f86f9000000410f2f42680f83ee000000f30f5ed0f3410f5f5260f3410f105a04f30f10a3a40c00000f57c00f2fe07654410f2f6274774df30f10ab3c0d0000f30f1083bc0c0000f30f5f83b80c0000f30f5de8f30f5fabb80c00000f57c00f2fe87621410f2f6a70771af3410f595a64f30f59ddf30f5edcf3410f5d5a04f3410f5f5a60f3410f104010f3410f584260f30f59c2f30f59c3f3410f5c42600f57e40f2fc47a587256410f2f427c774f0fb783d0020000a9fefe00007541f30f1183bc02000066c783d00200000000f3410f11401441ff401cf049ff424849895a50f3410f115a58f3410f11425cf3410f106010f3410f11a280000000eb50f30f1083bc020000f3410f104814f30f5cc8410f548aa0000000410f2f8ab00000007a2c772af3410f104010f30f1183bc020000f3410f1140146683bbd002000000750c410fb74018668983d0020000f049ff4a404889d94c8b442428488b5424304c8b4c2438f30f6f4c24404883c4705b48b89988776655443322ffe0");
        Patch(value,0x1122334455667788L,configuration);
        Patch(value,0x2233445566778899L,target);
        return value;
    }
    static byte[] Hex(string value)
    {
        var result=new byte[value.Length/2];
        for(int i=0;i<result.Length;i++) result[i]=Convert.ToByte(value.Substring(i*2,2),16);
        return result;
    }
    static void Patch(byte[] value,long marker,long replacement)
    {
        byte[] before=BitConverter.GetBytes(marker),after=BitConverter.GetBytes(replacement); int count=0;
        for(int i=0;i<=value.Length-8;i++)
            if(value.Skip(i).Take(8).SequenceEqual(before)) { Array.Copy(after,0,value,i,8); count++; }
        if(count!=1) throw new InvalidOperationException("Unexpected frame-wrapper relocation count.");
    }
    internal static byte[] Configuration(long basis,long entries,int capacity,float extra)
    {
        if(capacity<=0 || (capacity&(capacity-1))!=0) throw new ArgumentException("Ownership capacity must be a power of two.");
        var data=new byte[192];
        Action<int,long> q=delegate(int offset,long value) { Array.Copy(BitConverter.GetBytes(value),0,data,offset,8); };
        Action<int,int> i=delegate(int offset,int value) { Array.Copy(BitConverter.GetBytes(value),0,data,offset,4); };
        Action<int,float> f=delegate(int offset,float value) { Array.Copy(BitConverter.GetBytes(value),0,data,offset,4); };
        f(4,extra); q(8,basis+0xe830dc0); q(16,unchecked((long)0x5f85652ab88d6999UL));
        q(40,basis+0xd8a6b30); q(48,basis+VtableRva); q(56,basis+0xd365158);
        f(96,1); f(100,24.892f/35); f(104,8); f(108,0.5f);
        f(112,1000); f(116,200); f(120,5120f/1440f+0.001f); f(124,64);
        q(144,entries); i(152,capacity-1); i(156,capacity);
        for(int offset=160;offset<176;offset+=4) i(offset,0x7fffffff);
        f(176,0.00001f);
        q(184,basis+0xd06d118);
        return data;
    }
    public CameraFrameGate(IntPtr handle,long module)
    {
        h=handle; basis=module; slot=basis+VtableRva+SlotOffset; original=basis+ViewRva;
        Validate();
        try
        {
            config=Allocate(4096); entries=Allocate(Capacity*EntrySize); code=Allocate(4096);
            CameraFraming.Write(h,config,Configuration(basis,entries,Capacity,1.50f));
            byte[] wrapper=Wrapper(config,original); CameraFraming.Write(h,code,wrapper);
            uint previous;
            if(!VirtualProtectEx(h,new IntPtr(code),(UIntPtr)4096,0x20,out previous) ||
                !FlushInstructionCache(h,new IntPtr(code),(UIntPtr)wrapper.Length)) throw new Win32Exception();
        }
        catch { ReleaseUninstalled(); throw; }
    }
    void Validate()
    {
        if(CameraFraming.Q(h,slot)!=original ||
           !CameraFraming.Read(h,original,26).SequenceEqual(Hex("48895c2408574883ec30c5f829742420c5f828f1498bf8488bd9")))
            throw new CinematicFix.NotReadyException("Cinematic view dispatch differs. No changes made.");
    }
    long Allocate(int size)
    {
        IntPtr value=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)size,0x3000,4);
        if(value==IntPtr.Zero) throw new Win32Exception(); return value.ToInt64();
    }
    public void Update(CameraFraming.Objects objects,List<CameraFraming.Ref> managers,float extra)
    {
        CameraFraming.Ref selected=managers.FirstOrDefault(value=>objects.Valid(value,true) && CameraFraming.Q(h,value.Address)==basis+0xd8a6b30);
        CameraFraming.Write(h,config+4,BitConverter.GetBytes(extra));
        if(selected==null) { CameraFraming.Write(h,config,BitConverter.GetBytes(0)); return; }
        if(manager==null || manager.Address!=selected.Address || manager.Serial!=selected.Serial)
        {
            CameraFraming.Write(h,config,BitConverter.GetBytes(0));
            byte[] identity=new byte[16]; Array.Copy(BitConverter.GetBytes(selected.Address),identity,8);
            Array.Copy(BitConverter.GetBytes(selected.Index),0,identity,8,4); Array.Copy(BitConverter.GetBytes(selected.Serial),0,identity,12,4);
            CameraFraming.Write(h,config+24,identity);
            manager=new CameraFraming.Ref{Address=selected.Address,Index=selected.Index,Serial=selected.Serial};
        }
        CameraFraming.Write(h,config,BitConverter.GetBytes(1));
        if(!installed)
        {
            // Retain allocations if an install attempt fails after the pointer
            // write. They must never be freed while the game might reference them.
            everInstalled=true;
            try { ReplacePointer(original,code); }
            finally { installed=CameraFraming.Q(h,slot)==code; }
        }
    }
    void ReplacePointer(long expected,long desired)
    {
        // Park the process only around the pointer write, never for discovery,
        // configuration polling, or normal frame calculations.
        // Owned-memory fixtures exercise the same pointer/restore path in this
        // process. A process must never try to suspend itself here.
        bool park=GetProcessId(h)!=(uint)Process.GetCurrentProcess().Id;
        if(park && NtSuspendProcess(h)<0) throw new InvalidOperationException("Could not park the game for the view-dispatch update.");
        try
        {
            if(CameraFraming.Q(h,slot)!=expected) throw new InvalidOperationException("Cinematic dispatch changed; replacement cancelled.");
            uint protection,ignored;
            if(!VirtualProtectEx(h,new IntPtr(slot),(UIntPtr)8,4,out protection)) throw new Win32Exception();
            try { CameraFraming.Write(h,slot,BitConverter.GetBytes(desired)); }
            finally { if(!VirtualProtectEx(h,new IntPtr(slot),(UIntPtr)8,protection,out ignored)) throw new Win32Exception(); }
        }
        finally { if(park && NtResumeProcess(h)<0) throw new InvalidOperationException("Could not resume the game after its dispatch update."); }
    }
    public byte[] Status() { return CameraFraming.Read(h,config,192); }
    public void Dispose()
    {
        if(config==0 || disposed) return;
        CameraFraming.Write(h,config,BitConverter.GetBytes(0));
        if(installed)
        {
            ReplacePointer(code,original); installed=false;
            var timer=Stopwatch.StartNew();
            while(CameraFraming.Q(h,config+64)!=0 && timer.ElapsedMilliseconds<2000) Thread.Sleep(1);
            if(CameraFraming.Q(h,config+64)!=0) throw new InvalidOperationException("Cinematic wrapper is still active; restore could not finish.");
            var objects=new CameraFraming.Objects(h,basis);
            byte[] table=CameraFraming.Read(h,entries,Capacity*EntrySize);
            for(int offset=0;offset<table.Length;offset+=EntrySize)
            {
                long address=BitConverter.ToInt64(table,offset); if(address==0) continue;
                var camera=new CameraFraming.Ref{Address=address,Index=BitConverter.ToInt32(table,offset+8),Serial=BitConverter.ToInt32(table,offset+12)};
                if(!objects.Valid(camera,true)) continue;
                float current=BitConverter.ToSingle(CameraFraming.Read(h,address+0x2bc,4),0);
                if(CameraFraming.Same(current,BitConverter.ToSingle(table,offset+20)))
                    CameraFraming.Write(h,address+0x2bc,table.Skip(offset+16).Take(4).ToArray());
                byte[] flags=CameraFraming.Read(h,address+0x2d0,2);
                if(flags[0]==0 && flags[1]==0) CameraFraming.Write(h,address+0x2d0,table.Skip(offset+24).Take(2).ToArray());
            }
            // Retain these small disabled allocations until game exit. A thread
            // can have just left the wrapper when its counter reaches zero;
            // keeping its code mapped avoids freeing a live instruction pointer.
        }
        else if(!everInstalled) ReleaseUninstalled();
        disposed=true;
    }
    void ReleaseUninstalled()
    {
        foreach(long address in new[]{code,entries,config}) if(address!=0) VirtualFreeEx(h,new IntPtr(address),UIntPtr.Zero,0x8000);
        code=entries=config=0;
    }
}
