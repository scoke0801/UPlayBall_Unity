param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportRoot = Join-Path $repoRoot 'output/card-fx-validation'
$validationRoot = Join-Path $reportRoot 'UnityProject'
$initializeAssets = -not (Test-Path "$validationRoot/Assets/Resources/UI/PlayerCards")
New-Item -ItemType Directory -Path "$validationRoot/Assets/Tests", "$validationRoot/Packages", "$validationRoot/ProjectSettings", "$validationRoot/Assets/Resources/UI", "$validationRoot/Assets/Resources/UI/Portraits", "$validationRoot/Assets/08.Fonts" -Force | Out-Null
Copy-Item "$repoRoot/Assets/02.Scripts" "$validationRoot/Assets" -Recurse -Force
if ($initializeAssets) { Copy-Item "$repoRoot/Assets/Plugins" "$validationRoot/Assets" -Recurse -Force }
Copy-Item "$repoRoot/Assets/Tests/EditMode" "$validationRoot/Assets/Tests" -Recurse -Force
Copy-Item "$repoRoot/Assets/08.Fonts/esamanru Medium.ttf*" "$validationRoot/Assets/08.Fonts" -Force
# 재실행에서도 바뀐 시트를 검증하며 초기 리소스 사본을 그대로 사용하지 않는다.
if (-not $initializeAssets) {
    Copy-Item "$repoRoot/Assets/Resources/UI/PlayerCards/PlayerCardFX_GoldHolographic_Flipbook_v*.png*" "$validationRoot/Assets/Resources/UI/PlayerCards" -Force
}
# 카드 FX 검수는 Presentation의 실제 카드 렌더러를 사용한다.
# 경기 통계 테스트는 이 표현 전용 러너에서 실행하지 않는다.
$simulationTestPath = "$validationRoot/Assets/Tests/EditMode/Simulation/Baseball.Simulation.Tests.asmdef"
$simulationTests = Get-Content $simulationTestPath -Raw | ConvertFrom-Json
$simulationTests | Add-Member -NotePropertyName defineConstraints -NotePropertyValue @('CARD_FX_INCLUDE_SIMULATION_TESTS') -Force
[IO.File]::WriteAllText($simulationTestPath, ($simulationTests | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
Copy-Item "$repoRoot/ProjectSettings/ProjectVersion.txt" "$validationRoot/ProjectSettings" -Force
if ($initializeAssets) {
# 카드 FX 검사에는 전체 역사 콘텐츠와 수천 장의 유니폼 초상이 필요하지 않다.
Copy-Item "$repoRoot/Assets/Resources/UI/PlayerCards" "$validationRoot/Assets/Resources/UI" -Recurse -Force
Copy-Item "$repoRoot/Assets/Resources/UI/Portraits/img_*" "$validationRoot/Assets/Resources/UI/Portraits" -Force
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
$env:BASEBALL_CARD_FX_CAPTURE = "$reportRoot/screenshots"
$arguments = @('-batchmode', '-projectPath', $validationRoot, '-runTests', '-testPlatform', 'EditMode',
    '-testFilter', 'PlayerCardFlipbookTests', '-testResults', "$reportRoot/results.xml", '-logFile', "$reportRoot/unity.log")
$quotedArguments = $arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
$process = Start-Process -FilePath $UnityPath -ArgumentList $quotedArguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID=$($process.Id) / $reportRoot"
