# 프야매 레퍼런스 UI/UX + 상점 뽑기 연출 통합 구현 TODO

## 0. 이 문서의 위치

엔트리브소프트 「프로야구 매니저」의 UX 문법(카드 중심 선수단 관리, 높은 정보 밀도, 카드
선택→상세 확인, 팀/리그/기록 연결 구조)을 레퍼런스로 삼아 선수단·월드/구단/계약·상점(스카우트/
스킬/전술) UI와 뽑기 연출을 실제 Unity 화면으로 구현하기 위한 작업 명세다. 원작의 이미지·로고·
아이콘·폰트·에셋은 복제하지 않고 UX 문법만 참고한다.

**착수 전 필수 선행 조건 (미해결):** 이 작업은 감독모드 상점 경제(선수 스카우트·카드 강화·
전술카드)의 UI를 전제로 한다. 그런데 `docs/todo/역사시뮬레이션_감독모드/README.md` 4항은
"감독모드 플레이어 구단만 `OwnedPlayerCardState`, SP 스카우트, 중복 강화, 카드 DP 훈련 경제를
사용한다. AI 구단과 선수 커리어 모드에는 이 소유 경제를 적용하지 않는다"를 Source of Truth로
명시하고 있고, 감독모드 자체가 `구현_현황.md` 기준으로 아직 완성된 단계가 아니다. 따라서 이
문서의 상점/스카우트/전술카드 파트는 **감독모드 경제 시스템이 실제로 존재하고 안정화된 뒤** 착수
대상이며, 그 전에는 선수단 UI·월드/구단 UI 파트만 선수 커리어 모드 기준으로 먼저 진행할 수 있는지
판단이 필요하다. 착수 시점에 `구현_현황.md`를 다시 확인해 감독모드 경제 시스템(§13~§25 대상)의
실제 구현 여부를 재확인한다.

## 1. 목표와 금지 경계

- 목표는 기능 완성이 아니라 "선수 카드를 보며 라인업/구단/계약을 판단하는 느낌"을 실제 플레이
  경험으로 만드는 것. 완료 기준은 §35, 구현 중 금지 사항은 §36을 그대로 따른다.
- **Simulation/Core ↔ Presentation 경계는 이 작업 전체에서 가장 먼저 지켜야 할 계약이다.**
  UI는 Scout 확률, 강화 성공, Cost/Edition 판정, 포지션 적합도 등 어떤 결과도 재계산하지 않는다.
  Simulation/Game이 만든 결과(`ScoutResult` 등)를 Presentation ViewModel로 그대로 표시하고,
  Presenter는 이미 결정된 결과를 연출할 뿐이다(`ScoutRevealPresenter.Play(result)` 형태).
- 선수 커리어 모드에서는 로스터/라인업/전술 결정 권한을 플레이어에게 절대 넘기지 않는다
  (`BaseballManager_PROJECT.md` 절대 경계 3). 같은 선수단 화면을 재사용하되 편집 기능만 제거한
  읽기 전용으로 만든다.
- 모드별 상점 활성 범위 차이(요청 UI 안 vs. 기존 문서 명세)는 **`ShopCategoryAvailability`라는
  데이터 기반 레이어로 분리**한다. GameMode 분기를 UI 코드에 하드코딩하지 않고, 잠금 여부/사유를
  설정값으로 표현해 코드 수정 없이 문서 명세 방식으로도 되돌릴 수 있게 만든다.

## 2. 사전 조사 (구현 착수 전 필수)

구현 전 반드시 다음을 조사하고, 조사 결과를 바탕으로 기존 구조를 확장한다. 기획 문서보다 코드가
더 발전해 있으면 문서를 그대로 덮어쓰지 말고 코드 기준으로 자연스럽게 확장한다.

