# KBO Reference Data Extractor

KBO 공식 공개 기록실을 Runtime 게임 데이터와 분리해 수집하는 Offline Tool이다. Importer는
`Raw Snapshot → Normalized JSON`을 담당하고, 같은 도구 폴더의 `synthetic_bake.py`가 정규화
Archive를 Editor Source Audit과 익명 Runtime-safe Source-backed 콘텐츠로 Bake한다. Runtime Game
Flow에서는 두 도구를 호출하지 않는다.

## 실행

프로젝트 루트에서 실행한다. `uv`가 Python과 고정 의존성을 준비한다.

```powershell
uv run --project Tools/KBOImporter Tools/KBOImporter/fetch_kbo.py
```

기본 범위는 완료된 1982~2025 정규시즌이다.

```powershell
uv run --project Tools/KBOImporter Tools/KBOImporter/fetch_kbo.py --from-year 2025 --to-year 2025
uv run --project Tools/KBOImporter Tools/KBOImporter/fetch_kbo.py --from-year 2026 --to-year 2026 --include-current
uv run --project Tools/KBOImporter Tools/KBOImporter/fetch_kbo.py --from-year 2011 --to-year 2011 --category hitter
uv run --project Tools/KBOImporter Tools/KBOImporter/fetch_kbo.py --validate-only
```

`--category`는 `hitter`, `pitcher`, `defense`, `runner`, `team`, `awards`를 지원하며 여러 번 지정할 수
있다. 일부 Category만 실행한 결과는 완전한 시즌 파일을 덮어쓰지 않고
`Normalized/Partial/{year}_{categories}.json`에 저장한다.

## 요청 정책

- 동시성 1
- 요청 간 1~2초 지연
- 429/5xx/네트워크 오류에 2초, 5초, 10초 Backoff로 최대 3회 Retry
- Raw Cache가 있으면 요청 생략
- 공식 Page Selector가 요청 연도를 제공하지 않으면 `Unavailable` Marker를 Cache해 재조회 생략
- 미완성 WebForms Snapshot을 재개할 때는 새 Session을 위한 Endpoint별 Bootstrap GET 1회 수행
- 명시적인 `--force` 또는 `--force-aggregate`일 때만 해당 범위 재다운로드
- 로그인, CAPTCHA, 접근 제어, 비공개 API를 사용하지 않음

`--force`는 손상된 Year/Category가 확인된 경우에만 좁혀 사용한다. Player Aggregate만 Team Filter
응답으로 오염된 경우에는 Team Filter Cache를 유지하는 `--force-aggregate`를 함께 사용한다.

```powershell
uv run --project Tools/KBOImporter Tools/KBOImporter/fetch_kbo.py --from-year 2024 --to-year 2024 --category pitcher --force-aggregate
```

KBO 기록실은 단순 QueryString이나 별도 Ajax 통계 API가 아니라 ASP.NET WebForms PostBack을
사용한다. Tool은 공개 페이지에서 받은 `__VIEWSTATE`, `__EVENTVALIDATION`, 실제 Select name과
Pager event target을 그대로 제출한다. 기대 Header가 달라지면 `SchemaMismatchError`로 해당 시즌을
중단하고 기존 JSON을 유지한다. 시즌 Selector 변경 응답은 표가 아직 이전 시즌에 머물 수 있으므로
정규시즌 Selector PostBack까지 완료한 뒤 사용하며, 선수 Aggregate 팀이 해당 시즌 순위표와 다르면
시즌 의미가 섞인 응답으로 보고 정규화를 중단한다. Player Aggregate는 화면상 Team Selector가 이미
전체로 보여도 빈 Team Code를 강제 PostBack하여 서버에 남은 Team Filter 상태를 초기화한다. 이어서
순위가 있는 Aggregate의 대표 통계 표에 최소 두 구단이 있는지 검사해 단일 구단 Filter 응답이
Aggregate Cache로 저장되는 것을 차단한다.

## 출력

```text
Tools/KBOImporter/.cache/KBOImport/
  Raw/
    _Bootstrap/
    Awards/
      through_2025/...
    1982/
      hitter_basic1.html
      hitter_basic1_page_002.html
      Teams/{KBO Team Code}/...
      Unavailable/...
  Normalized/
    1982.json
    Partial/2011_hitter.json
  Reports/
    KBO_IMPORT_REPORT.md
    KBO_IMPORT_REPORT.json
```

