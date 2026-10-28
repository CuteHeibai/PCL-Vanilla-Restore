using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
namespace PCLRestore.Runtime {
 public static class Entry {
  // Kept for verifying/bootstrap-upgrading old r2 clients; normal updates use OfficialSource.
  internal const string FeedUrl="https://raw.githubusercontent.com/CuteHeibai/PCL-Vanilla-Restore/main/update/stable.json";
  static readonly object StateGate=new object();
  static string exe,root,sourceCommit="",sourceSha="",vanillaMode="native";static Version version;static int revision,busy;
  public static object Initialize(object[] args) {
   try {
    if(args==null||args.Length!=3||!(args[0] is string)||!(args[1] is string)||!(args[2] is int))return false;
    string path=Path.GetFullPath((string)args[0]);var v=UpdateCore.Version4((string)args[1]);int r=(int)args[2];
    if(r<0||!File.Exists(path)||!String.Equals(Path.GetExtension(path),".exe",StringComparison.OrdinalIgnoreCase))return false;
    lock(StateGate) {
     exe=path;root=Path.Combine(Path.GetDirectoryName(path),"PCL","Cache","Update");version=v;revision=r;sourceCommit="";sourceSha="";
     vanillaMode="native";
     using(var stream=Assembly.GetEntryAssembly().GetManifestResourceStream("PCLRestore.Profile.json"))if(stream!=null)using(var reader=new StreamReader(stream)){var profile=UpdateCore.Json(reader.ReadToEnd());sourceCommit=UpdateCore.Str(profile,"sourceCommit");sourceSha=UpdateCore.Str(profile,"sourceExeSha256");object mode;if(profile.TryGetValue("vanillaMode",out mode)&&mode is string)vanillaMode=(string)mode;}
    }
    return true;
   }catch{return false;}
  }
  public static object MarkReady(object[] args) {
   try {
    if(exe==null)return false;string pending=Path.Combine(root,"pending.json");if(!File.Exists(pending))return false;
    var p=UpdateCore.Json(File.ReadAllText(pending));string session=UpdateCore.Str(p,"session"),sha=UpdateCore.Str(p,"expectedSha256");Guid guid;
    if(!Guid.TryParseExact(session,"N",out guid)||!String.Equals(UpdateCore.Hash(exe),sha,StringComparison.Ordinal))return false;
    WriteJson(Path.Combine(root,"ready.json"),new {session=session,expectedSha256=sha,pid=Process.GetCurrentProcess().Id});return true;
   }catch{return false;}
  }
  public static object CheckUpdates(object[] args) {return Check(false);}
  public static object CheckSilently(object[] args) {return Check(true);}
  static object Check(bool silent) {
   if(exe==null){if(!silent)Message("更新模块尚未就绪。",true);return false;}
   if(Interlocked.CompareExchange(ref busy,1,0)!=0){if(!silent)Message("正在检查或安装更新，请稍候。");return false;}
   FileStream gate=null;bool helperLaunched=false;string ownSession=null;
   try {
    var settings=Settings.Read(Path.Combine(root,"settings.json"));if(silent&&!settings.CheckAutomatically)return false;
    Directory.CreateDirectory(root);
    try {gate=new FileStream(Path.Combine(root,"update.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){if(!silent)Message("另一项更新正在进行，请稍后重试。");return false;}
    string pendingPath=Path.Combine(root,"pending.json");
    if(File.Exists(pendingPath)) {
     var pending=UpdateCore.Json(File.ReadAllText(pendingPath));object helper;
     if(pending.TryGetValue("helperPid",out helper)&&helper is int&&(int)helper>0) {
      try {var process=Process.GetProcessById((int)helper);if(!process.HasExited){if(!silent)Message("上一项更新正在进行。");return false;}}catch(ArgumentException){}
     }
     File.Delete(pendingPath);
    }
    OfficialRelease release;
    try {release=OfficialSource.ParseCommitList(ReadFeed(OfficialSource.CommitApiUrl));}catch(Exception e){Log("无法查询官方更新："+e.Message);if(!silent)Message("暂时无法获取官方正式版更新，请稍后重试。",true);return false;}
    if(String.Equals(release.Commit,sourceCommit,StringComparison.OrdinalIgnoreCase)){if(!silent)Message("当前已是最新官方正式版，原版下载恢复功能已启用。");return false;}
    string noUpdateState=Path.Combine(root,"unchanged-official.json"),installedSha=UpdateCore.Hash(exe);
    if(OfficialUpdate.IsKnownNoUpdate(noUpdateState,release.Commit,installedSha)){if(!silent)Message("已核对此官方安装包，没有更新的可用版本，当前启动器保持不变。");return false;}
    if((!silent||settings.PromptBeforeInstall)&&!Confirm("官方正式版安装包已更新。\n"+release.Message+"\n是否下载并恢复原版下载功能？\n验证通过后才替换并重启；无法恢复时保留当前版本。"))return false;
    string archive=Path.Combine(root,"official-"+release.Commit+".zip.bin"),raw=Path.Combine(root,"official-source.bin"),candidate=Path.Combine(root,"candidate.bin");
    Download(release.ArchiveUrl,archive,512L*1024*1024);OfficialSource.ExtractOriginal(archive,raw);
    Version incoming=OfficialUpdate.InspectVersion(raw);string incomingSha=UpdateCore.Hash(raw);
    if(!OfficialUpdate.ShouldInstall(incoming,incomingSha,version,sourceSha)){OfficialUpdate.RememberNoUpdate(noUpdateState,release.Commit,installedSha);if(!silent)Message("官方安装包没有更新的可用版本，当前启动器保持不变。");return false;}
    var prepared=OfficialUpdate.Prepare(raw,exe,candidate,revision,release.Commit);
    Log("官方原包已在本机适配并验证："+prepared.Version+"，模式 "+prepared.Mode+"，源 "+prepared.SourceSha+"，成品 "+prepared.Sha);
    var transaction=new Transaction {Directory=root,Exe=exe,Candidate=candidate,Backup=Path.Combine(root,"previous.bak"),Session=Guid.NewGuid().ToString("N"),ExpectedSha=prepared.Sha,OriginalSha=UpdateCore.Hash(exe),Pid=Process.GetCurrentProcess().Id};
    ownSession=transaction.Session;
    string powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","powershell.exe");
    if(!File.Exists(powershell))throw new IOException("未找到 Windows PowerShell。");
    string scriptPath=Path.Combine(root,"replace.ps1"),startedPath=Path.Combine(root,"helper-started.json");if(File.Exists(startedPath))File.Delete(startedPath);
    File.WriteAllText(scriptPath,ReplacementScript.Build(transaction),new UTF8Encoding(true));
    WriteJson(pendingPath,new {session=transaction.Session,expectedSha256=transaction.ExpectedSha,helperPid=0});
    // Respect machine and user PowerShell policy; no ExecutionPolicy override.
    var helperProcess=Process.Start(new ProcessStartInfo(powershell,"-NoProfile -NonInteractive -File \""+scriptPath+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=root});
    if(helperProcess==null)throw new IOException("无法启动更新程序。");
    WriteJson(pendingPath,new {session=transaction.Session,expectedSha256=transaction.ExpectedSha,helperPid=helperProcess.Id});
    var until=DateTime.UtcNow.AddSeconds(8);bool started=false;
    while(DateTime.UtcNow<until&&!helperProcess.HasExited) {
     if(File.Exists(startedPath)) {try{var hello=UpdateCore.Json(File.ReadAllText(startedPath));if(UpdateCore.Str(hello,"session")==ownSession&&UpdateCore.Int(hello,"pid")==helperProcess.Id){started=true;break;}}catch{}}
     Thread.Sleep(100);
    }
    if(!started)throw new IOException("更新脚本未能启动，请检查 PowerShell 策略。");
    helperLaunched=true;Log("已启动官方更新与本地恢复事务 "+ownSession);
    gate.Dispose();gate=null;
    var app=Application.Current;if(app==null)throw new InvalidOperationException("启动器窗口尚未就绪。");
    app.Dispatcher.BeginInvoke(new Action(()=>app.Shutdown()));
    return true;
   }catch(Exception e){Log("官方更新/本地恢复未完成："+e.Message);if(!silent)Message("官方更新或自动恢复未完成，当前启动器保持不变。\n"+e.Message,true);return false;}
   finally {
    if(gate!=null)gate.Dispose();
    if(!helperLaunched&&ownSession!=null){try{string path=Path.Combine(root,"pending.json");if(File.Exists(path)&&UpdateCore.Str(UpdateCore.Json(File.ReadAllText(path)),"session")==ownSession)File.Delete(path);}catch{}}
    Interlocked.Exchange(ref busy,0);
   }
  }
  internal static void WriteJson(string path,object value) {
   string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try {File.WriteAllText(temp,new JavaScriptSerializer().Serialize(value),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
  }
  static void Log(string value){try{if(root!=null){Directory.CreateDirectory(root);File.AppendAllText(Path.Combine(root,"update.log"),DateTime.UtcNow.ToString("o")+" "+value+Environment.NewLine);}}catch{}}
  static T OnUi<T>(Func<T> work){var app=Application.Current;return app!=null&&!app.Dispatcher.CheckAccess()?app.Dispatcher.Invoke(work):work();}
  static int NativeMessage(string text,string first,string second,bool warning){return OnUi(()=>Convert.ToInt32(Assembly.GetEntryAssembly().GetType("PCLRestore.Hooks",true).GetMethod("NativeMessage").Invoke(null,new object[]{text,"检查更新",first,second,warning,Assembly.GetEntryAssembly()})));}
  static void Message(string text,bool warning=false){NativeMessage(text,"确定","",warning);}
  static bool Confirm(string text){return NativeMessage(text,"下载并更新","取消",false)==1;}
  static HttpWebResponse Request(string address) {
   for(int redirects=0;redirects<=5;redirects++) {
    Uri uri;if(!Uri.TryCreate(address,UriKind.Absolute,out uri)||uri.Scheme!=Uri.UriSchemeHttps)throw new InvalidDataException("更新地址必须使用 HTTPS。");
    var req=(HttpWebRequest)WebRequest.Create(uri);req.AllowAutoRedirect=false;req.Timeout=15000;req.ReadWriteTimeout=30000;req.UserAgent="PCLRestore-Updater/1";
    var response=(HttpWebResponse)req.GetResponse();int status=(int)response.StatusCode;
    if(status>=300&&status<400){string location=response.Headers["Location"];response.Dispose();if(location==null)throw new IOException("更新源重定向无效。");address=new Uri(uri,location).AbsoluteUri;continue;}
    if(status!=200){response.Dispose();throw new IOException("更新源返回异常状态。");}return response;
   }
   throw new IOException("更新源重定向过多。");
  }
  static string ReadFeed(string address){using(var response=Request(address))using(var source=response.GetResponseStream())using(var buffer=new MemoryStream()){CopyLimited(source,buffer,1048576);return Encoding.UTF8.GetString(buffer.ToArray()).TrimStart('\uFEFF');}}
  static void Download(string address,string path,long maximum){using(var response=Request(address))using(var source=response.GetResponseStream())using(var target=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None))CopyLimited(source,target,maximum);}
  static void CopyLimited(Stream source,Stream target,long maximum){byte[] buffer=new byte[81920];long total=0;int n;while((n=source.Read(buffer,0,buffer.Length))!=0){total+=n;if(total>maximum)throw new IOException("更新文件超过大小限制。");target.Write(buffer,0,n);}}
 }
 internal sealed class Settings {
  internal bool CheckAutomatically=true,PromptBeforeInstall=true;
  internal static Settings Read(string file) {
   var settings=new Settings();try{if(File.Exists(file)){var d=UpdateCore.Json(File.ReadAllText(file));object v;if(d.TryGetValue("checkAutomatically",out v)&&v is bool)settings.CheckAutomatically=(bool)v;if(d.TryGetValue("promptBeforeInstall",out v)&&v is bool)settings.PromptBeforeInstall=(bool)v;}}catch{}
   return settings;
  }
 }
}
