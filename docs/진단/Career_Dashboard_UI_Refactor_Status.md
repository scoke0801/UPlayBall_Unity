# Career Dashboard 장식 프레임 통합 작업 기록

## 작업 목표

자동 생성된 야구 장식 프레임을 각 최상위 정보 모듈의 유일한 외곽 경계로 사용한다.
장식 프레임 내부의 레거시 사각 `Image`, 중첩 Border, `Outline`, Bevel형 표현은 제거하고,
내부 정보는 `FlatSurface`, 간격, Divider, 타이포그래피로 구분한다.

게임 데이터, 시뮬레이션, 시즌 진행, 저장 구조와 버튼 콜백은 변경하지 않는다.

## 범위

- `UI_Scene_CareerDashboard`와 관련 partial 클래스
- `CareerUiSkin`
- `CareerTabNavigation`
- Dashboard가 사용하는 공통 Presentation UI 컴포넌트
- Career UI Legacy Visual Reference Validator

변경 금지 범위:

- `Baseball.Core`
- `Baseball.Simulation`
- 성장·계약·경기 계산 로직
- 세이브 데이터 구조
- 장식 프레임 PNG 픽셀
- 메뉴 순서와 기존 버튼 콜백

## 조사 결과 요약

Dashboard 전용 프리팹은 없으며 `UI_Scene_CareerDashboard`가 전체 계층을 런타임 생성한다.
최상위 `Surface`는 `CareerUiSkin`에 의해 장식 프레임으로 변환되지만 다음 레거시 표현이 함께 남아 있다.

- 최상위 카드마다 사각 `*PanelShadow`
- `CreateSection` 호출마다 외곽 Image와 내부 Surface의 이중 구조
- `AdvanceFrame`의 사각 Image와 자동 `Outline`
- 상단 League/Date/Money Segment의 독립 사각 배경
- 최근 경기의 고정 회색 빈 칸과 뉴스의 불완전한 Empty State
- 절대 좌표 기반 1920×1080 Content와 Scroll 없는 목록

## 목표 Hierarchy

```text
CardRoot
├─ DecorativeFrame
├─ HeaderRoot
├─ ContentSafeArea
└─ InteractionRoot
```

규칙:

1. `DecorativeFrame`은 카드당 한 장만 사용한다.
2. `CardRoot`는 레이아웃 컨테이너이며 시각 Image를 갖지 않는다.
3. 내부 Section은 기본적으로 RectTransform만 사용한다.
4. 배경이 필요한 Section만 테두리 없는 `FlatSurface` 한 장을 사용한다.
5. 버튼만 Hover/Pressed/Focused 상태 표현을 갖는다.
6. 프레임은 Variant별 Content Padding을 소유한다.

## 단계 상태

| 단계 | 상태 | 완료 조건 |
|---|---|---|
| 0. 전수조사 | 완료 | 런타임 Hierarchy, Sprite, Raycast, 공유 범위, 제거 영향 분류 |
| 1. 작업 문서화 | 완료 | 범위·구조·단계·검증 제한 기록 |
| 2. 공통 기반 | 완료 | 명시적 Visual Role, Theme Token, Frame Padding과 SafeArea 추가 |
| 3. Dashboard Layout | 완료 | 상·하단 사이 3열 2행 LayoutGroup 전환 |
| 4. 영역별 정리 | 완료 | Legacy Box 제거, Empty State, Scroll, 버튼 체계 통일 |
| 5. 공통 Chrome | 완료 | 상단 Segment 배경 제거, 하단 탭 상태 정리 |
| 6. Validator | 완료 | 중첩 프레임·Legacy Image·Raycast·SafeArea 위반 정적 검출 |
| 7. 검증·마감 | 완료(선행 차단 기록) | 정적 검사 완료, 컴파일 선행 차단과 미검증 항목 기록 |

## 후속 범위: Career 탭·뉴스 화면

