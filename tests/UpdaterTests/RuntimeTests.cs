using System;
using System.IO;
using System.Web.Script.Serialization;
using PCLRestore.Runtime;
static class RuntimeTests {
 internal static void SettingsDefaults() {var s=Settings.Read(Path.GetFullPath("tests/UpdaterTests/nonexistent-settings.json")); if(!s.CheckAutomatically||!s.PromptBeforeInstall)throw new Exception("unsafe defaults");}
 internal static void SettingsDisabled() {var p=Path.GetFullPath("tests/UpdaterTests/settings-fixture.json");File.WriteAllText(p,"{\"checkAutomatically\":false,\"promptBeforeInstall\":true}");try{if(Settings.Read(p).CheckAutomatically)throw new Exception("background setting ignored");}finally{File.Delete(p);}}
 internal static void Ready(bool matches) {
  string dir=Path.GetFullPath("tests/UpdaterTests/fixtures/readiness "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);string exe=Path.Combine(dir,"启动器.exe");File.WriteAllText(exe,"fixture");
  if(!(bool)Entry.Initialize(new object[]{exe,"2.8.2.0",1}))throw new Exception("initialize failed");
  string root=Path.Combine(dir,"PCL/Cache/Update");Directory.CreateDirectory(root);string session=Guid.NewGuid().ToString("N");string hash=UpdateCore.Hash(exe);
  File.WriteAllText(Path.Combine(root,"pending.json"),new JavaScriptSerializer().Serialize(new {session=session,expectedSha256=matches?hash:new string('a',64)}));
  bool result=(bool)Entry.MarkReady(new object[0]);
  if(result!=matches||File.Exists(Path.Combine(root,"ready.json"))!=matches)throw new Exception("wrong readiness decision");
  if(matches) {var json=UpdateCore.Json(File.ReadAllText(Path.Combine(root,"ready.json")));if(UpdateCore.Str(json,"session")!=session||UpdateCore.Str(json,"expectedSha256")!=hash)throw new Exception("wrong transaction ready");}
 }
}
