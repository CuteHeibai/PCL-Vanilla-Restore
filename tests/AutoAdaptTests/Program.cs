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
Test("changed native restriction silently restores native pages",()=>{var changed=Rewrite(m=>{foreach(var i in m.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall").Methods.Single(x=>x.Name=="LoaderInit").Body.Instructions)if(i.Operand is string s&&s=="关闭下载")i.Operand="changed restriction";});Require(SelfAdapter.Adapt(changed,donor,runtime,4,new string('a',40)).Mode=="native-fallback","silent fallback missing");});
Test("changed native install behavior falls back",()=>{var changed=Rewrite(m=>m.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall").Methods.Single(x=>x.Name=="BtnStart_Click").Body.Instructions.Insert(0,Instruction.Create(OpCodes.Nop)));Require(SelfAdapter.Adapt(changed,donor,runtime,4,new string('a',40)).Mode=="native-fallback","fallback rejected");});
Test("changed host startup is rejected",()=>Reject(Rewrite(m=>m.GetTypes().Single(t=>t.FullName=="PCL.Application").Methods.Single(x=>x.Name=="Application_Startup").Body.Instructions.Insert(0,Instruction.Create(OpCodes.Nop)))));
Test("missing PCL transport source API is rejected",()=>Reject(Rewrite(m=>m.GetTypes().Single(t=>t.FullName=="PCL.ModDownload").Methods.Single(x=>x.Name=="DlSourceAssetsGet").Name="RemovedAssetsSource")));
Test("changed PCL download cancellation API is rejected",()=>Reject(Rewrite(m=>m.GetTypes().Single(t=>t.FullName=="PCL.ModLoader/LoaderBase").Methods.Single(x=>x.Name=="WaitForExit").Name="RemovedWaitForExit")));
Test("silent fallback output can restore the next generation",()=>{var changed=Rewrite(m=>m.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall").Methods.Single(x=>x.Name=="BtnStart_Click").Body.Instructions.Insert(0,Instruction.Create(OpCodes.Nop)));var first=SelfAdapter.Adapt(changed,donor,runtime,4,new string('a',40));var next=SelfAdapter.Adapt(changed,first.Image,runtime,4,new string('b',40));Require(next.Mode=="native-fallback","fallback engine not inherited");var native=SelfAdapter.Adapt(original,next.Image,runtime,4,new string('c',40));Require(native.Mode=="native","native mode cannot resume");});
Test("malformed executable rejected",()=>Reject(new byte[]{77,90,0,0}));
Test("runtime missing next generation adapter rejected",()=>{try{SelfAdapter.Adapt(original,donor,new byte[]{77,90},3,new string('a',40));}catch(InvalidDataException){return;}throw new Exception("missing next generation adapter accepted");});
Test("already adapted candidate rejected",()=>{var result=SelfAdapter.Adapt(original,donor,runtime,3,new string('a',40));Reject(result.Image);});
Test("second generation carries adapter and can restore again",()=>{var first=SelfAdapter.Adapt(original,donor,runtime,3,new string('a',40));var second=SelfAdapter.Adapt(original,first.Image,runtime,3,new string('c',40));Require(second.Image.Length>original.Length,"second generation payload missing");});
Test("fallback restores original location and native install flow",()=>{
 var changed=Rewrite(m=>{m.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall").Methods.Single(x=>x.Name=="BtnStart_Click").Body.Instructions.Insert(0,Instruction.Create(OpCodes.Nop));});
 using var image=ModuleDefMD.Load(SelfAdapter.Adapt(changed,donor,runtime,6,new string('a',40)).Image);
 var page=image.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall");
 Require(page.Methods.Single(m=>m.Name=="LoaderInit").Body.Instructions.Any(i=>i.Operand is IField f&&f.Name=="DlClientListLoader"),"fallback leaves original location unavailable");
 Require(page.Methods.Single(m=>m.Name=="StartInstall").Body.Instructions.Any(i=>i.Operand is IMethod call&&call.Name=="McInstallStart"&&call.DeclaringType.FullName=="PCL.ModDownloadLib"),"fallback bypasses native installer");
 Require(!image.GetTypes().Single(t=>t.FullName=="PCLRestore.Hooks").Methods.Single(m=>m.Name=="Brand").Body.Instructions.Any(i=>Equals(i.Operand,"AttachDownloader")),"separate button still attached");
});
Test("fallback rejects removed page dependency",()=>Reject(Rewrite(m=>{
 m.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall").Methods.Single(x=>x.Name=="BtnStart_Click").Body.Instructions.Insert(0,Instruction.Create(OpCodes.Nop));
 m.GetTypes().Single(t=>t.FullName=="PCL.ModDownloadLib").Methods.Single(x=>x.Name=="McInstallStart").Name="RemovedInstallStart";
})));
Test("fallback rejects changed WPF resources",()=>Reject(Rewrite(m=>{
 m.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall").Methods.Single(x=>x.Name=="BtnStart_Click").Body.Instructions.Insert(0,Instruction.Create(OpCodes.Nop));
 var r=m.Resources.OfType<EmbeddedResource>().Single(x=>x.Name=="Plain Craft Launcher 2.g.resources");m.Resources.Remove(r);m.Resources.Add(new EmbeddedResource(r.Name,new byte[]{1,2,3},r.Attributes));
})));
Test("newer fallback uses current assembly references",()=>{
 var changed=Rewrite(m=>{m.Assembly.Version=new Version(2,99,0,2);foreach(var method in m.GetTypes().SelectMany(t=>t.Methods).Where(x=>x.HasBody))foreach(var i in method.Body.Instructions)if(i.Operand is string text)i.Operand=text.Replace("2.99.0.1","2.99.0.2");m.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall").Methods.Single(x=>x.Name=="BtnStart_Click").Body.Instructions.Insert(0,Instruction.Create(OpCodes.Nop));});
 var result=SelfAdapter.Adapt(changed,donor,runtime,6,new string('d',40));using var output=ModuleDefMD.Load(result.Image);
 Require(result.Mode=="native-fallback"&&output.Assembly.Version==new Version(2,99,0,2),"newer fallback not produced");
 Require(!output.GetAssemblyRefs().Any(a=>a.Name==output.Assembly.Name),"fallback references previous launcher assembly");
 if(args.Length>3){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);File.WriteAllBytes(args[3],result.Image);}
});
Test("fallback rejects generic dependency field changed to static",()=>Reject(Rewrite(m=>{
 m.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall").Methods.Single(x=>x.Name=="BtnStart_Click").Body.Instructions.Insert(0,Instruction.Create(OpCodes.Nop));
 m.GetTypes().Single(t=>t.FullName=="PCL.ModLoader/LoaderTask`2").Fields.Single(f=>f.Name=="Output").IsStatic=true;
})));
return failures==0?0:1;
