using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;
using PCLRestore.Runtime;
internal static class PclTransportTests {
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 internal static void Run(Action<string,Action> test){
  test("PCL source bridge uses native category and preference ordering",()=>{var bridge=new PclDownloadBridge(typeof(PCL.ModDownload).Assembly);PCL.ModDownload.OfficialFirst=false;var file=new VanillaFile{Path="libraries/a.jar",Url="https://libraries.minecraft.net/a.jar"};Assert(bridge.Sources(file).First().StartsWith("https://bmclapi2.bangbang93.com/maven/"),"mirror order ignored");PCL.ModDownload.OfficialFirst=true;Assert(bridge.Sources(file).First()==file.Url,"official priority ignored");file.Path="assets/objects/aa/x";file.Url="https://resources.download.minecraft.net/aa/x";Assert(bridge.Sources(file).Last().Contains("/assets/aa/x"),"asset source helper not used");file.Path="client.jar";file.Url="https://piston-data.mojang.com/v1/x";Assert(bridge.Sources(file).Last()=="https://bmclapi2.bangbang93.com/v1/x","client source helper not used");});
  test("PCL native mirror policy may supply a new HTTPS host",()=>{var bridge=new PclDownloadBridge(typeof(PCL.ModDownload).Assembly);PCL.ModDownload.NativeMirrorOverride="https://new-pcl-mirror.example/maven/a";try{Assert(bridge.Sources(new VanillaFile{Path="libraries/a.jar",Url="https://libraries.minecraft.net/a.jar"}).Contains(PCL.ModDownload.NativeMirrorOverride),"native mirror replaced by duplicate host list");}finally{PCL.ModDownload.NativeMirrorOverride=null;}});
  test("PCL catalog bridge reuses shared version loader and source name",()=>{var bridge=new PclDownloadBridge(typeof(PCL.ModDownload).Assembly);int count=PCL.ModDownload.DlClientListLoader.Waits;string json=bridge.Catalog(CancellationToken.None);Assert(PCL.ModDownload.DlClientListLoader.Waits==count+1&&json.Contains("versions")&&bridge.CatalogSource=="BMCLAPI","native version loader bypassed");});
  test("PCL file task receives checker and batches multiple native files",()=>{InDirectory(dir=>{PCL.ModNet.LoaderDownload.Last=null;var bridge=new PclDownloadBridge(typeof(PCL.ModDownload).Assembly);var requests=new[]{new PclDownloadRequest{File=new VanillaFile{Url="https://libraries.minecraft.net/a",Path="libraries/a.jar",Sha1=new string('a',40),Size=123},Destination=Path.Combine(dir,"a.bin")},new PclDownloadRequest{File=new VanillaFile{Url="https://piston-meta.mojang.com/a.json",Path="version.json",IsJson=true},Destination=Path.Combine(dir,"b.bin")}};bridge.Download(requests,CancellationToken.None,null);var loader=PCL.ModNet.LoaderDownload.Last;Assert(loader.Files.Length==2&&loader.Waits==1&&loader.Cancelled,"native batch loader was not used/closed");Assert(loader.Files[0].Check.Hash==new string('a',40)&&loader.Files[0].Check.ActualSize==123&&loader.Files[1].Check.IsJson,"native file checker missing");});});
  test("PCL download cancellation reaches native wait and cancels task",()=>{InDirectory(dir=>{var bridge=new PclDownloadBridge(typeof(PCL.ModDownload).Assembly);var cts=new CancellationTokenSource();PCL.ModNet.LoaderDownload.OnWait=()=>cts.Cancel();try{bridge.Download(new[]{new PclDownloadRequest{File=new VanillaFile{Url="https://libraries.minecraft.net/a",Path="libraries/a",Sha1=new string('a',40)},Destination=Path.Combine(dir,"a.bin")}},cts.Token,null);throw new Exception("cancel ignored");}catch(OperationCanceledException){}finally{PCL.ModNet.LoaderDownload.OnWait=null;}Assert(PCL.ModNet.LoaderDownload.Last.Cancelled,"native task remained active after cancel");});});
  test("PCL manifest accepts v1 metadata hash from package URL",()=>{var list=VanillaModel.Catalog("{\"versions\":[{\"id\":\"x\",\"type\":\"release\",\"url\":\"https://bmclapi2.bangbang93.com/v1/packages/"+new string('a',40)+"/x.json\"}]}");Assert(list[0].Sha1==new string('a',40),"v1 package checksum not derived");});
  test("PCL v1 custom metadata without hash retains JSON validation",()=>{var list=VanillaModel.Catalog("{\"versions\":[{\"id\":\"x\",\"type\":\"release\",\"url\":\"https://bmclapi2.bangbang93.com/version/x.json\"}]}");Assert(list.Count==1&&list[0].Sha1==null,"native v1/custom list rejected");});
 }
 static void InDirectory(Action<string> action){string dir=Path.GetFullPath("tests/UpdaterTests/fixtures/pcl-transport-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);action(dir);}
}
namespace MeloongCore {public class ProgressProvider {} public class FileChecker {public long ActualSize=-1;public string Hash;public bool IsJson;}}
namespace PCL {
 public static class ModLoader {public class LoaderBase {}}
 public static class ModDownload {
  public static string NativeMirrorOverride;public static bool OfficialFirst;public static CatalogLoader DlClientListLoader=new CatalogLoader();
  static IEnumerable<string> Order(string original,string mirror){mirror=NativeMirrorOverride??mirror;return OfficialFirst?new[]{original,mirror}:new[]{mirror,original};}
  public static IEnumerable<string> DlSourceLibraryGet(string original){return Order(original,original.Replace("https://libraries.minecraft.net","https://bmclapi2.bangbang93.com/maven"));}
  public static IEnumerable<string> DlSourceAssetsGet(string original){return Order(original,original.Replace("https://resources.download.minecraft.net","https://bmclapi2.bangbang93.com/assets"));}
  public static IEnumerable<string> DlSourceLauncherOrMetaGet(string original){return Order(original,original.Replace("https://piston-data.mojang.com","https://bmclapi2.bangbang93.com").Replace("https://piston-meta.mojang.com","https://bmclapi2.bangbang93.com"));}
 }
 public class CatalogLoader {public int Waits;public CatalogOutput Output=new CatalogOutput();public void WaitForExit(object input,ModLoader.LoaderBase sync,bool force,CancellationToken token,MeloongCore.ProgressProvider progress){token.ThrowIfCancellationRequested();Waits++;}}
 public class CatalogOutput {public string SourceName="BMCLAPI";public object Value=new CatalogJson();}
 public class CatalogJson {public override string ToString(){return "{\"versions\":[]}";}}
 public static class ModNet {
  public class NetFile {public string[] Urls;public string Path;public MeloongCore.FileChecker Check;public NetFile(IEnumerable<string> urls,string path,MeloongCore.FileChecker checker,bool browser){Urls=urls.ToArray();Path=path;Check=checker;}}
  public class LoaderDownload {public static LoaderDownload Last;public static Action OnWait;public NetFile[] Files;public int Waits;public bool Cancelled;public double Progress {get{return 1;}}public LoaderDownload(string name,IEnumerable<NetFile> files){Last=this;Files=files.ToArray();}public void WaitForExit(object input,ModLoader.LoaderBase sync,bool force,CancellationToken token,MeloongCore.ProgressProvider progress){Waits++;if(OnWait!=null)OnWait();token.ThrowIfCancellationRequested();}public void Cancel(){Cancelled=true;}}
 }
}
