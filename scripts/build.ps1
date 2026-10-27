param([switch]$SkipAssets)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $dotnetExe = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
    if (!(Test-Path -LiteralPath $dotnetExe)) {
        $dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source
    }
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\nuget'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $restoreConfig = '-p:RestoreConfigFile=' + (Join-Path $projectRoot 'NuGet.Config')
    if (!$SkipAssets) {
        & $dotnetExe run --project tools/AssetCompiler -c Release $restoreConfig -- $projectRoot
        if ($LASTEXITCODE -ne 0) { throw '素材编译失败' }
        & $dotnetExe run --project tools/PetMatte -c Release $restoreConfig -- $projectRoot --export-continuous
        if ($LASTEXITCODE -ne 0) { throw '连贯摸头 GIF 导出失败' }
        & $dotnetExe run --project tools/PetMatte -c Release $restoreConfig -- $projectRoot --export-belly
        if ($LASTEXITCODE -ne 0) { throw '收手护肚 GIF 导出失败' }
    }
    & $dotnetExe run --project tests/Nailong.Tests -c Release $restoreConfig
    if ($LASTEXITCODE -ne 0) { throw '行为测试失败' }
    & $dotnetExe publish src/Nailong.Desktop -c Release -r win-x64 --self-contained true -o dist/Nailong $restoreConfig
    if ($LASTEXITCODE -ne 0) { throw '程序打包失败' }
    Copy-Item -LiteralPath README.md -Destination dist/Nailong/使用说明.md -Force
    Copy-Item -LiteralPath 'assets/reference/pet/摸头-连贯全身.gif' -Destination 'dist/Nailong/摸头-连贯全身.gif' -Force
    Copy-Item -LiteralPath 'artifacts/pet/original-comparison.png' -Destination 'dist/Nailong/原帧对比.png' -Force
    Copy-Item -LiteralPath 'artifacts/pet/continuous-contact-sheet.png' -Destination 'dist/Nailong/摸头关键帧.png' -Force
    Copy-Item -LiteralPath 'assets/reference/belly-click/收手护肚.gif' -Destination 'dist/Nailong/收手护肚.gif' -Force
    Copy-Item -LiteralPath 'artifacts/belly/contact-sheet.png' -Destination 'dist/Nailong/收手护肚关键帧.png' -Force
    Write-Host '成品：dist\Nailong\Nailong.exe'
} finally { Pop-Location }
