using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

// Prepares only loaded cinematic camera templates owned by a MovieScene and
// LevelSequence. New spawned cameras inherit these data values before rendering.
internal sealed class CameraSources
{
    sealed class Source { public CameraFraming.Camera Camera; public uint ActorName; }
    readonly IntPtr h; readonly long basis; readonly List<Source> sources=new List<Source>();
    int scannedCount=-1;
    public CameraSources(IntPtr handle,long module) { h=handle; basis=module; }
    static long Q(IntPtr h,long a) { return CameraFraming.Q(h,a); }
    static byte[] Read(IntPtr h,long a,int n) { return CameraFraming.Read(h,a,n); }
    public void Refresh(CameraFraming.Objects objects)
    {
        sources.RemoveAll(source=>!objects.Valid(source.Camera,true));
        if (scannedCount>=0 && Math.Abs(objects.Count-scannedCount)<2048) return;
        long cls=Q(h,basis+0xed4de18); if (cls==0) return;
        foreach (var item in objects.FindNative(cls))
        {
            try
            {
                if (sources.Any(source=>source.Camera.Address==item.Address && source.Camera.Index==item.Index)) continue;
                byte[] body=Read(h,item.Address,0x2d2);
                if (BitConverter.ToInt64(body,0)!=basis+0xd365980) continue;
                long actor=BitConverter.ToInt64(body,32);
                if (actor==0 || Q(h,actor)!=basis+0xd365158 || Q(h,actor+0xce0)!=item.Address) continue;
                long scene=Q(h,actor+32);
                if (scene==0 || Q(h,scene)!=basis+0xd06d118) continue;
                long sequence=Q(h,scene+32);
                if (sequence==0 || Q(h,sequence)!=basis+0xd3734a8) continue;
                float original=BitConverter.ToSingle(body,0x2bc), aspect=BitConverter.ToSingle(body,0x2b4);
                if (!(original>=0 && original<=10 && aspect>0.5f && aspect<8) || body[0x2d0]>1 || body[0x2d1]>1) continue;
                sources.Add(new Source { ActorName=(uint)Q(h,actor+24), Camera=new CameraFraming.Camera {
                    Address=item.Address,Index=item.Index,Serial=item.Serial,Original=original,Applied=original,Aspect=aspect,Scale=body[0x2d0],Crop=body[0x2d1] } });
            }
            catch (Win32Exception) { }
        }
        scannedCount=objects.Count;
    }
    public void Prepare(CameraFraming.Objects objects,float viewport,float extra,Dictionary<string,CameraFraming.Camera> changed)
    {
        sources.RemoveAll(source=>!objects.Valid(source.Camera,true));
        foreach (var source in sources)
        {
            var camera=source.Camera;
            byte[] body=Read(h,camera.Address+0x2b4,30);
            float aspect=BitConverter.ToSingle(body,0), current=BitConverter.ToSingle(body,8);
            if (!(aspect>0.5f && aspect<8 && current>=0 && current<=64)) continue;
            if (viewport<=aspect+0.001f) continue;
            if (!CameraFraming.Same(current,camera.Applied)) camera.Original=current;
            camera.Aspect=aspect;
            float sensorWidth=BitConverter.ToSingle(Read(h,camera.Address+0xca4,4),0);
            float focalLength=BitConverter.ToSingle(Read(h,camera.Address+0xd3c,4),0);
            float lensExtra=CameraFraming.LensExtra(extra,sensorWidth,focalLength);
            float desired=(1+camera.Original)*Math.Max(1,viewport/aspect)*lensExtra-1;
            if (!(desired>=0 && desired<=64)) continue;
            if (!CameraFraming.Same(current,desired)) CameraFraming.Write(h,camera.Address+0x2bc,BitConverter.GetBytes(desired));
            if (body[28]!=0 || body[29]!=0) CameraFraming.Write(h,camera.Address+0x2d0,new byte[]{0,0});
            camera.Applied=desired;
            changed["source:"+camera.Index+":"+camera.Address]=camera;
        }
    }
    public CameraFraming.Camera Inherited(long actor,float overscan)
    {
        uint name=(uint)Q(h,actor+24);
        foreach (var source in sources)
            if (source.ActorName==name && !CameraFraming.Same(source.Camera.Original,source.Camera.Applied) && CameraFraming.Same(source.Camera.Applied,overscan))
                return source.Camera;
        return null;
    }
}
