param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportRoot = Join-Path $repoRoot 'output/card-fx-stable-bake'
$bakeRoot = Join-Path $reportRoot 'UnityProject'
New-Item -ItemType Directory -Force -Path "$bakeRoot/Assets/Editor", "$bakeRoot/Packages", "$bakeRoot/ProjectSettings" | Out-Null
Copy-Item "$PSScriptRoot/PlayerCardFxAtlasBaker.cs" "$bakeRoot/Assets/Editor" -Force
Copy-Item "$PSScriptRoot/PlayerCardFxBake.shader", "$PSScriptRoot/StableFlipbook.json" "$bakeRoot/Assets" -Force
Copy-Item "$repoRoot/Assets/Resources/UI/PlayerCards/PlayerCardFX_GoldHolographic_Flipbook_v1.png" "$bakeRoot/Assets/Source.png" -Force
Copy-Item "$repoRoot/Assets/Resources/UI/PlayerCards/PlayerCardFX_GoldHolographic_Flipbook_v1.png.meta" "$bakeRoot/Assets/Source.png.meta" -Force
Copy-Item "$repoRoot/ProjectSettings/ProjectVersion.txt" "$bakeRoot/ProjectSettings" -Force
# 원화 한 장과 셰이더만 베이크하므로 게임 전체·초상 라이브러리를 복제하지 않는다.
[IO.File]::WriteAllText("$bakeRoot/Packages/manifest.json", '{"dependencies":{}}')
$env:BASEBALL_CARD_FX_BAKE_OUTPUT = $reportRoot
$arguments = @('-batchmode', '-quit', '-projectPath', $bakeRoot, '-executeMethod',
    'Baseball.Editor.CardFx.PlayerCardFxAtlasBaker.Build', '-logFile', "$reportRoot/unity.log")
$quotedArguments = $arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
$process = Start-Process -FilePath $UnityPath -ArgumentList $quotedArguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID=$($process.Id) / $reportRoot"
