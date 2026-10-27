$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$dotnetExe = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $dotnetExe)) {
    $dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source
}
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\nuget'
& $dotnetExe run --project "$projectRoot\tests\Nailong.Tests" -c Release ('-p:RestoreConfigFile=' + (Join-Path $projectRoot 'NuGet.Config'))
if ($LASTEXITCODE -ne 0) { throw '行为测试失败' }
$outputPath = Join-Path $projectRoot 'artifacts\published-smoke'
$process = Start-Process -FilePath "$projectRoot\dist\Nailong\Nailong.exe" -ArgumentList @('--smoke-test', ('"' + $outputPath + '"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "启动验证失败，详见 $outputPath" }
Get-Content -LiteralPath "$outputPath\smoke-test.json"
