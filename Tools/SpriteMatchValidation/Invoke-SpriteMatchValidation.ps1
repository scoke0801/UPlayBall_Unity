param(
    [ValidateSet('Tests', 'Visual')][string]$Mode = 'Tests',
    [string]$TestFilter = 'SpriteMatchPresentationTests;SpriteSheetImporterTests;MatchGameCastTests;OwnerMatchSpectatorTests.관전화면은중계와결과에필요한계층을구성한다;OwnerMatchSpectatorTests.관전용야구장이미지는Resources에서불러온다',
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportRoot = Join-Path $repoRoot 'output/sprite-sheet-validation'
$validationRoot = Join-Path $reportRoot 'UnityProject'
New-Item -ItemType Directory -Path "$validationRoot/Assets/Tests/EditMode", "$validationRoot/Packages", "$validationRoot/ProjectSettings" -Force | Out-Null
# 실행 중인 원본 Unity와 씬·Library를 공유하지 않는 코드 검증 프로젝트다.
Copy-Item "$repoRoot/Assets/02.Scripts" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$repoRoot/Assets/Plugins" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$repoRoot/Assets/Tests/EditMode/Presentation" "$validationRoot/Assets/Tests/EditMode" -Recurse -Force
Copy-Item "$repoRoot/Assets/Tests/EditMode/Editor" "$validationRoot/Assets/Tests/EditMode" -Recurse -Force
Copy-Item "$repoRoot/ProjectSettings/ProjectVersion.txt" "$validationRoot/ProjectSettings" -Force
New-Item -ItemType Directory -Path "$validationRoot/Assets/Resources/UI" -Force | Out-Null
Copy-Item "$repoRoot/Assets/Resources/UI/MiniGame" "$validationRoot/Assets/Resources/UI" -Recurse -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/UI/OwnerMatch" "$validationRoot/Assets/Resources/UI" -Recurse -Force
New-Item -ItemType Directory -Path "$validationRoot/Assets/04.Images", "$validationRoot/Assets/10.Datas" -Force | Out-Null
Copy-Item "$repoRoot/Assets/04.Images/SpriteMatch" "$validationRoot/Assets/04.Images" -Recurse -Force
Copy-Item "$repoRoot/Assets/10.Datas/SpriteMatch" "$validationRoot/Assets/10.Datas" -Recurse -Force
Copy-Item "$repoRoot/Assets/Resources/UI/SpriteMatch" "$validationRoot/Assets/Resources/UI" -Recurse -Force
$dependencies = [ordered]@{}
Get-ChildItem "$repoRoot/Library/PackageCache" -Directory | ForEach-Object {
    $packagePath = Join-Path $_.FullName 'package.json'
    if (Test-Path $packagePath) {
        $package = Get-Content $packagePath -Raw | ConvertFrom-Json
        $dependencies[$package.name] = if ($_.Name.Contains('@')) { 'file:' + $_.FullName.Replace('\', '/') } else { $package.version }
    }
}
$json = @{ dependencies = $dependencies } | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText("$validationRoot/Packages/manifest.json", $json, (New-Object Text.UTF8Encoding $false))
$arguments = @('-batchmode', '-projectPath', $validationRoot)
if ($Mode -eq 'Tests') {
    $arguments += @('-nographics', '-runTests', '-testPlatform', 'EditMode', '-testFilter', $TestFilter, '-testResults', "$reportRoot/editmode-results.xml")
} else {
    New-Item -ItemType Directory -Path "$validationRoot/Assets/02.Scripts/Editor/SpriteValidation", "$validationRoot/docs/design/sprite_sheet_ingame" -Force | Out-Null
    Copy-Item "$PSScriptRoot/SpriteMatchValidation.cs" "$validationRoot/Assets/02.Scripts/Editor/SpriteValidation/ValidationRunner.cs" -Force
    Copy-Item "$repoRoot/docs/design/sprite_sheet_ingame/경기장.png" "$validationRoot/docs/design/sprite_sheet_ingame/경기장.png" -Force
    $arguments += @('-quit', '-executeMethod', 'Baseball.Tools.SpriteMatchValidation.ValidationRunner.Run', '-spriteProcessedRoot', "$repoRoot/output/sprite-sheet-ingame/Processed", '-spriteReportRoot', $reportRoot)
}
$arguments += @('-logFile', "$reportRoot/unity-$($Mode.ToLowerInvariant()).log")
# 인자마다 따옴표를 씌워 다른 작업 경로의 공백도 보존한다.
$quotedArguments = $arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
$process = Start-Process -FilePath $UnityPath -ArgumentList $quotedArguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID=$($process.Id) / 결과 폴더: $reportRoot"
