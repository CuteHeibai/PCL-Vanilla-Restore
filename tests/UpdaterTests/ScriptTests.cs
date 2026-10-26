using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using PCLRestore.Runtime;
static class ScriptTests {
 static string Q(string s) {return "'"+s.Replace("'","''")+"'";}
 internal static void Run(string scenario) {
  string dir=Path.GetFullPath("tests/UpdaterTests/fixtures/更新 O'Brien "+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
  var t=new Transaction {Directory=dir, Exe=Path.Combine(dir,"用户 PCL's.exe"),Candidate=Path.Combine(dir,"candidate.bin"),Backup=Path.Combine(dir,"previous.bak"),Session=Guid.NewGuid().ToString("N"),Pid=int.MaxValue,ReadySeconds=2};
  File.WriteAllText(t.Exe,"old version"); File.WriteAllText(t.Candidate,"new version");t.ExpectedSha=Sha(t.Candidate);t.OriginalSha=Sha(t.Exe);
  FileStream hold=null;
  if(scenario=="locked original") {File.WriteAllText(t.Backup,"older backup");hold=new FileStream(t.Exe,FileMode.Open,FileAccess.Read,FileShare.Read);}
  var pending=new {session=t.Session,expectedSha256=t.ExpectedSha}; File.WriteAllText(Path.Combine(dir,"pending.json"),new JavaScriptSerializer().Serialize(pending));
  string script=ReplacementScript.Build(t);
  string launch="$launched = Start-Process -FilePath $exe -WorkingDirectory ([IO.Path]::GetDirectoryName($exe)) -PassThru";
  if(!script.Contains(launch)) throw new Exception("launch statement missing");
  if(scenario=="normal"||scenario=="concurrent guard") script=script.Replace(launch,"[IO.File]::WriteAllText($ready, "+Q(new JavaScriptSerializer().Serialize(pending))+")");
  else if(scenario=="start failure") script=script.Replace(launch,"throw 'fixture start failure'");
  else if(scenario=="wrong readiness session") script=script.Replace(launch,"[IO.File]::WriteAllText($ready, "+Q(new JavaScriptSerializer().Serialize(new {session="wrong",expectedSha256=t.ExpectedSha}))+")");
  else if(scenario=="wrong readiness hash") script=script.Replace(launch,"[IO.File]::WriteAllText($ready, "+Q(new JavaScriptSerializer().Serialize(new {session=t.Session,expectedSha256=new string('f',64)}))+")");
  else if(scenario=="replace failure") {script=script.Replace(launch,"throw 'fixture should not launch'").Replace("[IO.File]::Move($candidate, $exe)","[IO.File]::Delete($candidate); [IO.File]::Move($candidate, $exe)");}
  else if(scenario=="tampered candidate") {script=script.Replace(launch,"throw 'fixture should not launch'");File.WriteAllText(t.Candidate,"tampered");}
  else script=script.Replace(launch,"# fixture: application never becomes ready");
  // Never restart a fixture exe while testing rollback.
  script=script.Replace("Start-Process -FilePath $exe -WorkingDirectory ([IO.Path]::GetDirectoryName($exe)) | Out-Null","[IO.File]::WriteAllText((Join-Path $root 'rollback-restarted'), 'yes')");
  string scriptPath=Path.Combine(dir,"replace.ps1");File.WriteAllText(scriptPath,script,new UTF8Encoding(true));
  FileStream gate=null;
  if(scenario=="concurrent guard")gate=new FileStream(Path.Combine(dir,"update.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
  var p=Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32/WindowsPowerShell/v1.0/powershell.exe"),"-NoProfile -NonInteractive -File \""+scriptPath+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true});
  if(gate!=null){System.Threading.Thread.Sleep(1800);bool untouched=File.ReadAllText(t.Exe)=="old version";gate.Dispose();if(!untouched)throw new Exception("replacement ignored transaction lock");}
  if(!p.WaitForExit(20000)) {p.Kill();throw new Exception("fixture timed out");}
  string err=p.StandardError.ReadToEnd();
  if(hold!=null)hold.Dispose();
  bool success=scenario=="normal"||scenario=="concurrent guard";
  if(File.ReadAllText(t.Exe)!=(success?"new version":"old version")) throw new Exception("incorrect installed bytes: "+err);
  if(success && (!File.Exists(t.Backup)||File.ReadAllText(t.Backup)!="old version"||p.ExitCode!=0)) throw new Exception("success backup/exit invalid: "+err);
  if(!success && scenario!="locked original" && scenario!="tampered candidate" && (!File.Exists(t.Backup)||!File.Exists(Path.Combine(dir,"rollback-restarted")))) throw new Exception("rollback path not exercised: "+File.ReadAllText(Path.Combine(dir,"update.log")));
  if(scenario=="locked original"&&(!File.Exists(t.Backup)||File.ReadAllText(t.Backup)!="older backup"))throw new Exception("existing backup lost before replacement began");
  if(!success && p.ExitCode==0) throw new Exception("failure returned success");
  if(!success && !File.Exists(Path.Combine(dir,"update.log"))) throw new Exception("missing failure log");
 }
 static string Sha(string path) {using(var h=System.Security.Cryptography.SHA256.Create())using(var f=File.OpenRead(path)) return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
}
