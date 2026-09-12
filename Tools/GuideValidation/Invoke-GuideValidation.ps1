param(
    [string]$TestFilter = 'GuideProgressTests;OwnerGuidePresentationTests;FrontManagerGuideTests;OwnerRosterGuideEventAdapterTests;OwnerNavigationRefreshTests.Guide_',
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe',
    [string]$ReportDirectory = 'output/guide-validation',
    [switch]$IncludePlayerPortraits
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportRoot = Join-Path $repoRoot $ReportDirectory
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
Copy-Item "$repoRoot/Assets/10.Datas/FrontManager" "$validationRoot/Assets/10.Datas" -Recurse -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/DevelopmentKboIdentities" "$validationRoot/Assets/Resources" -Recurse -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/NewGame" "$validationRoot/Assets/Resources" -Recurse -Force
Copy-Item "$repoRoot/Assets/10.Datas/Resources/TeamEmblems" "$validationRoot/Assets/Resources" -Recurse -Force
New-Item -ItemType Directory -Path "$validationRoot/Assets/Resources/UI/Portraits" -Force | Out-Null
foreach ($item in Get-ChildItem "$repoRoot/Assets/Resources/UI") {
    if ($item.Name -eq 'Portraits' -and !$IncludePlayerPortraits) {
        # 홈·안내 검증에는 수천 장의 선수 유니폼이 필요하지 않다. 프런트 매니저 원화는 별도 폴더에서 복사한다.
        foreach ($portraitItem in Get-ChildItem $item.FullName) {
            if ($portraitItem.Name -in @('Players', 'Players.meta', 'Uniforms', 'Uniforms.meta')) { continue }
            Copy-Item $portraitItem.FullName "$validationRoot/Assets/Resources/UI/Portraits" -Recurse -Force
        }
    }
    else { Copy-Item $item.FullName "$validationRoot/Assets/Resources/UI" -Recurse -Force }
}
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
