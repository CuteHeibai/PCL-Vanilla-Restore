param([string]$VersionId='26.3',[string]$Name='Independent-26.3',[string]$GameDirectory,[string]$ExePath)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if (-not $ExePath) {$ExePath=Join-Path $projectRoot 'artifacts/dist/PCL-Restore.bin'}
if (-not $GameDirectory) {$GameDirectory=Join-Path $projectRoot 'artifacts/qa-full/.minecraft'}
$metadata=[Reflection.Assembly]::ReflectionOnlyLoad([IO.File]::ReadAllBytes($ExePath))
$stream=$metadata.GetManifestResourceStream('PCLRestore.Runtime.dll');$buffer=New-Object IO.MemoryStream;$stream.CopyTo($buffer);$runtime=[Reflection.Assembly]::Load($buffer.ToArray());$stream.Dispose();$buffer.Dispose()
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
$type=$runtime.GetType('PCLRestore.Runtime.VanillaInstaller',$true)
$cache=Join-Path $projectRoot 'artifacts/qa-independent/PCL/Cache/Vanilla'
$installer=$type.GetConstructor($flags,$null,@([string]),$null).Invoke(@([string]$cache))
$token=[Threading.CancellationToken]::None
$list=$type.GetMethod('Catalog',$flags).Invoke($installer,@($token))
$idField=$runtime.GetType('PCLRestore.Runtime.VanillaVersion').GetField('Id',$flags)
$version=@($list|Where-Object {$idField.GetValue($_) -ceq $VersionId})
if ($version.Count -ne 1) {throw 'Selected official version not found'}
Write-Output ('Official catalog loaded; independently installing '+$VersionId+' as '+$Name)
$clock=[Diagnostics.Stopwatch]::StartNew()
try {$null=$type.GetMethod('Install',$flags).Invoke($installer,@($version[0].PSObject.BaseObject,[string]$GameDirectory,[string]$Name,$token,$null))}catch {Write-Output $_.Exception.ToString();exit 1}
$directory=Join-Path (Join-Path $GameDirectory 'versions') $Name
$json=Get-Content -LiteralPath (Join-Path $directory ($Name+'.json')) -Raw | ConvertFrom-Json
$actual=(Get-FileHash -LiteralPath (Join-Path $directory ($Name+'.jar')) -Algorithm SHA1).Hash
if ($json.id -ne $Name -or $actual -ne $json.downloads.client.sha1) {throw 'Installed client hash or ID mismatch'}
$config=Get-Content -LiteralPath (Join-Path $directory 'PCL/config.json') -Raw | ConvertFrom-Json
if ($config.MinecraftInstallManifest.Components.MinecraftVersion -ne $VersionId -or @($config.MinecraftInstallManifest.Components.ModLoaderVersions.psobject.Properties).Count) {throw 'Pure install manifest mismatch'}
Write-Output ('PASS official independent installation: '+$VersionId+'; client SHA1 '+$actual+'; seconds '+[int]$clock.Elapsed.TotalSeconds)
Write-Output 'PASS metadata and pure PCL component manifest; shared dependencies verified/reused or downloaded'
