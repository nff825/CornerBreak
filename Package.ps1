param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
if (-not $SkipBuild) { & (Join-Path $projectDir 'Build.ps1') -Test }
$version = [IO.File]::ReadAllText((Join-Path $projectDir 'VERSION')).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$') { throw 'VERSION must contain a valid release version.' }
$distDir = Join-Path $projectDir 'dist'
$releaseDir = Join-Path $projectDir 'release'
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
$zipName = 'CornerBreak-v' + $version + '-windows.zip'
$zipPath = Join-Path $releaseDir $zipName
$files = @(
    @{ Source = (Join-Path $distDir 'CornerBreak.exe'); Relative = 'CornerBreak.exe' },
    @{ Source = (Join-Path $distDir 'CornerBreak.exe.config'); Relative = 'CornerBreak.exe.config' },
    @{ Source = (Join-Path $projectDir 'README.md'); Relative = 'README.md' },
    @{ Source = (Join-Path $projectDir '使用说明.txt'); Relative = '使用说明.txt' }
)
$licensePath = Join-Path $projectDir 'LICENSE'
if (Test-Path -LiteralPath $licensePath -PathType Leaf) { $files += @{ Source = $licensePath; Relative = 'LICENSE' } }
$imageDir = Join-Path $projectDir 'docs\images'
if (Test-Path -LiteralPath $imageDir -PathType Container) {
    foreach ($image in @(Get-ChildItem -LiteralPath $imageDir -Filter '*.png' -File | Sort-Object Name)) {
        $files += @{ Source = $image.FullName; Relative = ('docs/images/' + $image.Name) }
    }
}
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file.Source -PathType Leaf)) { throw ('Release file was not found: ' + $file.Source) }
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream = $archive = $null
try {
    $stream = [IO.File]::Open($zipPath, [IO.FileMode]::Create, [IO.FileAccess]::Write)
    $archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create, $false)
    foreach ($file in $files) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $file.Source, ('CornerBreak/' + $file.Relative), [IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally {
    if ($null -ne $archive) { $archive.Dispose() }
    if ($null -ne $stream) { $stream.Dispose() }
}
$checksum = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = $zipPath + '.sha256'
[IO.File]::WriteAllText($checksumPath, ($checksum + '  ' + $zipName + [Environment]::NewLine), [Text.Encoding]::ASCII)
Write-Output ('Package: ' + $zipPath)
Write-Output ('SHA256: ' + $checksumPath)
