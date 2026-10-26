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
foreach(var typeName in new[]{"PCL.ModLaunch","PCL.PageDownloadModify","PCL.ModModpack"})
{
 var before=original.GetTypes().Single(t=>t.FullName==typeName);
 var after=actual.GetTypes().Single(t=>t.FullName==typeName);
 Check(before.Methods.Where(m=>m.HasBody).All(m=>Body(m)==Body(after.Methods.Single(n=>n.Name==m.Name && n.MethodSig.ToString()==m.MethodSig.ToString()))),typeName+" existing behavior retained");
}
Environment.ExitCode=failures==0?0:1;
