using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;
namespace PCLRestore.Runtime {
 internal sealed class VanillaProgress {internal string Message;internal int Done,Total;}
 internal sealed class VanillaInstaller {
  [DllImport("kernel32.dll",EntryPoint="CreateDirectoryW",CharSet=CharSet.Unicode,SetLastError=true)]
  static extern bool CreateDirectoryExclusive(string path,IntPtr securityAttributes);
  readonly string cache;
  internal Action<string,string,CancellationToken> Fetch;
  PclDownloadBridge bridge;
  PclDownloadBridge Bridge {get{return bridge??(bridge=new PclDownloadBridge());}}
  internal string CatalogSource {get;private set;}
  internal VanillaInstaller(string cacheDirectory,PclDownloadBridge transport=null){cache=Path.GetFullPath(cacheDirectory);bridge=transport;}
  internal List<VanillaVersion> Catalog(CancellationToken token){
   if(Fetch==null){var result=VanillaModel.Catalog(Bridge.Catalog(token));CatalogSource=Bridge.CatalogSource;return result;}
   Directory.CreateDirectory(cache);string temporary=Path.Combine(cache,"manifest-"+Guid.NewGuid().ToString("N")+".bin");try{Fetch(VanillaModel.ManifestUrl,temporary,token);return VanillaModel.Catalog(File.ReadAllText(temporary));}finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
  static string FileHash(string path){using(var h=SHA1.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
  static bool Valid(string path,VanillaFile file){if(!File.Exists(path)||(file.Size>=0&&new FileInfo(path).Length!=file.Size))return false;if(!String.IsNullOrEmpty(file.Sha1))return String.Equals(FileHash(path),file.Sha1,StringComparison.OrdinalIgnoreCase);if(!file.IsJson||new FileInfo(path).Length>20*1024*1024)return false;try{VanillaModel.Json(File.ReadAllText(path));return true;}catch(InvalidDataException){return false;}}
  void Transfer(VanillaFile file,string destination,string staging,CancellationToken token){
   token.ThrowIfCancellationRequested();VanillaModel.Url(file.Url);if(!String.IsNullOrEmpty(file.Sha1))VanillaModel.Hash(file.Sha1);else if(!file.IsJson)throw new InvalidDataException("非元数据文件必须有校验值。");if(Valid(destination,file))return;
   string temporary=Path.Combine(staging,Guid.NewGuid().ToString("N")+".bin");
   try {if(Fetch!=null)Fetch(file.Url,temporary,token);else Bridge.Download(new[]{new PclDownloadRequest{File=file,Destination=temporary}},token,null);token.ThrowIfCancellationRequested();if(!Valid(temporary,file))throw new InvalidDataException("文件校验失败："+file.Path);Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(temporary,destination,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
  internal void Install(VanillaVersion version,string gameDirectory,string name,CancellationToken token,Action<VanillaProgress> report){
   if(version==null)throw new InvalidDataException("请选择游戏版本。");VanillaModel.Name(name);VanillaModel.Name(version.Id);VanillaModel.Url(version.Url);if(version.Sha1!=null)VanillaModel.Hash(version.Sha1);token.ThrowIfCancellationRequested();
   string game=Path.GetFullPath(gameDirectory),target=VanillaModel.Under(game,"versions/"+name);if(Directory.Exists(target)||File.Exists(target))throw new InvalidDataException("同名版本已存在，请使用其它名称。");Directory.CreateDirectory(cache);
   string lockKey=VanillaModel.Sha1(Encoding.UTF8.GetBytes(target.ToLowerInvariant())),stage=Path.Combine(cache,"install-"+Guid.NewGuid().ToString("N"));
   using(var gate=new FileStream(Path.Combine(cache,"install-"+lockKey+".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
    Directory.CreateDirectory(stage);bool ownsTarget=false;string marker=Path.Combine(target,".pclrestore-installing");var created=new List<string>();
    try {
     Report(report,"读取并校验版本信息",0,0);
     var metadataFile=new VanillaFile{Url=version.Url,Sha1=version.Sha1,Path="version.json",IsJson=true};string metadataPath=Path.Combine(stage,"version.json");Transfer(metadataFile,metadataPath,stage,token);if(new FileInfo(metadataPath).Length>20*1024*1024)throw new InvalidDataException("版本信息过大。");
     var metadata=VanillaModel.Json(File.ReadAllText(metadataPath));if(VanillaModel.Text(metadata,"id")!=version.Id)throw new InvalidDataException("版本信息与选择不一致。");var plan=VanillaModel.Plan(metadata,VanillaModel.Architecture);
     string indexPath=VanillaModel.Under(game,plan.Index.Path);Transfer(plan.Index,indexPath,stage,token);var assets=VanillaModel.Assets(VanillaModel.Json(File.ReadAllText(indexPath)),plan.IndexId);
     var files=new List<VanillaFile>();files.AddRange(plan.Libraries);files.AddRange(assets.Files);if(plan.Logging!=null)files.Add(plan.Logging);
     var unique=new Dictionary<string,VanillaFile>(StringComparer.OrdinalIgnoreCase);foreach(var file in files){VanillaFile prior;if(unique.TryGetValue(file.Path,out prior)&&prior.Sha1!=file.Sha1)throw new InvalidDataException("依赖文件路径冲突。");unique[file.Path]=file;}
     int total=unique.Count+1,done=0;Report(report,"下载游戏客户端",done,total);string client=Path.Combine(stage,"client.jar");Transfer(plan.Client,client,stage,token);Interlocked.Increment(ref done);
     if(Fetch!=null){foreach(var file in unique.Values){Transfer(file,VanillaModel.Under(game,file.Path),stage,token);Report(report,"校验/下载 "+file.Path,++done,total);}}
     else {
      var requests=new List<PclDownloadRequest>();foreach(var file in unique.Values){token.ThrowIfCancellationRequested();string destination=VanillaModel.Under(game,file.Path);if(Valid(destination,file)){done++;continue;}requests.Add(new PclDownloadRequest{File=file,Destination=destination});}
      int reused=done;Bridge.Download(requests,token,p=>Report(report,"PCL 下载引擎：支持库与资源文件",reused+(int)(p*requests.Count),total));
      foreach(var request in requests){token.ThrowIfCancellationRequested();if(!Valid(request.Destination,request.File))throw new InvalidDataException("文件校验失败："+request.File.Path);}
     }
     foreach(var copy in assets.Copies){token.ThrowIfCancellationRequested();string destination=VanillaModel.Under(game,copy.Target);Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(VanillaModel.Under(game,copy.Source),destination,true);}
     string ready=Path.Combine(stage,"version");Directory.CreateDirectory(Path.Combine(ready,"PCL"));
     File.Copy(client,Path.Combine(ready,name+".jar"));
     var config=new{MinecraftInstallManifest=new{SchemaVersion=1,Components=new{MinecraftVersion=version.Id,ModLoaderVersions=new Dictionary<string,string>()},LastModifiedUtc=DateTime.UtcNow.ToString("o"),ManagedFiles=new object[0]}};
     File.WriteAllText(Path.Combine(ready,"PCL","config.json"),VanillaModel.Serialize(config),new UTF8Encoding(false));metadata["id"]=name;
     File.WriteAllText(Path.Combine(ready,name+".json"),VanillaModel.Serialize(metadata),new UTF8Encoding(false));
     token.ThrowIfCancellationRequested();Directory.CreateDirectory(Path.GetDirectoryName(target));
     // On the same volume the whole ready version becomes visible atomically; never stage game data outside ExeDir/PCL.
     if(String.Equals(Path.GetPathRoot(ready),Path.GetPathRoot(target),StringComparison.OrdinalIgnoreCase)) {
      try {Directory.Move(ready,target);Report(report,"安装完成："+name,total,total);return;}
      catch(IOException e){if((e.HResult&65535)!=17)throw new InvalidDataException("无法发布版本目录，已有同名版本时不会覆盖。",e);}
     }
     if(!CreateDirectoryExclusive(target,IntPtr.Zero)){int error=Marshal.GetLastWin32Error();if(error==183)throw new InvalidDataException("安装期间出现同名版本，已取消覆盖。");throw new IOException("无法创建版本目录。",new Win32Exception(error));}
     ownsTarget=true;
     // Across volumes copy only into the exclusively claimed directory, keep metadata locked until flushed, and request a version-list refresh after success.
     WriteNew(marker,new byte[0],created);
     using(var input=File.OpenRead(client))using(var output=new FileStream(Path.Combine(target,name+".jar"),FileMode.CreateNew,FileAccess.Write,FileShare.None)){created.Add(Path.Combine(target,name+".jar"));input.CopyTo(output);output.Flush(true);}
     Directory.CreateDirectory(Path.Combine(target,"PCL"));
     WriteNew(Path.Combine(target,"PCL","config.json"),Encoding.UTF8.GetBytes(VanillaModel.Serialize(config)),created);
     token.ThrowIfCancellationRequested();WriteNew(Path.Combine(target,name+".json"),Encoding.UTF8.GetBytes(VanillaModel.Serialize(metadata)),created);File.Delete(marker);ownsTarget=false;Report(report,"安装完成："+name,total,total);
    }finally{
     if(ownsTarget){foreach(string path in created)if(File.Exists(path))File.Delete(path);string pcl=Path.Combine(target,"PCL");if(Directory.Exists(pcl)&&!Directory.EnumerateFileSystemEntries(pcl).Any())Directory.Delete(pcl);if(Directory.Exists(target)&&!Directory.EnumerateFileSystemEntries(target).Any())Directory.Delete(target);}
     if(Directory.Exists(stage))Directory.Delete(stage,true);
    }
   }
  }
  static void WriteNew(string path,byte[] bytes,List<string> created){using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){created.Add(path);output.Write(bytes,0,bytes.Length);output.Flush(true);}}
  static void Report(Action<VanillaProgress> callback,string message,int done,int total){if(callback!=null)callback(new VanillaProgress{Message=message,Done=done,Total=total});}
 }
}
