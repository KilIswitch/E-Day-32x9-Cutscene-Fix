using System;
using System.IO;
internal static class BuildPolicyTests
{
 public static int Main()
 {
  var output=new StringWriter();
  CinematicFix.AnnounceBuild("future-build-9999999",400000000,output);
  string text=output.ToString();
  if(!text.Contains("future-build-9999999") || !text.Contains("required camera layout validation") || text.Contains("Yes/No")) throw new Exception("Unknown build must continue without a question.");
  Console.WriteLine("PASS unknown build announces automatic required validation without prompting");
  CinematicFix.AnnounceBuild("future-build-9999999",400000000,output);
  if(output.ToString()!=text) throw new Exception("Repeated startup attempts must not spam build messages.");
  Console.WriteLine("PASS retries announce the same build only once");
  Console.WriteLine("2 build-policy checks passed.");return 0;
 }
}
