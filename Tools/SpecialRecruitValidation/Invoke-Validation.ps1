param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportRoot = Join-Path $repoRoot 'output/special-recruit-validation'
$validationRoot = Join-Path $reportRoot 'UnityProject'
$initializeAssets = -not (Test-Path "$validationRoot/Assets/Resources/UI/PlayerCards")
New-Item -ItemType Directory -Path "$validationRoot/Assets/Tests", "$validationRoot/Packages", "$validationRoot/ProjectSettings", "$validationRoot/Assets/Resources" -Force | Out-Null
Copy-Item "$repoRoot/Assets/02.Scripts" "$validationRoot/Assets" -Recurse -Force
if ($initializeAssets) { Copy-Item "$repoRoot/Assets/Plugins" "$validationRoot/Assets" -Recurse -Force }
Copy-Item "$repoRoot/Assets/Tests/EditMode" "$validationRoot/Assets/Tests" -Recurse -Force
# 영입 UI 검수는 Game 거래 Fixture와 Presentation 테스트를 컴파일한다.
# 별도 작업 중인 스카우트 Simulation 테스트 어셈블리는 이 격리 러너의 대상이 아니다.
$simulationTestPath = "$validationRoot/Assets/Tests/EditMode/Simulation/Baseball.Simulation.Tests.asmdef"
$simulationTests = Get-Content $simulationTestPath -Raw | ConvertFrom-Json
$simulationTests | Add-Member -NotePropertyName defineConstraints -NotePropertyValue @('SPECIAL_RECRUIT_INCLUDE_SIMULATION_TESTS') -Force
[IO.File]::WriteAllText($simulationTestPath, ($simulationTests | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
Copy-Item "$repoRoot/ProjectSettings/ProjectVersion.txt" "$validationRoot/ProjectSettings" -Force
if ($initializeAssets) {
foreach ($folder in @('FrontManager', 'DevelopmentKboIdentities', 'NewGame', 'TeamEmblems', 'UI')) {
    Copy-Item "$repoRoot/Assets/10.Datas/Resources/$folder" "$validationRoot/Assets/Resources" -Recurse -Force
}
foreach ($folder in @('UI', 'Input')) {
    Copy-Item "$repoRoot/Assets/Resources/$folder" "$validationRoot/Assets/Resources" -Recurse -Force
}
}
$dependencies = [ordered]@{}
Get-ChildItem "$repoRoot/Library/PackageCache" -Directory | ForEach-Object {
    $packagePath = Join-Path $_.FullName 'package.json'
    if (Test-Path $packagePath) {
        $package = Get-Content $packagePath -Raw | ConvertFrom-Json
        $dependencies[$package.name] = if ($_.Name.Contains('@')) { 'file:' + $_.FullName.Replace('\', '/') } else { $package.version }
    }
}
[IO.File]::WriteAllText("$validationRoot/Packages/manifest.json", (@{dependencies=$dependencies} | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
$env:BASEBALL_RECRUIT_CAPTURE = "$reportRoot/screenshots"
$arguments = @('-batchmode', '-projectPath', $validationRoot, '-runTests', '-testPlatform', 'EditMode',
    '-testFilter', 'SpecialRecruitInteractionTests', '-testResults', "$reportRoot/results.xml", '-logFile', "$reportRoot/unity.log")
$quotedArguments = $arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
$process = Start-Process -FilePath $UnityPath -ArgumentList $quotedArguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID=$($process.Id) / $reportRoot"
