using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;
public static class PclNativeProbe {
 static BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
 static object Field(object value,string name){return value.GetType().GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);}
 static object Invoke(MethodInfo method,object target,object[] args){try{return method.Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 [STAThread]
 public static int Main(string[] args){
  int result=1;Assembly launcher=Assembly.GetEntryAssembly();string root=Path.GetDirectoryName(launcher.Location),report=Path.Combine(root,"PCL/Cache/Vanilla/probe-report.json");Directory.CreateDirectory(Path.GetDirectoryName(report));Type settings=null;MethodInfo get=null,set=null;object originalFile=null,originalList=null;
  try{
   if(args.Length<3)throw new ArgumentException("versionId name preference[0 mirror,2 official] [game-dir]");
   Activator.CreateInstance(launcher.GetType("PCL.Application",true),true);
   Assembly core=Assembly.Load("MeloongCore"),wpf=Assembly.Load("MeloongCore.Wpf");core.GetType("MeloongCore.Main",true).GetMethod("Init",new[]{typeof(string)}).Invoke(null,new object[]{"PCL"});wpf.GetType("MeloongCore.Wpf.Main",true).GetMethod("Init",Type.EmptyTypes).Invoke(null,new object[0]);
   Type hooks=launcher.GetType("PCLRestore.Hooks",true);hooks.GetMethod("PrepareEnvironment").Invoke(null,null);hooks.GetMethod("Call").Invoke(null,new object[]{"Initialize",new object[]{launcher.Location,"2.99.0.1",5}});Assembly runtime=(Assembly)hooks.GetField("RuntimeAssembly").GetValue(null);
   settings=launcher.GetType("PCL.Settings",true);foreach(var method in settings.GetMethods())if(method.Name=="Get"&&method.IsGenericMethodDefinition)get=method.MakeGenericMethod(typeof(int));set=settings.GetMethod("Set");originalFile=get.Invoke(null,new object[]{"ToolDownloadSource",null});originalList=get.Invoke(null,new object[]{"ToolDownloadVersion",null});int preference=Int32.Parse(args[2]);if(preference>=0){set.Invoke(null,new object[]{"ToolDownloadSource",preference,null});set.Invoke(null,new object[]{"ToolDownloadVersion",preference,null});}
   Type installerType=runtime.GetType("PCLRestore.Runtime.VanillaInstaller",true);string cache=Path.Combine(root,"PCL/Cache/Vanilla");object installer=installerType.GetConstructors(Hidden)[0].Invoke(new object[]{cache,null});
   using(var cts=new CancellationTokenSource(TimeSpan.FromMinutes(3))){
    var catalog=(IEnumerable)Invoke(installerType.GetMethod("Catalog",Hidden),installer,new object[]{cts.Token});object version=null;foreach(object item in catalog)if((string)Field(item,"Id")==args[0]){version=item;break;}if(version==null)throw new Exception("requested version missing");
    string source=(string)installerType.GetProperty("CatalogSource",Hidden).GetValue(installer,null);string game=args.Length>3?Path.GetFullPath(args[3]):Path.Combine(root,".minecraft");
    Console.WriteLine("PCL native catalog: "+source+"; source preference "+preference);
    Invoke(installerType.GetMethod("Install",Hidden),installer,new object[]{version,game,args[1],cts.Token,null});
    string target=Path.Combine(game,"versions",args[1]);var metadata=new JavaScriptSerializer{MaxJsonLength=20*1024*1024}.DeserializeObject(File.ReadAllText(Path.Combine(target,args[1]+".json"))) as Dictionary<string,object>;
    string actual;using(var hash=System.Security.Cryptography.SHA1.Create())using(var file=File.OpenRead(Path.Combine(target,args[1]+".jar")))actual=BitConverter.ToString(hash.ComputeHash(file)).Replace("-","").ToLowerInvariant();var downloads=(Dictionary<string,object>)metadata["downloads"];var client=(Dictionary<string,object>)downloads["client"];if(actual!=(string)client["sha1"])throw new Exception("client SHA mismatch");
    File.WriteAllText(report,new JavaScriptSerializer().Serialize(new Dictionary<string,object>{{"status","passed"},{"version",args[0]},{"name",args[1]},{"catalogSource",source},{"preference",preference},{"clientSha1",actual}}),new System.Text.UTF8Encoding(false));Console.WriteLine("PASS native PCL install and official client SHA1 "+actual);result=0;
   }
  }catch(Exception e){Console.WriteLine("FAIL "+e);File.WriteAllText(report,new JavaScriptSerializer().Serialize(new Dictionary<string,object>{{"status","failed"},{"error",e.ToString()}}));}
  finally{if(settings!=null&&set!=null&&originalFile!=null&&originalList!=null){set.Invoke(null,new object[]{"ToolDownloadSource",originalFile,null});set.Invoke(null,new object[]{"ToolDownloadVersion",originalList,null});Console.WriteLine("Restored original PCL source preferences");}}
  Environment.Exit(result);return result;
 }
}
