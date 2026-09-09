# 연구 일반 카드의 선수풀 보충

`BaseballManager_PROJECT.md` 42.1의 사용자 승인에 따른 별도 Source다. 연구 카드의 일반 판본·연도·
소속·수치와 원본 행을 보존한다. KBO 통계 원본을 수정하거나 실제 출전 통계를 추정하지 않는다.

보충 정본은 `research_roster_supplement.json`, 정책은 `research_roster_policy.json`이다. 공식 인물
후보를 동일 이름·근접 경력(최대 5년)·같은 구단 이력으로 좁혀 하나만 남을 때 연결하고 그 판정 근거를
기록한다. 이는 인물 연결 규칙이며 공식 재직 기간을 확인했다는 뜻은 아니다. 동명이인·다중 구단 시즌과
투타 타입 차이는 별도 검토한다. 공식 ID가 없는 정성주는 연구 카드 3장과 [선수 입단·프런트 전환 기사](https://www.osen.co.kr/article/G0612130013)로
명시적으로 검토한 하나의 연구 인물 ID를 사용한다. 1994년 카드는 실제 출장 증거로 취급하지 않는다.

웹 타자 카드에서 확보하지 못한 Arm과 반대 타입 능력값은 정책의 35를 사용하고,
`observedAttributeIndices`로 실측값과 구분한다. 외야 세부 위치가 없는 카드의 LF는 공통 배치 기본값이다.
투수 구종과 성장 상한은 기존 결정론적 생성 규칙을 쓴다. 한정 보직은 기존 통계 기반 선수의 자리를
밀어내지 않고 남은 정원만 사용하며 보충 선수 사이 동점은 Stable PlayerSeasonId로 정렬한다.

보충 카드는 `sourceDataKind=ResearchCardSupplement`, `sourceRecordAvailability=Unavailable`로
저장한다. `originalSeasonRecords`에 가짜 0경기 행을 추가하지 않는다. 기존 KBO 파생 카드 17,333장과
생성 대체 선수 54명은 원본 값·Core25·기록을 유지한다. 보충 자료의 수치로 일반 산식을 재학습하지 않는다.

## 결과

- 120개 구단·연도에 939개 선수 시즌 추가. 전체 17,387 → 18,326.
- 1994 LG는 42 → 58명, Core25는 25명 유지. 연구 일반 카드 54명이 모두 연결된다.
- 보충 Cost는 1: 631명, 2: 262명, 3: 45명, 4: 1명.
- 추가 보류 693건: 신원 미연결 332, 근접 신원 재검토 48, 복수 인물 14, 소속 차이 189,
  타자·투수 타입 차이 93, 동일 인물·연도 중복 17. 보류 목록은 보충 정본의 `deferred`에 있다.
- 신규 계약 테스트 9건과 기존 최종 bake 테스트 5건 통과.
- 실제 C# 전체 해시 로더·1994년 20시즌 11,733경기(정규시즌 11,520)·시즌 결정론 검증 통과.
  AVG .26935, ERA 3.18980, 팀 경기당 3.29188득점, 경기당 HR 1.21224, BB/SO .45481.
  변경 전후 같은 시드의 첫 시즌 구단 성적은 동일하다. 이 검증은 기존 Core25의 회귀 검사이며
  추가 선수로 편성한 팀이나 스카우트 경제의 장기 밸런스를 증명하지 않는다.
- Unity Test Runner·Play Mode와 새 WorldHistory 캐시 생성은 미실행.

## 재현

프로젝트 루트에서:

```powershell
uv run --project Tools/KBOImporter Tools/KBOImporter/audit_roster_coverage.py --output docs/reports/historical-roster-coverage/after-audit.json
uv run --project Tools/KBOImporter Tools/KBOImporter/research_roster_supplement.py --audit docs/reports/historical-roster-coverage/audit.json
uv run --project Tools/KBOImporter Tools/KBOImporter/synthetic_bake.py --input-dir Tools/KBOImporter/.cache/KBOImport/Normalized --years 1982-2025 --research-supplement Tools/KBOImporter/research_roster_supplement.json --editor-assets-dir .tmp/research-roster/rebaked --verify-editor-assets
uv run --project Tools/KBOImporter python -m unittest discover -s Tools/KBOImporter -p test_research_roster_supplement.py
```

처음 보충한 공식 데이터의 게시 전 백업은 `.tmp/research-roster/Before`에 있다. 현재 보충 정본을
다시 만들 때에는 보충 전 audit.json을 사용한다. 보충 후 점검은 다른 출력 경로(`after-audit.json`)를
사용해 최초 비교 근거를 유지한다. KBO 원본이 변경되면 입력 해시 불일치로 재베이크를 차단하므로
새 원본과 연구 카드를 다시 대조해야 한다.

게임은 `Assets/10.Datas/HistoricalSimulation/1982-2025`를 읽으며 같은 파일을 Editor의 `Runtime/`에도
동기화했다. 기존 `.meta`·GUID는 보존했다. 개발용 실명 카탈로그에는 신규 연구 인물만 추가했다.
ContentHash 변경으로 기존 WorldHistory 캐시는 무효이며 Unity의 통합 World History Bake 도구에서
재생성한다. 과거 시뮬레이션 결과의 해시만 바꿔 재사용하지 않는다.
