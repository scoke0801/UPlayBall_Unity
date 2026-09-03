# KBO Reference Data Extractor

KBO 공식 공개 기록실을 Runtime 게임 데이터와 분리해 수집하는 Offline Tool이다. Importer는
`Raw Snapshot → Normalized JSON`을 담당하고, 같은 도구 폴더의 `synthetic_bake.py`가 정규화
Archive를 Editor 원본 1:1 Archive와 익명 Runtime-safe 가상 콘텐츠로 분리한다. Runtime Game Flow에서는
두 도구를 호출하지 않는다.

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

## Editor 원본 기록 / Runtime-safe 가상 콘텐츠 분리 Bake

`--editor-assets-dir`로 생성하는 루트 Archive는 공식 `sourcePlayerId` 기준 실제 선수 한 명과 실제
시즌 기록 한 건을 1:1로 보존한다. 목록 이름은 해당 선수의 `originalName`이며 여러 선수 기록을 평균하거나
혼합하지 않는다. 원본에 없는 생년·투타·등록 유형·훈련 상한·잠재 성향은 임의 생성하지 않는다. 비용과
기본 능력치는 원본 시즌 기록의 연도별 분포에서 환산한 파생값으로 명시한다.

전체 Archive의 Editor 원본용 JSON은 Runtime `Resources`와 분리된 Unity 공식 Editor 전용 경로에
연도별로 분할해 생성한다. 이 경로의 파일은 Player Build에 포함하지 않으며, Runtime Provider 입력으로
직접 사용하지 않는다. 같은 명령은 기존 10개 가상 Franchise 합성본을 루트와 별개의 `Runtime/` 아래에
생성하며, Runtime Provider와 Exporter는 이 하위 경로만 읽는다.

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
Stable ID와 `ContentHash`가 들어간다. 루트 Editor 원본의 Original Record는 cache 값을 그대로 옮기며,
`Runtime/` 합성본만 연속 연도를 Person 단위로 연결하고 직전 시즌 능력치를 약한 prior로 반영한다.
같은 입력·버전·Seed는 같은 바이트를 만든다.
`synthetic_bake.py`는 Offline 전용이며 Runtime Game Flow에서 호출하지 않는다.

Bake 산출물의 공식 단일 위치는 `Assets/Editor Default Resources/HistoricalSimulation/1982-2025/`이다.
Unity Editor의 `EditorGUIUtility.Load`/`LoadRequired`로 저작·검증 도구가 읽는 경로이며 Runtime
`Resources.Load` 경로가 아니다. 일반 `Assets/**/Resources`나 `Temp/`에 배포용 복사본을 만들지
않는다. 이 JSON 생성은 Editor Bake 완료를 뜻할 뿐, Runtime Provider와 Player Build 배포 완료를
뜻하지 않는다.

Player Build용 단일 JSON이 필요할 때만 `--output`을 사용한다. 이 출력은 `originalName`과
`sourceReferenceNames`를 제거하고 `nameDataPolicy=runtime-fictional-only-v2`를 기록한다.
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

2026-09-03 전체 Archive 검증 결과:

```text
Editor 원본         44시즌 / PlayerPerson 3,510 / PlayerSeason 17,333
Editor 팀/수상      TeamSeason 363 / Original Record 17,333 / Award 555
Editor ContentHash  620f15ab60b874ce00a309fe39b2dd4635b09b1bd0f07fbecdfe97f794649447
Editor ArchiveHash  3976bd8812848d443810d71679bdf1820a763736754341ce9c20eaf621e38ab5
Editor Files        46 JSON / 23,541,304 bytes
Runtime 합성        44시즌 / PlayerPerson 1,757 / PlayerSeason 13,200 / TeamSeason 440
Runtime 팀          구단별 Pool 30 / Core25 25
Runtime 기록/수상   Original Record 13,200 / Award 1,672
Runtime ContentHash 784b519248c25f12b3b1e7a6b43ae2c9ec706aa2f5fe3824e8fa8b6e9d22bdee
Runtime ArchiveHash a28468580aa7c1e09488708948796c439cb95e590fb670a5ec46f96674f25667
Runtime Files       46 JSON / 12,908,941 bytes
```

같은 입력/버전/Seed로 두 번 생성한 내용이 일치했고, 분할 Archive의 파일별 SHA-256과 재조립
내용까지 검증하는 Offline Python 테스트 3/3과 Unity Import/Compile(종료 코드 0)이 통과했다.
장기 Historical Simulation은 실제 시즌 Source와 Hot Path 개선 뒤 별도로 수행한다.

```powershell
uv run python -m unittest -v test_synthetic_bake.py
```