- 기존 `Unity_UI_Production_Guidelines_UPlayBall.md`, UIBase/UISceneBase/UIPopupBase 등 공통 구조
- 기존 Home/Match/Player/Team/League/Contract/Growth/Shop 화면
- 기존 대형 Player Card Front/Back 디자인·Prefab, Flip/Tween 구현 여부, 공통 Tween 라이브러리
- 현재 Roster/Lineup/Bench/PitcherRole 구조, `TeamSeason`, `LeagueGrade`, Club DNA, TeamColor
- `OwnedPlayerCardState`, `ScoutPoolDefinition`/`ScoutRoller`/Pity Gauge, 전술카드/전술 연구소
- 선수 커리어 계약 시스템, GameMode 구분, 현재 세이브 구조
- 기존 UI 공통 컬러/폰트/패널/버튼/탭 디자인
- 핵심 기획 문서: `docs/todo/역사시뮬레이션_감독모드/` 하위 01~08, `BaseballManager_PROJECT.md`,
  `docs/구현_현황.md`, `docs/지침/` 전체

## 3. 작업 방식 — 병렬화 전 공통 계약 확정

영역이 독립적이므로 병렬 작업이 유리하지만, 같은 파일을 여러 작업 스레드가 동시에 수정하면
충돌한다. **공통 인터페이스와 파일 소유권을 먼저 확정한 뒤에만 병렬화**한다.

권장 분할:

1. **공통 구조 분석 + 공통 컴포넌트** — UI 아키텍처 분석, MiniPlayerCard, FullPlayerCardViewer
   연결, Card Flip, 공통 Tooltip/Modal/Tab, TeamSeason Header, 공통 Card Reveal Controller
   인터페이스, 공통 Tween/Transition 규칙. 이 계약이 확정된 뒤 나머지가 시작한다.
2. **선수단 UI** — 감독모드 편성 화면, 선수모드 관람(읽기 전용) 화면, 동적 Roster Slot, 역할별
   UI, 선수 교체 UX, TeamColor 상태 표시, Mini→Full 카드 전환.
3. **월드/리그/구단/계약 UI** — 다른 리그 조회, TeamSeason 상세, Club DNA 표현, 계약 오퍼→구단
   상세 연결, 내 포지션 경쟁자 필터.
4. **상점 UI** — Shop 공통 Shell, 선수 스카우트/스킬/전술 연구소 화면, 재화 표시, 확률 UI, Pity
   Gauge, 모드별 잠금, 결과 화면, 보관함 연결, 재구매 UX.
5. **뽑기/연출 + ImageGen** — 스카우트/스킬/전술 연구 연출, Cost/Edition Reveal, Skip/최소 연출,
   필요한 그래픽 에셋 생성·가공·적용.
6. **QA/통합 검증** — Prefab Reference 누락, 해상도 대응, GameMode 경계, Simulation/Presentation
   경계, 기존 UI 회귀, 신규 UI 상태 테스트, 중복 카드/강화 상한/Pity 표시 검증, Placeholder 검출.

공통 파일(공통 컴포넌트, ViewModel 계약)은 1번 담당만 수정한다. 각 영역은 조사 결과를 먼저 짧게
공유하고 공통 계약을 확정한 뒤 코드 수정을 시작한다.

## 4. 공통 디자인 방향

키워드: Professional Baseball Manager / Baseball Front Office / Roster Management / Card
Collection / Sports Data / Warm Stadium Atmosphere.

피할 것: Sci-Fi HUD, 네온 사이버펑크, 모바일 RPG식 과도한 광원, 우주/홀로그램, 지나친 유리 패널,
화면 전체를 팀 컬러 하나로 덮기, 버튼마다 다른 디자인, 실제 프로야구 구단 로고/상표 복제.

기본 원칙: 정보 가독성 우선, Neutral Background + Dark/Light Panel, Team Primary Color는 Accent
(탭 인디케이터·얇은 테두리·선택 강조)로만 사용, 카드가 가장 중요한 시각 오브젝트.

## 5. ImageGen 사용 규칙

새로 필요한 UI/연출 디자인은 ImageGen으로 만들되, 전체 화면 이미지를 그대로 UI 한 장으로 쓰지
않는다.

1. 현재 게임 UI 스크린샷/에셋 분석
2. 같은 디자인 언어로 ImageGen UI Concept 생성
3. Background/Frame/Decoration/Stamp/Reveal Element를 개별 그래픽 자산으로 제작
4. Text/Button/List/Card는 Unity UI Component로 구현
5. 필요 시 9-Slice로 가공
6. 여러 해상도에서 안 깨지도록 Anchor/Layout 사용
7. 실제 Prefab에 적용
8. 시안과 구현이 지나치게 다르면 ImageGen 재생성

