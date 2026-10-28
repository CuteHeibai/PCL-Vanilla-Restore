using dnlib.DotNet;
using System.Resources;
namespace PCLRestore.Adaptation;
internal static class PclDownloadContract {
 static TypeDef Type(ModuleDefMD module,string name)=>module.GetTypes().SingleOrDefault(t=>t.FullName==name)??throw new InvalidDataException("PCL 下载类型缺失："+name);
 static bool Sequence(TypeSig type,string element)=>type.FullName=="System.Collections.Generic.IEnumerable`1<"+element+">";
 static void Require(bool ok){if(!ok)throw new InvalidDataException("PCL 下载引擎接口不兼容，保留当前版本。");}
 internal static void Check(ModuleDefMD module){
  var source=Type(module,"PCL.ModDownload");
  foreach(string name in new[]{"DlSourceAssetsGet","DlSourceLibraryGet","DlSourceLauncherOrMetaGet"})Require(source.Methods.Count(m=>m.Name==name&&m.IsPublic&&m.IsStatic&&m.MethodSig.Params.Count==1&&m.MethodSig.Params[0].ElementType==ElementType.String&&Sequence(m.MethodSig.RetType,"System.String"))==1);
  var catalog=source.Fields.SingleOrDefault(f=>f.Name=="DlClientListLoader"&&f.IsPublic&&f.IsStatic);var catalogSig=catalog?.FieldSig.Type as GenericInstSig;Require(catalogSig!=null&&catalogSig.GenericType.FullName=="PCL.ModLoader/LoaderTask`2"&&catalogSig.GenericArguments.Count==2&&catalogSig.GenericArguments[0].ElementType==ElementType.String&&catalogSig.GenericArguments[1].FullName=="PCL.ModDownload/DlClientListResult");
  var result=Type(module,"PCL.ModDownload/DlClientListResult");Require(result.Fields.Any(f=>f.IsPublic&&f.Name=="SourceName"&&f.FieldSig.Type.ElementType==ElementType.String)&&result.Fields.Any(f=>f.IsPublic&&f.Name=="Value"&&f.FieldSig.Type.FullName=="Newtonsoft.Json.Linq.JObject"));
  var task=Type(module,"PCL.ModLoader/LoaderBase");Require(task.Methods.Count(m=>m.Name=="WaitForExit"&&m.IsPublic&&!m.IsStatic&&m.MethodSig.RetType.ElementType==ElementType.Void&&m.MethodSig.Params.Select(p=>p.FullName).SequenceEqual(new[]{"System.Object","PCL.ModLoader/LoaderBase","System.Boolean","System.Threading.CancellationToken","MeloongCore.ProgressProvider"}))==1);Require(task.Methods.Any(m=>m.Name=="Cancel"&&m.IsPublic&&!m.IsStatic&&m.MethodSig.RetType.ElementType==ElementType.Void&&m.MethodSig.Params.Count==0));
  var file=Type(module,"PCL.ModNet/NetFile");var constructors=file.Methods.Where(m=>m.IsConstructor&&m.IsPublic&&m.MethodSig.Params.Count==4&&Sequence(m.MethodSig.Params[0],"System.String")&&m.MethodSig.Params[1].ElementType==ElementType.String&&m.MethodSig.Params[3].ElementType==ElementType.Boolean).ToArray();Require(constructors.Length==1);
  var loader=Type(module,"PCL.ModNet/LoaderDownload");Require(loader.BaseType.FullName==task.FullName);Require(loader.Methods.Any(m=>m.IsConstructor&&m.IsPublic&&m.MethodSig.Params.Count==2&&m.MethodSig.Params[0].ElementType==ElementType.String&&Sequence(m.MethodSig.Params[1],"PCL.ModNet/NetFile")));
  Require(loader.Properties.Any(p=>p.Name=="Progress"&&p.GetMethod?.IsPublic==true));
  var resources=module.Resources.OfType<EmbeddedResource>().SingleOrDefault(r=>r.Name=="PCL.Resources.resources");Require(resources!=null);byte[]? core=null;
  using(var stream=new MemoryStream(resources!.CreateReader().ReadBytes((int)resources.Length)))using(var reader=new ResourceReader(stream)){
   var entries=reader.GetEnumerator();while(entries.MoveNext()){string key=(string)entries.Key;if(!key.Equals("meloongcore",StringComparison.OrdinalIgnoreCase))continue;reader.GetResourceData(key,out string kind,out byte[] raw);Require(kind=="ResourceTypeCode.ByteArray"&&raw.Length>=6&&BitConverter.ToInt32(raw,0)==raw.Length-4);core=raw.Skip(4).ToArray();break;}
  }
  Require(core!=null);using var library=ModuleDefMD.Load(core!);var checker=Type(library,constructors[0].MethodSig.Params[2].FullName);Require(checker.Methods.Any(m=>m.IsConstructor&&m.IsPublic&&m.MethodSig.Params.Count==0));
  foreach(var member in new[]{("Hash","System.String"),("ActualSize","System.Int64"),("IsJson","System.Boolean")})Require(checker.Fields.Any(f=>f.Name==member.Item1&&f.IsPublic&&!f.IsInitOnly&&f.FieldSig.Type.FullName==member.Item2)||checker.Properties.Any(p=>p.Name==member.Item1&&p.SetMethod?.IsPublic==true&&p.PropertySig.RetType.FullName==member.Item2));
 }
}