Dashboard 정리 후 `UI_Scene_League`, `UI_Scene_Team`, `UI_Scene_Contract`,
일정 화면과 커리어 뉴스 팝업에서 동일한 런타임 생성형 Legacy Box Panel이 확인되었다.
성장 화면의 선수 요약과 Dashboard에는 프레임 안전 영역을 벗어나는 콘텐츠도 남아 있다.
따라서 공통 장식 프레임 규칙과 이름 추정 스킨의 적용 경계를 Career 화면군 전체로 확장한다.

| 단계 | 상태 | 완료 조건 |
|---|---|---|
| L0. 추가 화면 구조 조사 | 완료 | Panel/Section/Header/행/팝업의 구형 Image와 스킨 오인 경로 확인 |
| L1. 후속 작업 문서화 | 완료 | 대상·유지 범위·검증 제한 기록 |
| L2. 공통 스킨 경계 수정 | 완료 | 데이터 행·배지·카테고리가 버튼/프레임 스킨으로 오인되지 않음 |
| L3. 리그·구단 화면 전환 | 완료 | 카드당 장식 프레임 1개, 내부 FlatSurface/Divider 전환 |
| L4. 계약·일정 화면 전환 | 완료 | 최상위 장식 셸 유지, 내부 Legacy Box 제거 |
| L5. 성장·홈 위치 수정 | 완료 | 제목·푸터·선수 요약이 ContentSafeArea 안에 포함 |
| L6. 커리어 뉴스 정리 | 완료 | 팝업 외곽 프레임 1개, 필터·목록·닫기 위치/문법 통일 |
| L7. Validator 확장 | 완료 | 대상 런타임 소스의 Legacy 생성 패턴 검출 |
| L8. 정적 검증·마감 | 완료 | 소스 구조·diff·보조 컴파일 검사, 테스트 미실행 사실 기록 |

후속 작업도 데이터 바인딩, 탭 순서, 버튼 콜백, Core/Simulation/Save 구조와
원본 프레임 PNG를 변경하지 않는다. 사용자 요청에 따라 Unity Test Mode와 Play Mode는 실행하지 않는다.

## 완료 조건

### 시각 구조

- 최상위 카드당 장식 프레임은 한 장이다.
- `ContentSafeArea` 아래에 장식 프레임이 중첩되지 않는다.
- 일반 데이터 셀에 장식 코너와 중첩 Outline이 없다.
- Primary 버튼은 `경기 진행` 하나다.
- 일반 역할 상태에 금색을 사용하지 않는다.

### 레이아웃과 상태

- 카드 배치는 LayoutGroup과 LayoutElement 비율로 구성한다.
- 하단 카드와 내비게이션이 겹치지 않는다.
- 뉴스와 최근 경기 Empty State가 존재한다.
- 포지션 경쟁과 예정 경기 목록은 넘칠 때 내부 Scroll 처리한다.
- Key Prompt는 버튼 라벨과 독립된 요소로 표시한다.

### 회귀 방지

- Core, Simulation, 세이브 구조를 변경하지 않는다.
- 기존 CareerTabNavigation 순서와 버튼 콜백을 유지한다.
- Presentation 어셈블리와 Editor 어셈블리가 컴파일된다.
- Validator는 자동 수정하지 않고 경로와 위반 내용을 보고한다.

## 검증 제한

사용자 요청에 따라 Unity 테스트 모드와 Play Mode 검증은 실행하지 않는다.
코드 작업 완료 후 `dotnet build --no-restore`와 정적 구조 검증만 수행한다.
1280×720, 1920×1080, 2560×1440, 3440×1440 시각 캡처 및 실제 입력 검증은 미검증 항목으로 남긴다.

## 작업 로그

