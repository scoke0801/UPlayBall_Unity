# Owner Mode Runtime Adapter 변경 요청 — 부분 해결

## 2026-09-05 해결 상태

- `OwnerModeManager`가 새 게임, Save/Load, 다음 경기, 주차 진행과 실제 운영 Command를 소유한다.
- `OwnerModeRosterStatus`가 인원 요약과 `RosterValidationResult`를 Game 레이어에서 공급한다.
- Home, 재정, 시설, Staff, Pregame, Condition·궁합이 Production Shared Shell에 연결됐다.
- 실제 순위/최근 5경기와 편집 중 Roster Draft/Commit Preview는 아직 공급되지 않는다.
- 아래 내용은 미해결 계약의 배경 기록으로 유지한다.

## 현재 확인된 상태

- `ManagerHistoricalRuntimeState`에는 Player Team key, League Grade, 25인 Roster, Owned Card, Money/SP/DP/Pity가 존재한다.
- `ManagerHistoricalSaveData`와 JSON Store는 Career Save와 분리되어 있다.
- 주차와 다음 경기는 Production Owner Game Manager가 소유한다. 순위와 최근 5경기 Query는 아직 없다.
- `ManagerHistoricalRuntimeState`는 완성된 Roster만 허용하므로 편성 중 Preview의 `RosterValidationResult`를 보관하거나 발행하지 않는다.

## 필요한 Game 레이어 계약

Presentation의 `OwnerHomeSnapshot`을 만들기 위해 다음을 공급하는 Owner 전용 Manager/Query를 추가해야 한다.

- 실제 World의 시즌/날짜/주차
- Player Franchise 표시 이름
- 현재 순위와 최근 경기
- 다음 경기와 예정 선발
- `ManagerHistoricalRuntimeState.Economy`의 Money/SP/DP/Pity
- Player Team의 현재 편집 Draft와 Simulation `RosterValidationResult`
- 로스터 저장 Command 결과

UI가 25/14/11/3, 중복 PlayerPerson, Off Position, Pitcher Role 규칙을 다시 계산하면 안 된다. 편집 Preview도 Simulation Resolver가 계산하고 Game 레이어가 표시용 결과를 전달한다.

## Production 진입 조건

Owner 타이틀 카드는 Home/Save/Load의 최소 Production 경로가 연결되어 활성화했다. 다음은 핵심 Lineup Workspace 완료 조건으로 남는다.

1. 편집 Draft/Commit Command가 `ActiveRosterValidator` 결과를 제공한다.
2. Roster/Collection의 실제 데이터 경로를 Unity에서 검증한다.
3. Off Position·외국인·중복 사유를 같은 Resolver Preview로 표시한다.

시설은 실제 Runtime에 연결했다. 계약·트레이드와 아직 View가 없는 Scout/육성/전술/Owner 리그 화면은 `OwnerModeUiProfileFactory`에서 구체적인 잠금 사유와 함께 비활성화한다.