ImageGen 프롬프트는 `Docs/UI/ImageGenPrompts/` (또는 기존 문서 구조에 맞는 위치)에 기록하고,
최종 보고에서 어떤 이미지가 어떤 UI에 쓰였는지 명시한다.

ImageGen 결과 검수 기준: 같은 게임으로 보이는가, SF 느낌이 과한가, 팀 컬러와 희귀도 컬러를
혼동하는가, 실제 구단 브랜드처럼 보이는 마크가 생겼는가, 텍스트가 이미지에 박혀 있는가, TMP로
교체 가능한가, 9-Slice 가능한가, 여러 해상도에서 쓸 수 있는가, 카드보다 배경 장식이 더 눈에
띄는가.

## 6. 선수단 UI

- 감독모드는 편집 가능, 선수 커리어 모드는 동일 화면을 읽기 전용으로 재사용 — 화면을 두 벌 만들지
  않는다. 선수 모드 상단에 "현재 선수단은 감독 AI가 운영 중입니다" 안내를 둔다.
- **MiniPlayerCard**: 기존 Full Card를 단순 축소하지 않고 새로 설계. 표시 우선순위 — Portrait,
  Name, Position, OriginYear, Cost, Edition, Condition, 현재 역할/출장 상태. 감독모드에서는
  EnhancementLevel도 표시. 정보 과다 시 아이콘/Badge로 압축.
  - Mini Card 클릭 → 선택 Tween → 기존 Full Player Card Overlay(기존 디자인 재사용, 새로
    만들지 않음) → 클릭 시 Front/Back Flip Tween → ESC/외부 클릭으로 닫기.
- **편성 화면**: 실제 Roster 데이터로 슬롯을 동적 생성한다(고정 25개 GameObject를 이름으로 찾는
  방식 금지). `RosterViewModel` 등 Presentation Model을 도입. 좌측 Lineup/Batting Order, 중앙
  포지션 기반 Starter 배치, 하단 Bench + Starting Pitchers/Bullpen/Setup/Closer, 우측 Quick
  Detail + TeamColor Panel. Layout Group/동적 Grid로 구성해 인원 변경에도 재작성 불필요.
- **선수 교체 UX**: 클릭 기반 기본(드래그 앤 드롭은 보조). 현재 선수 클릭 → 교체 가능 Drawer,
  Position/Condition/Cost/OriginYear/Franchise/Edition 필터, 가능하면 포지션 적합도 표시. 정상/
  서브/비정상 포지션을 명확히 구분하고, 비정상 배치는 금지가 아니라 Warning으로 표시한다.
- **TeamColor UI**: 상태는 기존 Resolver 결과를 그대로 쓰고 UI가 재계산하지 않는다. 진행도
  변화(예: 23/25 → 24/25)에 가벼운 숫자·Progress Bar Tween을 준다.

## 7. 월드 / 리그 / 구단 / 계약 UI

- `TeamSeason`/`LeagueGrade`(Rookie~Galaxy) 탐색 UI. 같은 Franchise라도 OriginYear가 다르면
  다른 TeamSeason이므로 표시는 항상 `OriginYear + Franchise`("2011 COMETS") 형태를 우선한다.
- 다른 리그의 진행 중 경기(LIVE) 상태를 선수모드/감독모드 공통으로 조회 가능. 전체/진행 중/내
  구단 관련 필터 지원.
- **구단 상세(TeamDetail)는 어디서 구단명을 눌러도 동일 화면으로 연결**한다. 중복 화면 금지.
  탭: OVERVIEW/ROSTER/SEASON/MANAGER/HISTORY. ROSTER 탭은 선수단 UI의 MiniPlayerCard를 재사용.
  Club DNA는 단순 Power Gauge가 아니라 운영 성향(장타 중심/컨택 중심/주루/수비/선발 신뢰/불펜
  의존/육성/경험 선호)으로 표현한다.
