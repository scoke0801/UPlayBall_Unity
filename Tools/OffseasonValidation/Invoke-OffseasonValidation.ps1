param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe',
    [string]$TestFilter = 'OwnerOffseasonPresentationTests')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$report = Join-Path $repo 'output/offseason-validation'
$project = Join-Path $report 'UnityProject'
New-Item -ItemType Directory -Force "$project/Assets/Tests/EditMode/Presentation/Owner", "$project/Packages", "$project/ProjectSettings", "$project/Assets/Resources/UI" | Out-Null
# 열려 있는 에디터와 분리된 프로젝트에서 현재 소스 전체를 컴파일한다.
Copy-Item "$repo/Assets/02.Scripts" "$project/Assets" -Recurse -Force
# 별도 작업의 잘못된 GUID로 스크립트가 누락되면 검증 복사본에서만 재발급한다.
Get-ChildItem "$project/Assets/02.Scripts" -Recurse -Filter '*.cs.meta' | ForEach-Object {
    $metadata = Get-Content $_.FullName -Raw
    $match = [regex]::Match($metadata, '(?m)^guid:\s*(\S+)')
    if ($match.Success -and $match.Groups[1].Value -notmatch '^[0-9a-f]{32}$') {
        Write-Output "검증 복사본 GUID 보정: $($_.Name)"
        [IO.File]::WriteAllText($_.FullName, "fileFormatVersion: 2`nguid: $([guid]::NewGuid().ToString('N'))`n")
    }
}
Copy-Item "$repo/Assets/Plugins" "$project/Assets" -Recurse -Force
Copy-Item "$repo/Assets/Tests/EditMode/Presentation/*.asmdef" "$project/Assets/Tests/EditMode/Presentation" -Force
Copy-Item "$repo/Assets/Tests/EditMode/Presentation/Owner/OwnerOffseasonPresentationTests.cs" "$project/Assets/Tests/EditMode/Presentation/Owner" -Force
if (Test-Path "$repo/Assets/Tests/EditMode/Presentation/Owner/OwnerDevelopmentPresentationTests.cs") {
    Copy-Item "$repo/Assets/Tests/EditMode/Presentation/Owner/OwnerDevelopmentPresentationTests.cs" "$project/Assets/Tests/EditMode/Presentation/Owner" -Force
    New-Item -ItemType Directory -Force "$project/Assets/Tests/EditMode/Game/Historical", "$project/Assets/Resources/NewGame" | Out-Null
    Copy-Item "$repo/Assets/Tests/EditMode/Game/Baseball.Game.Tests.asmdef" "$project/Assets/Tests/EditMode/Game" -Force
    Copy-Item "$repo/Assets/Tests/EditMode/Game/Historical/ManagerHistoricalSaveTests.cs" "$project/Assets/Tests/EditMode/Game/Historical" -Force
    Copy-Item "$repo/Assets/10.Datas/Resources/NewGame/OwnerDevelopment.json", "$repo/Assets/10.Datas/Resources/NewGame/OwnerSupportCards.json" "$project/Assets/Resources/NewGame" -Force
}
Copy-Item "$repo/ProjectSettings/ProjectVersion.txt" "$project/ProjectSettings" -Force
Copy-Item "$repo/Assets/08.Fonts" "$project/Assets" -Recurse -Force
Copy-Item "$repo/Assets/08.Fonts.meta" "$project/Assets" -Force
Copy-Item "$repo/Assets/10.Datas/Resources/DevelopmentKboIdentities" "$project/Assets/Resources" -Recurse -Force
Copy-Item "$repo/Assets/10.Datas/Resources/DevelopmentKboIdentities" "$project/Assets/Resources" -Recurse -Force
Copy-Item "$repo/Assets/Resources/UI/Skin" "$project/Assets/Resources/UI" -Recurse -Force
foreach ($folder in @('OwnerSkin', 'OwnerPowerUp', 'PlayerGrowthBadges')) {
    Copy-Item "$repo/Assets/10.Datas/Resources/UI/$folder" "$project/Assets/Resources/UI" -Recurse -Force
}
$dependencies = [ordered]@{}
Get-ChildItem "$repo/Library/PackageCache" -Directory | ForEach-Object {
    $packageFile = Join-Path $_.FullName 'package.json'
    if (Test-Path $packageFile) {
        $package = Get-Content $packageFile -Raw | ConvertFrom-Json
        $dependencies[$package.name] = 'file:' + $_.FullName.Replace('\', '/')
    }
}
[IO.File]::WriteAllText("$project/Packages/manifest.json", (@{ dependencies = $dependencies } | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
$env:BASEBALL_OFFSEASON_VISUAL_OUTPUT = Join-Path $report 'screenshots'
$arguments = @('-batchmode', '-projectPath', $project, '-runTests', '-testPlatform', 'EditMode',
    '-testFilter', $TestFilter, '-testResults', "$report/editmode-results.xml", '-logFile', "$report/unity.log")
$quoted = $arguments | ForEach-Object { '"' + $_ + '"' }
$process = Start-Process -FilePath $UnityPath -ArgumentList $quoted -WindowStyle Hidden -PassThru
Write-Output "Unity PID=$($process.Id) / 결과: $report"
