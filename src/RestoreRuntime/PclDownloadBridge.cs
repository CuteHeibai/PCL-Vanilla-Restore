using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
namespace PCLRestore.Runtime {
 internal sealed class PclDownloadRequest {internal VanillaFile File;internal string Destination;}
 internal sealed class PclDownloadBridge {
  readonly Type sourceType,fileType,loaderType;readonly MethodInfo wait;readonly ConstructorInfo fileConstructor;readonly FieldInfo catalogField;
  internal string CatalogSource {get;private set;}
  internal PclDownloadBridge(Assembly host=null){
   host=host??Assembly.GetEntryAssembly();if(host==null)throw new InvalidDataException("PCL 下载环境尚未就绪。");
   sourceType=host.GetType("PCL.ModDownload",true);fileType=host.GetType("PCL.ModNet+NetFile",true);loaderType=host.GetType("PCL.ModNet+LoaderDownload",true);
   fileConstructor=fileType.GetConstructors().Single(c=>c.GetParameters().Length==4&&c.GetParameters()[0].ParameterType==typeof(IEnumerable<string>));
   wait=WaitMethod(loaderType);catalogField=sourceType.GetField("DlClientListLoader",BindingFlags.Public|BindingFlags.Static);
   if(catalogField==null)throw new InvalidDataException("PCL 版本列表接口不兼容。");
  }
  static MethodInfo WaitMethod(Type type){return type.GetMethods().Single(m=>m.Name=="WaitForExit"&&!m.IsStatic&&m.ReturnType==typeof(void)&&m.GetParameters().Select(p=>p.ParameterType.FullName).SequenceEqual(new[]{"System.Object","PCL.ModLoader+LoaderBase","System.Boolean","System.Threading.CancellationToken","MeloongCore.ProgressProvider"}));}
  static object Call(MethodInfo method,object target,object[] args){try{return method.Invoke(target,args);}catch(TargetInvocationException e){if(e.InnerException!=null)ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
  static object Get(object target,string name){var type=target.GetType();var field=type.GetField(name,BindingFlags.Public|BindingFlags.Instance);if(field!=null)return field.GetValue(target);var property=type.GetProperty(name,BindingFlags.Public|BindingFlags.Instance);if(property!=null)return property.GetValue(target,null);throw new InvalidDataException("PCL 下载成员缺失："+name);}
  static void Set(object target,string name,object value){var type=target.GetType();var field=type.GetField(name,BindingFlags.Public|BindingFlags.Instance);if(field!=null){field.SetValue(target,value);return;}var property=type.GetProperty(name,BindingFlags.Public|BindingFlags.Instance);if(property!=null&&property.CanWrite){property.SetValue(target,value,null);return;}throw new InvalidDataException("PCL 校验器成员缺失："+name);}
  internal string[] Sources(VanillaFile file){
   VanillaModel.Url(file.Url);string method=file.Path.StartsWith("libraries/",StringComparison.Ordinal)?"DlSourceLibraryGet":file.Path.StartsWith("assets/objects/",StringComparison.Ordinal)?"DlSourceAssetsGet":"DlSourceLauncherOrMetaGet";
   var result=(IEnumerable<string>)Call(sourceType.GetMethod(method,BindingFlags.Public|BindingFlags.Static),null,new object[]{file.Url});var urls=result.Distinct().ToArray();if(urls.Length==0||urls.Length>16)throw new InvalidDataException("PCL 未提供可用下载源。");
   // Candidate hosts come from trusted native PCL policy; do not keep a second mirror-host list here.
   foreach(string url in urls){Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https"||!uri.IsDefaultPort||!String.IsNullOrEmpty(uri.UserInfo))throw new InvalidDataException("PCL 下载候选地址无效。");}return urls;
  }
  internal string Catalog(CancellationToken token){
   token.ThrowIfCancellationRequested();object loader=catalogField.GetValue(null);Call(WaitMethod(loader.GetType()),loader,new object[]{null,null,false,token,null});token.ThrowIfCancellationRequested();object output=Get(loader,"Output");CatalogSource=(string)Get(output,"SourceName");return Get(output,"Value").ToString();
  }
  internal void Download(IEnumerable<PclDownloadRequest> input,CancellationToken token,Action<double> progress){
   token.ThrowIfCancellationRequested();var requests=input.ToArray();if(requests.Length==0)return;Array files=Array.CreateInstance(fileType,requests.Length);Type checkerType=fileConstructor.GetParameters()[2].ParameterType;
   for(int i=0;i<requests.Length;i++){var request=requests[i];var file=request.File;string[] urls=Sources(file);object check=Activator.CreateInstance(checkerType);if(!String.IsNullOrEmpty(file.Sha1))Set(check,"Hash",VanillaModel.Hash(file.Sha1));if(file.Size>=0)Set(check,"ActualSize",file.Size);if(file.IsJson)Set(check,"IsJson",true);Directory.CreateDirectory(Path.GetDirectoryName(request.Destination));files.SetValue(fileConstructor.Invoke(new object[]{urls,request.Destination,check,false}),i);}
   object loader=Activator.CreateInstance(loaderType,new object[]{"独立原版下载 · PCL 下载任务",files});Timer timer=null;
   try{
    if(progress!=null)timer=new Timer(_=>{try{progress(Convert.ToDouble(Get(loader,"Progress")));}catch{}},null,0,150);
    Call(wait,loader,new object[]{null,null,false,token,null});token.ThrowIfCancellationRequested();if(progress!=null)progress(1);
   }finally{if(timer!=null)timer.Dispose();Call(loaderType.GetMethod("Cancel",Type.EmptyTypes),loader,new object[0]);}
  }
 }
}
