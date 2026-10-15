using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

// Resolves a complete layout before any game writes. Displacements/addresses
// are discovered; instructions, field semantics and object identities are checked.
internal sealed class GameLayout
{
    internal sealed class InitializingException : InvalidOperationException
    { internal InitializingException(string message) : base(message) { } }
    internal sealed class IncompatibleException : InvalidOperationException
    { internal IncompatibleException(string message) : base("Camera layout is incompatible: "+message+" No changes made.") { } }
    internal long Limit,Context,ObjectGlobal,ManagerClass,ManagerVtable,CineVtable,ActorVtable,MovieVtable,View,Slot;
    internal long FovSetter,FovGetter;
    internal ulong ObjectKey;
    internal int FillField,TargetField,FovField,DefaultFovField,ControllerField;
    internal byte[] SetterBytes,GetterBytes;
    internal bool Cinematic,Gameplay;
    internal static GameLayout Current;
    internal static GameLayout Required { get { if(Current==null) throw new InvalidOperationException("Camera layout has not been resolved."); return Current; } }
    static long cachedBasis; static int cachedSize;
    [DllImport("kernel32",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr h,IntPtr address,byte[] buffer,UIntPtr count,out UIntPtr read);
    [DllImport("kernel32",SetLastError=true)] static extern UIntPtr VirtualQueryEx(IntPtr h,IntPtr address,out Region info,UIntPtr size);
    [StructLayout(LayoutKind.Sequential)] struct Region
    { public IntPtr Address,Allocation; public uint AllocationProtection; public UIntPtr Size; public uint State,Protection,Type; }
    internal static byte[] Read(IntPtr h,long address,int count)
    {
        byte[] value=new byte[count]; UIntPtr got;
        if(!ReadProcessMemory(h,new IntPtr(address),value,(UIntPtr)count,out got) || got.ToUInt64()!=(ulong)count)
            throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not read camera layout.");
        return value;
    }
    static long Q(IntPtr h,long address) { return BitConverter.ToInt64(Read(h,address,8),0); }
    static int I(IntPtr h,long address) { return BitConverter.ToInt32(Read(h,address,4),0); }
    internal sealed class Pattern
    {
        readonly byte[] bytes,mask,anchor; readonly int at; readonly int[] skip=new int[256];
        internal Pattern(string text)
        {
            string[] parts=text.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            bytes=new byte[parts.Length]; mask=new byte[parts.Length]; int best=0,length=0,start=0;
            for(int i=0;i<parts.Length;i++)
            {
                if(parts[i]=="??") length=0;
                else { bytes[i]=Convert.ToByte(parts[i],16); mask[i]=255; if(length==0) start=i; length++; }
                if(length>best) { best=length; at=start; }
            }
            if(best==0) throw new ArgumentException("Empty signature.");
            anchor=bytes.Skip(at).Take(best).ToArray();
            for(int i=0;i<256;i++) skip[i]=best;
            for(int i=0;i<best-1;i++) skip[anchor[i]]=best-i-1;
        }
        internal IEnumerable<int> Find(byte[] data)
        {
            for(int a=0;a<=data.Length-anchor.Length;)
            {
                int i=anchor.Length-1; while(i>=0 && data[a+i]==anchor[i]) i--;
                if(i>=0) { a+=skip[data[a+anchor.Length-1]]; continue; }
                int candidate=a-at; bool match=candidate>=0 && candidate<=data.Length-bytes.Length;
                if(match) for(int j=0;j<bytes.Length;j++) if(mask[j]!=0 && bytes[j]!=data[candidate+j]) { match=false; break; }
                if(match) yield return candidate;
                a++;
            }
        }
    }
    internal sealed class Segment { internal long Rva; internal byte[] Data; internal bool Executable; }
    internal sealed class Image
    {
        internal readonly List<Segment> Parts; internal readonly long Basis; internal readonly int Size;
        internal Image(long basis,int size,List<Segment> parts) { Basis=basis; Size=size; Parts=parts; }
        internal byte[] At(long rva,int count)
        {
            Segment s=Parts.FirstOrDefault(p=>rva>=p.Rva && rva-p.Rva<=p.Data.Length-count);
            if(s==null) throw new IncompatibleException("A resolved address is outside readable image data.");
            var data=new byte[count]; Array.Copy(s.Data,rva-s.Rva,data,0,count); return data;
        }
        internal bool In(long rva,int count,bool executable)
        { return Parts.Any(p=>p.Executable==executable && rva>=p.Rva && rva-p.Rva<=p.Data.Length-count); }
        internal List<long> Find(string pattern,bool executable)
        {
            var p=new Pattern(pattern); var result=new List<long>();
            foreach(Segment s in Parts.Where(s=>s.Executable==executable))
                foreach(int at in p.Find(s.Data)) { result.Add(s.Rva+at); if(result.Count>100000) throw new IncompatibleException("Excessive signature candidates."); }
            return result;
        }
        internal long Unique(string label,string pattern)
        { return One(Find(pattern,true),label); }
        internal string Ascii(long rva,int length)
        { return Encoding.ASCII.GetString(At(rva,length)); }
        internal string WideName(long rva)
        {
            var chars=new List<char>();
            for(int i=0;i<100;i++) { ushort c=BitConverter.ToUInt16(At(rva+i*2,2),0); if(c==0) return new string(chars.ToArray()); if(c<32 || c>126) return ""; chars.Add((char)c); }
            return "";
        }
        internal List<long> Pointers(long address)
        {
            var matches=new List<long>();
            foreach(Segment s in Parts.Where(s=>!s.Executable))
                for(int i=(int)((8-(s.Rva&7))&7);i<=s.Data.Length-8;i+=8)
                    if(BitConverter.ToInt64(s.Data,i)==address) matches.Add(s.Rva+i);
            return matches;
        }
    }
    static long One(IEnumerable<long> values,string label)
    {
        long[] unique=values.Distinct().ToArray();
        if(unique.Length!=1) throw new IncompatibleException(label+" requires one candidate; found "+unique.Length+".");
        return unique[0];
    }
    internal static long Rip(Image image,long at,int displacement,int next)
    {
        long target=at+next+BitConverter.ToInt32(image.At(at+displacement,4),0);
        if(target<0 || target>=image.Size) throw new IncompatibleException("Relative target is outside the executable.");
        return target;
    }
    static Image Capture(IntPtr h,long basis,int size)
    {
        if(size<0x100000 || size>0x40000000) throw new IncompatibleException("Unexpected image size.");
        var parts=new List<Segment>(); long address=basis,end=basis+size;
        while(address<end)
        {
            Region r;
            if(VirtualQueryEx(h,new IntPtr(address),out r,(UIntPtr)Marshal.SizeOf(typeof(Region)))==UIntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            long start=r.Address.ToInt64(); ulong length=r.Size.ToUInt64();
            if(length==0 || length>long.MaxValue || start>long.MaxValue-(long)length || start+(long)length<=address)
                throw new IncompatibleException("Invalid image memory map.");
            long next=start+(long)length,lo=Math.Max(basis,start),hi=Math.Min(end,next);
            uint protection=r.Protection&255;
            bool readable=protection==2 || protection==4 || protection==8 || protection==0x20 || protection==0x40 || protection==0x80;
            if(r.State==0x1000 && r.Type==0x1000000 && (r.Protection&0x100)==0 && readable && hi>lo)
                parts.Add(new Segment{Rva=lo-basis,Data=Read(h,lo,(int)(hi-lo)),Executable=protection>=0x20});
            address=next;
        }
        return new Image(basis,size,parts);
    }
    sealed class ClassInfo { internal long Global,Class,Default,Vtable; internal int Size; }
    static Dictionary<string,ClassInfo> Classes(Image image,IntPtr h,IEnumerable<string> required)
    {
        var names=new HashSet<string>(required); var found=new Dictionary<string,ClassInfo>();
        List<long> getters=image.Find("4c 8b dc 48 83 ec 78 48 8b 05 ?? ?? ?? ?? 48 85 c0 0f 85 92 00 00 00",true);
        if(getters.Count==0) throw new InitializingException("Waiting for the game's executable code to initialize.");
        foreach(long at in getters)
        {
            byte[] body=image.At(at,174);
            if(!body.Skip(70).Take(3).SequenceEqual(new byte[]{0x48,0x8d,0x15})) continue;
            string name=image.WideName(Rip(image,at+70,3,7)); if(!names.Contains(name)) continue;
            if(found.ContainsKey(name)) throw new IncompatibleException("Ambiguous native class "+name+".");
            long global=Rip(image,at+7,3,7);
            if(!body.Skip(52).Take(3).SequenceEqual(new byte[]{0x4c,0x8d,0x05}) || Rip(image,at+52,3,7)!=global ||
               !body.Skip(162).Take(3).SequenceEqual(new byte[]{0x48,0x8b,0x05}) || Rip(image,at+162,3,7)!=global ||
               !body.Skip(149).Take(4).SequenceEqual(new byte[]{0xc7,0x44,0x24,0x20}) || !image.In(global,8,false))
                throw new IncompatibleException("Native class registration changed for "+name+".");
            long cls=Q(h,image.Basis+global); if(cls==0) throw new InitializingException("Waiting for native camera classes.");
            int size=BitConverter.ToInt32(body,153);
            if(size<40 || size>0x20000 || I(h,cls+0x58)!=size) throw new IncompatibleException("Native class size differs for "+name+".");
            long cdo=Q(h,cls+0x110);
            if(cdo==0) throw new InitializingException("Waiting for native camera defaults.");
            if(Q(h,cdo+16)!=cls || (I(h,cdo+8)&0x10)==0) throw new IncompatibleException("Class default identity differs for "+name+".");
            long table=Q(h,cdo)-image.Basis;
            if(!image.In(table,8,false) || !image.In(Q(h,image.Basis+table)-image.Basis,1,true))
                throw new IncompatibleException("Native virtual table differs for "+name+".");
            found.Add(name,new ClassInfo{Global=global,Class=cls,Default=cdo,Vtable=table,Size=size});
        }
        foreach(string name in names) if(!found.ContainsKey(name)) throw new IncompatibleException("Native class registration missing: "+name+".");
        return found;
    }
    static int Property(Image image,string name)
    {
        string signature=String.Join(" ",Encoding.ASCII.GetBytes(name+"\0").Select(b=>b.ToString("x2")));
        var offsets=new List<long>();
        foreach(long text in image.Find(signature,false))
            foreach(long p in image.Pointers(image.Basis+text))
            {
                byte[] b; try { b=image.At(p,56); } catch(IncompatibleException) { continue; }
                if(BitConverter.ToUInt32(b,28)==0x45 && BitConverter.ToUInt16(b,48)==1) offsets.Add(BitConverter.ToUInt32(b,50));
            }
        return (int)One(offsets,"Property "+name);
    }
    static void Field(int offset,int count,int size,string label)
    { if(offset<40 || offset>size-count) throw new IncompatibleException(label+" exceeds its native object."); }
    internal static GameLayout Discover(Image image,IntPtr h,bool cinematic,bool gameplay)
    {
        var result=new GameLayout{Cinematic=cinematic,Gameplay=gameplay};
        string[] classNames=cinematic ? new[]{"TCPlayerCameraManager_CamBridge","CineCameraComponent","CineCameraActor","MovieScene"} : new[]{"TCPlayerCameraManager_CamBridge"};
        var classes=Classes(image,h,classNames); ClassInfo manager=classes["TCPlayerCameraManager_CamBridge"];
        result.ManagerClass=manager.Global; result.ManagerVtable=manager.Vtable;
        var globals=new List<long>(); var keys=new List<ulong>();
        foreach(long at in image.Find("4c 8b 15 ?? ?? ?? ?? 48 be ?? ?? ?? ?? ?? ?? ?? ??",true))
        { globals.Add(Rip(image,at,3,7)); keys.Add(BitConverter.ToUInt64(image.At(at+9,8),0)); }
        result.ObjectGlobal=One(globals,"Object array global");
        if(keys.Distinct().Count()!=1 || !image.In(result.ObjectGlobal,8,false)) throw new IncompatibleException("Object array decoding differs.");
        result.ObjectKey=keys[0]; long objects=unchecked((long)((ulong)Q(h,image.Basis+result.ObjectGlobal)^result.ObjectKey));
        if(objects==0) throw new InitializingException("Waiting for the object array.");
        foreach(ClassInfo cls in classes.Values)
        {
            ValidateObject(h,objects,cls.Class); ValidateObject(h,objects,cls.Default);
            int depth=I(h,cls.Class+0x38);
            if(depth<0 || depth>64 || Q(h,Q(h,cls.Class+0x30)+depth*8)!=cls.Class+0x30)
                throw new IncompatibleException("Native class hierarchy layout differs.");
        }
        if(cinematic)
        {
            if(classes["CineCameraComponent"].Size!=0xdb0 || classes["CineCameraActor"].Size!=0xcf0 || classes["MovieScene"].Size!=0x168)
                throw new IncompatibleException("Cinematic object structure changed.");
            result.Context=image.Unique("Cinematic limit","41 c6 86 06 02 00 00 01 c4 c1 7a 10 86 08 02 00 00 c5 fa 5d 0d ?? ?? ?? ?? c5 f8 29 74 24 30 c5 f2 5f 35 ?? ?? ?? ??");
            result.Limit=Rip(image,result.Context,21,25);
            float cap=BitConverter.ToSingle(image.At(result.Limit,4),0);
            if(Math.Abs(cap-2.3888888f)>0.000001f && Math.Abs(cap-5120f/1440f)>0.000001f)
                throw new IncompatibleException("Cinematic limit constant changed.");
            byte[] nearby=image.At(result.Context-256,256);
            long store=One(new Pattern("44 89 af ?? ?? ?? ?? 48 85 db 0f 84 ?? ?? ?? ?? 8b 43 08 0f ba e0 1e").Find(nearby).Select(n=>(long)n),"Cinematic manager field");
            result.FillField=BitConverter.ToInt32(nearby,(int)store+3); Field(result.FillField,4,manager.Size,"Cinematic fill field");
            result.View=image.Unique("Cinematic view","48 89 5c 24 08 57 48 83 ec 30 c5 f8 29 74 24 20 c5 f8 28 f1 49 8b f8 48 8b d9 e8 ?? ?? ?? ?? 4c 8b c7 48 8b cb c5 f8 28 ce e8 ?? ?? ?? ?? 48 8b 03 4c 8b c7 c5 f8 28 ce 48 8b cb ff 90 d8 06 00 00");
            result.CineVtable=classes["CineCameraComponent"].Vtable; result.ActorVtable=classes["CineCameraActor"].Vtable; result.MovieVtable=classes["MovieScene"].Vtable;
            long lens=Rip(image,result.View,27,31),parent=Rip(image,result.View,42,46);
            if(!image.In(lens,128,true) || !image.In(parent,4096,true)) throw new IncompatibleException("View call targets differ.");
            byte[] lensBytes=image.At(lens,128),parentBytes=image.At(parent,4096);
            if(!new Pattern("c5 fa 10 91 b8 0c 00 00 c5 fa 10 81 bc 0c 00 00 c5 fa 5f ca").Find(lensBytes).Any() ||
               !new Pattern("44 8a 8b d1 02 00 00 44 8a 83 d0 02 00 00").Find(parentBytes).Any())
                throw new IncompatibleException("Lens clamp or aspect flag layout differs.");
            byte[] neighborhood=image.At(result.View,4096);
            foreach(string pattern in new[]{"c5 fa 10 a9 3c 0d 00 00","c5 f8 28 d3 c4 e2 61 99 91 bc 02 00 00","c5 fa 10 91 38 0d 00 00 c5 f8 2f d1 c5 fa 10 81 cc 0c 00 00 c5 fa 59 99 a4 0c 00 00"})
                if(!new Pattern(pattern).Find(neighborhood).Any()) throw new IncompatibleException("Cinematic sensor/overscan structure differs.");
            result.Slot=0x6c8;
        }
        if(gameplay)
        {
            result.FovSetter=image.Unique("Gameplay setter","c5 f2 5d 05 ?? ?? ?? ?? c5 fa 5f 05 ?? ?? ?? ?? c5 fa 5e 89 ?? ?? ?? ?? c5 fa 11 89 ?? ?? ?? ?? c3");
            result.SetterBytes=image.At(result.FovSetter,33);
            result.DefaultFovField=BitConverter.ToInt32(result.SetterBytes,20); result.FovField=BitConverter.ToInt32(result.SetterBytes,28);
            Field(result.DefaultFovField,4,manager.Size,"Default FOV"); Field(result.FovField,4,manager.Size,"FOV multiplier");
            if(BitConverter.ToSingle(image.At(Rip(image,result.FovSetter,4,8),4),0)!=90 ||
               BitConverter.ToSingle(image.At(Rip(image,result.FovSetter,12,16),4),0)!=60)
                throw new IncompatibleException("Gameplay slider semantics changed.");
            string field=String.Join(" ",BitConverter.GetBytes(result.FovField).Select(b=>b.ToString("x2")));
            result.FovGetter=image.Unique("Gameplay getter","c5 fa 10 83 "+field+" 48 8b 5c 24 30 48 83 c4 20 5f c3");
            result.GetterBytes=image.At(result.FovGetter,8);
            long target=image.Unique("Camera target selector","48 8b 81 ?? ?? ?? ?? 0f b6 d2 48 8d 0c d0 e9 ?? ?? ?? ?? cc");
            result.TargetField=BitConverter.ToInt32(image.At(target+3,4),0); Field(result.TargetField,16,manager.Size,"Camera targets");
            result.ControllerField=Property(image,"PCOwner"); Field(result.ControllerField,8,manager.Size,"Controller owner");
        }
        return result;
    }
    static void ValidateObject(IntPtr h,long objects,long obj)
    {
        int count=I(h,objects+36),index=I(h,obj+12);
        if(count<=0 || count>4000000 || index<0 || index>=count) throw new IncompatibleException("Object identity layout differs.");
        long chunks=Q(h,objects+16),item=Q(h,chunks+(index>>16)*8)+(index&65535)*24;
        if(Q(h,item)!=obj || (I(h,item+8)&0x10200000)!=0) throw new IncompatibleException("Native object array identity differs.");
    }
    internal static void Resolve(IntPtr h,long basis,int size,bool cinematic,bool gameplay)
    {
        if(Current!=null && cachedBasis==basis && cachedSize==size && (!cinematic || Current.Cinematic) && (!gameplay || Current.Gameplay)) return;
        Console.WriteLine("Locating and validating the camera layout...");
        GameLayout candidate=Discover(Capture(h,basis,size),h,cinematic,gameplay);
        // Publish only the complete validated result; no partial address fallback.
        Current=candidate; cachedBasis=basis; cachedSize=size;
        Console.WriteLine("Camera layout resolved: object array 0x"+candidate.ObjectGlobal.ToString("x")+
            (cinematic ? ", view 0x"+candidate.View.ToString("x")+", fill +0x"+candidate.FillField.ToString("x") : "")+
            (gameplay ? ", gameplay FOV +0x"+candidate.FovField.ToString("x") : "")+".");
    }
    internal static void ValidateView(IntPtr h,long basis,bool allowInstalled)
    {
        var l=Required;
        if(!l.Cinematic || (!allowInstalled && Q(h,basis+l.CineVtable+l.Slot)!=basis+l.View))
            throw new IncompatibleException("Original cinematic dispatch differs.");
    }
    internal static void ValidateGameplay(IntPtr h,long basis)
    {
        var l=Required;
        if(!l.Gameplay || !Read(h,basis+l.FovSetter,l.SetterBytes.Length).SequenceEqual(l.SetterBytes) ||
           !Read(h,basis+l.FovGetter,l.GetterBytes.Length).SequenceEqual(l.GetterBytes))
            throw new IncompatibleException("Gameplay code changed after validation.");
    }
}
