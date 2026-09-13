param([string]$UnityPath='C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe')
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$report=Join-Path $root 'output/skill-block-ux-validation'
$project=Join-Path $report 'UnityProject'
if(-not(Test-Path $project)){throw '먼저 Tools/SkillBlockUxValidation/Invoke-SkillBlockUxValidation.ps1로 별도 검수 프로젝트를 준비하세요.'}
$env:BASEBALL_GROWTH_VISUAL_OUTPUT=Join-Path $report 'screenshots'
$arguments=@('-batchmode','-projectPath',$project,'-runTests','-testPlatform','EditMode','-testFilter','SkillBlockBaseballPresentationTests','-testResults',(Join-Path $report 'baseball-final-results.xml'),'-logFile',(Join-Path $report 'baseball-final.log'))
$quoted=$arguments | ForEach-Object { '"'+$_+'"' }
Start-Process -FilePath $UnityPath -ArgumentList $quoted -WindowStyle Hidden -PassThru | Select-Object Id