- 전수조사 완료: Dashboard는 프리팹이 아니라 런타임 생성 구조임을 확인했다.
- 작업 문서 작성: 범위, 목표 Hierarchy, 단계 상태와 검증 제한을 고정했다.
- 공통 기반 완료: `CareerUiTheme`, `CareerUiVisualElement`, `CareerUiFrame`을 추가했다.
- 공통 스킨은 명시적 `DecorativeFrame`과 `FlatSurface`를 이름 추정 전에 처리한다.
- Dashboard Layout 완료: 헤더와 하단 내비게이션 사이를 3열 2행 비율 Layout으로 전환했다.
- 모든 최상위 카드가 단일 `DecorativeFrame`과 Variant별 `ContentSafeArea`를 소유한다.
- 영역별 정리 완료: 6개 카드의 내부 규격을 안전 영역 기준으로 재배치했다.
- 다음 경기의 개별 Metadata Box를 단일 Row와 Divider로 통합하고, Primary/Secondary 버튼 문법과 독립 Key Prompt를 적용했다.
- 최근 경기와 뉴스에 실제 Empty State를 추가하고 회색 빈 슬롯을 제거했다.
- 포지션 경쟁과 예정 경기는 `ScrollRect`와 `RectMask2D` 안에서 전체 항목을 표시한다.
- 공통 Chrome 완료: 상단 League/Date/Money의 사각 Segment 배경을 제거하고 연속 Bar와 Divider 구조로 바꿨다.
- 하단 메뉴의 순서와 콜백은 유지하면서 공통 Theme Token과 명시적 선택·상호작용 역할을 적용했다.
- 보조 컴파일 1차 시도는 Game 레이어의 선행 누락 타입 3개 때문에 Presentation 진입 전에 중단됐다. UI 범위 밖 사용자 변경이므로 수정하지 않는다.
- Validator 완료: 통합 `BaseballToolsLauncher`의 UI 카테고리에 읽기 전용 검증 도구를 등록했다.
- Validator는 UI Prefab과 로드된 `CareerUiFrame`, 런타임 Dashboard 소스를 검사하며 자동 수정하지 않는다.
- CI 진입점은 위반 시 `BuildFailedException`을 발생시키고 Prefab·Hierarchy·Sprite 경로를 Console에 남긴다.
- 마감 정리: 내부 카드 규격과 위치를 4px 그리드에 맞추고 고정 색상을 `CareerUiTheme`으로 이동했다.
- 후속 컴파일 수정: partial 파일의 Narrative와 SeasonReview가 공유하는 `PanelColor` Theme 별칭을 복원했다.
- 후속 전수조사: 리그·구단·계약·일정·성장·뉴스가 Dashboard와 같은 런타임 Box 생성 패턴을 사용함을 확인했다.
- 공통 스킨 경계 수정: `FlatSurface` 역할의 클릭 가능한 데이터 행은 CTA 장식 Sprite 대신 기존 평면 색상 전환을 유지한다.
- 리그·구단·계약의 최상위 카드 생성기를 `CardRoot / DecorativeFrame / HeaderRoot / ContentSafeArea / InteractionRoot` 구조로 전환했다.
- 일정은 달력과 구단 요약만 최상위 장식 프레임으로 유지하고, 다음 경기·월 요약·목록 셀 등 내부 그룹은 FlatSurface로 전환했다.
- 성장 선수 요약의 컨디션·최근 성장·역할 경쟁 영역을 재배치해 하단 프레임 침범을 제거했다.
- Dashboard 카드 제목을 실밥 아래로 내리고 다음 경기 CTA와 예정 경기 Footer를 프레임 안전 영역 안으로 이동했다.
- 커리어 뉴스 팝업은 외곽 장식 프레임 한 장만 사용하며 제목·읽지 않음·닫기·3열 본문을 프레임 안쪽으로 이동했다.
- Validator 검사 범위를 Dashboard에서 League, Team, Contract, Schedule, Growth, CareerNews까지 확장했다.

## 결과 보고

### 수정·추가 파일

- `UI_Scene_CareerDashboard.cs`: 3열 2행 Layout, 단일 장식 프레임, SafeArea, 6개 카드 내부 구조와 상태 처리
- `CareerTabNavigation.cs`: Theme Token, 단일 선택 상태, 상호작용 Visual Role
- `CareerUiSkin.cs`: 명시적 Visual Role 우선 적용과 프레임 내부 이름 추정 차단
- `CareerUiTheme.cs`: 공통 색상·간격·프레임 Padding Token
- `CareerUiVisualStructure.cs`: `CareerUiVisualElement`, `CareerUiFrame`
- `CareerUiLegacyVisualValidator.cs`: 통합 런처와 CI용 읽기 전용 검증기
- 이 문서: 단계별 상태, 검증 결과와 미검증 범위

