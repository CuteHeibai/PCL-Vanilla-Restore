using System;
using System.Text;
namespace PCLRestore.Runtime {
 internal sealed class Transaction { internal string Directory, Exe, Candidate, Backup, Session, ExpectedSha, OriginalSha; internal int Pid, ReadySeconds=90; }
 internal static class ReplacementScript {
  static string Q(string value){return "'"+value.Replace("'","''")+"'";}
  internal static string Build(Transaction t) {
   var s=new StringBuilder();
   s.AppendLine("$ErrorActionPreference = 'Stop'");
   s.AppendLine("$root = "+Q(t.Directory));s.AppendLine("$exe = "+Q(t.Exe));s.AppendLine("$candidate = "+Q(t.Candidate));s.AppendLine("$backup = "+Q(t.Backup));
   s.AppendLine("$session = "+Q(t.Session));s.AppendLine("$expected = "+Q(t.ExpectedSha));s.AppendLine("$original = "+Q(t.OriginalSha));s.AppendLine("$waitPid = "+t.Pid);s.AppendLine("$readySeconds = "+t.ReadySeconds);
   s.AppendLine(@"
$ready = Join-Path $root 'ready.json'
$pending = Join-Path $root 'pending.json'
$log = Join-Path $root 'update.log'
$gate = $null
$moved = $false
$installed = $false
$launched = $null
$stagedBackup = Join-Path $root ('backup-' + $session + '.bin')
$restore = $backup
function Log([string]$message) { [IO.File]::AppendAllText($log, ([DateTime]::UtcNow.ToString('o') + ' ' + $message + [Environment]::NewLine)) }
function Hash([string]$path) {
 $h = [Security.Cryptography.SHA256]::Create(); $f = [IO.File]::OpenRead($path)
 try { return [BitConverter]::ToString($h.ComputeHash($f)).Replace('-', '').ToLowerInvariant() } finally { $f.Dispose(); $h.Dispose() }
}
function Wait-Unlocked {
 for ($i=0; $i -lt 120; $i++) {
  try { return [IO.File]::Open((Join-Path $root 'update.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
  catch { Start-Sleep -Milliseconds 250 }
 }
 throw '另一项更新尚未结束。'
}
try {
 [IO.File]::WriteAllText((Join-Path $root 'helper-started.json'), ('{""session"":""' + $session + '"",""pid"":' + $PID + '}'))
 # Wait only for the exact process that requested this transaction; never kill it.
 $caller = Get-Process -Id $waitPid -ErrorAction SilentlyContinue
 if ($null -ne $caller -and -not $caller.WaitForExit(120000)) { throw '原启动器未退出，已取消更新。' }
 $gate = Wait-Unlocked
 $p = Get-Content -LiteralPath $pending -Raw -Encoding UTF8 | ConvertFrom-Json
 if ($p.session -cne $session -or $p.expectedSha256 -cne $expected) { throw '更新事务已改变。' }
 if ((Hash $candidate) -cne $expected) { throw '候选文件校验失败。' }
 if ((Hash $exe) -cne $original) { throw '原启动器已改变。' }
 if (Test-Path -LiteralPath $ready) { Remove-Item -LiteralPath $ready -Force }
 # File.Move uses literal paths, preserves the user-selected executable filename.
 [IO.File]::Move($exe, $stagedBackup)
 $moved = $true
 $restore = $stagedBackup
 if (Test-Path -LiteralPath $backup) { [IO.File]::Delete($backup) }
 [IO.File]::Move($stagedBackup, $backup)
 $restore = $backup
 [IO.File]::Move($candidate, $exe)
 $installed = $true
 $launched = Start-Process -FilePath $exe -WorkingDirectory ([IO.Path]::GetDirectoryName($exe)) -PassThru
 $deadline = [DateTime]::UtcNow.AddSeconds($readySeconds)
 $accepted = $false
 do {
  if (Test-Path -LiteralPath $ready) {
   try {
    $r = Get-Content -LiteralPath $ready -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($r.session -ceq $session -and $r.expectedSha256 -ceq $expected -and (Hash $exe) -ceq $expected) { $accepted = $true; break }
   } catch { }
  }
  if ($null -ne $launched -and $launched.HasExited) { break }
  Start-Sleep -Milliseconds 200
 } while ([DateTime]::UtcNow -lt $deadline)
 if (-not $accepted) { throw '新版未在规定时间内完成启动。' }
 Remove-Item -LiteralPath $pending -Force
 Log '更新成功；旧版本保留于 previous.bak。'
 $gate.Dispose()
 exit 0
} catch {
 $reason = $_.Exception.Message
 try { Log ('更新失败：' + $reason) } catch { }
 if ($moved) {
  try {
   # Only the exact process created above may be stopped, to unlock our image.
   if ($null -ne $launched -and -not $launched.HasExited) { $launched.Kill(); $launched.WaitForExit(10000) | Out-Null }
   if ($installed -and (Test-Path -LiteralPath $exe)) { [IO.File]::Move($exe, (Join-Path $root ('failed-' + $session + '.bin'))) }
   if (-not (Test-Path -LiteralPath $exe)) { [IO.File]::Copy($restore, $exe, $false) }
   Start-Process -FilePath $exe -WorkingDirectory ([IO.Path]::GetDirectoryName($exe)) | Out-Null
   Log '已恢复原启动器。'
  } catch { try { Log ('恢复失败，旧版本保留于 ' + $restore + '：' + $_.Exception.Message) } catch { } }
 }
 if ($null -ne $gate) { $gate.Dispose() }
 exit 1
}");
   return s.ToString();
  }
 }
}
