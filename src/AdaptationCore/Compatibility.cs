using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Globalization;
using System.Text;

namespace PCLRestore.Adaptation;
internal static class Compatibility {
 static readonly string[] Required={
  "PCL.PageDownloadInstall::LoaderInit","PCL.PageDownloadInstall::PrepareSelectPage","PCL.PageDownloadInstall::ReloadSelected","PCL.PageDownloadInstall::ReloadComponentLists","PCL.PageDownloadInstall::BtnStart_Click",
  "PCL.PageDownloadLeft::.ctor","PCL.ModBase::.cctor","PCL.ModBase/_Closure$__::_Lambda$__0-1","PCL.ModMain::RequestTaskTempFolder","PCL.ModMain::TryClearTaskTemp","PCL.Application::Application_Startup",
  "PCL.ModSecret::DownloadLatestPCL","PCL.ModSecret::UpdateCheckByButton","PCL.ModSecret::UpdateStart","PCL.ModSecret::UpdateRestart","PCL.ModSecret::UpdateReplace","PCL.FormMain::FormMain_Loaded","PCL.PageOtherAbout::PageOtherAbout_Loaded"};
 internal static string Key(MethodDef m)=>m.DeclaringType.FullName+"::"+m.Name+"|"+m.MethodSig;
 internal static string Fingerprint(MethodDef m,string version) {
  var code=m.Body.Instructions;var indexes=code.Select((i,n)=>new{i,n}).ToDictionary(x=>x.i,x=>x.n);
  string Operand(object? value) {
   if(value==null)return "";
   if(value is Instruction branch)return "@"+indexes[branch];
   if(value is IList<Instruction> branches)return string.Join(",",branches.Select(i=>indexes[i]));
   if(value is string text)return "str:"+(version.Length==0?text:text.Replace(version,"<source-version>"));
   if(value is IMethod method)return method.FullName;
   if(value is IField field)return field.FullName;
   if(value is IType type)return type.FullName;
   if(value is Local local)return "local:"+local.Index;
   if(value is Parameter parameter)return "parameter:"+parameter.Index;
   return Convert.ToString(value,CultureInfo.InvariantCulture)??"";
  }
  int? buildCode=version.Length==0?null:m.Module.GetTypes().Single(t=>t.FullName=="PCL.ModBase").Fields.Single(f=>f.Name=="VersionCode").Constant.Value as int?;
  string Format(Instruction i)=>buildCode.HasValue&&i.IsLdcI4()&&i.GetLdcI4Value()==buildCode.Value?"Ldc_I4 <build-code>":i.OpCode.Code+" "+Operand(i.Operand);
  string body=string.Join("\n",code.Select(Format))+"\nEH:"+string.Join(";",m.Body.ExceptionHandlers.Select(e=>e.HandlerType+":"+Operand(e.TryStart)+":"+Operand(e.TryEnd)+":"+Operand(e.HandlerStart)+":"+Operand(e.HandlerEnd)+":"+Operand(e.FilterStart)+":"+Operand(e.CatchType)));
  return Hashing.Sha(Encoding.UTF8.GetBytes(body));
 }
 internal static CompatibilityContract Create(ModuleDefMD module) {
  string version=module.Assembly.Version.ToString();var selected=new HashSet<MethodDef>();var methods=module.GetTypes().SelectMany(t=>t.Methods).Where(m=>m.HasBody).ToArray();
  foreach(string selector in Required){var matches=methods.Where(m=>m.DeclaringType.FullName+"::"+m.Name==selector).ToArray();if(matches.Length!=1)throw new InvalidDataException("关键补丁入口不唯一或缺失："+selector);selected.Add(matches[0]);}
  foreach(var m in methods)if(m.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr&&i.Operand is string s&&(s=="Latest.exe"||s=="正式版 "+version)))selected.Add(m);
  return new CompatibilityContract{sourceSha256=SelfAdapter.KnownInputHash,version=version,methods=selected.OrderBy(Key,StringComparer.Ordinal).ToDictionary(Key,m=>Fingerprint(m,version),StringComparer.Ordinal)};
 }
 internal static void RequireCompatible(ModuleDefMD module) {
  using var resource=typeof(SelfAdapter).Assembly.GetManifestResourceStream("PCLRestore.Compatibility.json");
  using var bytes=new MemoryStream();resource!.CopyTo(bytes);var expected=JsonData.Read<CompatibilityContract>(bytes.ToArray());
  if(expected.sourceSha256!=SelfAdapter.KnownInputHash||expected.methods.Count==0)throw new InvalidDataException("适配规则缺失或无效。");
  var actual=Create(module);
  if(expected.methods.Count!=actual.methods.Count||expected.methods.Any(x=>!actual.methods.TryGetValue(x.Key,out var fingerprint)||fingerprint!=x.Value))throw new InvalidDataException("官方新版关键方法结构已改变，无法自动恢复；保留当前版本。");
 }
 internal static void PreserveProtected(ModuleDefMD input,ModuleDefMD output) {
  foreach(string name in new[]{"PCL.ModLaunch","PCL.PageDownloadModify","PCL.ModModpack"})
  foreach(var type in input.GetTypes().Where(t=>t.FullName==name||t.FullName.StartsWith(name+"/",StringComparison.Ordinal)))
  foreach(var before in type.Methods.Where(m=>m.HasBody)) {
   var matches=output.GetTypes().SelectMany(t=>t.Methods).Where(m=>m.HasBody&&Key(m)==Key(before)).ToArray();
   if(matches.Length!=1||Fingerprint(before,"")!=Fingerprint(matches[0],""))throw new InvalidDataException("登录或已有版本管理逻辑发生意外变更。");
  }
  var a=input.GetTypes().Single(t=>t.FullName=="PCL.ModSecret").Fields.Single(f=>f.Name=="OAuthClientId");
  var b=output.GetTypes().Single(t=>t.FullName=="PCL.ModSecret").Fields.Single(f=>f.Name=="OAuthClientId");
  if(!Equals(a.Constant?.Value,b.Constant?.Value))throw new InvalidDataException("正版登录应用标识意外改变。");
 }
}
