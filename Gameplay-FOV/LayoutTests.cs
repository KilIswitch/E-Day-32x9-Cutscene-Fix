using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
internal static class GammaLayoutTests
{
 [DllImport("kernel32")] static extern IntPtr OpenProcess(uint a,bool b,int c);
 [DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
 static int checks;
 static void Assert(bool value,string text) { if(!value) throw new Exception(text); checks++; Console.WriteLine("PASS "+text); }
 static byte[] Hex(string text) { return text.Split(' ').Select(s=>s=="??" ? (byte)0 : Convert.ToByte(s,16)).ToArray(); }
 sealed class Fixture : IDisposable
 {
  internal readonly long Basis,Shift,Code,Data,View,Lens,Parent,Setter,Getter,Target,Cap,ObjectsGlobal,OwnerName,OwnerProperty;
  internal readonly IntPtr Handle;
  internal readonly GameLayout.Image Image;
  internal readonly List<IntPtr> allocations=new List<IntPtr>();
  internal readonly List<long> NativeClasses=new List<long>();
  internal Fixture(long shift,bool gamma)
  {
   Shift=shift; Code=0x1000+shift;Data=0x10000+shift;
   View=0x1800+shift;Lens=0x3000+shift;Parent=0x4000+shift;
   Setter=0x5100+shift;Getter=0x5200+shift;Target=0x5300+shift;
   Cap=Data+0x110;ObjectsGlobal=Data+0x100;OwnerName=Data+0x3000;OwnerProperty=Data+0x3200;
   Basis=Allocate(0x80000); Handle=OpenProcess(0x410,false,Process.GetCurrentProcess().Id);
   Image=new GameLayout.Image(Basis,0x80000,new List<GameLayout.Segment>{
    new GameLayout.Segment{Rva=Code,Data=new byte[0x7000],Executable=true},
    new GameLayout.Segment{Rva=Data,Data=new byte[0x10000],Executable=false}});
   long header=Allocate(40),chunks=Allocate(8),items=Allocate(24*8);
   Q(header+16,chunks);I(header+36,8);Q(chunks,items);
   Put(ObjectsGlobal,BitConverter.GetBytes(unchecked((long)((ulong)header^0x5f85652ab88d6999))));
   Put(0x5400+shift,Hex("4c 8b 15 ?? ?? ?? ?? 48 be 99 69 8d b8 2a 65 85 5f"));
   Rip(0x5400+shift,3,7,ObjectsGlobal);
   string[] names={"TCPlayerCameraManager_CamBridge","CineCameraComponent","CineCameraActor","MovieScene"};
   int[] sizes={gamma ? 0x6090 : 0x6020,0xdb0,0xcf0,0x168};
   for(int n=0;n<names.Length;n++)
   {
    long reg=0x6000+shift+n*0x100,glob=Data+0x200+n*8,name=Data+0x2000+n*0x100,vt=Data+0x4000+n*0x1000;
    byte[] body=new byte[174]; Array.Copy(Hex("4c 8b dc 48 83 ec 78 48 8b 05 00 00 00 00 48 85 c0 0f 85 92 00 00 00"),body,23);
    Array.Copy(Hex("4c 8d 05"),0,body,52,3);Array.Copy(Hex("48 8d 15"),0,body,70,3);
    Array.Copy(Hex("c7 44 24 20"),0,body,149,4);Array.Copy(BitConverter.GetBytes(sizes[n]),0,body,153,4);
    Array.Copy(Hex("48 8b 05"),0,body,162,3); Put(reg,body);
    Rip(reg+7,3,7,glob);Rip(reg+52,3,7,glob);Rip(reg+70,3,7,name);Rip(reg+162,3,7,glob);
    Put(name,Encoding.Unicode.GetBytes(names[n]+"\0"));
    long cls=Allocate(0x200),cdo=Allocate(sizes[n]),hierarchy=Allocate(8);
    NativeClasses.Add(cls);
    I(cls+12,n*2);I(cdo+12,n*2+1);I(cdo+8,0x30);Q(cdo+16,cls);Q(cdo,Basis+vt);
    I(cls+0x58,sizes[n]);Q(cls+0x110,cdo);Q(cls+0x30,hierarchy);I(cls+0x38,0);Q(hierarchy,cls+0x30);
    Q(items+n*48,cls);Q(items+n*48+24,cdo);
    Put(glob,BitConverter.GetBytes(cls));Put(vt,BitConverter.GetBytes(Basis+Code));
    if(n==1) Put(vt+0x6c8,BitConverter.GetBytes(Basis+View));
   }
   Put(0x1600+shift,Hex("41 c6 86 06 02 00 00 01 c4 c1 7a 10 86 08 02 00 00 c5 fa 5d 0d ?? ?? ?? ?? c5 f8 29 74 24 30 c5 f2 5f 35 ?? ?? ?? ??"));
   Rip(0x1600+shift,21,25,Cap);Put(Cap,BitConverter.GetBytes(2.3888888f));
   Put(0x1550+shift,Hex("44 89 af 00 00 00 00 48 85 db 0f 84 00 00 00 00 8b 43 08 0f ba e0 1e"));
   Put(0x1553+shift,BitConverter.GetBytes(gamma ? 0x3a14 : 0x3a04));
   Put(View,Hex("48 89 5c 24 08 57 48 83 ec 30 c5 f8 29 74 24 20 c5 f8 28 f1 49 8b f8 48 8b d9 e8 ?? ?? ?? ?? 4c 8b c7 48 8b cb c5 f8 28 ce e8 ?? ?? ?? ?? 48 8b 03 4c 8b c7 c5 f8 28 ce 48 8b cb ff 90 d8 06 00 00"));
   Rip(View,27,31,Lens);Rip(View,42,46,Parent);
   Put(Lens,Hex("c5 fa 10 91 b8 0c 00 00 c5 fa 10 81 bc 0c 00 00 c5 fa 5f ca"));
   Put(Parent+128,Hex("44 8a 8b d1 02 00 00 44 8a 83 d0 02 00 00"));
   Put(View+256,Hex("c5 fa 10 a9 3c 0d 00 00"));
   Put(View+288,Hex("c5 f8 28 d3 c4 e2 61 99 91 bc 02 00 00"));
   Put(View+320,Hex("c5 fa 10 91 38 0d 00 00 c5 f8 2f d1 c5 fa 10 81 cc 0c 00 00 c5 fa 59 99 a4 0c 00 00"));
   int field=gamma ? 0x5f18 : 0x5ea8;
   Put(Setter,Hex("c5 f2 5d 05 ?? ?? ?? ?? c5 fa 5f 05 ?? ?? ?? ?? c5 fa 5e 89 40 03 00 00 c5 fa 11 89 00 00 00 00 c3"));
   Put(Setter+28,BitConverter.GetBytes(field));Rip(Setter,4,8,Data+0x120);Rip(Setter,12,16,Data+0x124);
   Put(Data+0x120,BitConverter.GetBytes(90f));Put(Data+0x124,BitConverter.GetBytes(60f));
   Put(Getter,Hex("c5 fa 10 83 00 00 00 00 48 8b 5c 24 30 48 83 c4 20 5f c3"));Put(Getter+4,BitConverter.GetBytes(field));
   Put(Target,Hex("48 8b 81 00 00 00 00 0f b6 d2 48 8d 0c d0 e9 00 00 00 00 cc"));Put(Target+3,BitConverter.GetBytes(gamma ? 0x3e78 : 0x3e10));
   Put(OwnerName,Encoding.ASCII.GetBytes("PCOwner\0"));Owner(OwnerProperty,0x328);
  }
  internal void Owner(long at,int offset)
  {
   Put(at,BitConverter.GetBytes(Basis+OwnerName));Put(at+28,BitConverter.GetBytes(0x45));Put(at+48,BitConverter.GetBytes((ushort)1));Put(at+50,BitConverter.GetBytes(offset));
  }
  internal long Allocate(int n) { IntPtr a=Marshal.AllocHGlobal(n);allocations.Add(a);Marshal.Copy(new byte[n],0,a,n);return a.ToInt64(); }
  internal void Q(long a,long v) { Marshal.WriteInt64(new IntPtr(a),v); }
  internal void I(long a,int v) { Marshal.WriteInt32(new IntPtr(a),v); }
  internal void Put(long rva,byte[] value)
  {
   var segment=Image.Parts.First(p=>rva>=p.Rva && rva-p.Rva<=p.Data.Length-value.Length);
   Array.Copy(value,0,segment.Data,rva-segment.Rva,value.Length);Marshal.Copy(value,0,new IntPtr(Basis+rva),value.Length);
  }
  internal void Rip(long at,int disp,int next,long target) { Put(at+disp,BitConverter.GetBytes(checked((int)(target-at-next)))); }
  public void Dispose() { CloseHandle(Handle);foreach(IntPtr a in allocations) Marshal.FreeHGlobal(a); }
 }
 static void Reject(Action<Fixture> mutate,string label)
 {
  using(var f=new Fixture(0,true))
  {
   mutate(f); var before=f.Image.Parts.Select(p=>p.Data.ToArray()).ToArray(); bool rejected=false;
   try { GameLayout.Discover(f.Image,f.Handle,true,true); } catch(GameLayout.IncompatibleException) { rejected=true; }
   Assert(rejected,label);
   Assert(f.Image.Parts.Select((p,i)=>p.Data.SequenceEqual(before[i])).All(v=>v),label+" leaves all image data unchanged");
  }
 }
 public static int Main()
 {
  foreach(bool gamma in new[]{false,true}) foreach(long shift in new long[]{0,0x30000})
   using(var f=new Fixture(shift,gamma))
   {
    var before=f.Image.Parts.Select(p=>p.Data.ToArray()).ToArray();
    GameLayout l=GameLayout.Discover(f.Image,f.Handle,true,true);
    Assert(l.View==f.View && l.Limit==f.Cap && l.ObjectGlobal==f.ObjectsGlobal,"relocated image resolves code, data and relative targets "+gamma+"/"+shift);
    Assert(l.FillField==(gamma ? 0x3a14 : 0x3a04) && l.FovField==(gamma ? 0x5f18 : 0x5ea8) && l.TargetField==(gamma ? 0x3e78 : 0x3e10),"fields are decoded rather than guessed "+gamma+"/"+shift);
    GameLayout.Current=l;GameLayout.ValidateView(f.Handle,f.Basis,false);GameLayout.ValidateGameplay(f.Handle,f.Basis);
    Assert(f.Image.Parts.Select((p,i)=>p.Data.SequenceEqual(before[i])).All(v=>v),"layout discovery and validation make no writes "+gamma+"/"+shift);
   }
  Reject(f=>f.Put(0x1680,f.Image.At(0x1600,39)),"ambiguous cinematic signature rejected");
  Reject(f=>f.Put(f.View,new byte[]{0x90}),"missing view signature rejected");
  Reject(f=>f.Put(f.Setter+28,BitConverter.GetBytes(0x70000)),"out-of-bounds FOV field rejected");
  Reject(f=>f.Put(f.Data+0x124,BitConverter.GetBytes(55f)),"changed slider clamp semantics rejected");
  Reject(f=>f.Put(f.Getter+4,BitConverter.GetBytes(0x5f1c)),"setter/getter disagreement rejected");
  Reject(f=>f.Put(f.Parent+128,new byte[]{0x90}),"changed camera flag layout rejected");
  Reject(f=>{ long cls=f.NativeClasses[1];f.I(cls+0x58,0xdc0); },"native class size disagreement rejected");
  Reject(f=>f.Owner(f.OwnerProperty+128,0x330),"ambiguous controller metadata rejected");
  Reject(f=>f.Put(0x5450,Hex("4c 8b 15 00 00 00 00 48 be 99 69 8d b8 2a 65 85 5f")),"conflicting object globals rejected");
  var random=new Random(4163);
  for(int trial=0;trial<1000;trial++)
  {
   int length=random.Next(1,40),count=random.Next(length,500);byte[] pattern=new byte[length],data=new byte[count];bool[] fixedByte=new bool[length];
   for(int i=0;i<length;i++) { pattern[i]=(byte)random.Next(4);fixedByte[i]=random.Next(3)!=0; } fixedByte[random.Next(length)]=true;
   for(int i=0;i<count;i++) data[i]=(byte)random.Next(4);
   int spot=random.Next(count-length+1);for(int i=0;i<length;i++) if(fixedByte[i]) data[spot+i]=pattern[i];
   string signature=String.Join(" ",Enumerable.Range(0,length).Select(i=>fixedByte[i] ? pattern[i].ToString("x2") : "??"));
   var expected=new List<int>();for(int at=0;at<=count-length;at++) if(Enumerable.Range(0,length).All(i=>!fixedByte[i] || data[at+i]==pattern[i])) expected.Add(at);
   if(!new GameLayout.Pattern(signature).Find(data).SequenceEqual(expected)) throw new Exception("Masked matcher differs from reference.");
  }
  Assert(true,"1000 masked pattern searches match the reference matcher");
  Console.WriteLine(checks+" layout checks passed; owned test memory only.");return 0;
 }
}
