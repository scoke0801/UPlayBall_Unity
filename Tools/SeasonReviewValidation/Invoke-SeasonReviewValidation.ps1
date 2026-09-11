param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportRoot = Join-Path $repoRoot 'output/season-review-validation'
$validationRoot = Join-Path $reportRoot 'UnityProject'
New-Item -ItemType Directory -Path "$validationRoot/Assets/Tests", "$validationRoot/Packages", "$validationRoot/ProjectSettings", "$validationRoot/Assets/Resources" -Force | Out-Null
# 원본 에디터의 씬과 Library를 건드리지 않는 결과 화면 전용 검증이다.
Copy-Item "$repoRoot/Assets/02.Scripts" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$repoRoot/Assets/Plugins" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$repoRoot/Assets/Tests/EditMode" "$validationRoot/Assets/Tests" -Recurse -Force
Copy-Item "$repoRoot/ProjectSettings/ProjectVersion.txt" "$validationRoot/ProjectSettings" -Force
New-Item -ItemType Directory -Path "$validationRoot/Assets/Resources/UI/Generated" -Force | Out-Null
Copy-Item "$repoRoot/Assets/Resources/UI/Skin" "$validationRoot/Assets/Resources/UI" -Recurse -Force
Copy-Item "$repoRoot/Assets/Resources/UI/Generated/bg_owner_season_review_v1.png*" "$validationRoot/Assets/Resources/UI/Generated" -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/UI/OwnerSkin" "$validationRoot/Assets/Resources/UI" -Recurse -Force
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
    '-testFilter', 'OwnerSeasonReviewPresentationTests', '-testResults', "$reportRoot/editmode-results.xml",
    '-seasonReviewReport', $reportRoot, '-logFile', "$reportRoot/unity.log")
$quotedArguments = $arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
$process = Start-Process -FilePath $UnityPath -ArgumentList $quotedArguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID=$($process.Id) / 결과: $reportRoot"
