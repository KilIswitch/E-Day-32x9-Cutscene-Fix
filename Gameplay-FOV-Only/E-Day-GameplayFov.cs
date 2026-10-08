using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

// Overrides the TC gameplay camera's settings multiplier, not cinematic lenses
// or the final view cache. The engine retains its normal aiming/zoom behavior.
internal sealed class GameplayFov
{
    sealed class Saved : GameplayRuntime.Ref { public float Original,Applied; }
    readonly IntPtr h; readonly long basis;
    readonly Dictionary<string,Saved> changed=new Dictionary<string,Saved>();
    readonly Stopwatch config=Stopwatch.StartNew();
    float desiredFov=120;
    public GameplayFov(IntPtr handle,long moduleBase)
    { h=handle; basis=moduleBase; ValidateMarkers(h,basis); ReadConfig(); }

    public static void ValidateMarkers(IntPtr h,long basis)
    {
        // Setter: clamp to 60..90, divide by DefaultFOV (+340), store +5ea8.
        // Getter: feed +5ea8 into the gameplay camera bridge.
        if (!GameplayRuntime.Read(h,basis+0x7cc4750,33).SequenceEqual(new byte[]{
            0xc5,0xf2,0x5d,0x05,0xd8,0x8a,0x1c,0x04,0xc5,0xfa,0x5f,0x05,0x0c,0x92,0x1c,0x04,
            0xc5,0xfa,0x5e,0x89,0x40,0x03,0x00,0x00,0xc5,0xfa,0x11,0x89,0xa8,0x5e,0x00,0x00,0xc3}) ||
            !GameplayRuntime.Read(h,basis+0x7cb48b2,8).SequenceEqual(new byte[]{0xc5,0xfa,0x10,0x83,0xa8,0x5e,0x00,0x00}))
            throw new GameplayFovFix.NotReadyException("Gameplay FOV signatures differ. No changes made.");
    }
    internal static bool ParseSetting(string text,out float value)
    {
        return float.TryParse(text.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out value) &&
            !float.IsNaN(value) && !float.IsInfinity(value) && (value==0 || (value>=60 && value<=150));
    }
    void ReadConfig()
    {
        try
        {
            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"E-Day-gameplay-fov.txt"); float value;
            if (File.Exists(path) && ParseSetting(File.ReadAllText(path),out value)) desiredFov=value;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        config.Restart();
    }
    bool LiveActor(GameplayRuntime.Objects objects,byte[] weak)
    {
        int index=BitConverter.ToInt32(weak,0),serial=BitConverter.ToInt32(weak,4);
        if (index<0 || serial==0) return false;
        byte[] item=objects.Item(index);
        if (item==null || BitConverter.ToInt32(item,16)!=serial ||
            (BitConverter.ToUInt32(item,8)&0x10200000)!=0) return false;
        long actor=BitConverter.ToInt64(item,0);
        return actor!=0 && GameplayRuntime.I(h,actor+12)==index && (GameplayRuntime.I(h,actor+8)&0x30)==0;
    }
    public void Update(GameplayRuntime.Objects objects,List<GameplayRuntime.Ref> managers)
    {
        if (config.ElapsedMilliseconds>=250) ReadConfig();
        var active=new HashSet<string>();
        foreach (GameplayRuntime.Ref manager in managers)
        {
            if (!objects.Valid(manager,true) || GameplayRuntime.Q(h,manager.Address)!=basis+0xd8a6b30) continue;
            long controller=GameplayRuntime.Q(h,manager.Address+0x328);
            if (controller==0 || GameplayRuntime.Q(h,controller)==0 || (GameplayRuntime.I(h,controller+8)&0x30)!=0) continue;
            byte[] targetArray=GameplayRuntime.Read(h,manager.Address+0x3e10,16);
            long targets=BitConverter.ToInt64(targetArray,0);
            if (targets==0 || BitConverter.ToInt32(targetArray,8)!=4 || BitConverter.ToInt32(targetArray,12)<4) continue;
            byte[] targetRefs=GameplayRuntime.Read(h,targets,32);
            // Slot 0 is the gameplay camera target; slot 3 is a cinematic target.
            if (!LiveActor(objects,targetRefs.Take(8).ToArray()) ||
                LiveActor(objects,targetRefs.Skip(24).Take(8).ToArray()) || desiredFov==0) continue;
            float baseline=BitConverter.ToSingle(GameplayRuntime.Read(h,manager.Address+0x340,4),0);
            float current=BitConverter.ToSingle(GameplayRuntime.Read(h,manager.Address+0x5ea8,4),0);
            if (!(baseline>=30 && baseline<=150 && current>=0.25f && current<=5)) continue;
            string key=manager.Index+":"+manager.Serial; Saved saved;
            if (!changed.TryGetValue(key,out saved))
            {
                // Before ownership the engine's settings input must be in its normal range.
                float originalFov=baseline*current;
                if (!(originalFov>=59.99f && originalFov<=90.01f)) continue;
                saved=new Saved{Address=manager.Address,Index=manager.Index,Serial=manager.Serial,Original=current,Applied=current};
                changed.Add(key,saved);
            }
            else if (!GameplayRuntime.Same(current,saved.Applied))
            {
                float authored=baseline*current;
                if (!(authored>=59.99f && authored<=90.01f)) continue;
                // Retain a later in-game settings change for restoration.
                saved.Original=current;
            }
            float desired=desiredFov/baseline;
            if (!(desired>=0.25f && desired<=5) || !objects.Valid(saved,true)) continue;
            if (!GameplayRuntime.Same(current,desired)) GameplayRuntime.Write(h,manager.Address+0x5ea8,BitConverter.GetBytes(desired));
            saved.Applied=desired; active.Add(key);
        }
        foreach (string key in changed.Keys.Where(key=>!active.Contains(key)).ToArray())
        { RestoreOne(objects,changed[key]); changed.Remove(key); }
    }
    void RestoreOne(GameplayRuntime.Objects objects,Saved saved)
    {
        if (!objects.Valid(saved,true) || GameplayRuntime.Q(h,saved.Address)!=basis+0xd8a6b30) return;
        float current=BitConverter.ToSingle(GameplayRuntime.Read(h,saved.Address+0x5ea8,4),0);
        if (GameplayRuntime.Same(current,saved.Applied)) GameplayRuntime.Write(h,saved.Address+0x5ea8,BitConverter.GetBytes(saved.Original));
    }
    public void Restore(GameplayRuntime.Objects objects)
    { foreach (Saved saved in changed.Values) RestoreOne(objects,saved); changed.Clear(); }
}
