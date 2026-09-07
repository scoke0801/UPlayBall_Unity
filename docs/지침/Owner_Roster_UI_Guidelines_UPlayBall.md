# 구단주 모드 선수단 UI 정규 지침

> 문서 버전: 1.4
> 기준일: 2026-09-06
> 적용 Route: `Owner.Roster.Lineup`, `Owner.Roster.TacticCards`, `Owner.Roster.SupportCards`, `Owner.Roster.TeamColor`

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
- 선수단 Local Tab은 `선수 오더 / 작전 카드 / 서포트 카드 / 팀 컬러` 네 개이며 선택 상태는 하나다.
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
| 선수 오더 Shell | Right Inspector와 Context Action Bar를 숨기고 Main Workspace가 전체 폭·높이를 사용 |
| Condition 폭 | 선수 목록 34% / 10단계 Plot 32% / 원인·영향 34% |
| Collection | 상단 Filter 46px / 가상화 Grid / 고정 Right Inspector / Action Bar |
| TeamColor | 상단 장착 후보 30% / 가용 목록 35% / 현재 적용 29%, 하단 정보 영역 |
| SupportCard | 상단 사용 슬롯 30% / 보유 목록 35% / 발동 슬롯 29%, 하단 정보 영역 |
| 카드 | 원작 세로 비율 유지, 가로 여유는 열 수 증가에 사용 |

1280×720에서는 보조 설명을 Inspector로 보내고 Scroll을 사용한다. 글자 크기를 줄이거나 Panel을
겹쳐 해결하지 않는다. 울트라와이드에서는 카드 열 수만 늘리고 주요 패널 비율을 과도하게 벌리지 않는다.

## 4. Route별 책임

### 4.1 `Owner.Roster.Lineup`

- Local Tab 표시 이름은 `선수 오더`이며 기존 선수단 목록을 이 화면 안에 보존한다.
- 타순 9명, Bench 5명, 보유 야수, 타순별 Condition Plot을 표시한다.
- 야수/투수 내부 선택으로 Rotation 5명, Bullpen 4명, Setup 1명, Closer 1명을 함께 다룬다.
- `타자 / 투수` 전환은 본문 상단 Toolbar에 항상 노출하며 별도 Deep Link 없이도 두 목록을 오갈 수 있다.
- 선수 오더에서는 Right Inspector와 하단 Action Bar를 사용하지 않는다. 프리셋·배치 편집·취소·저장은
  본문 상단 Toolbar로 옮기고, TeamColor와 Tactic 편집은 각 전용 Local Tab에서만 제공한다.
- 기본 Card Click은 큰 선수 카드 상세를 열며, `배치 편집`을 켠 경우에는 배치 슬롯과 보유 선수 Card Click을 교체 후보로 해석한다.
- 같은 역할 그룹의 두 슬롯은 순서를 바꾸고, 서로 다른 역할의 1군 선수는 전체 역할을 맞바꾼다. 미등록 보유 선수는 선택 슬롯의 기존 선수와 1군 등록을 교체한다.
- 선발 타자 카드 아래의 `포지션 ▾` 버튼은 조회 상태에서도 수비 위치 변경을 연다. 우측 분석 영역에
  9개 포지션과 현재 담당 선수를 표시하고, 선택 시 두 선수의 변경 전후 위치와 주 포지션을 보여준다.
  `변경안에 적용`은 두 수비 슬롯만 교환하며 타순을 유지한다. 기존 Preview에 누적하고 `배치 저장`으로
  확정한다. 비주포지션 경고·실책 위험은 기존 Validator 결과만 표시한다. `닫기`·ESC는 선택 창만
  닫고 이미 적용한 변경안을 보존한다. 벤치의 수비 위치는 선발로 교체한 후 지정한다.
- 선택 순서는 `배치 슬롯 → 보유 선수`, `보유 선수 → 배치 슬롯`을 모두 허용하며 현재 선택과 다음 행동을 상태 Strip에 표시한다.
- 선수 교체는 즉시 저장하지 않고 1군과 프리셋을 함께 Preview 후보로 만든다. 편집 중 추가 교체는
  같은 후보에 계속 누적하며, 25인 전체와 프리셋을 매번 다시 검증한 뒤 한 번의 `배치 저장`으로 확정한다.
