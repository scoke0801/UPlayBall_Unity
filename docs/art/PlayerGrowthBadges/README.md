# 선수 카드 성장 배지

기준: `docs/todo/특성훈련_시스템_구현기획.html` 6절과 사용자의 유학 표식 요청.

- 제작: 내장 ImageGen. [실제 프롬프트](prompts.json).
- 개별 Sprite: `Assets/10.Datas/Resources/UI/PlayerGrowthBadges/`.
- C: 은색 단일 테두리. B: 청색 이중 테두리. A: 금색 측면 날개. S: 금백색 월계관·왕관.
- 공통 중심 심볼은 네 갈래 별이며 카드 앞면에는 문자·등급명이 없다. 유학은 비행기·지구 심볼이다.
- 256×256 RGBA, 중심 피벗, Full Rect, Bilinear, 무압축, Mipmap Off.
- 원본: `output/imagegen/player-growth-badges/*-source.png`에 보존한다.
- C/A는 크로마키 초록을 `Remove-ImageBackground.ps1 -Mode ChromaKey -KeyColor '#00FF00' -KeyTolerance 48`로 제거했다.
- B/S/Study는 동일하게 단색 배경을 요청했으나 생성 도구가 실제 알파를 반환했다. 모서리 알파와 합성 검수 후 기존 알파를 보존했다.
- `Tools/ImageBackground/Export-PlayerGrowthBadges.ps1`로 전경을 동일한 256 캔버스에 맞춘다.
- 밝은 배경·어두운 배경 합성: `output/imagegen/player-growth-badges/alpha-review.png`.

## 표시 계약

`PlayerCardGrowthBadgeModel`은 Presentation 전용 불변 값이다. Sprite나 Game 상태를 저장 데이터에 추가하지 않는다.
카드 종류·컨디션·성장 블록 등급을 특성훈련 등급으로 사용하지 않는다.
현재 특성훈련 본체와 확정 등급 저장 필드는 없으므로 런타임 유학만 연결한다.
특성 배지 C/B/A/S는 생성·로딩·표시·툴팁과 검수용 확정 모델까지 제공한다. 본체 구현 시
`OwnerCardGrowthBadgeBuilder`에서 확정 등급과 한국어 특성명·핵심 효과를 공급해야 한다.

`OwnerCardGrowthBadgeBuilder`는 정확한 CardId의 보유 상태를 읽는다.
진행 프로젝트가 있으면 `유학 중 · N주 남음`, 없고 LastStudySeason 또는 유학 보너스가 있으면 `유학 완료`를 표시한다.
다른 연도·종류의 카드, 미보유 카드, AI 상대 카드에 플레이어의 이력을 붙이지 않는다.

## UI 적용

Skin Reference: 기존 `OwnerPlayerCardFrames`, `PlayerMiniCardView`, `UI_Popup_OwnerPlayerCard.BuildFrontCard`.
Layout Reference: 기존 카드 컨디션 표식과 초상 영역. 전체 셸·탭·메뉴는 기존 구조를 따른다.
Metrics: 카드 짧은 변의 13%, 최소 화면 24px, 미니 최대 32px, 상세 최대 48px.
배지는 오른쪽 상단 초상 영역에 세로로 쌓고 이름·연도·잠금·카드 종류 표기와 분리한다.
툴팁은 공통 Roster Theme/FlatSurface와 UIProjectText를 사용한다.
아이콘은 Raycast를 받지 않으므로 카드 선택·우클릭 상세·뒤집기 경로를 유지한다.
카드 호버/포커스는 설명 표시와 0.12초·1.08배 확대를 공유한다.
상세 앞면의 키보드·게임패드 포커스는 기존 뒤집기 버튼에서 전달한다.
미보유는 숨기고 재바인딩 시 기존 배지·툴팁을 제거한다. 자산 누락은 로그와 원형 대체 표시로 구분한다.

검수 실행: `Tools/PlayerGrowthBadgeValidation/Invoke-PlayerGrowthBadgeValidation.ps1`.
원본 에디터와 분리된 복사 프로젝트에서 EditMode 테스트 및 실제 카메라 렌더링을 실행한다.
검증 결과와 제한은 [작업 기록](../../reports/player-growth-badges-20260912.md)을 따른다.
