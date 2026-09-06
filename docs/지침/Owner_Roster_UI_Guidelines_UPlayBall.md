# 구단주 모드 선수단 UI 정규 지침

> 문서 버전: 1.0  
> 기준일: 2026-09-06  
> 적용 Route: `Owner.Roster.Lineup`, `Owner.Roster.Pitching`, `Owner.Roster.Collection`, `Owner.Roster.Condition`

## 1. 목적과 우선순위

이 문서는 구단주 모드 선수단 업무 화면의 정규 구현 계약이다. 공용 Shell과 입력·Skin·Slot 규칙은
`Unity_UI_Production_Guidelines_UPlayBall.md`, 게임 상태와 Simulation 경계는
`BaseballManager_PROJECT.md` 43절을 우선한다.

시각 구조는 인벤 「첫화면 대기실! 하나하나 뜯어 보자」의 공용 업무 UI와
`docs/디자인/ref`의 선수 오더·수비 위치·카드 이미지를 함께 참고한다. 원작의 온라인 기능, 실제 구단
로고, 현금 재화, 현재 Runtime에 없는 계약·재활 값은 가져오지 않는다.

완료 기준은 화면이 비슷해 보이는지가 아니라 다음 두 질문에 답하는가이다.

- 어떤 역할 배치가 다음 경기 전력에 반영되는지 알 수 있는가.
- 변경이 거부되었을 때 어떤 슬롯과 규칙이 원인인지 알 수 있는가.

## 2. 공통 화면 계약

- `SharedGameShellView`의 Global Header, Primary Navigation, Context Header를 유지한다.
- 선수단 Local Tab은 `라인업 / 투수진 / 보유선수 / 컨디션·궁합` 네 개이며 선택 상태는 하나다.
- 본문은 밝은 회백색 Canvas, 얇은 청회색 Border, 작은 카드와 조밀한 표를 사용한다.
- Panel, Button, Table, Plot, 상태 문구는 Native uGUI와 공용 `CareerUiSkin`으로 만든다.
- 선수 카드에는 공용 `PlayerMiniCardView`, `PlayerCardSurface`, `UI_Popup_OwnerPlayerCard`를 사용한다.
- 목록 선택·검색·정렬·Scroll은 Route를 벗어났다가 돌아와도 해당 View 인스턴스가 보존한다.
- 변경 Command는 `Preview → Validate → Confirm → Runtime 재조회` 순서를 지킨다. Presentation은
  규칙을 재계산하지 않는다.
- `Loading / Empty / Locked / Invalid / Error`는 빈 화면이나 Console 오류가 아니라 같은 Content 영역의
  상태 문구와 해결 행동으로 표현한다.

## 3. 기준 Layout

원본 1024×768의 정보 밀도를 16:9로 옮기되 카드와 표를 단순 확대하지 않는다.

| 영역 | 기준 |
|---|---|
| Shell Header / Navigation / Local Tab | 공용 Shell Metric 고정 |
| 화면 바깥 여백 | 12~16px, 4px 토큰 체계 |
| 역할 보드 상단 | Workspace 높이 약 29% |
| 보유 선수·분석 하단 | Workspace 높이 약 69% |
| Lineup 상단 폭 | 타순 64% / Bench 35% |
| Pitching 상단 폭 | Rotation 43% / Bullpen 34% / Setup 10% / Closer 10% |
| Condition 폭 | 선수 목록 34% / 10단계 Plot 32% / 원인·영향 34% |
| Collection | 상단 Filter 46px / 가상화 Grid / 고정 Right Inspector / Action Bar |
| 카드 | 원작 세로 비율 유지, 가로 여유는 열 수 증가에 사용 |

1280×720에서는 보조 설명을 Inspector로 보내고 Scroll을 사용한다. 글자 크기를 줄이거나 Panel을
겹쳐 해결하지 않는다. 울트라와이드에서는 카드 열 수만 늘리고 주요 패널 비율을 과도하게 벌리지 않는다.

## 4. Route별 책임

### 4.1 `Owner.Roster.Lineup`

- 타순 9명, Bench 5명, 보유 야수, 타순별 Condition Plot을 표시한다.
- 같은 역할 그룹의 두 슬롯 선택은 즉시 저장하지 않고 Preview 후보를 만든다.
- `ValidateLineupPreset`이 `Valid`를 반환한 Preview만 `검증된 배치 저장`으로 확정한다.
- `PartiallyValid`와 `Invalid`는 슬롯 경고와 원인을 유지하고 저장 CTA를 비활성화한다.
- 프리셋, TeamColor 2슬롯, Tactic 2슬롯은 Right Inspector에서 선택하고 같은 Preview 계약을 쓴다.
- Active Roster 등록 변경은 Production Command가 없으므로 잠금 사유와 함께 비활성 상태로 둔다.