Raw HTML과 정규화 JSON은 재생성 가능한 Local Cache이므로 Git에 Commit하지 않는다.
Unity는 프로젝트 `Temp/`를 실행·재로드 과정에서 정리하므로 장기 Cache/Resume 경로로 사용하지 않는다.
필요하면 `--data-root`로 별도 로컬 경로를 지정할 수 있다.

### 최신 Raw 하나를 전체 Archive로 사용할 수 없는 이유

KBO 통계 Raw는 선수 Master나 통산 원장이 아니라 `seasonYear + category + page + teamId` 요청 조건의
Snapshot이다. 한 HTML은 한 시즌의 한 통계표 Page만 담고, 2025 시즌 전체 선수도 여러 타격·투수·
수비·주루 Page를 PlayerId로 병합해야 585명이 된다. 반면 1982~2025 Archive에는 공식 PlayerId 기준
3,510명과 17,333 PlayerSeason이 있다. 같은 선수도 시즌별 값이 다르므로 최신 시즌 Snapshot에서
과거 Season 기록을 복원할 수 없다. 따라서 하나의 Raw로 축약하지 않고 시즌·Category별 Raw와
Sidecar를 유지하는 것이 Source Provenance와 결정론적 재처리에 필요하다.

## 정규화 계약

- 선수는 링크의 공식 `playerId`로 병합한다.
- 공식 ID가 없는 Row는 이름을 ID로 승격하지 않고 `UnresolvedComposite`로 격리한다.
- Aggregate Season Total, `teamFilterRecords[]`, `teamStints[]`, `tradeMovements[]`를 분리한다.
- KBO Team Filter는 트레이드 전후 분할 기록이 아니라 최종 소속 아래 시즌 합계를 반환할 수 있다.
  원문 결과는 `teamFilterRecords[]`에 보존하고, 시즌 중 이동 선수의 `teamStints[]`에는 확인된 소속만
  두며 분할 수치는 `null`, `dataScope=Unavailable`로 남긴다.
- 시즌 종료 뒤 이동은 `tradeMovements[]`에만 보존하며 직전 시즌 TeamStint로 만들지 않는다.
- `IP`는 `float`가 아니라 `inningsOuts` 정수로 변환한다.
- `-`와 빈 값은 `null`, 실제 0은 숫자 `0`으로 보존한다.
- 타자와 투수 기록은 상호 배타적이지 않다.
- 수비는 포지션별 `defenseRecords[]`로 보존한다.
- 1999~2000 순위처럼 공식 페이지가 동일 Header의 복수 League 표를 제공하면 모두 결합하고
  `standingsGroup`으로 원래 Group을 보존한다.
- 수상 페이지에는 PlayerId가 없으므로 `Year + PlayerName + Team` 후보가 정확히 하나일 때만
  PlayerSeason에 연결한다.
- `awardAvailabilityStatus`는 수상 유형별 `Available`, `AvailableEmpty`, `Unavailable`, `Partial`,
  `NotSelected`를 기록한다. 공식 Historical Bulk Selection을 확보하지 못한 All-Star Selection은
  0건으로 해석하지 않고 `Unavailable`, 공식 근거 Override 일부만 있으면 `Partial`로 기록한다.
- 팀 Code는 시즌 Selector 값을 우선 사용한다. 팀 통계만 단독 추출해 공식 Code를 얻지 못하면
  `unresolved-*` ID로 격리하고 `sourceTeamIdOrigin`에 표시한다.
- `dataAvailabilityStatus`는 `NotSelected`, `Unavailable`, `AvailableEmpty`, `Available`을 구분한다.

## 현재 수상 범위

- 정규시즌 MVP
- All-Star Game MVP
- Korean Series MVP
- Golden Glove 및 수상 포지션
- All-Star Selection은 공식 역사 명단의 형식이 일정하지 않아 자동 추론하지 않으며
  `Overrides/allstar_overrides.csv`의 공식 근거 Row만 포함한다.

신인상과 KBO 수비상은 이번 Normalized Award 대상이 아니다.

## 안전한 재개와 결정성

시즌 JSON은 모든 선택 Category의 Parse와 Validation이 성공한 후 임시 파일을 교체한다. 중간 실패는
기존 결과를 변경하지 않는다. 동일 Raw Snapshot, Override, Importer Version에서는 정렬된 Key/Row와
고정 Schema로 동일 JSON을 만든다. `importedAtUtc`는 Snapshot 최초 저장 메타데이터를 재사용하며,
`--force` 결과가 동일한 바이트면 기존 시각을 유지한다.

