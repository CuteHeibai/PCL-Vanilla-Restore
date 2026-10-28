using dnlib.DotNet;
using dnlib.DotNet.Emit;

using var original = ModuleDefMD.Load(args[0]);
using var actual = ModuleDefMD.Load(args[1]);
int failures = 0;
void Check(bool value,string message) { Console.WriteLine((value?"PASS ":"FAIL ")+message); if(!value) failures++; }
MethodDef Method(ModuleDefMD m,string type,string method) => m.GetTypes().Single(t=>t.FullName==type).Methods.Single(x=>x.Name==method);
string Body(MethodDef m) => string.Join("\n",m.Body.Instructions.Select(x=>x.OpCode+" "+x.Operand));
var loader=Method(actual,"PCL.PageDownloadInstall","LoaderInit");
Check(loader.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldsfld && i.Operand is IField f && f.Name=="DlClientListLoader"),"real vanilla version loader is connected");
Check(actual.Resources.Any(r=>r.Name=="PCLRestore.Profile.json"),"adaptation profile is embedded");
Check(actual.GetTypes().Any(t=>t.FullName=="PCLRestore.Hooks"),"portable hook implementation is embedded");
var nav=Method(actual,"PCL.PageDownloadLeft",".ctor");
Check(!nav.Body.Instructions.Any(i=>i.Operand is IMethod m && m.Name=="get_ItemMod"),"download navigation defaults to vanilla");
Check(!actual.GetAssemblyRefs().Any(a=>a.Name=="InjectedHooks"),"injected hooks have no external DLL dependency");
bool Calls(MethodDef m,string type,string name) => m.Body.Instructions.Any(i=>i.Operand is IMethod call && call.DeclaringType.FullName==type && call.Name==name);
Check(Calls(Method(actual,"PCL.ModBase",".cctor"),"PCLRestore.Hooks","PortableRoot"),"global temp path is portable");
Check(Calls(Method(actual,"PCL.Application","Application_Startup"),"PCLRestore.Hooks","PrepareEnvironment"),"child process temporary environment is portable");
Check(!Body(Method(actual,"PCL.ModMain","RequestTaskTempFolder")).Contains("ProgramData"),"task folders have no external fallback");
Check(Calls(Method(actual,"PCL.ModMain","RequestTaskTempFolder"),"PCL.ModMain","TryClearTaskTemp"),"task allocation waits for startup cleanup");
Check(!Body(Method(actual,"PCL.ModMain","TryClearTaskTemp")).Contains("ProgramData"),"cleanup never deletes outside portable task directory");
Check(Calls(Method(actual,"PCL.ModBase/_Closure$__","_Lambda$__0-1"),"PCLRestore.Hooks","PureRoot"),"native resource path has no external fallback");
Check(actual.Resources.Any(r=>r.Name=="PCLRestore.Runtime.dll"),"update runtime is embedded");
Check(Calls(Method(actual,"PCL.ModSecret","UpdateCheckByButton"),"PCLRestore.Hooks","Dispatch"),"manual update routes to embedded official adapter");
var profileResource=actual.Resources.OfType<EmbeddedResource>().Single(r=>r.Name=="PCLRestore.Profile.json");
using(var profile=System.Text.Json.JsonDocument.Parse(profileResource.CreateReader().ReadBytes((int)profileResource.Length)))
 {
 Check(profile.RootElement.GetProperty("officialAutoRestore").GetBoolean(),"official automatic restoration is enabled");
 Check(profile.RootElement.GetProperty("inlineVanillaFallback").GetBoolean()&&!profile.RootElement.GetProperty("independentVanilla").GetBoolean(),"silent native page fallback is enabled");
 }
var runtimeResource=actual.Resources.OfType<EmbeddedResource>().Single(r=>r.Name=="PCLRestore.Runtime.dll");
using(var runtime=ModuleDefMD.Load(runtimeResource.CreateReader().ReadBytes((int)runtimeResource.Length))) {
 Check(runtime.Resources.Any(r=>r.Name=="PCLRestore.Adaptation.dll"),"next-generation adapter is embedded");
 Check(Calls(Method(runtime,"PCLRestore.Runtime.OfficialUpdate","PrepareCore"),"PCLRestore.Adaptation.SelfAdapter","Adapt"),"official update invokes local restoration before replacement");
 Check(!runtime.GetTypes().Any(t=>t.FullName=="PCLRestore.Runtime.VanillaWindow"||t.FullName=="PCLRestore.Runtime.VanillaInstaller"),"no separate download window or custom installer is shipped");
 Check(Calls(Method(actual,"PCL.PageDownloadInstall","StartInstall"),"PCL.ModDownloadLib","McInstallStart"),"original page uses native PCL installation workflow");
 Check(!Method(actual,"PCLRestore.Hooks","Brand").Body.Instructions.Any(i=>Equals(i.Operand,"AttachDownloader")),"no floating download button is attached");
}
Check(Calls(Method(actual,"PCL.FormMain","FormMain_Loaded"),"PCLRestore.Hooks","Brand"),"third-party branding is connected to main window");
Check(!actual.GetTypes().SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.OpCode==OpCodes.Ldstr && i.Operand is string s && s=="Latest.exe"),"no second cached launcher EXE");
var originalOAuth=original.GetTypes().Single(t=>t.FullName=="PCL.ModSecret").Fields.Single(f=>f.Name=="OAuthClientId");
var actualOAuth=actual.GetTypes().Single(t=>t.FullName=="PCL.ModSecret").Fields.Single(f=>f.Name=="OAuthClientId");
Check(Equals(originalOAuth.Constant.Value,actualOAuth.Constant.Value),"official OAuth client ID is preserved");
foreach(var typeName in new[]{"PCL.ModLaunch","PCL.PageDownloadModify","PCL.ModModpack"})
{
 var originals=original.GetTypes().Where(t=>t.FullName==typeName || t.FullName.StartsWith(typeName+"/"));
 Check(originals.All(before=>{var after=actual.GetTypes().Single(t=>t.FullName==before.FullName); return before.Methods.Where(m=>m.HasBody).All(m=>Body(m)==Body(after.Methods.Single(n=>n.Name==m.Name && n.MethodSig.ToString()==m.MethodSig.ToString())));}),typeName+" existing behavior retained");
}
Environment.ExitCode=failures==0?0:1;
