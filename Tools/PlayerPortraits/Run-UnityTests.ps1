# 실행 중인 본 프로젝트를 건드리지 않고 동일 코드·Resources를 별도 Unity 프로젝트에서 검증한다.
param([string]$ValidationDirectory = 'output/portrait-validation')
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$validation = Join-Path $repository $ValidationDirectory
$utf8 = New-Object Text.UTF8Encoding($false)
foreach ($folder in @('Assets/Runtime', 'Assets/Tests', 'Assets/Resources/UI', 'Packages', 'ProjectSettings')) {
    [IO.Directory]::CreateDirectory((Join-Path $validation $folder)) | Out-Null
}
Copy-Item -LiteralPath (Join-Path $repository 'Assets/02.Scripts/Presentation/UI/PlayerPortraitSprites.cs') -Destination (Join-Path $validation 'Assets/Runtime') -Force
Copy-Item -LiteralPath (Join-Path $repository 'Assets/02.Scripts/Core/Players/PlayerPosition.cs') -Destination (Join-Path $validation 'Assets/Runtime') -Force
Copy-Item -LiteralPath (Join-Path $repository 'Assets/Tests/EditMode/Presentation/PlayerPortraitSpritesTests.cs') -Destination (Join-Path $validation 'Assets/Tests') -Force
Copy-Item -LiteralPath (Join-Path $repository 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $validation 'ProjectSettings') -Force
$targetPortraits = Join-Path $validation 'Assets/Resources/UI/Portraits'
[IO.Directory]::CreateDirectory($targetPortraits) | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repository 'Assets/Resources/UI/Portraits') | Copy-Item -Destination $targetPortraits -Recurse -Force
[IO.File]::WriteAllText((Join-Path $validation 'Assets/Runtime/Baseball.Presentation.asmdef'), '{"name":"Baseball.Presentation"}', $utf8)
[IO.File]::WriteAllText((Join-Path $validation 'Assets/Tests/Baseball.Presentation.Tests.asmdef'), '{"name":"Baseball.Presentation.Tests","references":["Baseball.Presentation"],"includePlatforms":["Editor"],"optionalUnityReferences":["TestAssemblies"]}', $utf8)
$packages = Join-Path $repository 'Library/PackageCache'
$testFramework = (Get-ChildItem $packages -Directory -Filter 'com.unity.test-framework@*' | Select-Object -First 1).FullName.Replace('\','/')
$nunit = (Get-ChildItem $packages -Directory -Filter 'com.unity.ext.nunit@*' | Select-Object -First 1).FullName.Replace('\','/')
$manifest = @{ dependencies = @{
    'com.unity.test-framework' = "file:$testFramework"
    'com.unity.ext.nunit' = "file:$nunit"
    'com.unity.modules.imgui' = '1.0.0'
    'com.unity.modules.jsonserialize' = '1.0.0'
} }
[IO.File]::WriteAllText((Join-Path $validation 'Packages/manifest.json'), ($manifest | ConvertTo-Json -Depth 4), $utf8)
& 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe' -batchmode -nographics -projectPath $validation -runTests -testPlatform EditMode -testFilter Baseball.Tests.EditMode.Presentation.PlayerPortraitSpritesTests -testResults (Join-Path $validation 'test-results.xml') -logFile (Join-Path $validation 'unity-tests.log')
exit $LASTEXITCODE
