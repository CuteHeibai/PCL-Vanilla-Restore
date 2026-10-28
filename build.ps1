param([ValidateRange(1,2147483647)][int]$PatchRevision = 5)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$officialCommit = '93ede5d9e638c99fcec853a9ba1eecd91ceee7f5'
$officialHash = '0DB34E19D9EEB6D496D20FABE786EC16BBD36BA6B0CCDC8D29CB7E909C114397'
$officialExe = Join-Path $PSScriptRoot 'vendor\PCL-2.99.0.1.exe'
New-Item -ItemType Directory -Force -Path 'vendor','artifacts\dist','update' | Out-Null
if (-not (Test-Path -LiteralPath $officialExe)) {
    $officialZip = Join-Path $PSScriptRoot 'vendor\official-2.99.0.1.zip'
    $officialUrl = 'https://raw.githubusercontent.com/Meloong-Git/PCL/' + $officialCommit + '/%E6%9C%80%E6%96%B0%E6%AD%A3%E5%BC%8F%E7%89%88.zip'
    Invoke-WebRequest -Uri $officialUrl -OutFile $officialZip
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($officialZip)
    try {
        $member = $archive.GetEntry('Plain Craft Launcher 2.exe')
        if ($null -eq $member) { throw 'Official launcher entry is missing.' }
        [IO.Compression.ZipFileExtensions]::ExtractToFile($member,$officialExe,$false)
    } finally { $archive.Dispose() }
}
if ((Get-FileHash -LiteralPath $officialExe -Algorithm SHA256).Hash -ne $officialHash) { throw 'Unsupported official EXE; input SHA-256 differs.' }
foreach ($project in @('src/InjectedHooks','src/AdaptationCore','src/RestoreRuntime','src/Patcher','tests/PatcherChecks','tests/UpdaterTests','tests/AutoAdaptTests','tests/PclDownloadProbe','tests/PclDownloadProbeBuilder')) {
    & dotnet build $project -c Release --nologo -v:q
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}
$output = Join-Path $PSScriptRoot 'artifacts\dist\PCL 原版下载恢复版.exe'
& dotnet 'src/Patcher/bin/Release/net8.0/Patcher.dll' adapt $officialExe $output 'src/InjectedHooks/bin/Release/net48/InjectedHooks.dll' 'src/RestoreRuntime/bin/Release/net48/PCLRestore.Runtime.dll' $PatchRevision
if ($LASTEXITCODE -ne 0) { throw 'Binary adaptation failed.' }
& dotnet 'tests/PatcherChecks/bin/Release/net8.0/PatcherChecks.dll' $officialExe $output
if ($LASTEXITCODE -ne 0) { throw 'Binary checks failed.' }
$artifact = Join-Path $PSScriptRoot 'artifacts\dist\PCL-Restore.bin'
Copy-Item -LiteralPath $output -Destination $artifact -Force
& dotnet 'tests/AutoAdaptTests/bin/Release/net8.0/AutoAdaptTests.dll' $officialExe $artifact 'src/RestoreRuntime/bin/Release/net48/PCLRestore.Runtime.dll'
if ($LASTEXITCODE -ne 0) { throw 'Automatic adaptation checks failed.' }
& 'tests/UpdaterTests/bin/Release/net48/UpdaterTests.exe'
if ($LASTEXITCODE -ne 0) { throw 'Updater regression tests failed.' }
$manifest = [ordered]@{
    schemaVersion = 1
    upstreamVersion = '2.99.0.1'
    patchRevision = $PatchRevision
    artifactUrl = "https://github.com/CuteHeibai/PCL-Vanilla-Restore/releases/download/2.99.0.1-r$PatchRevision/PCL-Restore.bin"
    artifactSha256 = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
    sourceCommit = $officialCommit
    sourceExeSha256 = $officialHash.ToLowerInvariant()
}
$manifest | ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath 'update\stable.json'
Write-Host "Built single launcher EXE: $output"
