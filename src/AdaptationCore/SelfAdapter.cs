using dnlib.DotNet;
using dnlib.DotNet.Emit;
using PclRestore.Patcher;
using System.Text.RegularExpressions;

namespace PCLRestore.Adaptation;
public sealed class AdaptedImage {
 public byte[] Image {get;set;}=Array.Empty<byte>();
 public string SourceSha256 {get;set;}="";
 public string Sha256 {get;set;}="";
 public Version Version {get;set;}=new Version(0,0,0,0);
}
public static class SelfAdapter {
 public const string KnownInputHash="0db34e19d9eeb6d496d20fabe786ec16bbd36ba6b0ccdc8d29cb7e909c114397";
 public static byte[] CreateCompatibilityContract(byte[] original) {
  if(Hashing.Sha(original)!=KnownInputHash)throw new InvalidDataException("规则只能从已固定的官方基准生成。");
  using var module=ModuleDefMD.Load(original);return JsonData.Write(Compatibility.Create(module));
 }
 public static AdaptedImage Adapt(byte[] original,byte[] donor,byte[] runtime,int revision,string sourceCommit) {
  if(revision<1||!Regex.IsMatch(sourceCommit??"","^[0-9a-fA-F]{40}$"))throw new InvalidDataException("适配来源或修订无效。");
  try {
   using var input=ModuleDefMD.Load(original);
   if(input.Assembly==null||input.Assembly.Name!="Plain Craft Launcher 2"||input.Assembly.Version<new Version(2,99,0,1)||input.Resources.Any(r=>r.Name=="PCLRestore.Profile.json"))throw new InvalidDataException("候选不是兼容的官方原始程序。");
   Compatibility.RequireCompatible(input);
   var adapter=new Adapter(input,donor,revision,Hashing.Sha(original),sourceCommit!);
   adapter.RestoreVanilla();adapter.PortablePaths();adapter.AddUpdater(runtime);adapter.Finish(true);
   using var stream=new MemoryStream();input.Write(stream);byte[] image=stream.ToArray();
   Validate(original,image,runtime,revision,sourceCommit!);
   return new AdaptedImage{Image=image,Version=input.Assembly.Version,SourceSha256=Hashing.Sha(original),Sha256=Hashing.Sha(image)};
  }catch(InvalidDataException){throw;}catch(Exception e){throw new InvalidDataException("无法自动适配官方程序，当前版本未被替换。",e);}
 }
 public static void Validate(byte[] original,byte[] image,byte[] runtime,int revision,string sourceCommit) {
  using(var library=ModuleDefMD.Load(runtime)) {
   if(library.Assembly?.Name!="PCLRestore.Runtime"||!library.Resources.Any(r=>r.Name=="PCLRestore.Adaptation.dll")||!library.Resources.Any(r=>r.Name=="PCLRestore.dnlib.dll"))throw new InvalidDataException("后续自动恢复所需的内嵌引擎缺失。");
  }
  using var input=ModuleDefMD.Load(original);using var output=ModuleDefMD.Load(image);
  if(output.Assembly.Version!=input.Assembly.Version||output.Assembly.Name!=input.Assembly.Name)throw new InvalidDataException("适配后程序身份改变。");
  Compatibility.PreserveProtected(input,output);
  var loader=output.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall").Methods.Single(m=>m.Name=="LoaderInit");
  if(!loader.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldsfld&&i.Operand is IField f&&f.Name=="DlClientListLoader"))throw new InvalidDataException("原版列表恢复验证失败。");
  foreach(string method in new[]{"PrepareSelectPage","ReloadSelected","ReloadComponentLists","BtnStart_Click"}) {
   var target=output.GetTypes().Single(t=>t.FullName=="PCL.PageDownloadInstall").Methods.Single(m=>m.Name==method);
   if(!target.Body.Instructions.Any(i=>i.Operand is IMethod call&&call.DeclaringType.FullName=="PCLRestore.Hooks"&&call.Name=="VanillaOnly"))throw new InvalidDataException("纯原版安装限制未恢复。");
  }
  var embedded=output.Resources.OfType<EmbeddedResource>().Single(r=>r.Name=="PCLRestore.Runtime.dll");
  if(!embedded.CreateReader().ReadBytes((int)embedded.Length).SequenceEqual(runtime))throw new InvalidDataException("后续自动恢复模块未保留。");
  var marker=output.Resources.OfType<EmbeddedResource>().Single(r=>r.Name=="PCLRestore.Profile.json");var profile=JsonData.Read<AdaptationProfile>(marker.CreateReader().ReadBytes((int)marker.Length));
  if(!profile.officialAutoRestore||!profile.vanillaDownload||!profile.portableTemp||profile.updaterProtocol!=1||profile.adapterProtocol!=1||profile.patchRevision!=revision||profile.upstreamVersion!=input.Assembly.Version.ToString()||profile.sourceCommit!=sourceCommit||profile.sourceExeSha256!=Hashing.Sha(original))throw new InvalidDataException("适配来源记录无效。");
  foreach(var resource in input.Resources.OfType<EmbeddedResource>()) {
   var copy=output.Resources.OfType<EmbeddedResource>().Single(r=>r.Name==resource.Name);
   if(!resource.CreateReader().ReadBytes((int)resource.Length).SequenceEqual(copy.CreateReader().ReadBytes((int)copy.Length)))throw new InvalidDataException("官方内嵌资源意外改变。");
  }
 }
}
