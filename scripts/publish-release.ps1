$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repositoryRoot 'SJHotkeys.csproj'
$projectText = Get-Content -LiteralPath $project -Raw
if ($projectText -notmatch '<Version>([^<]+)</Version>') { throw 'Could not read the app version from SJHotkeys.csproj.' }
$version = $Matches[1]

$artifactRoot = Join-Path $repositoryRoot "artifacts\v$version"
$lightweightOutput = Join-Path $artifactRoot 'lightweight-output'
$portableOutput = Join-Path $artifactRoot 'portable-output'
$lightweightAsset = Join-Path $artifactRoot "SJ-Hotkeys-v$version-win-x64-lightweight.exe"
$portableAsset = Join-Path $artifactRoot "SJ-Hotkeys-v$version-win-x64-portable.exe"
$installerAsset = Join-Path $artifactRoot "SJ-Hotkeys-Setup-v$version-win-x64.exe"
$installerSource = Join-Path $repositoryRoot "artifacts\installer\SJ-Hotkeys-Setup-v$version-win-x64.exe"

New-Item -ItemType Directory -Force $artifactRoot | Out-Null

dotnet publish $project -c Release -r win-x64 -p:SelfContained=false -o $lightweightOutput
if ($LASTEXITCODE -ne 0) { throw 'Lightweight publish failed.' }
Copy-Item -LiteralPath (Join-Path $lightweightOutput 'SJ Hotkeys.exe') -Destination $lightweightAsset -Force

dotnet publish $project -c Release -r win-x64 -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $portableOutput
if ($LASTEXITCODE -ne 0) { throw 'Portable publish failed.' }
Copy-Item -LiteralPath (Join-Path $portableOutput 'SJ Hotkeys.exe') -Destination $portableAsset -Force

& (Join-Path $PSScriptRoot 'build-installer.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
Copy-Item -LiteralPath $installerSource -Destination $installerAsset -Force

$assets = @($installerAsset, $portableAsset, $lightweightAsset)
$checksums = $assets | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($_))"
}
$checksums | Set-Content -LiteralPath (Join-Path $artifactRoot 'SHA256SUMS.txt') -Encoding ascii

Get-Item -LiteralPath ($assets + (Join-Path $artifactRoot 'SHA256SUMS.txt')) | Select-Object Name, Length
