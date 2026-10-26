using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;
using System.Web.Script.Serialization;
[assembly: InternalsVisibleTo("UpdaterTests")]
namespace PCLRestore.Runtime {
 internal sealed class Feed { internal Version Version; internal int Revision; internal string Url, Sha; }
 internal static class UpdateCore {
  internal static Dictionary<string,object> Json(string json) {
   try { var d=new JavaScriptSerializer {MaxJsonLength=1048576}.DeserializeObject(json) as Dictionary<string,object>; if(d==null) throw new InvalidDataException("更新信息格式无效。"); return d; }
   catch(InvalidDataException){throw;} catch(Exception e){throw new InvalidDataException("更新信息格式无效。",e);}
  }
  internal static string Str(Dictionary<string,object> d,string key) {object v;if(!d.TryGetValue(key,out v)||!(v is string)) throw new InvalidDataException("缺少更新字段："+key);return (string)v;}
  internal static int Int(Dictionary<string,object> d,string key) {object v;if(!d.TryGetValue(key,out v)||!(v is int)) throw new InvalidDataException("更新数字字段无效："+key);return (int)v;}
  internal static Version Version4(string value) {Version v;if(!Regex.IsMatch(value??"",@"^\d+\.\d+\.\d+\.\d+$")||!Version.TryParse(value,out v)) throw new InvalidDataException("更新版本号无效。");return v;}
  internal static Feed ParseFeed(string json) {
   var d=Json(json); if(Int(d,"schemaVersion")!=1) throw new InvalidDataException("不支持的更新协议。");
   var f=new Feed {Version=Version4(Str(d,"upstreamVersion")),Revision=Int(d,"patchRevision"),Url=Str(d,"artifactUrl"),Sha=Str(d,"artifactSha256").ToLowerInvariant()};
   Uri uri;if(f.Revision<0||!Regex.IsMatch(f.Sha,"^[0-9a-f]{64}$")||!Uri.TryCreate(f.Url,UriKind.Absolute,out uri)||uri.Scheme!=Uri.UriSchemeHttps||!String.IsNullOrEmpty(uri.UserInfo)) throw new InvalidDataException("更新地址或校验信息无效。");
   return f;
  }
  internal static bool IsNewer(Feed feed, Version current, int revision) {return feed.Version>current||(feed.Version==current&&feed.Revision>revision);}
  internal static Feed TryFeed(Func<string> fetch) {try{return ParseFeed(fetch());}catch{return null;}}
  internal static string Hash(string path) {using(var sha=SHA256.Create())using(var file=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
  internal static void VerifyImage(string path,Feed feed,string identity) {
   if(!String.Equals(Hash(path),feed.Sha,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("更新文件校验失败。");
   try {
    byte[] bytes=File.ReadAllBytes(path);
    if(bytes.Length<128||bytes[0]!=77||bytes[1]!=90)throw new InvalidDataException("更新文件不是可执行程序。");
    int pe=BitConverter.ToInt32(bytes,60);if(pe<64||pe>bytes.Length-24||BitConverter.ToUInt32(bytes,pe)!=0x4550)throw new InvalidDataException("更新文件 PE 标识无效。");
    ushort flags=BitConverter.ToUInt16(bytes,pe+22);if((flags&2)==0||(flags&0x2000)!=0)throw new InvalidDataException("更新文件不是启动器。");
    MetadataDependency.EnsureLoaded();
    VerifyMetadata(bytes,feed,identity);
   }catch(InvalidDataException){throw;}catch(Exception e){throw new InvalidDataException("更新程序元数据无法验证。",e);}
  }
  // Kept separate so the embedded metadata reader is loaded before this method is JIT compiled.
  [MethodImpl(MethodImplOptions.NoInlining)]
  static void VerifyMetadata(byte[] bytes,Feed feed,string identity) {
   using(var module=dnlib.DotNet.ModuleDefMD.Load(bytes,new dnlib.DotNet.ModuleCreationOptions {TryToLoadPdbFromDisk=false})) {
    if(module.Assembly==null||!String.Equals(module.Assembly.Name.String,identity,StringComparison.Ordinal)||module.Assembly.Version!=feed.Version)throw new InvalidDataException("更新程序名称或版本不匹配。");
    dnlib.DotNet.EmbeddedResource marker=null;
    foreach(var resource in module.Resources)if(resource.Name=="PCLRestore.Profile.json")marker=resource as dnlib.DotNet.EmbeddedResource;
    if(marker==null||marker.Length>65536)throw new InvalidDataException("更新程序缺少适配标记。");
    var profile=Json(Encoding.UTF8.GetString(marker.CreateReader().ReadBytes((int)marker.Length)).TrimStart('\uFEFF'));
    object vanilla,portable;
    if(Version4(Str(profile,"upstreamVersion"))!=feed.Version||Int(profile,"patchRevision")!=feed.Revision||Int(profile,"updaterProtocol")!=1||!profile.TryGetValue("vanillaDownload",out vanilla)||!(vanilla is bool)||!(bool)vanilla||!profile.TryGetValue("portableTemp",out portable)||!(portable is bool)||!(bool)portable)throw new InvalidDataException("更新程序未完成所需适配。");
   }
  }
 }
}