- **계약 오퍼 → 구단 상세**: 오퍼가 금액만 보고 선택하는 구조가 되지 않도록 각 Offer에 "구단
  자세히" 진입점을 두고 공통 TeamDetail을 연다. "내 포지션만 보기" 필터 제공. 주전 가능성처럼
  Simulation에 실제 값이 없는 지표는 UI가 임의로 만들어내지 않는다.

## 8. 상점 UI 공통 구조

- Shop Shell 하위에 PLAYER SCOUT / SKILL / TACTICAL LAB 카테고리. 상단에 실제 존재하는 재화만
  표시(Money/SP/DP 등, 임의 재화 신설 금지).
- **`ShopCategoryAvailability`**: Category별 Unlocked/LockedByGameMode/LockedByProgress/
  LockedByLeague 상태를 데이터로 표현. 감독모드에서 Player Scout/Tactic을 잠글 수도, §0에서
  언급한 기존 명세대로 전부 열 수도 있게 **설정값만으로** 전환 가능해야 한다. 잠긴 카테고리는
  완전히 숨기지 않고 잠금 상태·사유를 표시한다.

## 9. 선수 스카우트 + 뽑기 연출

- 기존 `ScoutPoolDefinition`/`ScoutRoller`를 그대로 사용. General/Franchise/Year/YearFranchise/
  Award 풀 지원. 가격/확률/필터는 Simulation/Balance 데이터에서 가져오고 UI에 하드코딩하지 않는다.
  필터로 특정 Cost/Edition Bucket이 사라져 확률이 재정규화되면 **실제 최종 확률**을 표시한다("원래
  확률" 고정 텍스트 금지).
- Pity Gauge 표시, 100 도달 시 Focused Scout 활성화.
- 연출 컨셉은 "카드팩 개봉"이 아니라 "스카우팅 보고서 전달": Scout 클릭 → Shop Dim → Scouting
  Desk/Report Overlay → Report 등장 → Cost 암시 → Player Reveal → Edition Reveal → Full Card →
  Result Action. Pool 종류별 Label(Franchise/Year/YearFranchise) 차이를 반영.
- **Cost 연출과 Edition 연출은 별개다.** Cost 1~10을 색상 10개로 나누지 않고 강도 구간(1~3 기본
  보고서, 4~6 고급 문서/Stamp, 7~8 강조 Report/집중 조명, 9 Dim+집중, 10 정적+특수 승인
  Stamp+강한 Reveal)으로 묶는다. 특정 팀 컬러로 오인될 빨강/파랑 중심 표현은 피한다.
- Edition(Normal/AllStar/GoldenGlove/Mvp) 연출은 기존 카드 Visual을 그대로 쓰고 새로 디자인하지
  않는다. Normal은 무연출, AllStar는 조명/Star 이벤트, GoldenGlove는 시상식 Stamp, Mvp는 정적+
  Zoom+Delay Reveal.
- **Focused Scout**: 여러 후보 Report가 빠르게 지나가다 최종 하나가 남는 연출("FOCUSED
  SCOUTING" → 후보 스크롤 → "FINAL REPORT" → Reveal). Cost 7+ 확정감이 체감되어야 한다.
- 결과 화면: Full Player Card(기존 디자인, Flip 지원)로 Name/Position/Cost/OriginYear/
  Franchise/Edition/NEW 여부 표시. 중복이면 Reveal 종료 후 Duplicate UI(EnhancementLevel,
  DuplicateCount, 강화 가능 여부, 판매 가능 SP)와 강화/보관/판매 Action. MAX +5는 강화 버튼 제거.

## 10. 스킬 카드 + 전술카드 연출

- 스킬: 기존 Skill Block/Tetromino/Growth Inventory 구조를 먼저 조사해 그대로 연동. Tier는
  문서가 아니라 실제 코드 Enum을 기준으로 쓴다. 연출 컨셉은 "PLAYER DEVELOPMENT/TRAINING
  ANALYSIS" — 스카우트 Report 연출을 그대로 복사하지 않는다(Purchase → Development Analysis →
  Ability Keyword Scan → Card Back → Tier Reveal → Skill Card Reveal → Storage). 최고 등급은
  이펙트 폭발이 아니라 분석음 정지 → 짧은 무음 → "EXCEPTIONAL RESULT" → Reveal로 처리.
- 전술: 기존 `TacticCardDefinition` 연동. "TACTICAL LAB" 컨셉(경기 상황 데이터 분석 →
  Category 결정 → Tier 암시 → "STRATEGY FOUND" → Reveal). Category: Batting/Pitching/
  Analysis/Common. **일반 연구 Reveal Pool에서 얻을 수 없는 Signature 전술은 절대 포함하지
  않고 별도 Achievement Unlock 연출을 쓴다.**

## 11. 다중 뽑기 / Skip / 반복 사용 대응

- 5개/10개 등 다중 획득이 있거나 확장 예정이면 공통 MultiReveal 구조(한 장씩 Flip, Rare 이상
  직전 Tempo 지연, 최종 결과 Grid에 NEW/Duplicate/Rare+ 명시)를 만든다.
- 모든 Draw/Research 연출에 우측 상단 SKIP을 지원하고, "전체 연출/희귀 결과만 전체 연출/최소
  연출" 3단계 Presentation 설정을 둔다. **결과 자체의 RNG나 Simulation 실행 시점을 Animation
  Length와 연결하지 않는다** — 결과는 먼저 결정되고 Presentation은 그것을 표현할 뿐이다.

## 12. 사운드

기존 Audio 시스템 조사 후 가능한 범위에서 Hook 연결(스카우트: Paper/Stamp/Card Flip, 스킬:
Analysis Beep/Training Equipment/Result, 전술: Keyboard·Data/Board·Strategy/Result). 고등급
결과라고 볼륨을 키우지 않고 짧은 무음/정적을 적극 활용. Asset이 없으면 외부 저작권 Asset을 넣지
말고 Hook/Event까지만 구현하거나 프로젝트 규칙에 맞는 자체 Asset 방식을 쓴다.

## 13. Animation/아키텍처 원칙

- ImageGen은 Visual Design/Key Frame/Graphic Asset 생성까지만 담당하고 실제 움직임은 Unity에서
  구현(CanvasGroup Alpha, RectTransform Position/Scale/Rotation, Card Y Rotation, Blur/Dim
  Overlay, Mask 등). 기존 Tween 라이브러리가 있으면 반드시 재사용하고 새 Tween Framework를
  도입하지 않는다.
- Simulation/Core: Scout 결과·Cost·Edition·중복 판정·Pity·Shop 비용·Card State.
  Presentation: 표시·Animation·Tween·Button·Popup·Sorting·Filter·Visual State.
  `ScoutResult`를 먼저 만들고 `ScoutRevealPresenter.Play(result)`로 소비하는 흐름을 지킨다.
  Animation 중간에 `Random.Range`로 "결과처럼 보이는" 값을 만들지 않는다.

## 14. UI 상태 안정성 / 반응형 / 성능

- 고려할 상태: Loading/Empty/Locked/No Candidate/Not Enough Currency/Invalid Filter/Purchase
  Processing/Reveal Playing/Result/Duplicate/MAX Duplicate/Error. Scene 전환 중 중복 클릭 방지,
  뽑기 버튼 더블 클릭으로 재화가 두 번 빠지지 않게, Reveal 중 Back/ESC도 안전하게 처리.
- 최소 프로젝트 Target Resolution에서 정상 동작, 가능하면 1920x1080/2560x1440 검증. SafeArea/
  Anchor/Layout 사용, Pixel Position 하드코딩 최소화, 긴 이름에도 Layout이 안 깨지게.
- MiniPlayerCard 다수 표시 시 불필요한 Instantiate/Destroy 반복을 피하고 필요하면 Pooling.
  Portrait 로딩은 기존 Asset System을 따르고, LayoutRebuilder를 Update마다 호출하지 않는다.

## 15. 기존 UI 유지

Home/Match/Career/Player/Growth/Contract/League/Team 화면과 공통 Shell을 최대한 유지한다.
`Unity_UI_Production_Guidelines_UPlayBall.md`를 위반하지 않고, 화면마다 다른 Header/Tab/Button을
새로 만들지 않는다.

## 16. 테스트

다음에 대한 자동 테스트를 추가한다(가능하면 Presentation Integration Test 위주, Simulation
Resolver 자체가 이미 테스트되어 있으면 중복 테스트 금지):

- GameMode별 `ShopCategoryAvailability`
- 선수모드 Roster ReadOnly / 감독모드 Roster Editable
- MiniPlayerCard ViewModel Binding
- TeamSeason OriginYear 표시
- Full Card Front/Back 상태
- Pity Gauge 표시, No Candidate 확률 재정규화 표시
- Duplicate 상태, MAX +5 중복
- Cost/Edition Reveal Type 매핑
- Tactic Signature 일반 연구 제외
- MultiReveal 결과 수
- Animation Skip 후 결과 State 정상 도달
- 더블 구매 방지, NotEnoughCurrency

## 17. 작업 순서

```text
공통 Architecture 분석
  ↓
공통 UI Component / MiniPlayerCard / FullCard 연결
  ↓ (이후 병렬)
선수단 / 월드·구단·계약 / 상점 / ImageGen·Reveal 디자인
  ↓
통합 → Animation → QA/Test → 기존 UI 회귀 확인
```

공통 Component Contract가 확정되기 전에는 영역별로 비슷한 PlayerCard/TeamHeader/Popup을 중복
구현하지 않는다.

## 18. 완료 기준 (요약)

- 선수단 화면에서 실제 선수 데이터가 MiniPlayerCard로 표시되고, 감독모드는 편집 가능, 선수모드는
  동일 화면에서 편집 불가.
- MiniPlayerCard → 기존 Full Player Card Open, Front/Back Flip 정상 동작.
- TeamColor 진행 상태가 실제 편성 결과와 연동.
- 다른 LeagueGrade TeamSeason 조회, 진행 중 경기 상태 표시 가능.
- 계약 Offer에서 공통 TeamDetail로 이동 가능.
- 상점 모드별 잠금 상태 표시, 허용된 설정에서 실제 Scout/Skill/Tactic 결과와 연결.
- Pity Gauge가 실제 데이터와 연동, Skill/Tactic 구매가 실제 보관 시스템과 연결.
- 선수/Skill/Tactic이 서로 다른 Reveal 연출을 가지며 ImageGen 결과가 실제 적용됨.
- Skip 기능 정상 동작, 중복 카드 강화/보관/판매 UI가 실제 State와 연결.
- 어떤 UI도 Simulation 규칙을 독자 재구현하지 않고, 기존 게임 플레이가 깨지지 않음.

## 19. 작업 중 금지 사항

- 목업 이미지 한 장 생성 후 완료 처리, 전체 화면 PNG를 UI로 붙이는 방식.
- 기존 Player Card 디자인 임의 교체.
- Simulation 코드에 UI Reference 추가, Presentation에서 Scout 확률 재계산.
- GameMode `if` 분기를 화면 곳곳에 난립, UI 문자열 무분별한 하드코딩.
- 새 UI Framework 도입, 기존 공통 컴포넌트가 있는데 동일 기능 재구현.
- 같은 파일을 여러 작업 스레드가 동시에 수정, 테스트 실패를 무시하고 완료 처리.
- Placeholder Asset을 최종 결과로 남기기, ImageGen 결과를 그대로 거대한 배경 한 장으로 사용.

## 20. 보고 형식

작업 완료 후 다음 항목으로 보고한다: 구현 결과 요약 / 영역별 작업과 결과 / 신규·수정 Scene·
Prefab·Popup·Component / ImageGen 사용 내역(프롬프트 위치·적용 Asset·재생성 이력) / 선수단
구현 내용 / 월드·구단·계약 구현 내용 / 상점(Player/Skill/Tactic) 구현 상태 / 뽑기 연출별 구현
상태(Player Scout/Focused Scout/Skill/Tactic/Multi Reveal/Skip) / 주요 신규·수정 파일 목록 /
추가한 테스트와 실행 결과 / 기존 시스템 회귀 테스트 결과 / 남은 사항(기술적으로 미구현인 부분만).