- 1군 교체로 발동 조건을 잃은 TeamColor는 Preview에서 해당 슬롯만 자동 해제하고 상태 Strip에 해제
  수를 표시한다. 선수 오더에 TeamColor 편집을 다시 넣거나 해결할 수 없는 숨은 오류로 저장을 막지 않는다.
- `ValidateLineupPreset`이 `Valid`를 반환한 Preview만 `검증된 배치 저장`으로 확정한다.
- `PartiallyValid`와 `Invalid`는 슬롯 경고와 원인을 유지하고 저장 CTA를 비활성화한다.
- 작은 선수 카드는 원작 선수 오더처럼 초상, 검은 이름·연도 Strip, 파란 Cost Strip, 하단 기용 포지션을 표시한다.
  6개 능력 막대는 확대 카드에서만 표시한다. 배치 역할 배지는 초상 위를 가리지 않도록 상단 라벨 영역에 둔다.
  상단 역할 그룹마다 카드 비율과 크기 기준을 공유하며 셋업·마무리만 독립적으로 확대하지 않는다.
- 카드 앞면의 초상은 명찰의 V자 윤곽을 따라 잘라 상반신 중심으로 표시한다. 포지션·이름은 은색 명찰 안에,
  연도는 우측 연도 배지 안에 정렬한다. 보유 목록의 배치 배지와 상단 포지션 글자를 동시에 표시하지 않는다.
- 카드 뒷면의 수비 위치는 부채꼴 외야와 내야 다이아몬드 위에 실제 주 포지션 한 곳만 표시한다.
  포지션 이름은 한국어를 사용하며 Runtime에 없는 수비 등급을 임의로 만들지 않는다.
- 보유 선수 목록의 카드를 선택하면 큰 선수 카드를 열고, 현재 화면의 야수 순서 또는 투수 순서에 따라
  `<`/`>`로 인접 선수를 탐색한다. 야수는 1번 타자와 Bench 5번, 투수는 1번 선발과 Closer를
  각각 탐색 경계로 삼아 바깥 방향 Button을 비활성화한다.
- 프리셋은 상단 Toolbar에서 선택하고 같은 Preview 계약을 쓴다. TeamColor 2슬롯과 Tactic 2슬롯은
  각각 `Owner.Roster.TeamColor`, `Owner.Roster.TacticCards`에서 편집한다.
- Active Roster 등록 변경은 선수 오더의 명시적 `배치 저장`으로만 확정하며 `ActiveRosterValidator`와 `ValidateLineupPreset`을 모두 통과해야 한다. 확정 시 기존 1군 계약은 보존하고 새 등록 선수의 1년 계약을 생성해 현재 25인과 계약 `CardId`를 같은 Aggregate에서 교체한다.

### 4.2 `Owner.Roster.TacticCards`

- 기존 `OwnerTacticsSnapshot`과 `UI_Scene_OwnerTactics`를 재사용한다.
- 보유 수량, 발동 조건, 대상, 지속시간, Counter를 비교하고 두 장착 슬롯을 편집한다.
- 선택은 즉시 저장하지 않고 명시적 `결정` 뒤 기존 LineupPreset 검증·저장 Command를 사용한다.

### 4.3 `Owner.Roster.SupportCards`

- 레퍼런스처럼 `사용 슬롯 / 보유 목록 / 현재 발동 슬롯 / 카드 정보` 영역을 먼저 제공한다.
- Runtime 정의·보유 목록·효과·저장 Command가 없으므로 모든 편성 Action은 잠그고 이유를 본문에 표시한다.
- 서포트 카드 능력치나 효과를 Presentation에서 임의 생성하지 않는다.

### 4.4 `Owner.Roster.TeamColor`

