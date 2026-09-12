param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportRoot = Join-Path $repoRoot 'output/skill-block-ux-validation'
$validationRoot = Join-Path $reportRoot 'UnityProject'
# 원본 에디터의 실행 상태와 저장 파일을 보존하는 별도 검수 프로젝트다.
New-Item -ItemType Directory -Path "$validationRoot/Assets/Tests", "$validationRoot/Packages", "$validationRoot/ProjectSettings", "$validationRoot/Assets/Resources", "$validationRoot/Assets/10.Datas" -Force | Out-Null
Copy-Item "$repoRoot/Assets/02.Scripts" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$repoRoot/Assets/Plugins" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$repoRoot/Assets/08.Fonts" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$repoRoot/Assets/Tests/EditMode" "$validationRoot/Assets/Tests" -Recurse -Force
Copy-Item "$repoRoot/ProjectSettings/ProjectVersion.txt" "$validationRoot/ProjectSettings" -Force
foreach ($folder in @('FrontManager', 'DevelopmentKboIdentities', 'NewGame', 'TeamEmblems', 'UI')) {
    Copy-Item "$repoRoot/Assets/10.Datas/Resources/$folder" "$validationRoot/Assets/Resources" -Recurse -Force
}
foreach ($folder in @('UI', 'Input')) {
    Copy-Item "$repoRoot/Assets/Resources/$folder" "$validationRoot/Assets/Resources" -Recurse -Force
}
$dependencies = [ordered]@{}
Get-ChildItem "$repoRoot/Library/PackageCache" -Directory | ForEach-Object {
    $packagePath = Join-Path $_.FullName 'package.json'
    if (Test-Path $packagePath) {
        $package = Get-Content $packagePath -Raw | ConvertFrom-Json
        $dependencies[$package.name] = 'file:' + $_.FullName.Replace('\', '/')
    }
}
$manifest = @{dependencies=$dependencies} | ConvertTo-Json -Depth 4
# 검수용 패키지 경로의 기계적 직렬화다.
[IO.File]::WriteAllText("$validationRoot/Packages/manifest.json", $manifest, [Text.UTF8Encoding]::new($false))
$env:BASEBALL_GROWTH_VISUAL_OUTPUT = "$reportRoot/screenshots"
$env:BASEBALL_ROSTER_CAPTURE = "$reportRoot/screenshots"
$arguments = @('-batchmode', '-projectPath', $validationRoot, '-runTests', '-testPlatform', 'EditMode',
    '-testFilter', 'OwnerGrowthPresentationTests', '-testResults', "$reportRoot/editmode-results.xml",
    '-logFile', "$reportRoot/unity.log")
$quotedArguments = $arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
$process = Start-Process -FilePath $UnityPath -ArgumentList $quotedArguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID=$($process.Id) / 결과 폴더: $reportRoot"
