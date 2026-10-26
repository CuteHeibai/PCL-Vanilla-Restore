using System;
using System.IO;
using PCLRestore.Runtime;
class Program {
 static int failed;
 static string FeedJson(string version="2.8.2.0", string revision="2", string sha=null, string url="https://example.com/adapted.exe", string schema="1") { return "{\"schemaVersion\":"+schema+",\"upstreamVersion\":\""+version+"\",\"patchRevision\":"+revision+",\"artifactUrl\":\""+url+"\",\"artifactSha256\":\""+(sha??new string('a',64))+"\"}"; }
 static void Assert(bool ok,string message) { if(!ok) throw new Exception(message); }
 static void Reject(Action f) { try {f();} catch(InvalidDataException) {return;} throw new Exception("invalid input accepted"); }
 static void Test(string name, Action test) { try {test();Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);} }
 static int Main() {
  Test("valid adapted feed and revision",()=>{var f=UpdateCore.ParseFeed(FeedJson()); Assert(f.Version==new Version(2,8,2,0)&&f.Revision==2,"wrong feed"); Assert(UpdateCore.IsNewer(f,new Version(2,8,2,0),1),"revision ignored"); Assert(!UpdateCore.IsNewer(f,new Version(2,8,3,0),0),"downgrade allowed");});
  Test("reject schema",()=>Reject(()=>UpdateCore.ParseFeed(FeedJson(schema:"2"))));
  Test("reject short version",()=>Reject(()=>UpdateCore.ParseFeed(FeedJson(version:"2.8.2"))));
  Test("reject negative revision",()=>Reject(()=>UpdateCore.ParseFeed(FeedJson(revision:"-1"))));
  Test("reject fractional revision",()=>Reject(()=>UpdateCore.ParseFeed(FeedJson(revision:"1.5"))));
  Test("reject hash",()=>Reject(()=>UpdateCore.ParseFeed(FeedJson(sha:new string('z',64)))));
  Test("reject HTTP artifact",()=>Reject(()=>UpdateCore.ParseFeed(FeedJson(url:"http://example.com/raw.exe"))));
  Test("unknown feed never produces install",()=>Assert(UpdateCore.TryFeed(()=>"{}") == null,"unknown feed accepted"));
  Test("unavailable feed never produces install",()=>Assert(UpdateCore.TryFeed(()=>{throw new IOException("offline");})==null,"offline accepted"));
  Test("hash mismatch blocks image",()=>{var p=Path.GetFullPath("tests/UpdaterTests/hash-fixture.bin"); File.WriteAllText(p,"not PE"); try {Reject(()=>UpdateCore.VerifyImage(p,UpdateCore.ParseFeed(FeedJson()),"PCL"));} finally {File.Delete(p);} });
  foreach(var scenario in new[]{"normal","start failure","ready timeout","wrong readiness session","wrong readiness hash","replace failure","tampered candidate","locked original","concurrent guard"}) { var local=scenario; Test("replacement "+local,()=>ScriptTests.Run(local)); }
  Test("settings default prompt",RuntimeTests.SettingsDefaults);
  Test("settings disable background",RuntimeTests.SettingsDisabled);
  Test("ready binds session and hash",()=>RuntimeTests.Ready(true));
  Test("wrong image cannot mark ready",()=>RuntimeTests.Ready(false));
  foreach(var scenario in new[]{"valid adapted image","wrong assembly version","wrong assembly identity","missing adaptation marker","wrong patch revision","missing vanilla adaptation","unknown updater protocol","DLL candidate"}) {var local=scenario;Test("metadata "+local,()=>ImageTests.Run(local));}
  return failed==0?0:1;
 }
}
