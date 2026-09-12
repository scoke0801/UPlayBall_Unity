param([switch]$Refresh, [switch]$UseCandidate)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$report = Join-Path $repo 'output/dugout-validation'
$project = Join-Path $report 'UnityProject'
New-Item -ItemType Directory -Force "$project/Assets/Tests/EditMode/Presentation/Owner", "$project/Packages", "$project/ProjectSettings", "$project/Assets/Resources/UI" | Out-Null
if (!(Test-Path "$project/Assets/02.Scripts") -or $Refresh) {
    Copy-Item "$repo/Assets/02.Scripts" "$project/Assets" -Recurse -Force
    Copy-Item "$repo/Assets/Plugins" "$project/Assets" -Recurse -Force
    Copy-Item "$repo/Assets/08.Fonts" "$project/Assets" -Recurse -Force
    Copy-Item "$repo/Assets/Resources/UI/Skin" "$project/Assets/Resources/UI" -Recurse -Force
    Copy-Item "$repo/Assets/10.Datas/Resources/UI/OwnerSkin" "$project/Assets/Resources/UI" -Recurse -Force
}
# 원본 화면을 덮어쓰지 않고 후보 화면의 컴파일·입력·시각 결과부터 검증한다.
foreach ($relative in @('Presentation/Owner/OwnerDugoutDetailUiFactory.cs', 'Presentation/Owner/OwnerDugoutPresentationModel.cs',
    'Core/Historical/DugoutManagementDefinitions.cs', 'Core/Historical/DugoutStaffBalanceData.cs',
    'Simulation/Historical/DugoutTacticalProfileResolver.cs', 'Simulation/Match/ManagerMatchupAi.cs',
    'Simulation/Match/DetailedMatchEngine.cs', 'Simulation/Match/DetailedMatchEngine.State.cs')) {
    Copy-Item "$repo/Assets/02.Scripts/$relative" "$project/Assets/02.Scripts/$relative" -Force
}
# 동시 작업의 잘못된 meta는 격리 복사본에서만 정규화한다.
Get-ChildItem "$project/Assets/02.Scripts" -Recurse -Filter '*.meta' | ForEach-Object {
    $text = [IO.File]::ReadAllText($_.FullName)
    if ($text.Contains('\n')) { [IO.File]::WriteAllText($_.FullName, $text.Replace('\n', "`n")) }
    if ($text -notmatch '(?m)^guid: [0-9a-f]{32}\r?$') {
        [IO.File]::WriteAllText($_.FullName, "fileFormatVersion: 2`nguid: $([Guid]::NewGuid().ToString('N'))`n")
    }
}
$viewSource = if ($UseCandidate) { "$report/candidate/UI_Scene_OwnerDugout.cs" } else { "$repo/Assets/02.Scripts/Presentation/Owner/UI_Scene_OwnerDugout.cs" }
Copy-Item $viewSource "$project/Assets/02.Scripts/Presentation/Owner/UI_Scene_OwnerDugout.cs" -Force
Copy-Item "$repo/Assets/10.Datas/Resources/UI/OwnerDugout" "$project/Assets/Resources/UI" -Recurse -Force
Copy-Item "$repo/Assets/10.Datas/Resources/NewGame" "$project/Assets/Resources" -Recurse -Force
Copy-Item "$repo/Assets/Tests/EditMode/Presentation/*.asmdef" "$project/Assets/Tests/EditMode/Presentation" -Force
Copy-Item "$repo/Assets/Tests/EditMode/Presentation/Owner/OwnerDugoutRedesignTests.cs" "$project/Assets/Tests/EditMode/Presentation/Owner" -Force
Copy-Item "$repo/ProjectSettings/ProjectVersion.txt" "$project/ProjectSettings" -Force
$dependencies = [ordered]@{}
Get-ChildItem "$repo/Library/PackageCache" -Directory | ForEach-Object {
    $file = Join-Path $_.FullName 'package.json'
    if (Test-Path $file) { $package = Get-Content $file -Raw | ConvertFrom-Json; $dependencies[$package.name] = 'file:' + $_.FullName.Replace('\','/') }
}
[IO.File]::WriteAllText("$project/Packages/manifest.json", (@{dependencies=$dependencies} | ConvertTo-Json -Depth 4))
$env:BASEBALL_DUGOUT_VISUAL_OUTPUT = "$report/screenshots"
$arguments = @('-batchmode', '-projectPath', $project, '-runTests', '-testPlatform', 'EditMode', '-testFilter', 'OwnerDugoutRedesignTests', '-testResults', "$report/results.xml", '-logFile', "$report/unity.log")
$quoted = $arguments | ForEach-Object { '"' + $_ + '"' }
$process = Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe' -ArgumentList $quoted -WindowStyle Hidden -PassThru
Write-Output "Unity PID=$($process.Id) / $report"
