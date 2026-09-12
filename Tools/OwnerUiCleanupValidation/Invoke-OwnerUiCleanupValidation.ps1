param(
    [ValidateSet('Tests', 'Visual')][string]$Mode = 'Tests',
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportRoot = Join-Path $repoRoot 'output/owner-ui-cleanup-validation'
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
$arguments = @('-batchmode', '-projectPath', $validationRoot)
if ($Mode -eq 'Tests') {
    # 새 게임 역사 전체를 생성하는 오디오 테스트는 UI 문구·배치 검증과 분리한다.
    $filter = 'OwnerMatchSpectatorTests\.(?!즉시결과는경기오디오상태를변경하지않는다);OwnerMatchPlaybackGroupTests;OwnerMatchHighlightInsetTests;MatchGameCastTests;OwnerRosterLineupPresentationTests;OwnerExpansionPresentationTests;OwnerHomeRuntimePresentationTests;OwnerModePresentationTests'
    $arguments += @('-nographics', '-runTests', '-testPlatform', 'EditMode', '-testFilter', $filter, '-testResults', "$reportRoot/editmode-results.xml")
} else {
    New-Item -ItemType Directory -Path "$validationRoot/Assets/02.Scripts/Editor/SpriteValidation", "$validationRoot/docs/design/sprite_sheet_ingame" -Force | Out-Null
    Copy-Item "$repoRoot/Tools/SpriteMatchValidation/SpriteMatchValidation.cs" "$validationRoot/Assets/02.Scripts/Editor/SpriteValidation/ValidationRunner.cs" -Force
    Copy-Item "$repoRoot/docs/design/sprite_sheet_ingame/경기장.png" "$validationRoot/docs/design/sprite_sheet_ingame/경기장.png" -Force
    $arguments += @('-quit', '-executeMethod', 'Baseball.Tools.SpriteMatchValidation.ValidationRunner.RunOwnerUi', '-spriteReportRoot', $reportRoot)
}
$arguments += @('-logFile', "$reportRoot/unity-$($Mode.ToLowerInvariant()).log")
$quotedArguments = $arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
$process = Start-Process -FilePath $UnityPath -ArgumentList $quotedArguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID=$($process.Id) / 결과 폴더: $reportRoot"
