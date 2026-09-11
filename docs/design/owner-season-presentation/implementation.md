# 구단주 시즌 마감 연출 구현 보고

## 플레이 흐름

구단주 홈의 시즌 대표 행동을 다음 상태 기계로 연결했다.

```text
정규시즌 진행
  → 페넌트레이스 최종 보고
  → 조별 포스트시즌 진행
  → 포스트시즌 결과
  → 시즌 결산·다음 등급 예고
  → 계약/급여 정산과 다음 시즌
```

결산을 보기 전 다음 시즌 진입을 막되, 포스트시즌 미진출 구단도 다른 조를 포함한 월드 우승팀이
확정될 때까지 같은 진행 화면을 소비한다. 승격·강등은 기존 정규시즌 최종 순위 규칙을 유지하고
포스트시즌 우승은 별도 시즌 성과로 기록한다.

## 구현 범위

- `OwnerPostseasonState`: 조별 시드, 준결승·챔피언십, 경기 Seed와 점수, 시리즈 승수, 우승팀
- `OwnerPostseasonService`: 전체 조의 다음 경기 선택과 결정론적 진행
- `ManagerModeMatchService.PlayPostseasonGame`: 기존 25인 로스터와 `DetailedMatchEngine` 재사용
- `OwnerPostseasonSimulationSession`: 한 Frame 한 경기 진행, 중단·재개 가능한 진행량
- `OwnerSeasonReviewSnapshot`: 페넌트레이스·포스트시즌·승강 결과의 표시 전용 불변 값
- `UI_Popup_OwnerSeasonReview`: 기존 Owner Shell 디자인을 사용하는 3단 결산 Popup
- SaveVersion 18: 현재/완료 조의 포스트시즌과 `CompetitionScope.Postseason` 기록 왕복

## 2026-09-09 UI/진행 수정

- 밝은 빈 문서형 Popup을 네이비·골드 시즌 피날레 무대로 교체했다. ImageGen 배경은 장식만 담당하고
  순위·시리즈·월드 진행률·CTA는 Native uGUI로 유지한다.
- `OwnerSeasonReviewSnapshot`이 우리 조 완료와 전체 월드 완료를 분리한다. 우리 조가 먼저 끝나도
  결산 완료로 오인하지 않으며 `남은 리그 마감`으로 중단 지점부터 재개한다.
- 홈도 같은 상태에서 `타 리그 진행 중`을 표시한다. 전체 조가 완료된 뒤에만 시즌 결산과 다음 시즌이 열린다.
- 선택 탭을 `interactable=false`로 만들던 표현을 제거해 활성 탭이 회색 비활성 버튼처럼 보이지 않게 했다.
- 어두운 Header의 제목은 밝은 화면용 색 변환을 우회해 아이보리 대비를 고정했다.

## 검증

- `Baseball.Game`, `Baseball.Presentation`, Game/Simulation/Presentation EditMode 테스트 어셈블리:
  경고 0, 오류 0으로 컴파일했다.
- Unity 비의존 실행 하네스로 시드 정렬, 4강→결승 전환, 우승 결과, 진행 중 Save/Load 왕복을 실행했다.
- 동일 Runtime Fixture 두 개에서 정규시즌과 전체 포스트시즌을 실제 `DetailedMatchEngine`으로 완료해
  시드, 경기 수, 모든 경기 Seed·점수, 우승팀이 동일하고 포스트시즌 무승부가 없음을 확인했다.
- Unity Editor가 같은 프로젝트를 열고 있어 별도 Batchmode Test Runner는 실행하지 않았다.
  Editor의 일반 Test Runner 전체 실행과 16:9 Play Mode 조작 검증은 후속 QA Gate다.
- 확률·밸런스 수치는 변경하지 않았으므로 대량 통계 재보정 대상은 아니다.

## 기획 및 UI 시안

- 인터랙티브 HTML: `docs/design/owner-season-presentation/index.html`
- ImageGen 시안: `docs/design/owner-season-presentation/assets/season-ui-concepts-v2.png`
- 실제 구현 배경: `Assets/Resources/UI/Generated/bg_owner_season_review_v1.png`
- ImageGen 프롬프트 기록: `docs/design/owner-season-presentation/imagegen-prompts.md`

## 2026-09-11 결과 화면 겹침·조작성 보완

- 오른쪽 기록 카드와 다음 단계 안내가 같은 좌표를 쓰던 문제를 수정했다. 기록은 80px 카드와
  16px 간격으로 배치하고, 안내는 별도 `NextStep` 영역 안에서 제목 위·본문 아래 순서로 표시한다.
- 빈 라벨로 생성되어 `OwnerUiButtonSkin.Apply` 대상에서 빠지던 대표 버튼을 유효한 초기 라벨로
  생성한다. 공통 스킨 재적용 후에도 밝은 글자와 네이비 프레임을 유지한다.
- 진행률·소제목을 한국어로 정리하고, 엔진 설명을 정규시즌 순위·진출·승강 안내로 바꿨다.
  시즌 결산 잠금 조건을 하단에 표시하며, 미진출 상태는 `남은 리그 마감`으로 안내한다.
- 팝업 크기를 호스트 안에 맞추고 탭·닫기·대표 버튼 사이의 명시적 포커스 이동,
  Cancel 입력과 이전 선택 복원을 연결했다. 비활성 호스트와 EditMode에서도 생성 시 초기화한다.
- 기존 트로피 배경과 공통 버튼 자산을 재사용했으며 새 이미지는 생성하지 않았다.

검증은 `Tools/SeasonReviewValidation/Invoke-SeasonReviewValidation.ps1`로 재현한다.
격리 Unity 6000.3.21f1에서 최신 소스 컴파일 및 EditMode **7/7 통과**를 확인했다.
1280×720 / 1920×1080 / 2560×1440 / 3440×1440의 세 페이지, 총 12장을 실제 uGUI로 렌더하고
텍스트 부모 영역 이탈·세로 잘림, 기록과 안내 중첩, 버튼 스킨·대비를 자동 검사했다.
긴 구단명·세 시리즈·세 자리 승수 fixture를 사용했으며 네 해상도의 대표 화면도 육안 확인했다.
결과는 `output/season-review-validation/editmode-results.xml`, `season-{해상도}-page{0~2}.png`에 있다.

이는 격리 EditMode 렌더 검증이다. 메인 프로젝트 Play Mode의 실제 시즌 진행 및 물리 입력 장치를
통한 조작은 별도 수동 확인 항목이다. 경기 엔진과 밸런스 수치는 이 UI 작업에서 변경하지 않았다.
