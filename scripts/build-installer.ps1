$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repositoryRoot 'SJHotkeys.csproj'
$installerScript = Join-Path $repositoryRoot 'installer\SJHotkeys.iss'
$artifactRoot = Join-Path $repositoryRoot 'artifacts\installer'
$publishDirectory = Join-Path $artifactRoot 'app'

$projectText = Get-Content -LiteralPath $project -Raw
if ($projectText -notmatch '<Version>([^<]+)</Version>') { throw 'Could not read the app version from SJHotkeys.csproj.' }
$version = $Matches[1]

$compilerCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
$compiler = if ($compilerCommand) { $compilerCommand.Source } else { $null }
if (-not $compiler) {
    $compilerCandidates = @(
        (Join-Path ${env:ProgramFiles} 'Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe')
    )
    $compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $compiler) {
    throw 'Inno Setup 7 is required to build the installer. Install it from https://jrsoftware.org/isdl.php and run this script again.'
}

New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null
dotnet publish $project -c Release -r win-x64 -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'Self-contained app publish failed.' }

& $compiler "/DAppVersion=$version" "/DAppExePath=$publishDirectory\SJ Hotkeys.exe" $installerScript
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed to compile the installer.' }

$setup = Join-Path $artifactRoot "SJ-Hotkeys-Setup-v$version-win-x64.exe"
if (-not (Test-Path -LiteralPath $setup)) { throw 'Installer output was not created.' }
Get-Item -LiteralPath $setup | Select-Object FullName, Length
