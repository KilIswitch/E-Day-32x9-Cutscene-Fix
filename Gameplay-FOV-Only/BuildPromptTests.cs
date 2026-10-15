using System;
using System.IO;
internal static class BuildPromptTests
{
    static void Check(string input,bool expected,string name)
    {
        var output=new StringWriter();
        bool result=GameplayFovFix.AskDifferentBuild("future-CL-9999999",400000000,new StringReader(input),output);
        if (result!=expected || !output.ToString().Contains("future-CL-9999999") || !output.ToString().Contains("[Yes/No]")) throw new Exception(name);
        Console.WriteLine("PASS "+name);
    }
    public static int Main()
    {
        Check("Yes\n",true,"Yes accepts a compatibility attempt");
        Check("y\n",true,"short Yes accepted");
        Check("  YES  \n",true,"case and whitespace accepted");
        Check("No\n",false,"No declines");
        Check("n\n",false,"short No accepted");
        Check("\n",false,"empty answer defaults to No");
        Check("",false,"unavailable input defaults to No");
        Check("maybe\nNo\n",false,"invalid answer asks again before declining");
        Check("maybe\nYes\n",true,"invalid answer asks again before accepting");
        return 0;
    }
}
