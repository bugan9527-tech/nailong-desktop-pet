$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$distPath = Join-Path $projectRoot 'dist'
$portablePath = Join-Path $distPath 'Nailong'
if (!(Test-Path -LiteralPath (Join-Path $portablePath 'Nailong.exe'))) { throw '请先执行 build.ps1' }
$staleSymbols = Join-Path $portablePath 'Nailong.Core.pdb'
if (Test-Path -LiteralPath $staleSymbols) { Remove-Item -LiteralPath $staleSymbols }
Compress-Archive -Path "$portablePath\*" -DestinationPath (Join-Path $distPath '奶龙桌宠-便携版.zip') -Force

# Select only source and generated art. Never include SDK, restore caches or compiler output.
$sourceArchive = Join-Path $distPath '奶龙桌宠-源码与素材.zip'
if (Test-Path -LiteralPath $sourceArchive) { Remove-Item -LiteralPath $sourceArchive }
$archive = [System.IO.Compression.ZipFile]::Open($sourceArchive, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $files = @()
    foreach ($name in @('src', 'tests', 'tools', 'scripts', 'assets', 'docs', 'artifacts')) {
        # Archive checksums are written after packaging; exclude that self-referencing report.
        $files += Get-ChildItem -LiteralPath (Join-Path $projectRoot $name) -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' -and $_.FullName -ne (Join-Path $projectRoot 'artifacts\package-validation.json') }
    }
    $files += Get-ChildItem -LiteralPath $projectRoot -File -Force | Where-Object { $_.Name -match '\.(md|slnx)$' -or $_.Name -in @('global.json', 'NuGet.Config', '.gitignore', '.gitattributes') }
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($projectRoot.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relative, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
Get-ChildItem -LiteralPath $distPath -Filter '*.zip' | Select-Object Name,Length
