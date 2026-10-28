param(
 [string]$VersionId='26.3', [string]$Name='PclNative-26.3',
 [string]$GameDirectory, [string]$ExePath,
 [ValidateSet(-1,0,1,2)][int]$SourcePreference=-1
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if(-not $ExePath){$ExePath=Join-Path $projectRoot 'artifacts/dist/PCL-Restore.bin'}
if(-not $GameDirectory){$GameDirectory=Join-Path $projectRoot 'artifacts/qa-full/.minecraft'}
$probeRoot=Join-Path $projectRoot 'artifacts/qa-pcl-native'
$probeExe=Join-Path $probeRoot 'PCL.exe'
$report=Join-Path $probeRoot 'PCL/Cache/Vanilla/probe-report.json'
Push-Location -LiteralPath $projectRoot
try {
 foreach($project in @('tests/PclDownloadProbe','tests/PclDownloadProbeBuilder')){
  & dotnet build $project -c Release --nologo -v:q
  if($LASTEXITCODE -ne 0){throw 'Probe build failed'}
 }
 & dotnet tests/PclDownloadProbeBuilder/bin/Release/net8.0/PclDownloadProbeBuilder.dll $ExePath tests/PclDownloadProbe/bin/Release/net48/PclDownloadProbe.dll $probeExe
 if($LASTEXITCODE -ne 0){throw 'Probe preparation failed'}
 if(Test-Path -LiteralPath $report){Remove-Item -LiteralPath $report}
 & $probeExe $VersionId $Name $SourcePreference ( [IO.Path]::GetFullPath($GameDirectory) ) | Out-Host
 if(-not (Test-Path -LiteralPath $report)){throw 'Native probe did not report completion'}
 $result=Get-Content -LiteralPath $report -Raw -Encoding UTF8 | ConvertFrom-Json
 if($result.status -ne 'passed'){throw $result.error}
 $result | ConvertTo-Json
} finally {Pop-Location}
