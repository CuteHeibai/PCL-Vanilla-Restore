param([string]$ExePath, [int]$PatchRevision = 6)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) { $ExePath = Join-Path $projectRoot 'artifacts\dist\PCL-Restore.bin' }
$probeRoot = Join-Path $projectRoot 'artifacts\official-integration'
$cache = Join-Path $probeRoot 'PCL\Cache\Update'
$null = New-Item -ItemType Directory -Path $cache -Force
$donor = Join-Path $probeRoot 'RenamedPCL.exe'
Copy-Item -LiteralPath $ExePath -Destination $donor -Force
$metadata = [Reflection.Assembly]::ReflectionOnlyLoad([IO.File]::ReadAllBytes($donor))
$resource = $metadata.GetManifestResourceStream('PCLRestore.Runtime.dll')
$buffer = New-Object IO.MemoryStream
$resource.CopyTo($buffer)
$runtime = [Reflection.Assembly]::Load($buffer.ToArray())
$resource.Dispose()
$buffer.Dispose()
$hidden = [Reflection.BindingFlags]'NonPublic,Static'
$entry = $runtime.GetType('PCLRestore.Runtime.Entry', $true)
$sourceType = $runtime.GetType('PCLRestore.Runtime.OfficialSource', $true)
$updateType = $runtime.GetType('PCLRestore.Runtime.OfficialUpdate', $true)
$api = $sourceType.GetField('CommitApiUrl', $hidden).GetRawConstantValue()
$json = $entry.GetMethod('ReadFeed', $hidden).Invoke($null, @([string]$api))
$release = $sourceType.GetMethod('ParseCommitList', $hidden).Invoke($null, @([string]$json))
$instance = [Reflection.BindingFlags]'NonPublic,Instance'
$commit = [string]$release.GetType().GetField('Commit', $instance).GetValue($release)
$url = [string]$release.GetType().GetField('ArchiveUrl', $instance).GetValue($release)
$archive = Join-Path $cache 'official.zip.bin'
$raw = Join-Path $cache 'official-source.bin'
$candidate = Join-Path $cache 'candidate.bin'
$null = $entry.GetMethod('Download', $hidden).Invoke($null, @([string]$url, [string]$archive, [long]536870912))
$null = $sourceType.GetMethod('ExtractOriginal', $hidden).Invoke($null, @([string]$archive, [string]$raw))
$before = (Get-FileHash -LiteralPath $donor -Algorithm SHA256).Hash
$prepared = $updateType.GetMethod('Prepare', $hidden).Invoke($null, @([string]$raw, [string]$donor, [string]$candidate, [int]$PatchRevision, [string]$commit))
if ((Get-FileHash -LiteralPath $donor -Algorithm SHA256).Hash -ne $before) { throw 'Staging modified the current executable.' }
Write-Output ('PASS official download and local restoration: ' + $commit)
Copy-Item -LiteralPath $candidate -Destination $donor -Force
$next = $updateType.GetMethod('Prepare', $hidden).Invoke($null, @([string]$raw, [string]$donor, [string]$candidate, [int]$PatchRevision, [string]$commit))
Write-Output 'PASS restored next generation can restore an official package again'
$loaderNames = @('PCLRestore.Runtime','PCLRestore.Adaptation','dnlib')
foreach ($name in $loaderNames) {
    $loaded = @([AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq $name })
    if ($loaded.Count -eq 0 -or @($loaded | Where-Object { $_.Location }).Count -ne 0) { throw ('Dependency was not loaded solely from memory: ' + $name) }
    Write-Output ('PASS embedded dependency: ' + $name)
}
$sidecars = @(Get-ChildItem -LiteralPath $probeRoot -Recurse -File | Where-Object { $_.Extension -eq '.dll' -or ($_.Extension -eq '.exe' -and $_.FullName -ne $donor) })
if ($sidecars.Count -ne 0) { throw 'Unexpected DLL or temporary launcher EXE extracted.' }
Write-Output ('PASS only the original launcher EXE remains; candidate SHA256: ' + (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash)
