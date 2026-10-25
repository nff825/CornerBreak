param(
    [switch]$Test,
    [string]$OutputDirectory = 'dist'
)

$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
$outputDir = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $projectDir $OutputDirectory }
$outputDir = [IO.Path]::GetFullPath($outputDir)
$frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $frameworkRoot 'csc.exe'))) {
    $frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compiler = Join-Path $frameworkRoot 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The .NET Framework C# compiler was not found. Install .NET Framework 4.7.2 or later.'
}

$sourceFiles = @(Get-ChildItem -LiteralPath $projectDir -Filter '*.cs' -File | Sort-Object Name)
if ($sourceFiles.Count -eq 0) { throw 'No C# source files were found beside Build.ps1.' }
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

# Draw the existing blue pause mark using Windows APIs. No runtime icon file is required.
Add-Type -AssemblyName System.Drawing
if (-not ('CornerBreakBuildNativeMethods' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class CornerBreakBuildNativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr handle);
}
'@
}
$iconPath = Join-Path $outputDir 'CornerBreak.ico'
$bitmap = $graphics = $blueBrush = $whiteBrush = $icon = $iconStream = $null
$iconHandle = [IntPtr]::Zero
try {
    $bitmap = New-Object System.Drawing.Bitmap 64, 64
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::Transparent)
    $blueBrush = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(43, 111, 214))
    $whiteBrush = New-Object Drawing.SolidBrush ([Drawing.Color]::White)
    $graphics.FillEllipse($blueBrush, 2, 2, 60, 60)
    $graphics.FillRectangle($whiteBrush, 21, 18, 8, 28)
    $graphics.FillRectangle($whiteBrush, 35, 18, 8, 28)
    $iconHandle = $bitmap.GetHicon()
    $icon = [Drawing.Icon]::FromHandle($iconHandle)
    $iconStream = [IO.File]::Create($iconPath)
    $icon.Save($iconStream)
}
finally {
    foreach ($disposable in @($iconStream, $icon, $whiteBrush, $blueBrush, $graphics, $bitmap)) {
        if ($null -ne $disposable) { $disposable.Dispose() }
    }
    if ($iconHandle -ne [IntPtr]::Zero) { [CornerBreakBuildNativeMethods]::DestroyIcon($iconHandle) | Out-Null }
}

$references = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Web.Extensions.dll') |
    ForEach-Object { Join-Path $frameworkRoot $_ }
foreach ($referencePath in $references) {
    if (-not (Test-Path -LiteralPath $referencePath)) { throw ('Required assembly was not found: ' + $referencePath) }
}
$executablePath = Join-Path $outputDir 'CornerBreak.exe'
$compilerArgs = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/langversion:5', '/codepage:65001', '/optimize+', '/warn:4',
    '/main:CornerBreak.Program',
    ('/out:' + $executablePath),
    ('/win32manifest:' + (Join-Path $projectDir 'app.manifest')),
    ('/win32icon:' + $iconPath)
)
foreach ($referencePath in $references) { $compilerArgs += '/reference:' + $referencePath }
foreach ($mascot in @('cat', 'fish', 'companion')) {
    $assetPath = Join-Path $projectDir ('assets\mascots\' + $mascot + '.png')
    if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) { throw ('Mascot image was not found: ' + $assetPath) }
    $compilerArgs += '/resource:' + $assetPath + ',CornerBreak.Mascots.' + $mascot + '.png'
}
foreach ($sourceFile in $sourceFiles) { $compilerArgs += $sourceFile.FullName }
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw ('Compilation failed with exit code ' + $LASTEXITCODE + '.') }

$configPath = Join-Path $projectDir 'CornerBreak.exe.config'
if ([IO.Path]::GetFullPath($configPath) -ne [IO.Path]::GetFullPath($executablePath + '.config')) {
    Copy-Item -LiteralPath $configPath -Destination ($executablePath + '.config') -Force
}
foreach ($document in @('README.md', '使用说明.txt')) {
    $documentPath = Join-Path $projectDir $document
    $destinationPath = Join-Path $outputDir $document
    if ([IO.Path]::GetFullPath($documentPath) -ne [IO.Path]::GetFullPath($destinationPath)) {
        Copy-Item -LiteralPath $documentPath -Destination $destinationPath -Force
    }
}
$documentationImages = Join-Path $projectDir 'docs\images'
if ((Test-Path -LiteralPath $documentationImages -PathType Container) -and ($outputDir -ne [IO.Path]::GetFullPath($projectDir))) {
    $outputImages = Join-Path $outputDir 'docs\images'
    New-Item -ItemType Directory -Path $outputImages -Force | Out-Null
    foreach ($documentImage in @(Get-ChildItem -LiteralPath $documentationImages -Filter '*.png' -File)) {
        Copy-Item -LiteralPath $documentImage.FullName -Destination (Join-Path $outputImages $documentImage.Name) -Force
    }
}
Write-Output ('Built: ' + $executablePath)

if ($Test) {
    $testSources = @(Get-ChildItem -LiteralPath (Join-Path $projectDir 'tests') -Filter 'Native*.cs' -File | Sort-Object Name)
    if ($testSources.Count -eq 0) { throw 'No tests/Native*.cs test sources were found.' }
    $testOutputDir = Join-Path $projectDir 'tests\output\native'
    New-Item -ItemType Directory -Path $testOutputDir -Force | Out-Null
    $testExecutable = Join-Path $testOutputDir 'CornerBreak.Tests.exe'
    $testArgs = @(
        '/nologo', '/target:exe', '/platform:anycpu', '/langversion:5', '/codepage:65001', '/optimize+', '/warn:4',
        '/main:CornerBreak.Tests.NativeTestProgram',
        ('/out:' + $testExecutable), ('/reference:' + $executablePath),
        ('/win32manifest:' + (Join-Path $projectDir 'app.manifest'))
    )
    foreach ($referencePath in $references) { $testArgs += '/reference:' + $referencePath }
    foreach ($testSource in $testSources) { $testArgs += $testSource.FullName }
    & $compiler @testArgs
    if ($LASTEXITCODE -ne 0) { throw ('Test compilation failed with exit code ' + $LASTEXITCODE + '.') }
    Copy-Item -LiteralPath $executablePath -Destination (Join-Path $testOutputDir 'CornerBreak.exe') -Force
    Copy-Item -LiteralPath ($executablePath + '.config') -Destination (Join-Path $testOutputDir 'CornerBreak.exe.config') -Force
    Copy-Item -LiteralPath $configPath -Destination ($testExecutable + '.config') -Force
    & $testExecutable
    if ($LASTEXITCODE -ne 0) { throw ('Native tests failed with exit code ' + $LASTEXITCODE + '.') }
}