- 기존 `OwnerTeamColorSnapshot`과 `UI_Scene_OwnerTeamColor`를 재사용한다.
- 상단에서 장착 후보, 사용 가능한 팀 컬러, 현재 저장된 적용 구성을 분리하고 하단에 조건·적용 대상을 표시한다.
- 발동 인원, 효과 총량 기반 S/A/B/C 표시 등급, StackPolicy, 적용 선수와 대상 선수 1명당 역할별 실제 능력치 상승량을 비교한 뒤 두 슬롯을 명시적으로 확정한다. 상세 효과에 역할별 보너스 합계는 표시하지 않는다.
- 결정 전 Draft와 현재 저장 구성을 동시에 보여 변경 결과를 오인하지 않게 한다.
- TeamColor의 ID·UpgradeGroupId·FranchiseId·TeamSeasonKey는 화면에 직접 출력하지 않는다. World 표시명으로
  치환할 수 없는 값은 Family 기반 한국어 안전 이름으로 대체한다.

### 4.5 기존 세부 View 보존

- Pitching, Collection, Condition Production View와 Snapshot은 삭제하지 않는다.
- 선수 오더의 야수/투수 내부 선택, 전력보강, 경기 준비 Context에서 기존 기능을 계속 소비한다.
- 과거 `Owner.Roster.Pitching/Collection/Condition` Navigation Deep Link는 `Owner.Roster.Lineup`으로 이관한다.

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
- 서포트 카드 화면은 Runtime Consumer가 생길 때까지 Presentation 전용 Locked 상태다.
- Condition 단계명과 경기 능력치 보정은 `ConditionChemistryBalanceTable`을 사용한 결과만 소비한다.
- 투구 부하는 저장된 최근 3일 투구 수만 표시한다.
- 실패한 Preview는 원래 Runtime과 선택 프리셋을 바꾸지 않는다.

## 6. 자산 결정

기존 `PlayerCard_MainFrame_V2`, `PlayerCard_PortraitMiniFrame_V2`, `PlayerCard_LineupSubFrame_V1`은
원작의 조밀한 정보 구조와 맞지 않아 선수 오더 Card에는 사용하지 않는다.

큰 카드에는 `PlayerCard_Front_Reference`와 `PlayerCard_Back_Reference`, 작은 카드에는
`PlayerCard_Mini_Reference`를 사용한다. 기존 OrderFrame은 교체되었다.
초상·텍스트·실제 능력치는 Runtime UI가 합성하고, 작은 카드에는 능력치 막대를 넣지 않는다.
현재 생성 Prompt는 `docs/UI/ImageGenPrompts/PlayerCardReference.md`를 따른다.
아래는 교체 전 Frame의 생성 이력이다.

```text
원작 선수 오더의 조밀한 세로 카드 구조를 따르는 Unity용 빈 2:3 Player Card Frame.
상단 54% 투명 Portrait Window, Name/Year Strip, 정확히 6개 Stat Row, 하단 COST Plate.
Dark Navy/Charcoal Body, Silver Bevel, 작은 Red Corner Accent.
선수·실루엣·문자·숫자·로고·워터마크·Glow·Particle·Rarity 효과 금지.
Card 바깥과 Portrait Window는 실제 Alpha Transparency.
```

생성에는 built-in `imagegen`을 사용했으며 텍스트·수치·로고는 Runtime UI가 합성한다.

Skill Board는 기존 `Assets/Resources/UI/Growth/skill_block_tetromino_neutral_atlas.png`의
`SkillBlock_I/O/T/S/Z/J/L` Sprite를 실제 배치 위치·회전에 맞춰 표시한다. Rarity 색, Glow, 선택 강조,
Particle 등 특수효과는 붙이지 않으며 기존 효과가 있어도 이 카드 화면에서는 노출하지 않는다.

TeamColor 카드는 ImageGen으로 생성한 텍스트 없는 공통·타자·투수 전용 Plate 세 장을 사용한다.

- `team_color_card_plate_common_v2.png`: 양 역할 효과, Navy·Teal 연결 띠
- `team_color_card_plate_hitter_v2.png`: 타자 전용 효과, Oxblood·Orange Home Plate와 전진 사선
- `team_color_card_plate_pitcher_v2.png`: 투수 전용 효과, Cobalt 회전 궤도와 Precision Rail

