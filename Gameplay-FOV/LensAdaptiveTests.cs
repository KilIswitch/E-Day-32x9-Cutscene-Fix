using System;
internal static class LensAdaptiveTests
{
    static int count;
    static void Eq(float actual,float expected,string message)
    {
        if(Math.Abs(actual-expected)>0.00001f || float.IsNaN(actual)) throw new Exception(message+": "+actual);
        count++;
    }
    public static int Main()
    {
        Eq(CameraFraming.LensExtra(3,24.892f,50),3,"Tight shot unchanged");
        Eq(CameraFraming.LensExtra(3,24.892f,35),3,"Reference lens unchanged");
        Eq(CameraFraming.LensExtra(3,24.892f,21),1.8f,"Wide shot reduced");
        Eq(CameraFraming.LensExtra(3.5f,24.892f,21),2.1f,"Framing setting respected");
        Eq(CameraFraming.LensExtra(3,49.784f,42),1.8f,"Sensor scale invariant");
        Eq(CameraFraming.LensExtra(1,24.892f,21),1,"Aspect correction retained");
        Eq(CameraFraming.LensExtra(3,24.892f,5),1,"Never reduce below base aspect correction");
        Eq(CameraFraming.LensExtra(3,float.NaN,21),3,"Invalid sensor fallback");
        Eq(CameraFraming.LensExtra(3,24.892f,0),3,"Invalid focal fallback");
        Eq(CameraFraming.LensExtra(3,24.892f,float.PositiveInfinity),3,"Invalid focal infinity fallback");
        float wide=CameraFraming.LensExtra(3,24.892f,21);
        float tight=CameraFraming.LensExtra(3,24.892f,35);
        Eq(wide*24.892f/21,tight*24.892f/35,"Projection expansion bounded consistently");
        Console.WriteLine(count+" adaptive lens checks passed.");
        return 0;
    }
}