공식 선수 이동 페이지의 공개 `GetTradeList` 요청도 같은 단일 Session/지연 정책으로 수집한다.
선수가 아닌 신인 지명권은 `assetType=DraftPick`으로 Raw 이동 내역에만 보존한다. 3월·10월처럼
정규시즌 경계가 연도마다 달라지는 이동은 자동 TeamStint 증거로 사용하지 않는다.

## Editor Source Audit / Runtime-safe Source-backed 콘텐츠 분리 Bake

`--editor-assets-dir`로 생성하는 루트 Archive는 공식 `sourcePlayerId` 기준 실제 선수 한 명과 실제
시즌 기록 한 건을 1:1로 보존한다. 목록 이름은 해당 선수의 `originalName`이며 여러 선수 기록을 평균하거나
혼합하지 않는다. 원본에 없는 생년·투타·등록 유형·잠재 성향은 실제 정보처럼 임의 생성하지 않는다.
Cost와 BaseAttributes는 Source-backed 시즌 기록의 연도·Position/Role 공통 분포와 Reliability에서
파생한다. `Qualified/Limited`는 진단 metadata일 뿐 별도 Z-score baseline이 아니다.

전체 Archive의 Editor 원본용 JSON은 Runtime `Resources`와 분리된 Unity 공식 Editor 전용 경로에
연도별로 분할해 생성한다. 이 경로의 파일은 Player Build에 포함하지 않으며, Runtime Provider 입력으로
직접 사용하지 않는다. 같은 명령은 Source-backed 시즌을 10개 가상 Franchise에 배치한 익명 정제본을
루트와 별개의 `Runtime/` 아래에 생성하며, Runtime Provider와 Exporter는 이 하위 경로만 읽는다.

Runtime 정규 구단은 연도마다 10팀이고 팀당 Core25는 정확히 25명이다. 해당 OriginYear의 모든
Source-backed 시즌을 10팀의 가변 전체 Pool에 중복 없이 배치한다. 10개 Core25의 Hitter140/Pitcher110
quota보다 Source-backed 인원이 적을 때 그 차이만 `ReplacementGenerated`로 보충한다. Replacement는
`SourcePlayerId`가 없고 해당 OriginYear+Position/Role Source aggregate와 covariance에서 생성하며 특정
Source vector를 복제하지 않는다.

```powershell
uv run python synthetic_bake.py `
  --input-dir .cache/KBOImport/Normalized `
  --years 1982-2025 `
  --seed 20260901 `
  --editor-assets-dir "../../Assets/Editor Default Resources/HistoricalSimulation/1982-2025" `
  --verify-editor-assets
```

분할 결과는 `manifest.json`, `player_persons.json`, `Years/{year}.json`으로 구성한다. Manifest에는
원본 `ContentHash`, 분할 Archive Hash, 파일별 SHA-256/크기와 핵심 건수가 들어간다.
`--verify-editor-assets`는 저장 직후 모든 파일을 다시 읽어 루트의 실제 선수·시즌·기록 1:1 연결,
파생값 경계, Stable ID, 팀/수상 참조와 `Runtime/`의 10개 Franchise·Core25·외국인·중복·수상
quota를 각각 재검증한다.

두 산출물에는 공통 `PlayerPerson`/`PlayerSeason`, Normal Card, TeamSeason/Core25, Original Record,
Stable ID와 `ContentHash`가 들어간다. Source Player 1명은 가상 PlayerPerson 1명, Source PlayerSeason
1건은 가상 PlayerSeason 1건에 대응한다. 같은 SourcePlayerId의 여러 시즌은 같은 가상 Person ID와
가명을 유지한다. 직전 시즌 능력치나 다른 SourceSeason의 metric을 현재 시즌에 섞지 않는다.

Cost percentile threshold는 해당 OriginYear 전체 Source-backed 모집단에서 먼저 확정한다. Core25에
선택되지 않은 SourceSeason도 모집단에 포함하고 Replacement는 제외한다. Replacement Cost는 확정된
composite threshold에 대입한다. 같은 입력·버전·Seed는 같은 바이트를 만든다.
`synthetic_bake.py`는 Offline 전용이며 Runtime Game Flow에서 호출하지 않는다.

Bake 산출물의 공식 단일 위치는 `Assets/Editor Default Resources/HistoricalSimulation/1982-2025/`이다.
Unity Editor의 `EditorGUIUtility.Load`/`LoadRequired`로 저작·검증 도구가 읽는 경로이며 Runtime
`Resources.Load` 경로가 아니다. 일반 `Assets/**/Resources`나 `Temp/`에 배포용 복사본을 만들지
않는다. 이 JSON 생성은 Editor Bake 완료를 뜻할 뿐, Runtime Provider와 Player Build 배포 완료를
뜻하지 않는다.

Player Build용 단일 JSON이 필요할 때만 `--output`을 사용한다. 이 출력은 `originalName`, 실제
Source ID와 Editor provenance/Trace를 제거하고 Runtime 이름 정책 버전을 기록한다.
가명은 임의 음절 조합이 아니라 Normalized cache에서 반복 확인된 한국식 두 글자 이름 부분과 일반
성씨를 재조합한다. 전체 원본 실명과의 일치, 가명 중복, 이름 안의 음절 반복은 Bake 오류다.

```text
1982-2025/
  manifest.json          // 파일별 SHA-256, Count, Source Manifest, Asset Archive Hash
  player_persons.json
  Years/
    1982.json
    ...
    2025.json
  Runtime/               // 실명 제거 후 Exporter가 읽는 정제 분할본
    manifest.json
    player_persons.json
    Years/...
