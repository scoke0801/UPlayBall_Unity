param(
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe',
    [string]$ReportDirectory = 'output/menu-entry-validation',
    [string]$TestFilter = 'OwnerNavigationRefreshTests;OwnerManagementPresentationTests',
    [switch]$EnableGraphics
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportRoot = Join-Path $repoRoot $ReportDirectory
$validationRoot = Join-Path $reportRoot 'UnityProject'
# 다른 작업의 Unity·Library와 분리하고 원본 씬이나 저장 파일에는 접근하지 않는다.
New-Item -ItemType Directory -Path "$validationRoot/Assets/Tests", "$validationRoot/Packages", "$validationRoot/ProjectSettings", "$validationRoot/Assets/Resources", "$validationRoot/Assets/10.Datas", "$validationRoot/Assets/04.Images" -Force | Out-Null
Copy-Item "$repoRoot/Assets/02.Scripts" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$repoRoot/Assets/Plugins" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$repoRoot/Assets/Tests/EditMode" "$validationRoot/Assets/Tests" -Recurse -Force
Copy-Item "$repoRoot/ProjectSettings/ProjectVersion.txt" "$validationRoot/ProjectSettings" -Force
foreach ($folder in @('FrontManager', 'DevelopmentKboIdentities', 'NewGame', 'TeamEmblems', 'UI')) {
    Copy-Item "$repoRoot/Assets/10.Datas/Resources/$folder" "$validationRoot/Assets/Resources" -Recurse -Force
}
foreach ($folder in @('UI', 'Input')) {
    Copy-Item "$repoRoot/Assets/Resources/$folder" "$validationRoot/Assets/Resources" -Recurse -Force
}
foreach ($folder in @('HistoricalSimulation', 'FrontManager', 'SpriteMatch')) {
    Copy-Item "$repoRoot/Assets/10.Datas/$folder" "$validationRoot/Assets/10.Datas" -Recurse -Force
}
Copy-Item "$repoRoot/Assets/04.Images/SpriteMatch" "$validationRoot/Assets/04.Images" -Recurse -Force
$dependencies = [ordered]@{}
Get-ChildItem "$repoRoot/Library/PackageCache" -Directory | ForEach-Object {
    $packagePath = Join-Path $_.FullName 'package.json'
    if (Test-Path $packagePath) {
        $package = Get-Content $packagePath -Raw | ConvertFrom-Json
        $dependencies[$package.name] = if ($_.Name.Contains('@')) { 'file:' + $_.FullName.Replace('\', '/') } else { $package.version }
    }
}
[IO.File]::WriteAllText("$validationRoot/Packages/manifest.json", (@{dependencies=$dependencies} | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
$env:BASEBALL_GROWTH_VISUAL_OUTPUT = Join-Path $reportRoot 'screenshots'
$arguments = @('-batchmode', '-projectPath', $validationRoot,
    '-runTests', '-testPlatform', 'EditMode',
    '-testFilter', $TestFilter,
    '-testResults', "$reportRoot/editmode-results.xml", '-logFile', "$reportRoot/unity-tests.log")
if (!$EnableGraphics) { $arguments += '-nographics' }
$quotedArguments = $arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
$process = Start-Process -FilePath $UnityPath -ArgumentList $quotedArguments -WindowStyle Hidden -PassThru
# 라이선스 클라이언트처럼 계속 살아 있는 자식 서비스까지 기다리지 않고 검수 Editor 종료만 확인한다.
$process.WaitForExit()
Write-Output "Unity PID=$($process.Id) / 결과 폴더: $reportRoot"
exit $process.ExitCode
