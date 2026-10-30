param(
    [ValidateSet('all', 'arm64', 'x64')][string]$Architecture = 'all',
    [string]$DotNetPath = '',
    [string]$PythonPath = '',
    [string]$OutputDirectory = '',
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
if (-not $DotNetPath) {
    $localSdk = Join-Path $projectDir '.tools/dotnet/dotnet.exe'
    if (Test-Path -LiteralPath $localSdk) { $DotNetPath = $localSdk }
    else { $DotNetPath = (Get-Command dotnet -ErrorAction Stop).Source }
}
if (-not $PythonPath) {
    foreach ($candidate in @('python3', 'python')) {
        $found = Get-Command $candidate -ErrorAction SilentlyContinue
        if ($found -and $found.Source -notmatch 'WindowsApps') { $PythonPath = $found.Source; break }
    }
}
if (-not $PythonPath) { throw 'Python 3 is required to create ZIP files with Mac executable permissions. Pass -PythonPath.' }
$DotNetPath = (Resolve-Path -LiteralPath $DotNetPath).Path
$PythonPath = (Resolve-Path -LiteralPath $PythonPath).Path
$env:DOTNET_ROOT = Split-Path -Parent $DotNetPath
$env:DOTNET_CLI_HOME = Join-Path $projectDir '.tools/cli-home'
$env:NUGET_PACKAGES = Join-Path $projectDir '.tools/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$desktopProject = Join-Path $PSScriptRoot 'Desktop/CornerBreak.MacDesktop.csproj'
$testProject = Join-Path $PSScriptRoot 'Tests/CornerBreak.Core.Tests.csproj'
$configPath = Join-Path $PSScriptRoot 'NuGet.Config'
$version = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VERSION')).Trim()
$artifactDir = Join-Path $PSScriptRoot 'artifacts'
$outputDir = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $projectDir 'release' }
New-Item -ItemType Directory -Path $artifactDir, $outputDir -Force | Out-Null
function Invoke-DotNet([string[]]$Arguments) {
    & $DotNetPath @Arguments
    if ($LASTEXITCODE -ne 0) { throw ('dotnet failed: ' + ($Arguments -join ' ')) }
}
if (-not $SkipTests) {
    Invoke-DotNet -Arguments @('restore', $testProject, '--configfile', $configPath)
    Invoke-DotNet -Arguments @('run', '--project', $testProject, '-c', 'Release', '--no-restore')
}
$targets = if ($Architecture -eq 'all') { @('arm64', 'x64') } else { @($Architecture) }
foreach ($targetArch in $targets) {
    $runtime = 'osx-' + $targetArch
    $publishDir = Join-Path $artifactDir ('publish-' + $runtime)
    Invoke-DotNet -Arguments @('restore', $desktopProject, '--configfile', $configPath, '-r', $runtime, '--use-lock-file', '-p:SelfContained=true')
    Invoke-DotNet -Arguments @('publish', $desktopProject, '-c', 'Release', '-r', $runtime,
        '--self-contained', 'true', '--no-restore', '-o', $publishDir,
        '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', '-p:UseAppHost=true')
    $licenses = Join-Path $PSScriptRoot 'ThirdPartyLicenses'
    if (-not (Test-Path -LiteralPath $licenses -PathType Container)) { throw 'Third-party license texts are missing.' }
    $publishedLicenses = Join-Path $publishDir 'ThirdPartyLicenses'
    New-Item -ItemType Directory -Path $publishedLicenses -Force | Out-Null
    Get-ChildItem -LiteralPath $licenses -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $publishedLicenses $_.Name) -Force
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md') -Destination (Join-Path $publishDir 'THIRD-PARTY-NOTICES.md') -Force
    $packager = Join-Path $PSScriptRoot 'Packaging/build_bundle.py'
    $guide = Join-Path $PSScriptRoot 'TESTING.md'
    & $PythonPath $packager --publish-dir $publishDir --output-dir $outputDir --arch $targetArch --version $version --guide $guide
    if ($LASTEXITCODE -ne 0) { throw ('Mac packaging verification failed: ' + $runtime) }
}
Write-Output 'Experimental packages created. macOS execution and Gatekeeper verification still require a real Mac.'
