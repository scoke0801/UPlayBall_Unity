# Shared / Mode 전용 경계

## 판정 규칙

다음 세 조건이 모두 같을 때만 공용 Component로 만든다.

1. 같은 의미인가.
2. 같은 Presentation Model 계약을 쓰는가.
3. 같은 Interaction인가.

모양만 비슷하면 공용화하지 않는다.

## Matrix

| 기능 | Shared Visual | Shared Data Contract | Owner Action | Player Action |
|---|---:|---:|---|---|
| GlobalTopBar | O | Shell model | Money/SP/DP | Money/Condition/Fatigue |
| PrimaryNavigation | O | Manifest | Owner routes | Player routes |
| PlayerMiniCard | O | `PlayerMiniCardModel` | 선택/편성 후보 | 선택/내 선수 강조 |
| PlayerDetail | O | 공통 Player/Card 정보 | 강화·훈련·잠금 | 성장·계약 또는 읽기 전용 |
| Roster visual | O | `RosterWorkspaceModel` | 편집 Command | 읽기 전용, 감독 이유 보기 |
| League/Schedule/Records | O | 기존 View data | 분석/준비 | 내 선수 Filter/기용 |
| Match HUD | O | `MatchEvent`/View state | Owner overlay | Player overlay |
| Popup/Tooltip/Toast | O | UI message model | 동일 | 동일 |
| Scout | X | Owner economy | 구매/결과 | 노출 금지 |
| TeamColor 장착 | X | Owner Resolver | 2 Slot 편집 | 노출 금지 |
| Card Enhancement/Training | X | `OwnedPlayerCardState` | 편집 | 노출 금지 |
| Career Growth | X | `CareerPlayerState` | 노출 금지 | 편집 |
| Contract Workspace | Layout 일부만 | 계약 종류가 다름 | 구단 자산 관점 | 개인 커리어 관점 |

## 강제 경계

- Player namespace/assembly 경로는 `OwnedPlayerCardState`, SP, DP, CardTraining을 참조하지 않는다.
- `ManagerTacticProfile`, 감독 AI, 감독 교체 판단은 Owner로 이름을 바꾸지 않는다.
- Header와 Navigation은 `if (mode)`를 소유하지 않고 공급된 Profile/Slot을 그린다.
- Roster 숫자, Foreign/중복/OffPosition, TeamColor, Scout 확률은 UI가 계산하지 않는다.
- Mode별 action은 `IModeSpecificActionProvider` 또는 구체 Presenter에 둔다.
