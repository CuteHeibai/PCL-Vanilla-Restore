using System;
using System.IO;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using PCLRestore.Runtime;

internal static class OfficialUpdateTests {
 const string Commit="93ede5d9e638c99fcec853a9ba1eecd91ceee7f5";
 internal static void Run(Action<string,Action> test) {
  test("unchanged official archive is remembered only for the same installed image",()=>InDirectory((exe,root)=>{
   string state=Path.Combine(root,"unchanged-official.json"),sha=UpdateCore.Hash(exe);
   Assert(!OfficialUpdate.IsKnownNoUpdate(state,Commit,sha),"missing state suppressed update");
   OfficialUpdate.RememberNoUpdate(state,Commit,sha);
   Assert(OfficialUpdate.IsKnownNoUpdate(state,Commit,sha),"verified no-op was forgotten");
   Assert(!OfficialUpdate.IsKnownNoUpdate(state,new string('a',40),sha),"new official commit suppressed");
   Assert(!OfficialUpdate.IsKnownNoUpdate(state,Commit,new string('b',64)),"changed installed image suppressed");
   File.WriteAllText(state,"corrupt");Assert(!OfficialUpdate.IsKnownNoUpdate(state,Commit,sha),"corrupt state suppressed update");
  }));
  test("official update ordering permits rebuilt same version",()=>{
   string a=new string('a',64),b=new string('b',64);var v=new Version(2,99,0,1);
   Assert(!OfficialUpdate.ShouldInstall(new Version(2,98,0,0),a,v,b),"downgrade accepted");
   Assert(!OfficialUpdate.ShouldInstall(v,a,v,a),"identical official source accepted");
   Assert(OfficialUpdate.ShouldInstall(v,b,v,a),"same-version official rebuild ignored");
   Assert(OfficialUpdate.ShouldInstall(new Version(2,99,0,2),a,v,a),"new official version ignored");
  });
  test("official local restoration preserves launcher and game files",()=>InDirectory((exe,root)=>{
   string raw=Path.Combine(root,"official-source.bin"),candidate=Path.Combine(root,"candidate.bin");File.Copy("vendor/PCL-2.99.0.1.exe",raw);
   string before=UpdateCore.Hash(exe);var output=OfficialUpdate.Prepare(raw,exe,candidate,3,Commit);
   Assert(output.Version==new Version(2,99,0,1)&&output.SourceSha==UpdateCore.Hash(raw)&&output.Sha==UpdateCore.Hash(candidate),"adaptation identity not bound");
   Assert(UpdateCore.Hash(exe)==before,"running launcher changed before replacement");
   Assert(File.ReadAllText(Path.Combine(Path.GetDirectoryName(exe),".minecraft","saves","world","level.dat"))=="world data","game data changed");
   Assert(File.Exists(Path.Combine(root,"adaptation.json")),"missing adaptation journal");
  }));
  test("incompatible official structure leaves current image and candidate intact",()=>InDirectory((exe,root)=>{
   string raw=Path.Combine(root,"official-source.bin"),candidate=Path.Combine(root,"candidate.bin");File.WriteAllText(candidate,"previous candidate");string before=UpdateCore.Hash(exe);
   using(var m=ModuleDefMD.Load("vendor/PCL-2.99.0.1.exe")){foreach(var t in m.GetTypes())if(t.FullName=="PCL.PageDownloadInstall")foreach(var method in t.Methods)if(method.Name=="BtnStart_Click")method.Body.Instructions.Insert(0,Instruction.Create(OpCodes.Nop));m.Write(raw);}
   Reject(()=>OfficialUpdate.Prepare(raw,exe,candidate,3,Commit));Assert(UpdateCore.Hash(exe)==before&&File.ReadAllText(candidate)=="previous candidate","failed patch modified current files");
  }));
  test("automatic adapter rejects output outside portable update cache",()=>InDirectory((exe,root)=>Reject(()=>OfficialUpdate.Prepare(Path.Combine(root,"raw.bin"),exe,Path.Combine(Path.GetDirectoryName(exe),"bad.bin"),3,Commit))));
  test("official restored output retains next generation restoration",()=>InDirectory((exe,root)=>{
   string raw=Path.Combine(root,"official-source.bin"),candidate=Path.Combine(root,"candidate.bin");File.Copy("vendor/PCL-2.99.0.1.exe",raw);
   OfficialUpdate.Prepare(raw,exe,candidate,3,Commit);File.Copy(candidate,exe,true);var next=OfficialUpdate.Prepare(raw,exe,candidate,3,new string('a',40));
   Assert(File.Exists(next.Candidate)&&next.SourceSha==UpdateCore.Hash(raw),"next generation adaptation failed");
  }));
 }
 static void InDirectory(Action<string,string> action) {
  string dir=Path.GetFullPath("tests/UpdaterTests/fixtures/official-auto-"+Guid.NewGuid().ToString("N")),root=Path.Combine(dir,"PCL","Cache","Update"),exe=Path.Combine(dir,"Renamed Launcher.exe");Directory.CreateDirectory(root);
  File.Copy("artifacts/dist/PCL-Restore.bin",exe);
  string world=Path.Combine(dir,".minecraft","saves","world");Directory.CreateDirectory(world);File.WriteAllText(Path.Combine(world,"level.dat"),"world data");action(exe,root);
 }
 static void Reject(Action action){try{action();}catch(InvalidDataException){return;}throw new Exception("invalid official update accepted");}
 static void Assert(bool value,string text){if(!value)throw new Exception(text);}
}
