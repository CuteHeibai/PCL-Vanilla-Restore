using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PCLRestore.Runtime {
 internal sealed class PreparedOfficialImage {internal string Candidate,Sha,SourceSha,Commit;internal Version Version;}
 internal static class OfficialUpdate {
  internal static bool IsKnownNoUpdate(string state,string commit,string installedSha) {
   try {var data=UpdateCore.Json(File.ReadAllText(state));return String.Equals(UpdateCore.Str(data,"commit"),commit,StringComparison.OrdinalIgnoreCase)&&String.Equals(UpdateCore.Str(data,"installedSha256"),installedSha,StringComparison.OrdinalIgnoreCase);}catch{return false;}
  }
  internal static void RememberNoUpdate(string state,string commit,string installedSha) {Entry.WriteJson(state,new{commit=commit,installedSha256=installedSha});}
  internal static bool ShouldInstall(Version incoming,string incomingSha,Version current,string currentSourceSha) {
   if(incoming==null||current==null||!Regex.IsMatch(incomingSha??"",@"\A[0-9a-fA-F]{64}\z"))throw new InvalidDataException("官方更新版本或校验信息无效。");
   return incoming>current||(incoming==current&&!String.Equals(incomingSha,currentSourceSha,StringComparison.OrdinalIgnoreCase));
  }
  internal static Version InspectVersion(string raw) {MetadataDependency.EnsureLoaded();return InspectVersionCore(raw);}
  [MethodImpl(MethodImplOptions.NoInlining)]
  static Version InspectVersionCore(string raw) {using(var module=dnlib.DotNet.ModuleDefMD.Load(raw,new dnlib.DotNet.ModuleCreationOptions {TryToLoadPdbFromDisk=false})){if(module.Assembly==null)throw new InvalidDataException("官方更新包不是有效的程序集。");return module.Assembly.Version;}}
  internal static PreparedOfficialImage Prepare(string raw,string donor,string candidate,int revision,string commit) {
   string cache=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(donor)),"PCL","Cache","Update"));
   if(!String.Equals(Path.GetDirectoryName(Path.GetFullPath(raw)),cache,StringComparison.OrdinalIgnoreCase)||!String.Equals(Path.GetDirectoryName(Path.GetFullPath(candidate)),cache,StringComparison.OrdinalIgnoreCase)||String.Equals(Path.GetFullPath(raw),Path.GetFullPath(candidate),StringComparison.OrdinalIgnoreCase)||String.Equals(Path.GetFullPath(candidate),Path.GetFullPath(donor),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("自动适配文件必须位于启动器更新缓存中。");
   MetadataDependency.EnsureLoaded();return PrepareCore(raw,donor,candidate,revision,commit);
  }
  [MethodImpl(MethodImplOptions.NoInlining)]
  static PreparedOfficialImage PrepareCore(string raw,string donor,string candidate,int revision,string commit) {
   byte[] runtime;
   using(var source=dnlib.DotNet.ModuleDefMD.Load(donor,new dnlib.DotNet.ModuleCreationOptions{TryToLoadPdbFromDisk=false})) {
    dnlib.DotNet.EmbeddedResource resource=null;foreach(var r in source.Resources)if(r.Name=="PCLRestore.Runtime.dll")resource=r as dnlib.DotNet.EmbeddedResource;
    if(resource==null)throw new InvalidDataException("当前启动器缺少可传递的自动更新模块。");
    runtime=resource.CreateReader().ReadBytes((int)resource.Length);
   }
   var image=PCLRestore.Adaptation.SelfAdapter.Adapt(File.ReadAllBytes(raw),File.ReadAllBytes(donor),runtime,revision,commit);
   string temporary=candidate+"."+Guid.NewGuid().ToString("N")+".bin";
   try {File.WriteAllBytes(temporary,image.Image);if(File.Exists(candidate))File.Delete(candidate);File.Move(temporary,candidate);}finally{if(File.Exists(temporary))File.Delete(temporary);}
   var feed=new Feed{Version=image.Version,Revision=revision,Sha=image.Sha256,Url="https://raw.githubusercontent.com/Meloong-Git/PCL/"+commit+"/"};
   UpdateCore.VerifyImage(candidate,feed,AssemblyName.GetAssemblyName(donor).Name);
   Entry.WriteJson(Path.Combine(Path.GetDirectoryName(candidate),"adaptation.json"),new{sourceCommit=commit,sourceExeSha256=image.SourceSha256,adaptedSha256=image.Sha256,upstreamVersion=image.Version.ToString(),adapterRevision=revision});
   return new PreparedOfficialImage{Candidate=candidate,Sha=image.Sha256,SourceSha=image.SourceSha256,Version=image.Version,Commit=commit};
  }
 }
}
