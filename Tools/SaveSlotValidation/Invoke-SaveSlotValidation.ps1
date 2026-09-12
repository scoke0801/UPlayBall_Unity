param(
    [string]$TestFilter = 'SaveSlotTests;CareerSettingsModeTests;ManagerHistoricalSaveJsonStoreTests;OwnerMatchSpectatorTests;PitchOutcomePresentationTests;PlayResolution',
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportRoot = Join-Path $repoRoot 'output/save-slot-validation'
$validationRoot = Join-Path $reportRoot 'UnityProject'
New-Item -ItemType Directory -Path "$validationRoot/Assets/Tests", "$validationRoot/Packages", "$validationRoot/ProjectSettings", "$validationRoot/Assets/Resources" -Force | Out-Null
# 메인 에디터와 진행 중인 다른 검증 프로젝트의 Library를 공유하지 않는다.
Copy-Item "$repoRoot/Assets/02.Scripts" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$repoRoot/Assets/Plugins" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$repoRoot/Assets/08.Fonts" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$repoRoot/Assets/Tests/EditMode" "$validationRoot/Assets/Tests" -Recurse -Force
Copy-Item "$repoRoot/ProjectSettings/ProjectVersion.txt" "$validationRoot/ProjectSettings" -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/FrontManager" "$validationRoot/Assets/Resources" -Recurse -Force
New-Item -ItemType Directory -Path "$validationRoot/Assets/10.Datas" -Force | Out-Null
Copy-Item "$repoRoot/Assets/10.Datas/HistoricalSimulation" "$validationRoot/Assets/10.Datas" -Recurse -Force
Copy-Item "$repoRoot/Assets/10.Datas/FrontManager" "$validationRoot/Assets/10.Datas" -Recurse -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/DevelopmentKboIdentities" "$validationRoot/Assets/Resources" -Recurse -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/NewGame" "$validationRoot/Assets/Resources" -Recurse -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/TeamEmblems" "$validationRoot/Assets/Resources" -Recurse -Force
Copy-Item "$repoRoot/Assets/Resources/UI" "$validationRoot/Assets/Resources" -Recurse -Force
Copy-Item "$repoRoot/Assets/Resources/Input" "$validationRoot/Assets/Resources" -Recurse -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/UI" "$validationRoot/Assets/Resources" -Recurse -Force
$dependencies = [ordered]@{}
Get-ChildItem "$repoRoot/Library/PackageCache" -Directory | ForEach-Object {
    $packagePath = Join-Path $_.FullName 'package.json'
    if (Test-Path $packagePath) {
        $package = Get-Content $packagePath -Raw | ConvertFrom-Json
        $dependencies[$package.name] = if ($_.Name.Contains('@')) { 'file:' + $_.FullName.Replace('\', '/') } else { $package.version }
    }
}
[IO.File]::WriteAllText("$validationRoot/Packages/manifest.json", (@{ dependencies = $dependencies } | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
$arguments = @('-batchmode', '-projectPath', $validationRoot, '-runTests', '-testPlatform', 'EditMode',
    '-testFilter', $TestFilter, '-testResults', "$reportRoot/editmode-results.xml", '-logFile', "$reportRoot/unity.log")
$quotedArguments = $arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
$process = Start-Process -FilePath $UnityPath -ArgumentList $quotedArguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID=$($process.Id) / 결과: $reportRoot"
