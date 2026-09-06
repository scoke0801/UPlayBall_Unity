# 인벤 대기실 레퍼런스 적용

## 기준과 구현

- 기준: [공민환, 첫화면 대기실! 하나하나 뜯어 보자](https://m.inven.co.kr/webzine/wznews.php?idx=86747). 모바일 원문 HTML과 본문 이미지에 직접 접근해 확인했다.
- 원문 전체 대기실: https://img.inven.co.kr/column/jukz_bm_news/20100316180742226.jpg
- 원문 비기너 사무실: https://img.inven.co.kr/column/jukz_bm_news/20100316181542668.jpg
- 원문은 상점·작전·정보·스킬·인사·연습경기를 상단 메뉴로 묶고, 비서 안내와 우측 하단 구단 정보창을 제공한다. 리그별 사무실 배경도 소개한다.
- 이번 변경은 원문의 화면 비율, 상단 중앙 아이콘 메뉴, 검정·은색 상태 바, 밝은 구단 정보창, 별도 경기 상태창을 구단주 홈과 공용 셸의 구단주 표현에 적용한다.
- 업무 영역과 모드 권한은 기존 Profile을 따른다. 홈/선수단/전력보강/덕아웃/구단/리그를 유지하고 메뉴 아트만 원문 문법에 맞춘다. 온라인 채팅·연습경기·현금 재화·가짜 미션은 추가하지 않는다.
- 선수 커리어의 공용 셸 기본 배치는 유지한다. 구단주 세부 화면도 같은 아이콘 메뉴를 유지하고 기존 Context Header/SubTab을 복원한다.

## 실제 연결

- 우측 아래: 구단명·리그·시즌·주차·성적·1군 구성·기본 전력·편성 비용. 값은 OwnerHomeSnapshot과 기존 Formatter를 사용한다.
- 경기 상태창: 실제 다음 대진, 상대 전력, 준비 여부와 진행 버튼. 로스터가 잘못돼도 수정할 수 있도록 경기 준비·상대 분석은 열어 둔다. 진행만 차단한다.
- 바로가기: 상대 분석 → 기존 Match Center, 경기 준비 → 기존 준비 화면, 일정·결과 → 실제 일정 Route, 구단 정보 → 기존 구단 화면, 저장 → 기존 Save Command.
- 정보가 없는 순위나 종료된 일정은 없는 상태를 표시한다. 저장 결과는 정보창 피드백 영역에 표시한다.
- 프런트 매니저 안내는 경기 상태창을 포함한 전체 Dock의 상단 경계에 배치한다.
- 배경은 EnvelopeParent로 비율을 유지하며 울트라와이드에서는 일부가 잘릴 수 있다.

## 생성 에셋

실행 방식: built-in Imagegen. 배경과 아이콘은 새로 생성했고 원문 스크린샷을 런타임 에셋으로 사용하지 않는다.

- 배경: `Assets/Resources/UI/Generated/bg_owner_reference_office_v3.png`
- 6종 아이콘: `Assets/Resources/UI/Generated/owner_navigation_atlas_v1.png`
- 아이콘은 3×2 Atlas의 UV를 RawImage로 참조한다. 첫 투명 배경 시도는 실제 Alpha가 없어 채택하지 않았다. 최종 자산은 어두운 불투명 배경이며 투명 PNG로 기술하지 않는다.
- 패널·표·버튼·레이블은 기존 Native uGUI Builder를 사용한다. 전체 UI를 이미지로 만들지 않는다.

배경 최종 프롬프트:

```text
Use case: stylized-concept. Asset type: production background texture for a Korean PC baseball management game main lobby. Use the referenced image ONLY as a spatial composition and period game-art reference. Create a new original very close composition of the modest beginner baseball club office inside a gray corrugated metal shipping container, extending to all edges. Camera faces diagonally into the room from front left, wide 16:9. Left wall: large circular wall ventilation fan high at left, square window below, worn blue armchair, water dispenser, several wooden bats leaning at far left. Back wall: hanging blue baseball jacket left of center, red electrical bell, plain dark green cloth banner without any lettering, white wall air conditioner above cork notice board on right. Center-right: large worn gray manager desk with paper stacks, old black monitor, dark rolling office chair; small standing floor fan just left of desk. Ribbed low ceiling and single hanging warm bare bulb. Gray brown scuffed floor visible lower left. Nostalgic detailed early 2000s pre-rendered 3D management-game environment, understated realistic materials, charcoal gray and faded navy, soft diffuse window light and warm bulb, readable midtones. Preserve reference perspective and object placement closely while adapting to a wide 16:9 canvas. Room alone, no humans, no UI, no panels, no text, no logos, no watermark. Output 1920x1080 landscape if supported.
```

아이콘 생성 프롬프트:

```text
Use case: stylized-concept. Asset type: transparent game navigation icon atlas. Create exactly SIX separate silver and blue pictogram icons in a precise 3-column by 2-row equal-cell grid, landscape 3:2 canvas. Transparent background and ample transparent padding inside each cell. Early 2010 Korean PC baseball management game interface style: compact beveled silver edges, glossy steel-blue shading, readable silhouettes, no enclosing buttons. Top row left: a small baseball club office building (home); top row center: three overlapping baseball player cards with anonymous silhouettes (roster); top row right: a baseball scouting clipboard with small magnifying glass (scouting). Bottom row left: a dugout bench with a baseball cap (dugout); bottom row center: a shield with a small baseball diamond motif (club); bottom row right: a silver trophy with blue globe behind it (league). Each icon centered exactly within its cell, same visual size, all isolated, consistent perspective. The atlas contains only these six icons, no words, no letters, no numbers, no watermark, no grid lines, no background. 1536x1024.
```

아이콘 최종 수정 프롬프트:

```text
Change ONLY the background behind these six icons to a perfectly flat, uniform opaque dark charcoal color, exact RGB 22,26,26 (#161A1A), throughout the entire canvas including between the icons. Absolutely no checkerboard, no glow, no gradients, no cast shadows on the background, no texture. Preserve the six icons and their exact positions and sizes and 1536x1024 canvas. This is a game icon atlas intended to sit on an identically colored dark navigation bar.
```

## 검증

Unity 6000.3.21f1의 스크립트 컴파일이 통과했다. 기존 커리어 저장 설정 신규 파일의 Object 타입 충돌 2곳은 UnityEngine.Object로 한정했다.

최종 Presentation 보조 컴파일: `dotnet build Baseball.Presentation.csproj --no-restore`에 로컬 `CustomAfterMicrosoftCommonTargets=.tmp/OwnerLobbyCompile.targets`를 지정해 생성 csproj에 누락된 신규 소스만 포함했다. 경고 0개·오류 0개. 프로젝트 파일 자체는 수정하지 않았다.

사용자 지시에 따라 렌더링·시뮬레이션 테스트를 작업 범위에서 제외하며 별도 렌더링 검증 도구는 남기지 않는다. 요청 이전에 시작한 렌더링 실행은 중단 요청 시 이미 종료되어 있었으며, 이를 UI 수락 검증의 근거로 사용하지 않는다. 변경한 UI EditMode 테스트는 정보 표시·기존 Route 요청·로스터 오류와 일정 종료 시 행동 제한을 명세하며 이 작업에서는 실행하지 않는다.
