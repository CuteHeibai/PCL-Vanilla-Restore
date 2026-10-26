using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Text;
using Microsoft.CSharp;
using PCLRestore.Runtime;
static class ImageTests {
 internal static void Run(string scenario) {
  string dir=Path.GetFullPath("tests/UpdaterTests/fixtures/image "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  string name="PCLFixture"+Guid.NewGuid().ToString("N"),path=Path.Combine(dir,name+".exe"),marker=Path.Combine(dir,"PCLRestore.Profile.json"),ran=Path.Combine(dir,"EXECUTED");
  string version=scenario=="wrong assembly version"?"2.8.1.0":"2.8.2.0";
  File.WriteAllText(marker,"{\"upstreamVersion\":\"2.8.2.0\",\"patchRevision\":"+(scenario=="wrong patch revision"?"99":"2")+",\"vanillaDownload\":"+(scenario=="missing vanilla adaptation"?"false":"true")+",\"portableTemp\":true,\"updaterProtocol\":"+(scenario=="unknown updater protocol"?"2":"1")+"}");
  var options=new CompilerParameters {GenerateExecutable=scenario!="DLL candidate",OutputAssembly=path,TempFiles=new TempFileCollection(dir,false),CompilerOptions="/optimize+"};options.ReferencedAssemblies.Add("System.dll");
  if(scenario!="missing adaptation marker")options.EmbeddedResources.Add(marker);
  using(var compiler=new CSharpCodeProvider()) {var result=compiler.CompileAssemblyFromSource(options,"using System.Reflection;[assembly:AssemblyVersion(\""+version+"\")]class App {static void Main(){System.IO.File.WriteAllText(@\""+ran.Replace("\"","\"\"")+"\",\"bad\");}}");if(result.Errors.HasErrors)throw new Exception("fixture compile failed: "+result.Errors[0]);}
  var feed=new Feed{Version=new Version(2,8,2,0),Revision=2,Sha=UpdateCore.Hash(path),Url="https://example.com/adapted.exe"};
  bool rejected=false;try{UpdateCore.VerifyImage(path,feed,scenario=="wrong assembly identity"?"OtherPCL":name);}catch(InvalidDataException){rejected=true;}
  if(rejected!=(scenario!="valid adapted image"))throw new Exception("unexpected image verification outcome");
  if(File.Exists(ran))throw new Exception("candidate was executed");
 }
}
