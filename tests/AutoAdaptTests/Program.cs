using PCLRestore.Adaptation;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Text.Json;
byte[] original=File.ReadAllBytes(args[0]),donor=File.ReadAllBytes(args[1]),runtime=File.ReadAllBytes(args[2]);
int failures=0;
void Test(string name,Action action){try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Reject(byte[] bytes){try{SelfAdapter.Adapt(bytes,donor,runtime,3,new string('a',40));}catch(InvalidDataException){return;}throw new Exception("incompatible official input was accepted");}
byte[] Rewrite(Action<ModuleDefMD> edit){using var m=ModuleDefMD.Load(original);edit(m);using var ms=new MemoryStream();m.Write(ms);return ms.ToArray();}
Test("official baseline restored locally",()=>{var image=SelfAdapter.Adapt(original,donor,runtime,3,new string('a',40));using var m=ModuleDefMD.Load(image.Image);var loader=m.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall").Methods.Single(x=>x.Name=="LoaderInit");Require(loader.Body.Instructions.Any(i=>i.Operand is IField f&&f.Name=="DlClientListLoader"),"vanilla loader missing");using var profile=JsonDocument.Parse(m.Resources.OfType<EmbeddedResource>().Single(r=>r.Name=="PCLRestore.Profile.json").CreateReader().ReadBytes((int)m.Resources.OfType<EmbeddedResource>().Single(r=>r.Name=="PCLRestore.Profile.json").Length));Require(profile.RootElement.GetProperty("upstreamVersion").GetString()=="2.99.0.1","wrong profile version");Require(profile.RootElement.GetProperty("officialAutoRestore").GetBoolean(),"future auto-adapter missing");});
Test("synthetic compatible newer version restored",()=>{var next=Rewrite(m=>{m.Assembly.Version=new Version(2,99,0,2);m.GetTypes().Single(t=>t.FullName=="PCL.ModBase").Fields.Single(f=>f.Name=="VersionCode").Constant.Value=416;foreach(var method in m.GetTypes().SelectMany(t=>t.Methods).Where(x=>x.HasBody))foreach(var i in method.Body.Instructions){if(i.Operand is string s)i.Operand=s.Replace("2.99.0.1","2.99.0.2");if(i.IsLdcI4()&&i.GetLdcI4Value()==415){i.OpCode=OpCodes.Ldc_I4;i.Operand=416;}}});var output=SelfAdapter.Adapt(next,donor,runtime,3,new string('b',40));Require(output.Version==new Version(2,99,0,2),"new version not preserved");});
Test("missing critical anchor rejected",()=>Reject(Rewrite(m=>{foreach(var i in m.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall").Methods.Single(x=>x.Name=="LoaderInit").Body.Instructions)if(i.Operand is string s&&s=="关闭下载")i.Operand="changed restriction";})));
Test("changed patch-site behavior rejected",()=>Reject(Rewrite(m=>m.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall").Methods.Single(x=>x.Name=="BtnStart_Click").Body.Instructions.Insert(0,Instruction.Create(OpCodes.Nop)))));
Test("malformed executable rejected",()=>Reject(new byte[]{77,90,0,0}));
Test("runtime missing next generation adapter rejected",()=>{try{SelfAdapter.Adapt(original,donor,new byte[]{77,90},3,new string('a',40));}catch(InvalidDataException){return;}throw new Exception("missing next generation adapter accepted");});
Test("already adapted candidate rejected",()=>{var result=SelfAdapter.Adapt(original,donor,runtime,3,new string('a',40));Reject(result.Image);});
Test("second generation carries adapter and can restore again",()=>{var first=SelfAdapter.Adapt(original,donor,runtime,3,new string('a',40));var second=SelfAdapter.Adapt(original,first.Image,runtime,3,new string('c',40));Require(second.Image.Length>original.Length,"second generation payload missing");});
return failures==0?0:1;