### 4.2 `Owner.Roster.Pitching`

- Lineup과 같은 저장 프리셋을 사용하지만 독립된 View State와 Route 선택 상태를 가진다.
- Rotation 5명, Bullpen 4명, Setup 1명, Closer 1명을 역할별 고정 슬롯으로 표시한다.
- 선택 투수의 저장 Condition 단계, 최근 3일 일별 투구 수, 휴식일, 실제 구종과 시즌 기록을 비교한다.
- 장기 피로 값을 새로 추정하지 않는다. 현재 Runtime 정본인 `PitchingWorkloadState`를 근거로 표시한다.
- 역할 교환은 Lineup과 동일한 Preview/Validate/Confirm Command를 사용한다.

### 4.3 `Owner.Roster.Collection`

- 검색·이름·포지션·Cost·Edition 정렬을 제공한다.
- Grid는 전체 카드 GameObject를 만들지 않고 Viewport에 보이는 행과 Buffer 행만 Pool로 유지한다.
- 선택 카드의 공용 카드 표면과 소유·성장·1군 상태를 Right Inspector에 표시한다.
- 상세 입력은 공용 앞/뒤 카드 Popup을 연다.
- 훈련·유학·Skill Block·강화·판매는 기존 Preview와 Production Command를 유지한다.

### 4.4 `Owner.Roster.Condition`

- 좌측 목록에서 선수를 선택하고 중앙 10단계 Plot에서 기본과 최종 단계를 비교한다.
- 우측에는 배치, 타선, Battery, 일시 보정의 실제 합산 근거와 다음 경기 모든 능력치 보정을 표시한다.
- `라인업에서 배치 확인`은 `Owner.Roster.Lineup` Route로 이동한다.
- 읽기 전용이며 UI가 Condition이나 Chemistry를 계산하지 않는다.
- 시즌 일정 종료는 Locked가 아니라 정상 Empty 상태로 표시한다.

## 5. 데이터와 Command 소유권

```text
OwnerModeManager / Game Query
    → OwnerModeRuntimeSnapshotFactory
    → immutable Presentation Model
    → Owner Roster View

사용자 변경 의도
    → OwnerLineupPresetCommandBuilder가 후보 복사
    → OwnerModeManager.ValidateLineupPreset
    → Preview 표시
    → 명시적 Confirm
    → OwnerModeManager.UpsertLineupPreset
    → RuntimeChanged 후 Snapshot 재조회
```

- UI는 `ManagerModeRuntimeState`와 Save DTO를 직접 수정하지 않는다.
- Condition 단계명과 경기 능력치 보정은 `ConditionChemistryBalanceTable`을 사용한 결과만 소비한다.
- 투구 부하는 저장된 최근 3일 투구 수만 표시한다.
- 실패한 Preview는 원래 Runtime과 선택 프리셋을 바꾸지 않는다.

## 6. 자산 결정

이번 선수단 화면에는 신규 Bitmap 장식이 필요하지 않다. 공용 카드 표면, 기존 선수 실루엣,
Native Panel·Badge·Plot으로 필요한 정보 구조를 모두 표현한다. 따라서 원본 스크린샷 복사나 신규
ImageGen 자산을 추가하지 않는다. 이후 실제 Card Frame 교체가 필요하면 텍스트·수치·로고가 없는
자산만 별도 생성하고 이 절에 Prompt와 저장 경로를 기록한다.

## 7. 구현 연결 상태

- `Owner.Roster.Pitching` Runtime Adapter와 Local Tab 활성화 완료
- Lineup/Pitching 독립 View State 완료
- 슬롯·TeamColor·Tactic Preview/Validate/Confirm 완료
- PitchingWorkload·Condition 단계·구종·시즌 기록 Inspector 연결 완료
- Collection Viewport Row Pool 가상화와 선택·검색·Scroll 보존 완료
- Condition 3열 화면, 10단계 Plot, 원인·경기 영향, Lineup 이동, 시즌 종료 Empty 완료
- 공용 Shell Header/Navigation/Context/Inspector/Action Slot 유지

## 8. 검증 범위

- `Baseball.Presentation.csproj` 보조 컴파일을 수행한다.
- 확률·밸런스·경기 Simulation 공식은 바꾸지 않았으므로 대량 Simulation은 요구하지 않는다.
- 실제 Unity 화면, 해상도별 Layout, Pointer/Keyboard/Gamepad, 렌더링 Acceptance는 사용자가 검증한다.
- 사용자 화면 검증에서 발견한 잘림·간격·긴 문자열 문제는 이 문서의 Layout 계약을 수정하지 않고
  공용 Metric과 해당 View의 Content Safe Bounds 안에서 보정한다.
