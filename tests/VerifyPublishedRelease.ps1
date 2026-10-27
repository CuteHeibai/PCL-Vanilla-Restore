param([switch]$LocalOnly, [string]$RuntimeExePath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseExe = Join-Path $projectRoot 'artifacts\dist\PCL-Restore.bin'
if (-not $RuntimeExePath) { $RuntimeExePath = $releaseExe }
$metadata = [Reflection.Assembly]::ReflectionOnlyLoad([IO.File]::ReadAllBytes($RuntimeExePath))
$stream = $metadata.GetManifestResourceStream('PCLRestore.Runtime.dll')
$buffer = New-Object IO.MemoryStream
$stream.CopyTo($buffer)
$runtime = [Reflection.Assembly]::Load($buffer.ToArray())
$stream.Dispose()
$buffer.Dispose()
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$entryType = $runtime.GetType('PCLRestore.Runtime.Entry', $true)
$coreType = $runtime.GetType('PCLRestore.Runtime.UpdateCore', $true)
$feedUrl = $entryType.GetField('FeedUrl', $flags).GetRawConstantValue()
$json = if ($LocalOnly) { [IO.File]::ReadAllText((Join-Path $projectRoot 'update\stable.json')).TrimStart([char]0xFEFF) } else { $entryType.GetMethod('ReadFeed', $flags).Invoke($null, @($feedUrl)) }
$feed = $coreType.GetMethod('ParseFeed', $flags).Invoke($null, @($json))
$url = $feed.GetType().GetField('Url', [Reflection.BindingFlags]'NonPublic,Instance').GetValue($feed)
$probeRoot = Join-Path $projectRoot 'artifacts\remote-probe\PCL\Cache\Update'
$null = New-Item -ItemType Directory -Path $probeRoot -Force
$candidate = Join-Path $probeRoot 'candidate.bin'
if ($LocalOnly) { Copy-Item -LiteralPath $releaseExe -Destination $candidate -Force } else { $null = $entryType.GetMethod('Download', $flags).Invoke($null, @([string]$url, [string]$candidate, [long]536870912)) }
$identity = [Reflection.AssemblyName]::GetAssemblyName($releaseExe).Name
try { 1..2 | ForEach-Object { $null = $coreType.GetMethod('VerifyImage', $flags).Invoke($null, @([string]$candidate, $feed, [string]$identity)) } }
catch { Write-Output $_.Exception.ToString(); exit 1 }
$sha = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
if ($sha -ne (Get-FileHash -LiteralPath $releaseExe -Algorithm SHA256).Hash) { throw 'Published bytes differ from validated deliverable.' }
$mode = if ($LocalOnly) { 'local candidate' } else { 'public feed and download' }
Write-Output "PASS embedded production updater validated twice ($mode): $sha"
