# Owner 선수단용 공용 Compact Player Slot 변경 요청

## 현재 구현

- `PlayerMiniCardView`의 기준 크기는 156×212라 720p 선수단 역할 슬롯 25개를 한 화면에서 비교하기 어렵다.
- Owner 선수단 첫 Workspace는 공용 파일을 변경하지 않고 Owner 전용 38px 역할 Row를 사용한다.
- Row는 `OwnerRosterPlayerSnapshot`만 입력받으며 Player Career State를 참조하지 않는다.

## SharedUI 요청

다음 작업에서 `PlayerMiniCardView`에 크기 분기를 누적하기보다 별도 공용 시각 컴포넌트를 검토한다.

```text
PlayerAssignmentSlotModel
  PlayerId
  DisplayName
  PositionLabel
  YearLabel
  CostLabel
  EditionLabel
  WarningText
  IsInteractable

PlayerAssignmentSlotView
  34~54px compact row
  Warning / Selected / Disabled 상태
  클릭 이벤트만 제공
```

Owner는 같은 구역 두 슬롯 교환 Action Adapter를, Player Career는 읽기 전용 Highlight Adapter를 주입한다.
공용 View가 `OwnedPlayerCardState`, `CareerPlayerState`, GameMode를 직접 탐색해서는 안 된다.

## 완료 조건

- 1280×720에서 14인 타자·11인 투수 역할을 Scroll 또는 compact row로 읽을 수 있다.
- Warning은 색상뿐 아니라 텍스트로도 표시한다.
- Owner 편집과 Player 읽기 전용 권한이 View 내부 `if (mode)` 분기로 들어가지 않는다.
- 현재 Owner 전용 Row를 교체한 뒤 중복 UI를 제거한다.
