using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
internal static class CameraFrameGateTests
{
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate ulong View(IntPtr camera,float delta,IntPtr output);
 [DllImport("kernel32")] static extern IntPtr OpenProcess(uint a,bool i,int p);
 [DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32")] static extern IntPtr VirtualAlloc(IntPtr a,UIntPtr n,uint type,uint protection);
 [DllImport("kernel32")] static extern bool VirtualProtect(IntPtr a,UIntPtr n,uint protection,out uint previous);
 [DllImport("kernel32")] static extern bool VirtualFree(IntPtr a,UIntPtr n,uint type);
 static int checks;
 static void Assert(bool ok,string label) { if(!ok) throw new Exception(label); checks++; Console.WriteLine("PASS "+label); }
 static void Equal(float a,float b,string label) { Assert(!float.IsNaN(a) && Math.Abs(a-b)<0.0001f,label+" ("+a+")"); }
 static void Q(long a,long v) { Marshal.WriteInt64(new IntPtr(a),v); }
 static long Q(long a) { return Marshal.ReadInt64(new IntPtr(a)); }
 static void I(long a,int v) { Marshal.WriteInt32(new IntPtr(a),v); }
 static void F(long a,float v) { Bytes(a,BitConverter.GetBytes(v)); }
 static float F(long a) { var b=new byte[4]; Marshal.Copy(new IntPtr(a),b,0,4); return BitConverter.ToSingle(b,0); }
 static void Bytes(long a,byte[] b) { Marshal.Copy(b,0,new IntPtr(a),b.Length); }
 static byte[] Hex(string value) { return Enumerable.Range(0,value.Length/2).Select(i=>Convert.ToByte(value.Substring(i*2,2),16)).ToArray(); }
 public static int Main()
 {
  IntPtr reserve=VirtualAlloc(IntPtr.Zero,(UIntPtr)0xf000000,0x2000,4); if(reserve==IntPtr.Zero) throw new Exception("reserve");
  long basis=reserve.ToInt64(); IntPtr h=OpenProcess(0xc38,false,Process.GetCurrentProcess().Id); var allocations=new List<IntPtr>(); CameraFrameGate gate=null;
  Func<int,long> allocate=delegate(int n) { var p=Marshal.AllocHGlobal(n); allocations.Add(p); Bytes(p.ToInt64(),new byte[n]); return p.ToInt64(); };
  try
  {
   foreach(long rva in new long[]{0xe830dc0,0xd365980+0x6c8,0x551829c})
    if(VirtualAlloc(new IntPtr((basis+rva)&~4095L),(UIntPtr)4096,0x1000,4)==IntPtr.Zero) throw new Exception("commit");
   long original=basis+0x551829c,slot=basis+0xd365980+0x6c8;
   byte[] fake=Hex("48895c2408574883ec30c5f829742420c5f828f1498bf8488bd9f30f1083bc020000f30f1107f30f1177040fb783d0020000894708488b5c2440c5f8287424204883c4305fb8f0debc9ac3"); Bytes(original,fake); uint old;
   if(!VirtualProtect(new IntPtr(original&~4095L),(UIntPtr)4096,0x20,out old)) throw new Exception("protect fake view");
   Q(slot,original);
   long manager=allocate(0x6200),actor=allocate(0xd00),cam=allocate(0xd40),replacement=allocate(0xd40),outView=allocate(16);
   long header=allocate(40),chunks=allocate(8),items=allocate(24*4);
   Q(basis+0xe830dc0,unchecked((long)((ulong)header^0x5f85652ab88d6999))); Q(header+16,chunks); I(header+36,4); Q(chunks,items);
   Q(manager,basis+0xd8a6b30); I(manager+12,0); Q(actor,basis+0xd365158); I(actor+12,2);
   long level=allocate(40); Q(level,basis+0xd384f18); Q(actor+32,level);
   Action<long,int,int> setup=delegate(long address,int index,int serial)
   {
    Q(address,basis+0xd365980); I(address+12,index); Q(address+32,actor);
    F(address+0x2b4,16f/9f); F(address+0xca4,24.892f); F(address+0xca8,24.892f/(16f/9f)); F(address+0xccc,1);
    F(address+0xd3c,21); F(address+0xcb8,5); F(address+0xcbc,500); Marshal.WriteInt16(new IntPtr(address+0x2d0),0x101);
    Q(items+index*24,address); I(items+index*24+16,serial);
   };
   Q(items,manager); I(items+16,101); Q(items+2*24,actor); I(items+2*24+16,103);
   setup(cam,1,102); setup(replacement,3,104); F(manager+0x3a04,32f/9f);
   var managers=new List<CameraFraming.Ref>{new CameraFraming.Ref{Address=manager,Index=0,Serial=101}};
   gate=new CameraFrameGate(h,basis); gate.Update(new CameraFraming.Objects(h,basis),managers,3.5f);
   Assert(Q(slot)!=original,"validated vtable data pointer installed");
   Func<long,ulong> frame=delegate(long address) { var view=(View)Marshal.GetDelegateForFunctionPointer(new IntPtr(Q(slot)),typeof(View)); return view(new IntPtr(address),0.125f,new IntPtr(outView)); };
   Assert(frame(cam)==0x9abcdef0UL,"original function return preserved");
   Equal(F(outView),3.2f,"new 21 mm camera widened before its first original view calculation");
   Equal(F(outView+4),0.125f,"delta-time argument preserved"); Assert(Marshal.ReadInt32(new IntPtr(outView+8))==0,"overscan flags disabled for adjusted view");
   F(cam+0xd3c,35); frame(cam); Equal(F(outView),6,"lens change corrected in same call");
   F(cam+0xd3c,100); frame(cam); Equal(F(outView),6,"tight 100 mm lens retains full framing");
   F(cam+0xd38,7f/3f); F(cam+0x2b4,16f/9f); frame(cam); Equal(F(outView),13f/3f,"final crop overrides transient cached aspect");
   F(cam+0x2b4,7f/3f); frame(cam); Equal(F(outView),13f/3f,"completed crop update has identical framing");
   F(cam+0xd38,0); F(cam+0x2b4,16f/9f); F(cam+0xca8,24.892f/(7f/3f)); frame(cam); Equal(F(outView),13f/3f,"uncropped filmback recomputed before cache refresh");
   F(cam+0xca8,24.892f/(16f/9f)); F(cam+0xd3c,21); F(cam+0x2bc,0.1f); frame(cam); Equal(F(outView),3.62f,"new authored overscan preserved");
   frame(cam); Equal(F(outView),3.62f,"own applied overscan does not compound");
   Q(level,basis+0xd06d118); F(cam+0x2bc,0);
   frame(cam); Equal(F(outView),0,"serialized MovieScene template left authored");
   Assert(Marshal.ReadInt16(new IntPtr(cam+0x2d0))==0,"template flags not rewritten");
   Q(level,basis+0xd384f18); F(cam+0x2bc,3.62f);
   F(manager+0x3a04,0); frame(cam); Equal(F(outView),0.1f,"off mode restores original before view calculation");
   Assert(Marshal.ReadInt32(new IntPtr(outView+8))==0x101,"off mode restores authored flags");
   F(manager+0x3a04,32f/9f); frame(cam); Equal(F(outView),3.62f,"on mode resumes from original baseline");
   Q(actor,basis+0xd365158+8); frame(cam); Equal(F(outView),0.1f,"non-cinematic actor left unadjusted"); Q(actor,basis+0xd365158);
   I(items+16,999); frame(cam); Equal(F(outView),0.1f,"stale manager serial blocks adjustment"); I(items+16,101);
   F(cam+0x2bc,float.NaN); frame(cam); Assert(float.IsNaN(F(outView)),"invalid authored overscan left untouched"); F(cam+0x2bc,0.1f);
   F(cam+0xd3c,10); F(cam+0xcb8,21); frame(cam); Equal(F(outView),3.62f,"original focal-length clamp matched"); F(cam+0xcb8,5);
   gate.Update(new CameraFraming.Objects(h,basis),managers,3.0f); F(cam+0xd3c,35); frame(cam); Equal(F(outView),5.6f,"custom framing updates wrapper");
   // Replace an object at the same index while the object-array count remains four.
   setup(replacement,1,202); frame(replacement); Equal(F(outView),2.6f,"reused camera slot receives correct first-call framing");
   float obsolete=F(cam+0x2bc);
   byte[] status=gate.Status(); Assert(BitConverter.ToInt64(status,64)==0,"wrapper entry counter returns to zero"); Assert(BitConverter.ToInt64(status,72)>0,"applied view calls counted"); Assert(BitConverter.ToInt64(status,136)==0,"ownership table did not overflow");
   gate.Dispose(); Equal(F(replacement+0x2bc),0,"undo restores replacement's original overscan");
   Equal(F(cam+0x2bc),obsolete,"undo skips stale camera identity"); Assert(Marshal.ReadInt16(new IntPtr(replacement+0x2d0))==0x101,"undo restores replacement flags");
   Assert(Q(slot)==original,"undo restores original dispatch pointer"); gate.Dispose(); Assert(Q(slot)==original,"undo is idempotent"); gate=null;
   Console.WriteLine(checks+" frame-gate fixtures passed; only this test process was modified."); return 0;
  }
  finally { if(gate!=null) gate.Dispose(); CloseHandle(h); foreach(var p in allocations) Marshal.FreeHGlobal(p); VirtualFree(reserve,UIntPtr.Zero,0x8000); }
 }
}
