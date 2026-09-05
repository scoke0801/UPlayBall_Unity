# 멀티 에이전트 파일 소유권

## 공통 규칙

- 아래 Owner 외 Agent는 해당 파일을 직접 수정하지 않는다.
- 공통 API 변경 요청은 `docs/UI/ChangeRequests/`에 기록하거나 Lead에게 메시지로 전달한다.
- 현재 사용자 변경이 있는 Historical/Importer 파일은 이번 UI 작업에서 수정하지 않는다.

## Phase 1

| Owner | 전용 영역 | 단일 Owner 파일 |
|---|---|---|
| Agent A — Terminology | `GameMode` 명칭, 사용자 문자열, Owner 관련 문서/테스트 | `CareerCreationDraft.cs`, `UI_Scene_NewGame.Title.cs`, Owner 문서 링크 |
| Agent B — Shared Architecture | `Assets/02.Scripts/Presentation/SharedUI/**`, 공용 UI 테스트 | 새 Shell/Profile/Navigation/Component 파일 |
| Agent C — Visual Asset | `Assets/Resources/UI/Generated/**`, `GeneratedUIAssets.md` | 생성 Raster와 prompt manifest |
| Lead | Route 통합, Theme, 기존 화면 변경, 최종 merge | `CareerUiTheme.cs`, `CareerUiSkin.cs`, `CareerTabNavigation.cs`, Bootstrap/Router |

## Phase 2

| Owner | 전용 영역 |
|---|---|
| Agent D — Player | 기존 `Presentation/Career/UI_Scene_*`의 Player 전용 Workspace 이관 |
| Agent E — Owner | 신규 `Presentation/Owner/**`, Owner Game manager/adapter |
| Agent F — Shared Screens | 신규 `Presentation/SharedScreens/**`, Schedule/League/Records/Team read-only |
| Agent G — QA | UI EditMode/PlayMode, screenshot fixture, terminology/permission audit |

## 충돌 고위험 파일

- `CareerUiTheme.cs`
- `CareerUiSkin.cs`
- `CareerTabNavigation.cs`
- `NewGamePresentationBootstrap.cs`
- `UI_Scene_NewGame.Title.cs`
- `UI_System_Root.prefab`
- `UI_CareerPresentation.prefab`

고위험 파일은 Lead 또는 표의 단일 Owner만 수정한다.