세 Plate는 2048×720 RGBA, 실제 카드 영역 약 5.4:1, 동일 등급 홈과 Text Safe Area로 정규화한다.
효과 범위는 `HitterBonus.Total`과 `PitcherBonus.Total`의 실제 값으로 고르며 Family 이름만으로 추정하지 않는다.
등급·이름·진행도·발동 상태는 이미지에 굽지 않고 Unity Text로 렌더링한다. 생성·보정 Prompt와 치수 검증은
`docs/UI/ImageGenPrompts/TeamColorCardPlatesV2.md`를 따른다.

## 7. 구현 연결 상태

- 선수단 Local Tab을 `선수 오더 / 작전 카드 / 서포트 카드 / 팀 컬러`로 재편 완료
- Lineup/Pitching View와 기존 Production Adapter 보존 완료
- 슬롯·TeamColor·Tactic Preview/Validate/Confirm 완료
- TeamColor 레퍼런스 3열 편성·하단 정보 Layout 완료
- TeamColor 공통·타자·투수 효과별 ImageGen Plate 선택 완료
- TeamColor 상세의 역할별 합계 제거 및 대상 타자·투수 1명당 능력치 효과 표시 완료
- SupportCard 레퍼런스 편성 Layout과 명시적 Locked 상태 완료
- PitchingWorkload·Condition 단계·구종·시즌 기록 Inspector 연결 완료
- 큰 카드 Frame·초상·이름/연도·6개 능력 막대·Cost 구성, 작은 카드는 이름/연도·Cost·포지션 한 줄로 구분
- 앞면 저장 컨디션, 야구공 수비 위치 마커, 구종명/구속/등급 정렬 패널 연결
- 보유 목록 배치 상태 배지, 원 구단 엠블럼, 연도·구단 교차 드롭다운 필터 연결
- 배치 슬롯·보유 선수 양방향 선택, 1군 등록 교체 Preview·검증·저장 Command 연결
- 여러 1군 등록 교체를 동일 Preview에 누적하고 교체 건수 표시·일괄 검증·일괄 저장 연결
- 선수 오더 Right Inspector·하단 Action Bar 제거 및 본문 상단 Toolbar 이관 완료
- 상시 노출 `타자 / 투수` 전환과 전체 Workspace 확장 완료
- 큰 카드는 원본 목록과 선수단 맥락을 가리지 않는 우측 비모달 패널로 표시하며, 앞/뒤 전환과 야수·투수 표시 순서 기반 이전/다음 탐색 및 양 끝 Button 잠금을 지원
- 큰 카드 능력 막대는 기본 카드·훈련·스킬 블록·현재 장착 팀컬러·유학·합성 강화 기여분을 구분한 색 구간과 총 성장치로 표시
- 큰 카드 뒷면에 실제 배치된 중립 Tetromino Sprite 연결 완료
- 선수 기록을 현재 Runtime의 현재 Season·현재 League·현재 TeamSeasonKey로 제한 완료
- Collection Viewport Row Pool 가상화와 선택·검색·Scroll 보존 완료
- Condition 3열 화면, 10단계 Plot, 원인·경기 영향, Lineup 이동, 시즌 종료 Empty 완료
- 공용 Shell Header/Navigation/Context 유지, 선수 오더에서 Inspector/Action Slot만 숨김

## 8. 검증 범위

- `Baseball.Presentation.Tests.csproj` 보조 컴파일을 수행한다. 이는 Test 실행이 아니라 Production과
  Test Assembly의 C# 컴파일 확인이다.
- 확률·밸런스·경기 Simulation 공식은 바꾸지 않았으므로 대량 Simulation은 요구하지 않는다.
- 별도 Unity 검수 프로젝트에서 NUnit 메서드 회귀 및 1280×720 / 1920×1080 화면 렌더를 확인한다.
  원 프로젝트 Play Mode 및 Pointer/Keyboard/Gamepad 전체 조작 검증 여부는 별도로 기록한다.
- 사용자 화면 검증에서 발견한 잘림·간격·긴 문자열 문제는 이 문서의 Layout 계약을 수정하지 않고
  공용 Metric과 해당 View의 Content Safe Bounds 안에서 보정한다.
