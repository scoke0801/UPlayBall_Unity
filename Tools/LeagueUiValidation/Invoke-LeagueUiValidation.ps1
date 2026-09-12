param(
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe',
    [string]$TestFilter = 'OwnerLeaguePresentationTests;OwnerLeaguePostseasonTests;OwnerNavigationRefreshTests',
    [string]$ReportDirectory = 'output/league-ui-validation'
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportRoot = Join-Path $repoRoot $ReportDirectory
$validationRoot = Join-Path $reportRoot 'UnityProject'
New-Item -ItemType Directory -Path "$validationRoot/Assets/Tests", "$validationRoot/Packages", "$validationRoot/ProjectSettings", "$validationRoot/Assets/Resources" -Force | Out-Null
# 원본 에디터의 씬과 Library를 건드리지 않는 결과 화면 전용 검증이다.
Copy-Item "$repoRoot/Assets/02.Scripts" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$repoRoot/Assets/Plugins" "$validationRoot/Assets" -Recurse -Force
# 진행 중인 다른 UI 테스트의 컴파일 상태와 독립적으로 리그·내비게이션을 검수한다.
New-Item -ItemType Directory -Path "$validationRoot/Assets/Tests/EditMode/Presentation/Owner" -Force | Out-Null
Copy-Item "$repoRoot/Assets/Tests/EditMode/Presentation/*.asmdef" "$validationRoot/Assets/Tests/EditMode/Presentation" -Force
Copy-Item "$repoRoot/Assets/Tests/EditMode/Presentation/Owner/OwnerLeague*.cs" "$validationRoot/Assets/Tests/EditMode/Presentation/Owner" -Force
Copy-Item "$repoRoot/Assets/Tests/EditMode/Presentation/Owner/OwnerNavigationRefreshTests*.cs" "$validationRoot/Assets/Tests/EditMode/Presentation/Owner" -Force
Copy-Item "$repoRoot/ProjectSettings/ProjectVersion.txt" "$validationRoot/ProjectSettings" -Force
New-Item -ItemType Directory -Path "$validationRoot/Assets/Resources/UI/Generated" -Force | Out-Null
Copy-Item "$repoRoot/Assets/Resources/UI/Skin" "$validationRoot/Assets/Resources/UI" -Recurse -Force
Copy-Item "$repoRoot/Assets/Resources/UI/Generated/bg_owner_league_postseason_v1.png*" "$validationRoot/Assets/Resources/UI/Generated" -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/UI/OwnerSkin" "$validationRoot/Assets/Resources/UI" -Recurse -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/UI/OwnerPostseason" "$validationRoot/Assets/Resources/UI" -Recurse -Force
Copy-Item "$repoRoot/Assets/Resources/UI/SpriteMatch" "$validationRoot/Assets/Resources/UI" -Recurse -Force
Copy-Item "$repoRoot/Assets/08.Fonts" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$repoRoot/Assets/08.Fonts.meta" "$validationRoot/Assets" -Force
New-Item -ItemType Directory -Path "$validationRoot/Assets/Resources/UI/Portraits" -Force | Out-Null
Copy-Item "$repoRoot/Assets/Resources/UI/Portraits/*.json*" "$validationRoot/Assets/Resources/UI/Portraits" -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/DevelopmentKboIdentities" "$validationRoot/Assets/Resources" -Recurse -Force
$dependencies = [ordered]@{}
Get-ChildItem "$repoRoot/Library/PackageCache" -Directory | ForEach-Object {
    $packagePath = Join-Path $_.FullName 'package.json'
    if (Test-Path $packagePath) {
        $package = Get-Content $packagePath -Raw | ConvertFrom-Json
        $dependencies[$package.name] = if ($_.Name.Contains('@')) { 'file:' + $_.FullName.Replace('\', '/') } else { $package.version }
    }
}
[IO.File]::WriteAllText("$validationRoot/Packages/manifest.json", (@{ dependencies = $dependencies } | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
$env:BASEBALL_LEAGUE_VISUAL_OUTPUT = Join-Path $reportRoot 'screenshots'
Copy-Item "$repoRoot/Assets/10.Datas/Resources/TeamEmblems" "$validationRoot/Assets/Resources" -Recurse -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/FrontManager" "$validationRoot/Assets/Resources" -Recurse -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/NewGame" "$validationRoot/Assets/Resources" -Recurse -Force
Copy-Item "$repoRoot/Assets/Resources/Input" "$validationRoot/Assets/Resources" -Recurse -Force
$arguments = @('-batchmode', '-projectPath', $validationRoot, '-runTests', '-testPlatform', 'EditMode',
    '-testFilter', $TestFilter, '-testResults', "$reportRoot/editmode-results.xml",
    '-seasonReviewReport', $reportRoot, '-logFile', "$reportRoot/unity.log")
$quotedArguments = $arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
$process = Start-Process -FilePath $UnityPath -ArgumentList $quotedArguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID=$($process.Id) / 결과: $reportRoot"
