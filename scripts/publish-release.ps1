$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repositoryRoot 'SJHotkeys.csproj'
$artifactRoot = Join-Path $repositoryRoot 'artifacts\v1.1.0'
$lightweightOutput = Join-Path $artifactRoot 'lightweight-output'
$portableOutput = Join-Path $artifactRoot 'portable-output'
$lightweightAsset = Join-Path $artifactRoot 'SJ-Hotkeys-v1.1.0-win-x64-lightweight.exe'
$portableAsset = Join-Path $artifactRoot 'SJ-Hotkeys-v1.1.0-win-x64-portable.exe'
$repositoryLightweight = Join-Path $repositoryRoot 'release\SJ-Hotkeys-v1.1.0-win-x64-lightweight.exe'

New-Item -ItemType Directory -Force $artifactRoot | Out-Null

dotnet publish $project -c Release -r win-x64 -p:SelfContained=false -o $lightweightOutput
if ($LASTEXITCODE -ne 0) { throw 'Lightweight publish failed.' }
Copy-Item -LiteralPath (Join-Path $lightweightOutput 'SJ Hotkeys.exe') -Destination $lightweightAsset -Force
Copy-Item -LiteralPath $lightweightAsset -Destination $repositoryLightweight -Force

dotnet publish $project -c Release -r win-x64 -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $portableOutput
if ($LASTEXITCODE -ne 0) { throw 'Portable publish failed.' }
Copy-Item -LiteralPath (Join-Path $portableOutput 'SJ Hotkeys.exe') -Destination $portableAsset -Force

$checksums = @($lightweightAsset, $portableAsset) | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($_))"
}
$checksums | Set-Content -LiteralPath (Join-Path $artifactRoot 'SHA256SUMS.txt') -Encoding ascii

Get-Item $lightweightAsset, $portableAsset | Select-Object Name, Length