```

기존 3~7 Reference 혼합 Bake의 Hash/Count/파일 크기/테스트 수는 새 정본 계약의 최신 검증값으로
사용하지 않는다. `synthetic_bake.py`가 하나의 Manifest/ValidationReport에 Source-backed/Replacement
수와 사유, ContentHash, ArchiveHash, 파일 크기를 생성하고, `sync_historical_validation_docs.py`가
검증 실행 상태와 함께 README/01/08에 한 번에 동기화한다. 새 값은 실행 전에 추정해 기록하지 않는다.

```powershell
uv run python -m unittest -v test_synthetic_bake.py
```

<!-- HISTORICAL_BAKE_SNAPSHOT:START -->
## 2026-09-04 Source-backed Bake 검증 스냅샷

이 블록은 `Runtime/validation_report.json`에서 생성한다. 수동으로 숫자를 고치지 않는다.

| Archive | ContentHash | ArchiveHash | Person | Season | SourceSeason | ReplacementSeason | Payload bytes | Manifest bytes |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Source Audit | `9e98aa5626cb56a0855c868fdba8a983b16df149183e08dc6aee8c37209df0fe` | `606a9ed852fe81df459b1430fd5335c467efe9c3f2e14ff0da2b2ca9d28a9d02` | 3,510 | 17,333 | 17,333 | 0 | 189,199,356 | 19,245 |
| Runtime | `f52ff738c10520285e9ecaf9486d602a6cd382d04e20f1077c339296a0815c2c` | `d995ba952985a0a2e2c1622cc877db7e1293440249b853910a7e35ef8d224d12` | 3,865 | 17,688 | 17,333 | 355 | 24,053,170 | 19,291 |

| 연도 | Source H | Source P | Replacement H | Replacement P | Replacement 비율 | 평균 Cost | 평균 관련 능력치 |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1982 | 102 | 39 | 38 | 71 | 43.6% | 2.165 | 47.080 |
| 1983 | 118 | 54 | 22 | 56 | 31.2% | 2.000 | 47.310 |
| 1984 | 135 | 59 | 5 | 51 | 22.4% | 2.196 | 47.333 |
| 1985 | 137 | 66 | 3 | 44 | 18.8% | 2.085 | 47.645 |
| 1986 | 171 | 87 | 0 | 23 | 9.2% | 1.696 | 47.935 |
| 1987 | 162 | 87 | 0 | 23 | 9.2% | 1.957 | 47.254 |
| 1988 | 172 | 91 | 0 | 19 | 7.6% | 1.842 | 47.895 |

- Python 회귀: 52/52 통과, 실패 0, Skip 0
- C# 컴파일: PASS — Core/Simulation/Game/Editor Tests 어셈블리, 경고 0/오류 0
- Unity EditMode: PASS — Provider 12/12, Archive Browser 21/21, Runtime Builder 9/9
- Historical World: PASS — 44시즌/18,047경기, same-seed 0ed6549e474f2e62, different-seed e9e8e4b87054f31d, Replacement Awards AS 55/1100·GG 9/440·MVP 3/132

<!-- HISTORICAL_BAKE_SNAPSHOT:END -->
