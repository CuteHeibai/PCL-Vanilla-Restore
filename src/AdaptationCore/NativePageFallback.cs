using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace PCLRestore.Adaptation;

// Reuse the already restored native pages in the current EXE, including their normal
// controls, events and McInstallStart path. Nothing is attached to the window at runtime.
internal static class NativePageFallback {
 internal static bool Page(TypeDef type)=>new[]{"PCL.PageDownloadInstall","PCL.PageDownloadLeft"}.Any(n=>type.FullName==n||type.FullName.StartsWith(n+"/",StringComparison.Ordinal));
 static void Require(bool value,string detail){if(!value)throw new InvalidDataException("原下载页面备用恢复不兼容，保留当前版本："+detail);}
 static string Shape(TypeDef type)=>string.Join("\n",new[]{type.FullName,type.BaseType?.FullName??"",type.Attributes.ToString(),string.Join(";",type.Interfaces.Select(i=>i.Interface.FullName).OrderBy(x=>x,StringComparer.Ordinal)),string.Join(";",type.Fields.Select(f=>f.FullName+"|"+f.Attributes+"|"+f.Constant?.Value).OrderBy(x=>x,StringComparer.Ordinal)),string.Join(";",type.Methods.Select(m=>m.FullName+"|"+m.Attributes+"|"+m.ImplAttributes).OrderBy(x=>x,StringComparer.Ordinal))});
 internal static void CheckRestored(ModuleDefMD module){
  var page=module.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall");
  Require(page.Methods.Single(m=>m.Name=="LoaderInit").Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldsfld&&i.Operand is IField f&&f.Name=="DlClientListLoader"),"版本列表");
  foreach(string name in new[]{"PrepareSelectPage","ReloadSelected","ReloadComponentLists","BtnStart_Click"})Require(page.Methods.Single(m=>m.Name==name).Body.Instructions.Any(i=>i.Operand is IMethod call&&call.Name=="VanillaOnly"&&call.DeclaringType.FullName=="PCLRestore.Hooks"),"纯原版安装限制");
  Require(page.Methods.Single(m=>m.Name=="StartInstall").Body.Instructions.Any(i=>i.Operand is IMethod call&&call.Name=="McInstallStart"&&call.DeclaringType.FullName=="PCL.ModDownloadLib"),"原生安装任务");
  var nav=module.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadLeft").Methods.Single(m=>m.IsInstanceConstructor);
  Require(!nav.Body.Instructions.Any(i=>i.Operand is IMethod call&&call.Name=="get_ItemMod"),"原版导航");
 }
 internal static void Restore(ModuleDefMD target,byte[] donorImage){
  using var donor=ModuleDefMD.Load(donorImage);CheckRestored(donor);
  foreach(string name in new[]{"Plain Craft Launcher 2.g.resources","PCL.Resources.resources"}){
   var before=donor.Resources.OfType<EmbeddedResource>().Single(r=>r.Name==name);var after=target.Resources.OfType<EmbeddedResource>().Single(r=>r.Name==name);
   Require(before.CreateReader().ReadBytes((int)before.Length).SequenceEqual(after.CreateReader().ReadBytes((int)after.Length)),"界面或内嵌组件资源变化");
  }
  var sourceTypes=donor.GetTypes().Where(Page).ToArray();var destinationTypes=target.GetTypes().Where(Page).ToArray();
  Require(sourceTypes.Length==destinationTypes.Length,"页面类型变化");
  foreach(var type in sourceTypes)Require(destinationTypes.Any(t=>t.FullName==type.FullName&&Shape(t)==Shape(type)),"页面成员变化："+type.FullName);
  var mapper=new LocalMapper(donor,target);var importer=new Importer(target,ImporterOptions.TryToUseDefs,new GenericParamContext(),mapper);
  foreach(var type in sourceTypes)foreach(var method in type.Methods.Where(m=>m.HasBody)){
   var destination=mapper.Method(method);var body=new CilBody{InitLocals=method.Body.InitLocals,MaxStack=method.Body.MaxStack};destination.Body=body;
   var locals=method.Body.Variables.ToDictionary(v=>v,v=>new Local(importer.Import(v.Type)));foreach(var local in method.Body.Variables)body.Variables.Add(locals[local]);
   var code=method.Body.Instructions.ToDictionary(i=>i,i=>new Instruction(i.OpCode));
   object? Import(object? value)=>value switch {
    null=>null,Instruction i=>code[i],IList<Instruction> list=>list.Select(i=>code[i]).ToArray(),Local local=>locals[local],Parameter p=>destination.Parameters[p.Index],
    ITypeDefOrRef t=>importer.Import(t),IMethod m=>importer.Import(m),IField f=>importer.Import(f),MethodSig sig=>importer.Import(sig),_=>value
   };
   foreach(var instruction in method.Body.Instructions){code[instruction].Operand=Import(instruction.Operand);body.Instructions.Add(code[instruction]);}
   foreach(var handler in method.Body.ExceptionHandlers)body.ExceptionHandlers.Add(new ExceptionHandler(handler.HandlerType){TryStart=(Instruction?)Import(handler.TryStart),TryEnd=(Instruction?)Import(handler.TryEnd),HandlerStart=(Instruction?)Import(handler.HandlerStart),HandlerEnd=(Instruction?)Import(handler.HandlerEnd),FilterStart=(Instruction?)Import(handler.FilterStart),CatchType=handler.CatchType==null?null:importer.Import(handler.CatchType)});
  }
  CheckRestored(target);VerifyCopy(donor,target);
 }
 internal static void VerifyCopy(ModuleDefMD donor,ModuleDefMD output){
  foreach(var method in donor.GetTypes().Where(Page).SelectMany(t=>t.Methods).Where(m=>m.HasBody)){
   var restored=output.GetTypes().Where(Page).SelectMany(t=>t.Methods).Single(m=>m.FullName==method.FullName);
   Require(Compatibility.Fingerprint(method,"")==Compatibility.Fingerprint(restored,""),"备用页面代码未完整保留");
  }
 }
 sealed class LocalMapper:ImportMapper {
  readonly ModuleDefMD source,target;readonly Dictionary<string,TypeDef> types;
  internal LocalMapper(ModuleDefMD source,ModuleDefMD target){this.source=source;this.target=target;types=target.GetTypes().ToDictionary(t=>t.FullName,StringComparer.Ordinal);}
  bool Local(ITypeDefOrRef type)=>type.DefinitionAssembly?.Name==source.Assembly.Name;
  TypeDef Type(ITypeDefOrRef type){
   Require(types.TryGetValue(type.FullName,out var result),"依赖类型缺失："+type.FullName);
   var original=source.GetTypes().Single(t=>t.FullName==type.FullName);
   Require(result!.Attributes==original.Attributes&&result.BaseType?.FullName==original.BaseType?.FullName&&result.Interfaces.Select(i=>i.Interface.FullName).OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(original.Interfaces.Select(i=>i.Interface.FullName).OrderBy(x=>x,StringComparer.Ordinal)),"依赖类型结构变化："+type.FullName);
   return result;
  }
  internal MethodDef Method(MethodDef method){var type=Type(method.DeclaringType);var matches=type.Methods.Where(m=>m.FullName==method.FullName&&m.Attributes==method.Attributes&&m.ImplAttributes==method.ImplAttributes).ToArray();Require(matches.Length==1,"依赖方法变化："+method.FullName);return matches[0];}
  public override ITypeDefOrRef? Map(ITypeDefOrRef type)=>type is TypeSpec||!Local(type)?null:Type(type);
  public override IMethod? Map(MethodDef method)=>method.Module==source?Method(method):null;
  public override IField? Map(FieldDef field){if(field.Module!=source)return null;var matches=Type(field.DeclaringType).Fields.Where(f=>f.FullName==field.FullName&&f.Attributes==field.Attributes).ToArray();Require(matches.Length==1,"依赖字段变化："+field.FullName);return matches[0];}
  public override MemberRef? Map(MemberRef member){
   if(Local(member.DeclaringType)){
    var owner=member.DeclaringType is TypeSpec spec?spec.TypeSig.RemovePinnedAndModifiers().ToTypeDefOrRef():member.DeclaringType;
    if(owner is TypeSpec generic&&generic.TypeSig is GenericInstSig instance)owner=instance.GenericType.TypeDefOrRef;
    var type=Type(owner);var original=source.GetTypes().Single(t=>t.FullName==owner.FullName);
    if(member.IsMethodRef){
     var old=original.Methods.SingleOrDefault(m=>m.Name==member.Name&&m.MethodSig.ToString()==member.MethodSig.ToString());
     Require(old!=null&&type.Methods.Any(m=>m.Name==member.Name&&m.MethodSig.ToString()==member.MethodSig.ToString()&&m.Attributes==old.Attributes&&m.ImplAttributes==old.ImplAttributes),"依赖方法变化："+member.FullName);
    }else{
     var old=original.Fields.SingleOrDefault(f=>f.Name==member.Name&&f.FieldSig.ToString()==member.FieldSig.ToString());
     Require(old!=null&&type.Fields.Any(f=>f.Name==member.Name&&f.FieldSig.ToString()==member.FieldSig.ToString()&&f.Attributes==old.Attributes),"依赖字段变化："+member.FullName);
    }
   }
   return null;
  }
 }
}
