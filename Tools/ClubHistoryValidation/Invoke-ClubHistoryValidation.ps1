param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe', [switch]$ReuseSnapshot, [switch]$StabilizeRoster)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$report = Join-Path $repo 'output/club-history-validation'
$project = Join-Path $report 'UnityProject'
New-Item -ItemType Directory -Force "$project/Assets/Tests/EditMode/Presentation/Owner", "$project/Packages", "$project/ProjectSettings", "$project/Assets/Resources/UI" | Out-Null
# 원본 에디터의 씬과 Library에 영향을 주지 않는 독립 검증 프로젝트다.
if (!$ReuseSnapshot) { Copy-Item "$repo/Assets/02.Scripts" "$project/Assets" -Recurse -Force }
# 미완료된 다른 작업과 분리해 기록실을 검수할 때만 원본이 아닌 복사본의 오더 화면을 HEAD로 고정한다.
if ($StabilizeRoster) {
    $tracked = & git -c "safe.directory=$($repo.Replace('\','/'))" ls-tree -r --name-only HEAD Assets/02.Scripts/Presentation/Owner
    foreach ($path in $tracked) {
        if ($path -match '/UI_Scene_OwnerRosterLineup(\.[^/]+)?\.cs$') {
            $source = & git -c "safe.directory=$($repo.Replace('\','/'))" show "HEAD:$path"
            if ($LASTEXITCODE -ne 0) { throw "검증용 기준 소스를 읽지 못했습니다: $path" }
            [IO.File]::WriteAllText((Join-Path $project $path), ($source -join "`n"), [Text.UTF8Encoding]::new($false))
        }
    }
}
# 기록실 관련 소스는 매 실행마다 최신본을 사용한다.
foreach ($path in @('Game/Historical/OwnerClubHistoryStatistics.cs',
    'Presentation/Owner/OwnerSharedInformationSnapshotFactory.History.cs',
    'Presentation/Owner/OwnerClubHistoryPresentationModel.cs', 'Presentation/Owner/UI_Scene_OwnerClubHistory.cs',
    'Presentation/Owner/UI_Scene_OwnerClubHistory.Trophies.cs')) {
    Copy-Item "$repo/Assets/02.Scripts/$path" "$project/Assets/02.Scripts/$path" -Force
}
if (Test-Path "$repo/Assets/02.Scripts/Presentation/Owner/UICardGridFocusRelay.cs") {
    Copy-Item "$repo/Assets/02.Scripts/Presentation/Owner/UICardGridFocusRelay.cs" "$project/Assets/02.Scripts/Presentation/Owner" -Force
    $relayMeta = "$project/Assets/02.Scripts/Presentation/Owner/UICardGridFocusRelay.cs.meta"
    $originalMeta = Get-Content "$repo/Assets/02.Scripts/Presentation/Owner/UICardGridFocusRelay.cs.meta" -Raw
    if ($originalMeta -match '(?m)^guid: [0-9a-f]{32}\s*$') {
        Copy-Item "$repo/Assets/02.Scripts/Presentation/Owner/UICardGridFocusRelay.cs.meta" $relayMeta -Force
    } elseif ($StabilizeRoster) {
        [IO.File]::WriteAllText($relayMeta, "fileFormatVersion: 2`nguid: 287b27d147af4e829692a88692cdbf53`n", [Text.UTF8Encoding]::new($false))
    } else { throw '원본 Relay GUID가 올바르지 않습니다.' }
}
Copy-Item "$repo/Assets/Plugins" "$project/Assets" -Recurse -Force
Copy-Item "$repo/Assets/Tests/EditMode/Presentation/*.asmdef" "$project/Assets/Tests/EditMode/Presentation" -Force
Copy-Item "$repo/Assets/Tests/EditMode/Presentation/Owner/OwnerClubHistoryTests.cs" "$project/Assets/Tests/EditMode/Presentation/Owner" -Force
Copy-Item "$repo/Assets/Tests/EditMode/Presentation/Owner/OwnerNavigationRefreshTests*.cs" "$project/Assets/Tests/EditMode/Presentation/Owner" -Force
Copy-Item "$repo/ProjectSettings/ProjectVersion.txt" "$project/ProjectSettings" -Force
Copy-Item "$repo/Assets/08.Fonts" "$project/Assets" -Recurse -Force
Copy-Item "$repo/Assets/08.Fonts.meta" "$project/Assets" -Force
Copy-Item "$repo/Assets/Resources/UI/Skin" "$project/Assets/Resources/UI" -Recurse -Force
foreach ($folder in @('ClubHistory', 'OwnerSkin')) {
    Copy-Item "$repo/Assets/10.Datas/Resources/UI/$folder" "$project/Assets/Resources/UI" -Recurse -Force
}
foreach ($folder in @('NewGame', 'DevelopmentKboIdentities', 'TeamEmblems', 'FrontManager')) {
    Copy-Item "$repo/Assets/10.Datas/Resources/$folder" "$project/Assets/Resources" -Recurse -Force
}
New-Item -ItemType Directory -Force "$project/Assets/Resources/UI/Portraits" | Out-Null
Copy-Item "$repo/Assets/Resources/UI/Portraits/*.json*" "$project/Assets/Resources/UI/Portraits" -Force
Copy-Item "$repo/Assets/Resources/Input" "$project/Assets/Resources" -Recurse -Force
$dependencies = [ordered]@{}
Get-ChildItem "$repo/Library/PackageCache" -Directory | ForEach-Object {
    $packageFile = Join-Path $_.FullName 'package.json'
    if (Test-Path $packageFile) {
        $package = Get-Content $packageFile -Raw | ConvertFrom-Json
        $dependencies[$package.name] = 'file:' + $_.FullName.Replace('\', '/')
    }
}
[IO.File]::WriteAllText("$project/Packages/manifest.json", (@{ dependencies = $dependencies } | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
$env:BASEBALL_HISTORY_VISUAL_OUTPUT = Join-Path $report 'screenshots'
$arguments = @('-batchmode', '-projectPath', $project, '-runTests', '-testPlatform', 'EditMode',
    '-testFilter', 'OwnerClubHistoryTests;OwnerNavigationRefreshTests.구단기록실진입은실제Runtime기록을바인딩하고리그경로와화면을공유한다', '-testResults', "$report/editmode-results.xml", '-logFile', "$report/unity.log")
$quoted = $arguments | ForEach-Object { '"' + $_ + '"' }
$process = Start-Process -FilePath $UnityPath -ArgumentList $quoted -WindowStyle Hidden -PassThru
Write-Output "Unity PID=$($process.Id) / 결과: $report"
