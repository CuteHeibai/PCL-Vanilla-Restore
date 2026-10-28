using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;
namespace PCLRestore.Runtime {
 internal sealed class VanillaProgress {internal string Message;internal int Done,Total;}
 internal sealed class VanillaInstaller {
  [DllImport("kernel32.dll",EntryPoint="CreateDirectoryW",CharSet=CharSet.Unicode,SetLastError=true)]
  static extern bool CreateDirectoryExclusive(string path,IntPtr securityAttributes);
  readonly string cache;
  internal Action<string,string,CancellationToken> Fetch=HttpDownload;
  internal VanillaInstaller(string cacheDirectory){cache=Path.GetFullPath(cacheDirectory);}
  internal List<VanillaVersion> Catalog(CancellationToken token){Directory.CreateDirectory(cache);string temporary=Path.Combine(cache,"manifest-"+Guid.NewGuid().ToString("N")+".bin");try{Fetch(VanillaModel.ManifestUrl,temporary,token);if(new FileInfo(temporary).Length>20*1024*1024)throw new InvalidDataException("版本清单过大。");return VanillaModel.Catalog(File.ReadAllText(temporary));}finally{if(File.Exists(temporary))File.Delete(temporary);}}
  static string FileHash(string path){using(var h=SHA1.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
  static bool Valid(string path,VanillaFile file){return File.Exists(path)&&(file.Size<0||new FileInfo(path).Length==file.Size)&&String.Equals(FileHash(path),file.Sha1,StringComparison.OrdinalIgnoreCase);}
  void Transfer(VanillaFile file,string destination,string staging,CancellationToken token){
   token.ThrowIfCancellationRequested();VanillaModel.Url(file.Url);VanillaModel.Hash(file.Sha1);if(Valid(destination,file))return;
   string temporary=Path.Combine(staging,Guid.NewGuid().ToString("N")+".bin");
   try {Fetch(file.Url,temporary,token);token.ThrowIfCancellationRequested();if(!Valid(temporary,file))throw new InvalidDataException("文件校验失败："+file.Path);Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(temporary,destination,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
  internal void Install(VanillaVersion version,string gameDirectory,string name,CancellationToken token,Action<VanillaProgress> report){
   if(version==null)throw new InvalidDataException("请选择游戏版本。");VanillaModel.Name(name);VanillaModel.Name(version.Id);VanillaModel.Url(version.Url);VanillaModel.Hash(version.Sha1);token.ThrowIfCancellationRequested();
   string game=Path.GetFullPath(gameDirectory),target=VanillaModel.Under(game,"versions/"+name);if(Directory.Exists(target)||File.Exists(target))throw new InvalidDataException("同名版本已存在，请使用其它名称。");Directory.CreateDirectory(cache);
   string lockKey=VanillaModel.Sha1(Encoding.UTF8.GetBytes(target.ToLowerInvariant())),stage=Path.Combine(cache,"install-"+Guid.NewGuid().ToString("N"));
   using(var gate=new FileStream(Path.Combine(cache,"install-"+lockKey+".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
    Directory.CreateDirectory(stage);bool ownsTarget=false;string marker=Path.Combine(target,".pclrestore-installing");var created=new List<string>();
    try {
     Report(report,"读取并校验版本信息",0,0);
     var metadataFile=new VanillaFile{Url=version.Url,Sha1=version.Sha1,Path="version.json"};string metadataPath=Path.Combine(stage,"version.json");Transfer(metadataFile,metadataPath,stage,token);if(new FileInfo(metadataPath).Length>20*1024*1024)throw new InvalidDataException("版本信息过大。");
     var metadata=VanillaModel.Json(File.ReadAllText(metadataPath));if(VanillaModel.Text(metadata,"id")!=version.Id)throw new InvalidDataException("版本信息与选择不一致。");var plan=VanillaModel.Plan(metadata,VanillaModel.Architecture);
     string indexPath=VanillaModel.Under(game,plan.Index.Path);Transfer(plan.Index,indexPath,stage,token);var assets=VanillaModel.Assets(VanillaModel.Json(File.ReadAllText(indexPath)),plan.IndexId);
     var files=new List<VanillaFile>();files.AddRange(plan.Libraries);files.AddRange(assets.Files);if(plan.Logging!=null)files.Add(plan.Logging);
     var unique=new Dictionary<string,VanillaFile>(StringComparer.OrdinalIgnoreCase);foreach(var file in files){VanillaFile prior;if(unique.TryGetValue(file.Path,out prior)&&prior.Sha1!=file.Sha1)throw new InvalidDataException("依赖文件路径冲突。");unique[file.Path]=file;}
     int total=unique.Count+1,done=0;Report(report,"下载游戏客户端",done,total);string client=Path.Combine(stage,"client.jar");Transfer(plan.Client,client,stage,token);Interlocked.Increment(ref done);
     try {Parallel.ForEach(unique.Values,new ParallelOptions{MaxDegreeOfParallelism=8,CancellationToken=token},file=>{Transfer(file,VanillaModel.Under(game,file.Path),stage,token);int n=Interlocked.Increment(ref done);Report(report,"校验/下载 "+file.Path,n,total);});}
     catch(AggregateException e){var first=e.Flatten().InnerExceptions.FirstOrDefault(x=>!(x is OperationCanceledException));if(first!=null)throw new InvalidDataException("下载未完成："+first.Message,first);throw new OperationCanceledException(token);}
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
  internal static void HttpDownload(string address,string path,CancellationToken token){
   for(int attempt=0;;attempt++) {
    token.ThrowIfCancellationRequested();try {
     string current=VanillaModel.Url(address);
     for(int redirect=0;redirect<6;redirect++) {
      var request=(HttpWebRequest)WebRequest.Create(current);request.AllowAutoRedirect=false;request.Timeout=20000;request.ReadWriteTimeout=30000;request.UserAgent="PCLRestore-Vanilla/1";
      using(token.Register(()=>request.Abort()))using(var response=(HttpWebResponse)request.GetResponse()) {
       int status=(int)response.StatusCode;if(status>=300&&status<400){var location=response.Headers["Location"];if(String.IsNullOrEmpty(location))throw new IOException("游戏文件重定向无效。");current=VanillaModel.Url(new Uri(new Uri(current),location).AbsoluteUri);continue;}
       if(status!=200)throw new IOException("游戏文件服务器返回异常状态。");if(response.ContentLength>4L*1024*1024*1024)throw new InvalidDataException("游戏文件超出大小限制。");
       using(var input=response.GetResponseStream())using(var output=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None)) {byte[] buffer=new byte[81920];long total=0;int n;while((n=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();total+=n;if(total>4L*1024*1024*1024)throw new InvalidDataException("游戏文件超出大小限制。");output.Write(buffer,0,n);}}return;
      }
     }throw new IOException("游戏文件重定向过多。");
    }catch(WebException){token.ThrowIfCancellationRequested();if(attempt>=2)throw;token.WaitHandle.WaitOne(500*(attempt+1));}
   }
  }
 }
}
