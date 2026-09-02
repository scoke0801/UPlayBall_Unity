# KBO Reference Data Pipeline

## 목적과 경계

KBO 공식 기록은 실제 선수·구단 콘텐츠가 아니라 가상 리그를 만들기 위한 역사적 밸런스 Reference다.

```text
KBO Official WebForms
→ Raw Snapshot
→ Normalized JSON
→ (후속) Editor Raw Season SO
→ (후속) 익명 Derived Reference
→ (후속) Synthetic Player / Team Generator
```

현재 단계는 Normalized JSON까지 구현한다. 실제 선수명, KBO PlayerId, 실제 구단명은 Editor 전용
Raw 경계를 넘기지 않으며 Runtime Player Definition으로 복사하는 코드는 만들지 않았다.

## Source와 수집

선수 타격 Basic1/Basic2/Detail1, 투수 Basic1/Basic2/Detail1, 포지션별 수비, 주루, 팀 타격·투수·
수비·주루, 연도별 팀 순위, 선수 이동, 정규시즌 MVP, All-Star Game/Korean Series MVP, Golden
Glove를 KBO 공식 공개 페이지에서 조회한다. 통계는 ASP.NET WebForms PostBack, 선수 이동은 공개
페이지가 호출하는 `Player.asmx/GetTradeList` 형식을 그대로 사용한다.

전체 명령과 Cache 구조는 `Tools/KBOImporter/README.md`를 따른다. 매년 완료 시즌은 다음처럼
추가할 수 있다.

```powershell
uv run --project Tools/KBOImporter Tools/KBOImporter/fetch_kbo.py --from-year 2026 --to-year 2026
```

실행 시점에 2026이 진행 중이면 위 명령은 실패하며, 임시 데이터는 `--include-current`를 명시해야
한다. 진행 중 결과에는 `isSeasonComplete: false`가 저장된다.

Raw 하나는 전체 Player Archive가 아니다. 각 Snapshot은 `seasonYear/category/page/teamId`에 묶인
시즌 통계 응답이며, 최신 시즌 파일에는 과거 시즌 기록이 없다. 2025 시즌도 여러 Page와 Category를
병합해야 585명이 되고 1982~2025 전체는 3,510명·17,333 PlayerSeason이므로, 최신 Raw 하나로
축약하면 선수와 시즌 정보가 모두 부족하다. 따라서 시즌별 Raw/Sidecar를 Source of Truth로 유지한다.

## JSON 핵심 구조

시즌 파일은 `year`, `isSeasonComplete`, `sourceMetadata`, `dataAvailability`,
`dataAvailabilityStatus`, `teams[]`, `players[]`, `awards[]`, `tradeMovements[]`,
`validationSummary`를 가진다.

`players[]`는 Aggregate 타격/투수 기록, 포지션별 수비 배열, 주루 기록,
`teamFilterRecords[]`, `teamStints[]`, `tradeMovements[]`를 함께 보관한다. KBO Team Filter의 수치는
실제 분할 Stint라고 가정하지 않는다. 시즌 중 이동이 확인돼도 팀별 분할 Count를 공식 Source가
제공하지 않으면 Stint 수치는 `null/Unavailable`이다. 모든 누락 수치는 `null`이고 실제 무기록은
`0`이다. 투구·수비 이닝은 모두 `inningsOuts`다. `awardAvailabilityStatus`는 수상 유형별 가용성을
따로 기록하며, 공식 일괄 명단을 확보하지 못한 All-Star Selection 0건은 `Unavailable`로 보존한다.

## Update와 Validation

기존 Raw가 있으면 Cache를 사용하고, 명시적인 `--force`만 해당 범위를 재조회한다. Aggregate만
손상된 것으로 확인되면
Team Filter Cache를 보존하는 `--force-aggregate`로 Year/Category 범위를 제한한다. Header 변경,
PostBack 실패, Row 열 수
변경은 조용히 통과하지 않는다. 시즌 Selector만 요청 연도이고 통계 표가 다른 시즌에 남는 혼합
응답도 선수 Aggregate 팀과 해당 시즌 공식 순위표를 대조해 차단한다. 정상 Parse 뒤 PA/AB/H,
장타 합계, ER/R, SB/CS/SBA, 팀 G/W/L/D, AVG/OBP/SLG/ERA/WHIP 재계산을 확인한 후 JSON을
원자적으로 교체한다.

WebForms는 Team Selector가 화면상 전체로 보여도 서버 상태에 직전 Team Filter를 유지할 수 있다.
Player Aggregate 요청은 빈 Team Code를 강제 PostBack해 이를 초기화하고, 대표 통계 표의 순위 Row가
최소 두 구단을 포함하는지 검증한다. 1999~2000처럼 공식 순위 페이지가 Dream/Magic 복수 표를
제공하면 동일 Header 표를 모두 병합하고 `standingsGroup`을 보존한다.

`--validate-only`는 네트워크 요청 없이 저장된 JSON의 필수 Schema를 검사한다. 실행 요약과 누락
Category는 `Tools/KBOImporter/.cache/KBOImport/Reports/KBO_IMPORT_REPORT.md`에 기록된다. Unity가
관리하는 프로젝트 `Temp/`는 실행 중 삭제될 수 있어 Cache 위치로 사용하지 않는다.

## Known Missing Data와 Override

과거 수비·주루·팀 세부 기록은 KBO 공식 Page Selector가 2001년부터만 제공하는 경우가 있으며,
이때 `Unavailable`과 사유를 기록하고 0으로 채우지 않는다. All-Star
전체 Selection은 시대별 공식 형식이 일정하지 않아 성적으로 추론하지 않는다. 공식 보도자료나
명단으로 확인한 경우에만 `Overrides/allstar_overrides.csv`에 `Reason`, `SourceNote`와 함께 추가한다.

동명이인, 역사적 팀 Code/Franchise, 수상 Join 보정도 각각의 Override CSV에 공식 근거를 남겨야
한다. 자동 후보가 0명 또는 2명 이상이면 임의 연결하지 않는다.

## 후속 SO 단계

후속 구현은 시즌당 하나의 Editor Raw SO를 만들고, 별도 Builder가 실명과 KBO Source ID를 제거한
분포·익명 Feature만 Runtime Reference로 내보내야 한다. Runtime Synthetic Generator가 Raw JSON이나
KBO Season SO를 직접 참조해서는 안 된다.
