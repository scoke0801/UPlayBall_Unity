# 구단 재정 대시보드 개선

## 변경 의도와 범위

넓은 회색 버튼과 한 덩어리의 작은 결산 문구로 구성된 테스트 화면을 구단 운영 대시보드로 재구성했다.
기준 문서 43절의 밝은 작업면·청회색 경계·공용 Shell을 유지한다. 재정 전용 partial에서만 배치와
표시를 구성하며 Core/Simulation, 경제 계수, 저장 구조, 실제 Command는 변경하지 않는다.

- 상단 왼쪽: 생성 구장 삽화, 현재 단계·좌석, 증축 비용·불가 사유와 기존 증축 버튼.
- 상단 오른쪽: 팬 기반·인기도의 100점 기준 막대, 예상·최근 관중, 현재 티켓 정책 선택.
- 하단: 이번 주와 이번 시즌의 수입·지출·순이익을 독립된 행과 큰 금액으로 비교한다.
- 결산 부가 정보: 실제 홈 경기·관중 집계와 스카우트·육성 포인트 생산을 유지한다.
- 하단 행동: 안내문, 저장·불러오기, 주간 진행. 기존 이벤트를 그대로 사용한다.

관중 이력이 없으면 ‘아직 집계 전’, 예상치가 없으면 ‘정보 부족’으로 표시한다. 순이익은
양수·음수·0을 각각 녹색·적색·중립색으로 표현하며 금액 부호를 함께 표시한다.
가상의 추이·티켓별 예상 수익·계정 잔고는 만들지 않는다. 삽화에는 ‘구장 이미지’를 표시하여
실제 선택 구장이나 관중 수를 나타내는 장면으로 오인하지 않도록 한다.

## 생성 자산

- 도구: built-in ImageGen, 신규 생성 1회.
- 저장 경로: `Assets/Resources/UI/Generated/owner_finance_ballpark_v1.png`
- 소비: `UI_Scene_OwnerClubOperations.Finance.cs`에서 Sprite로 로드.
- 원본: 2048×683 PNG. Sprite importer, mipmap 없음, clamp, 최대 2048.
- 금액·버튼·패널·막대·한글은 Native uGUI이며 이미지에 포함하지 않는다.

최종 프롬프트:

```text
Use case: stylized-concept. Asset type: background illustration for a Korean baseball club finance dashboard in a Unity management game. Primary request: polished panoramic architectural visualization of a modest professional baseball ballpark seen from the upper concourse, lush groomed outfield and clear baseball diamond, orderly blue-gray grandstands, glass-front hospitality suites and a small concourse in the foreground. Style: refined hand-painted realistic game environment, precise architecture, premium sports management game art. Composition: very wide landscape 3:1, stadium centered, strong readable silhouette, no people close up. Lighting: warm late afternoon sunlight, soft atmospheric depth. Palette: slate navy, muted teal grass, silver architecture, restrained warm gold sunlight. Constraints: environment art only, absolutely no text, no letters, no numbers, no logos, no watermarks, no UI, no charts or money symbols. Output one landscape image.
```

## 검증

- `Baseball.Presentation.csproj`와 `Baseball.Presentation.Tests.csproj` 보조 빌드: 경고 0, 오류 0.
- Unity 생성 csproj 갱신 전 신규 partial 포함은 `.tmp/FinanceCompile.targets`를 통해 검증했다.
- 기존 운영 화면 회귀 테스트에 생성 Sprite, 실제 순이익·시즌 관중, 팬 기반 막대 바인딩 검증을 추가했다.
- 기존 티켓 선택·주간 진행·저장·불러오기 이벤트 연결을 유지했다.
- EditMode 실행은 동일 프로젝트를 연 Unity 인스턴스 때문에 차단됐다. 테스트 통과로 간주하지 않는다.
- 실제 Unity 1280×720 / 1920×1080 / 2560×1440 캡처와 클릭 QA는 미실시다.
- 밸런스 변경 없음. 대량 경기 시뮬레이션 재측정 대상이 아니다.