후속 화면 확장:

- `UI_Scene_League.cs`: 리그 카드·상단 Segment 단일 프레임 문법
- `UI_Scene_Team.cs`, `UI_Scene_Team.Helpers.cs`, `UI_Scene_Team.Roster.cs`: 구단 카드 평면화와 로스터 데이터 행 스킨 오인 방지
- `UI_Scene_Contract.cs`: 계약 카드·지표·상여·시장 정보 단일 프레임 문법
- `UI_Scene_CareerSchedule.cs`, `UI_Scene_CareerSchedule.Chrome.cs`: 달력·구단 요약 외곽 프레임과 내부 FlatSurface 분리
- `UI_Scene_CareerGrowth.Rendering.cs`, `UI_Scene_CareerGrowth.Workspace.cs`: 성장 카드 문법과 선수 요약 하단 배치 수정
- `UI_Popup_CareerNews.cs`: 팝업 외곽 프레임·3열 안전 배치·평면 필터/기사 행
- `CareerUiSkin.cs`: 클릭 가능한 FlatSurface와 CTA 버튼의 스킨 경계 분리

장식 프레임과 버튼 PNG는 원본을 유지했다. 새 Bitmap이 필요하지 않아 ImageGen을 사용하지 않았다.

### 제거한 레거시 구조

- 최상위 카드의 `*PanelShadow`
- `CreateSection`의 외곽 Border Image + 내부 Surface 이중 구조
- 다음 경기의 `AdvanceFrame`과 `ButtonGlow`
- 경기일·구분·시즌의 개별 `CreateInfoChip` Box
- 상단 League/Date/Money의 개별 Segment 배경 Image
- 최근 5경기의 데이터 없는 회색 슬롯
- 뉴스가 없을 때 임시 Feed Row로 Empty State를 대신하던 구조
- 일반 역할 상태인 포지션 경쟁의 금색 대형 강조

### 정적 검증 결과

| 항목 | 결과 |
|---|---|
| 최상위 Dashboard 카드 생성 수 | 6개 |
| `PanelShadow` 잔존 | 0개 |
| `AdvanceFrame` 잔존 | 0개 |
| `CreateInfoChip` 잔존 | 0개 |
| 단일 `DecorativeFrame` 명시 구조 | 확인 |
| `ContentSafeArea` 명시 구조 | 확인 |
| 내부 Scroll 생성 구조 | 확인 |
| 독립 Key Prompt 생성 구조 | 확인 |
| 하단 탭 순서 | 홈 / 선수 / 성장 / 일정 / 리그 / 구단 / 기록 / 계약 유지 |
| 대상 파일 `git diff --check` | 통과 |

### 컴파일 결과

후속 작업 시점에는 선행 Game 타입이 현재 작업 트리에 존재해 차단이 해소됐다.

| 명령 | 결과 |
|---|---|
| `dotnet build Baseball.Presentation.csproj --no-restore` | 경고 0, 오류 0 |
| `dotnet build Baseball.Editor.csproj --no-restore` | 경고 0, 오류 0 |

이는 Unity 외부 보조 컴파일 결과이며 실제 Sprite Import와 Canvas 렌더링 검증을 대신하지 않는다.

### 의도적으로 실행하지 않은 검증

사용자 요청에 따라 Unity Test Mode와 Play Mode는 실행하지 않았다. 다음 항목은 미검증이다.

- 1280×720, 1920×1080, 2560×1440, 3440×1440 실제 캡처
- 마우스·키보드·게임패드의 실제 포커스 이동과 Popup 복원
- Hover / Pressed / Focused / Disabled의 실제 렌더링
- Sprite Import 후 9-Slice 모서리와 실밥의 최종 픽셀 상태
- Unity Console Error/Warning
