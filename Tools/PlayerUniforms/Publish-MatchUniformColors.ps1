param([switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
# 원색은 카드 제작 정의만 저작하고, 런타임은 그 발행본을 읽는다.
$definitions = Get-Content (Join-Path $PSScriptRoot 'uniforms.json') -Raw | ConvertFrom-Json
$json = @{ uniforms = @($definitions) } | ConvertTo-Json -Depth 4
$target = Join-Path $PSScriptRoot '../../Assets/Resources/UI/SpriteMatch/UniformColors.json'
if ($VerifyOnly) {
    if ((Get-Content $target -Raw).Trim() -ne $json.Trim()) { throw '카드 제작 정의와 경기 색상 카탈로그가 다릅니다.' }
} else {
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($target), $json, (New-Object Text.UTF8Encoding($false)))
}
