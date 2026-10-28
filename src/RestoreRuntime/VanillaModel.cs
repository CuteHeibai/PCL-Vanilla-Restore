using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace PCLRestore.Runtime {
 internal sealed class VanillaVersion {
  internal string Id,Type,Url,Sha1;
  public override string ToString(){return Id+"  ·  "+Type;}
 }
 internal sealed class VanillaFile {internal string Url,Sha1,Path;internal long Size=-1;internal bool IsJson;}
 internal sealed class VanillaCopy {internal string Source,Target;}
 internal sealed class VanillaAssets {internal List<VanillaFile> Files=new List<VanillaFile>();internal List<VanillaCopy> Copies=new List<VanillaCopy>();}
 internal sealed class VanillaPlan {
  internal VanillaFile Client,Index,Logging;internal string IndexId;
  internal List<VanillaFile> Libraries=new List<VanillaFile>();
 }
 internal static class VanillaModel {
  internal const string ManifestUrl="https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
  internal static Dictionary<string,object> Json(string text){try {var d=new JavaScriptSerializer{MaxJsonLength=20*1024*1024,RecursionLimit=100}.DeserializeObject(text) as Dictionary<string,object>;if(d==null)throw new InvalidDataException("游戏清单格式无效。");return d;}catch(InvalidDataException){throw;}catch(Exception e){throw new InvalidDataException("游戏清单解析失败。",e);}}
  internal static string Serialize(object value){return new JavaScriptSerializer{MaxJsonLength=20*1024*1024}.Serialize(value);}
  internal static object Value(Dictionary<string,object> d,string key){object v;return d!=null&&d.TryGetValue(key,out v)?v:null;}
  internal static Dictionary<string,object> Map(object value){return value as Dictionary<string,object>;}
  internal static string Text(Dictionary<string,object> d,string key){var s=Value(d,key) as string;if(String.IsNullOrEmpty(s))throw new InvalidDataException("游戏清单缺少字段："+key);return s;}
  internal static long Number(Dictionary<string,object> d,string key,long fallback=-1){object v=Value(d,key);if(v==null)return fallback;if(!(v is int)&&!(v is long))throw new InvalidDataException("文件大小无效。");long n=Convert.ToInt64(v);if(n<0||n>4L*1024*1024*1024)throw new InvalidDataException("文件大小超出范围。");return n;}
  internal static string Hash(string value){if(!Regex.IsMatch(value??"",@"\A[0-9a-fA-F]{40}\z"))throw new InvalidDataException("游戏文件 SHA1 无效。");return value.ToLowerInvariant();}
  internal static string Sha1(byte[] bytes){using(var hash=SHA1.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
  internal static string Name(string value){
   if(String.IsNullOrWhiteSpace(value)||value.Length>100||value!=value.Trim()||value.EndsWith(".")||value=="."||value==".."||value.IndexOfAny(Path.GetInvalidFileNameChars())>=0||Regex.IsMatch(value,@"\A(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|\z)",RegexOptions.IgnoreCase))throw new InvalidDataException("版本名或路径名称无效。");return value;
  }
  internal static string Relative(string value){if(String.IsNullOrEmpty(value)||value.Contains("\\")||value.StartsWith("/")||value.Length>400)throw new InvalidDataException("游戏清单包含不安全路径。");foreach(string p in value.Split('/'))Name(p);return value;}
  internal static string Under(string root,string relative){Relative(relative);string prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;string result=Path.GetFullPath(Path.Combine(prefix,relative.Replace('/',Path.DirectorySeparatorChar)));if(!result.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("游戏文件路径越界。");return result;}
  internal static string Url(string value){Uri u;if(!Uri.TryCreate(value,UriKind.Absolute,out u)||u.Scheme!="https"||!String.IsNullOrEmpty(u.UserInfo)||!u.IsDefaultPort||!(u.Host=="piston-meta.mojang.com"||u.Host=="piston-data.mojang.com"||u.Host=="launchermeta.mojang.com"||u.Host=="launcher.mojang.com"||u.Host=="libraries.minecraft.net"||u.Host=="resources.download.minecraft.net"||u.Host=="bmclapi2.bangbang93.com"))throw new InvalidDataException("游戏文件地址不是受支持的 PCL 下载源。");return u.AbsoluteUri;}
  internal static List<VanillaVersion> Catalog(string json){var versions=Value(Json(json),"versions") as object[];if(versions==null||versions.Length>10000)throw new InvalidDataException("游戏版本清单为空或过大。");var result=new List<VanillaVersion>();var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var item in versions){var d=Map(item);string url=Url(Text(d,"url")),sha=Value(d,"sha1") as string;var package=Regex.Match(new Uri(url).AbsolutePath,@"/v1/packages/([0-9a-fA-F]{40})/");if(sha==null&&package.Success)sha=package.Groups[1].Value;var v=new VanillaVersion{Id=Name(Text(d,"id")),Type=Text(d,"type"),Url=url,Sha1=sha==null?null:Hash(sha)};if(!ids.Add(v.Id))continue;result.Add(v);}return result;}
  internal static string Architecture {get {string arch=Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")??"";return arch.IndexOf("ARM64",StringComparison.OrdinalIgnoreCase)>=0?"arm64":Environment.Is64BitOperatingSystem?"x64":"x86";}}
  internal static bool Allowed(Dictionary<string,object> library,string architecture){
   object raw=Value(library,"rules");if(raw==null)return true;var rules=raw as object[];if(rules==null)throw new InvalidDataException("依赖规则无效。");bool allowed=false;
   foreach(var item in rules){var r=Map(item);bool match=true;var os=Map(Value(r,"os"));if(os!=null){var name=Value(os,"name") as string;if(name!=null&&name!="windows")match=false;var arch=Value(os,"arch") as string;if(arch!=null){string normalized=arch=="amd64"||arch=="x86_64"?"x64":arch=="aarch64"?"arm64":arch;if(normalized!=architecture)match=false;}var pattern=Value(os,"version") as string;if(pattern!=null){try{if(!Regex.IsMatch(Environment.OSVersion.Version.ToString(),pattern,RegexOptions.None,TimeSpan.FromMilliseconds(100)))match=false;}catch(ArgumentException){throw new InvalidDataException("系统版本规则无效。");}}}
    var features=Map(Value(r,"features"));if(features!=null&&features.Any(f=>!(f.Value is bool)||(bool)f.Value))match=false;
    string action=Text(r,"action");if(action!="allow"&&action!="disallow")throw new InvalidDataException("依赖规则动作无效。");if(match)allowed=action=="allow";
   }return allowed;
  }
  internal static VanillaFile FileDescriptor(Dictionary<string,object> d,string path){if(d==null)throw new InvalidDataException("文件描述缺失。");return new VanillaFile{Url=Url(Text(d,"url")),Sha1=Hash(Text(d,"sha1")),Size=Number(d,"size"),Path=Relative(path),IsJson=path.EndsWith(".json",StringComparison.OrdinalIgnoreCase)};}
  internal static VanillaPlan Plan(Dictionary<string,object> meta,string arch){
   if(Value(meta,"inheritsFrom")!=null)throw new InvalidDataException("独立下载器只安装纯原版。");
   var downloads=Map(Value(meta,"downloads"));var plan=new VanillaPlan{Client=FileDescriptor(Map(Value(downloads,"client")),"client.jar")};var index=Map(Value(meta,"assetIndex"));plan.IndexId=Name(Text(index,"id"));plan.Index=FileDescriptor(index,"assets/indexes/"+plan.IndexId+".json");
   var libraries=Value(meta,"libraries") as object[];if(libraries==null)throw new InvalidDataException("游戏依赖列表缺失。");
   foreach(var item in libraries){var lib=Map(item);if(!Allowed(lib,arch))continue;var files=Map(Value(lib,"downloads"));var artifact=Map(Value(files,"artifact"));if(artifact!=null)plan.Libraries.Add(FileDescriptor(artifact,"libraries/"+Relative(Text(artifact,"path"))));var natives=Map(Value(lib,"natives"));string classifier=Value(natives,"windows") as string;if(classifier!=null){classifier=classifier.Replace("${arch}",arch=="x86"?"32":"64");var native=Map(Value(Map(Value(files,"classifiers")),classifier));if(native==null)throw new InvalidDataException("Windows 原生依赖缺失："+classifier);plan.Libraries.Add(FileDescriptor(native,"libraries/"+Relative(Text(native,"path"))));}}
   var logging=Map(Value(Map(Value(meta,"logging")),"client"));if(logging!=null){var file=Map(Value(logging,"file"));plan.Logging=FileDescriptor(file,"assets/log_configs/"+Name(Text(file,"id")));}return plan;
  }
  internal static VanillaAssets Assets(Dictionary<string,object> index,string id){
   Name(id);var objects=Map(Value(index,"objects"));if(objects==null||objects.Count>100000)throw new InvalidDataException("资源索引无效或过大。");var result=new VanillaAssets();bool virtualMap=Equals(Value(index,"virtual"),true),resources=Equals(Value(index,"map_to_resources"),true);var hashes=new HashSet<string>();
   foreach(var pair in objects){Relative(pair.Key);var info=Map(pair.Value);string hash=Hash(Text(info,"hash")),source="assets/objects/"+hash.Substring(0,2)+"/"+hash;if(hashes.Add(hash))result.Files.Add(new VanillaFile{Url="https://resources.download.minecraft.net/"+hash.Substring(0,2)+"/"+hash,Sha1=hash,Size=Number(info,"size"),Path=source});if(virtualMap)result.Copies.Add(new VanillaCopy{Source=source,Target="assets/virtual/"+id+"/"+pair.Key});if(resources)result.Copies.Add(new VanillaCopy{Source=source,Target="resources/"+pair.Key});}return result;
  }
 }
}
