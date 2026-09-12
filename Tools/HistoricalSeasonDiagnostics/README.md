# 실제 역사 시즌 진단

## 개인 타격 기록 후보

채택한 정책은 `Tools/KBOImporter/derivation_balance.json`의 `hitterRecordCalibration`이며
검증 입력 사본은 `hitting_record_candidate.json` v3이다. H/AB·ISO·BB/PA를 동년 가중 평균과
100기회의 사전 표본으로 수축한 뒤, 기존 추정과 `min(n/300, 1)`로 혼합한다. 300은 충분한
출전 표본에서 기존 추정의 이중 수축을 끝내기 위한 연구 설정이며 야구 규칙이 아니다.
관측 카드 수치는 보존한다. `audit_hitting_distribution.py <기준 Runtime> <후보 Runtime> <출력 JSON>`으로
코스트별 분포를 검사한다. 코스트·팀 성적·순위는 타격 산식에 들어가지 않는다.

`publish_hitting_candidate.py <후보 Editor> <채택 판정 JSON> <새 백업 경로>`는 통계 판정과
현재 기준선 해시, 원기록·신원·가격·포지션 보존을 확인한 뒤 로컬 정본을 게시한다.
채택 판정 외에도 실제 로더와 상세·간이 비교를 별도로 통과해야 한다.

최종 44개 연도×128시드에서 전후 각 3,345,408경기를 비교했다. 선두 승률 오차 3.65%p,
평균 상위 3위 43/47로 전체 통계 채택 조건을 통과했다. 개별 선두 조건은 30/47이다.
공식 Runtime 게시와 검증 근거는 `docs/reports/fixed-five-strength-20260911.md`를 따른다.
Editor 원본은 투수 부족 시 뒤 슬롯에도 타자를 담으므로 투수 신원·순서는 선수 유형으로 검사한다.
Runtime은 기존 14타자·11투수와 고정 5선발 계약을 검사한다.

## 역사 베이크 성능 검증

게시할 Unity 산출물은 `--validate-world-bakes <BakedWorldHistory 경로>`로 복원·재인코딩
바이트 일치와 개인 기록의 H ≤ AB ≤ PA를 검사한다. 이 검사는 현재 콘텐츠·설정과의 Key
일치 검사를 대체하지 않으므로 게시 전에 두 조건을 함께 확인한다.

전체 44시즌을 기존 경기 엔진으로 실행하고 산출물 SHA-256, 시간, 전체 할당량,
프로세스 최대 Working Set, 완료 파일 재사용 시간을 기록한다.
동시 실행 수가 달라도 동일 입력의 산출물 해시는 같아야 한다.

```powershell
dotnet run --project Tools/HistoricalSeasonDiagnostics -c Release -- `
  --bake-performance 'Assets/Editor Default Resources/HistoricalSimulation/1982-2025/Runtime' `
  .tmp/world-bake-performance/result.json 4
```

마지막 인수는 동시에 실행할 연도 수이며 생략하면 1이다. JSON 옆에 비교용 `.bytes`도 만든다.
이 측정은 .NET Release와 `BalanceTable.CreateDefault()` 기준이다. 실제 Unity Editor 실행 시간이나
Unity 밸런스 자산을 모두 반영한 측정으로 해석하지 않는다. 공식 베이크 자산은 수정하지 않는다.

Unity 통합 도구의 `World History Bake`는 기본 최대 4개 연도를 동시에 계산하고,
같은 Key의 정상 완료 파일을 자동 재사용한다. `World History Bake 설정`에서 동시 연도 수를
1~32로 조정할 수 있다. 전체 재계산은 `World History 강제 재베이크`를 실행한다.
CLI는 `-worldHistoryBakeWorkers 4`, `-forceWorldHistoryBake` 인수를 지원한다.
취소하면 완료한 Seed 파일은 남고, 다음 실행에서 해당 Seed를 건너뛴다. 중간 연도 재개는 지원하지 않는다.

## 시즌 통계 진단

전체 연도 반복은 명령 맨 앞에 `--workers 4`를 붙여 연도별로 병렬화할 수 있다(기본 1, 최대 16).
연도마다 별도 시즌 상태를 쓰고 보고서는 연도·시드 순서로 합친다. 44개 연도의 단일/4작업 실행에서
체크섬·팀 통계·5선발 기록 전체 일치를 검증했다. 반복 수를 늘린 결과는 기존 파일을 덮어쓰지 말고
`summarize_leaders.py ... --supplement <확장 실행>`으로 공통 시드 일치를 확인한다.

완료한 연도는 `<출력 JSON>.parts/연도.json`에 먼저 저장한다. 같은 명령을 다시 실행하면 입력 해시와
반복 수·첫 시드 재실행을 확인하고 재사용한다. 다른 입력으로 같은 중간 저장을 쓰면 실패한다.
최종 JSON은 UTF-8 스트림으로 원자적으로 저장하며, 분석기는 선수별 상세를 읽는 즉시 정규시즌 합계로
줄인다. 원시 보고서에는 상세 기록이 남는다. 이는 128시드 보고서의 전체 문자열 생성 한도를 피하기 위한 처리다.

Runtime Archive를 읽어 `BakedHistoricalDetailedSeasonSource → DetailedMatchEngine`으로 시즌을 반복한다.
정규시즌·올스타·포스트시즌, 원래 손잡이, 수비 적응도, 투수 피로와 팀별 5인 선발 순환을 사용한다.
경량 경기 공식이나 중립 손잡이·수비로 만든 대체 로스터를 사용하지 않는다.

현재 기본값은 팀당 정규시즌 **144경기**이며 인게임과 같은 `CareerSeasonBalance` 설정을 읽는다.
10구단 리그는 총 720경기다. 반복 수 32는 시즌을 32번 실행한다는 뜻이며 시즌당 경기 수가 아니다.
기존 보고서의 78~80경기 검증은 당시 실행 이력으로 남기고 다음 실행부터 144경기를 적용한다.

```powershell
dotnet run --project Tools/HistoricalSeasonDiagnostics -c Release -- `
  .tmp/team-strength/fixed_rotation_v18/Runtime .tmp/team-strength/reproduced.json `
  32 1982,1985,1988,1991,1994,1998,2000,2008,2010,2016
```

마지막에 `45 0.45`처럼 Rating Curve의 center와 slope를 지정해 후보를 비교할 수 있다.
추가 인수는 `[inputOffset [pitcherSlope pitcherInputOffset]]`이며 타자/투수 격차를 분리 검증한다.
곡선 인수는 생산용 기본값을 변경하지 않는다. `verify_strength.py`는 실제 승률 ±.05·최소 32시드와
동년 최강 대상의 평균 1~3위를 잠정 기준으로 검사하며 실패하면 0이 아닌 코드로 종료한다.
전체 대상 중 실행하지 않은 연도도 실패한다. 단계 검증은 `--years 1985,1992,2010`을 명시한다.

고정 5선발에서 발생하는 과거 구단의 전력 손실은 결과 보정으로 상쇄하지 않는다.
`rotations`에는 정규시즌 선발 순번별 등판 수와 **그 경기의 팀 승·패·무**를 기록한다.
개인 투수 승패나 패배 원인 기여도가 아니다. 다음 검사로 순번별 합계와 시즌 기록을 대조한다.

```powershell
python Tools/HistoricalSeasonDiagnostics/verify_rotation.py .tmp/rotation.json .tmp/rotation-report.json
```

승률 오차가 크다는 이유만으로 5선발 예외를 자동 인정하지 않는다. 포지션 결측·기용 오류와
실제 로스터 깊이의 한계를 구분하고, 예외가 있어도 원래 승률 오차와 순위는 보고서에 남긴다.

`prepare_hitting_candidate.py <Runtime> <Normalized> <정책 JSON> <새 후보 경로>`는
Contact·Power·Mental의 개인 기록 기반 후보를 만들며 공식 콘텐츠를 수정하지 않는다.
연구 설정은 같은 폴더의 `hitting_record_candidate.json`에 둔다.
동년 타격 기록을 표본 수로 수축하고, 주 자료와 추가 출처를 모두 읽어 관측 카드 값을 보존한다.
소표본이 평균급 타자로 올라가지 않도록 기존 개인 기록 추정과 표본 신뢰도로 혼합한다.
가격·포지션·투수 능력·Core25는 고정한 원인 분리 실험이며, 실제 베이크에 적용하려면 로스터
재선정과 파생 근거를 포함해 다시 검증해야 한다. 후보 승률만으로 게시 완료로 판정하지 않는다.

`prepare_hitting_bake.py`는 `--editor`, `--runtime`, `--normalized`, `--policy`, `--supplement`,
`--output`을 받아 실제 베이크·로스터 재선정을 수행한다. 이미 발급한 가격과 그 평가 근거를
보존하고, 타격 3속성 외 능력치·포지션·원기록 변경을 거부한다. 새 포지션 정본이 게시되면
새 기준선에서 다시 만들어야 하며 이전 포지션 후보를 현재 데이터에 덮어쓰지 않는다.

상세/간이 비교 도구의 `aggregate-historical <Runtime> <쌍별 경기 수> [MiniGame JSON]`은
동일 후보 밸런스를 두 계산 경로에 주입한다. 출력의 `balanceHash`로 실행 입력을 식별한다.

`--validate-content <Runtime>`는 같은 폴더의 `BakedSpecialCards.json`까지 Fast/Full 로더로
검사하고 전체 카드·영입 레시피 참조를 만든다. 게시 후보를 검증할 때 사용하며 경기는 실행하지 않는다.

`controlled-cost <시나리오별 경기 수> <연도 JSON> [비교할 낮은 Cost]`는 한 슬롯을 교체해
Cost 10과 비교한다. 낮은 Cost 기본값은 1이다. 해당 연도에 Cost 1의 표본이 없으면 2처럼
실제로 있는 등급을 명시하며 표본 누락을 자동으로 다른 등급으로 바꾸지 않는다.
공동 실제 선두는 최상위 묶음, 나머지 동년 대상은 실제 상대 순서를 확인한다.
이 기준의 허용 오차와 동년 복수 대상 해석은 사용자 확정 전이며 매 시즌 우승 보장이 아니다.
이전 실험의 게시 보류 이력은 `Tools/KBOImporter/PositionRepair.md`, 추가 개선 및 잔여 오차는
`docs/reports/historical-strength-continuation/README.md`를 따른다.
곡선을 생략하면 `Assets/10.Datas/Resources/NewGame/MatchRatingCurve.json`을 사용한다.
공통 상세 투구·타격 및 도루·불펜 기용 설정은 프로젝트 루트의
`Assets/10.Datas/Resources/NewGame/MiniGameBalance.json`을 로드하고 두 파일의 해시를 결과에 기록한다.
`balanceInputs`에는 실행에 사용한 순수 계수 스냅샷도 남겨 이후 자산 수정과 구분한다.
`engineVersion`은 계수 변경 없이 달라진 공통 엔진 로직을 식별한다.
앞에 `--balance <후보 JSON>`을 붙이면 공통 경기 계수만 후보 파일로 교체할 수 있다.
명시적 곡선 인수는 기존 선형 비교를 유지하며 `--upper-spread-start <값>`을 최앞단에 붙이면
그 후보의 상위 성장 구간도 검증할 수 있다. 생산용 다구간 곡선은 곡선 인수를 생략해 검증한다.
Unity의 다른 SO/JSON이나 전술 효과를 자동 로드하지 않는다.
실제 게임에서 밸런스 자산을 별도로 변경했다면 그 변경을 포함한 검증을 추가해야 한다.

시드는 `20260905 + 반복 인덱스 × 104729`이며 연도마다 첫 시드를 다시 실행한다.
모든 경기 결과·이벤트를 직렬화한 SHA-256이 다르면 실패한다. `games`는 추가 결정론 재실행을 제외한
정규시즌·올스타·포스트시즌 합계다. `statistics`에서 전반기·올스타·포스트시즌을 제외한 행만 합쳐야
정규시즌 누적 기록을 중복 없이 얻는다. 승률은 무승부를 제외한 `W/(W+L)`로 비교한다.

원본 Archive와 역사 캐시는 읽기 전용이다. 출력 경로에는 새 진단 JSON을 저장한다.
`UnityInputShim.cs`는 콘솔에서 TextAsset과 JSON 입력만 대체한다. 실제 Runtime Provider의 전체 해시 검증과
Definition 매핑을 사용하지만 **Unity Test Runner·Unity JsonUtility·Player Build 검증은 아니다.**
Headless 전용 프로젝트이며 Unity 어셈블리에 추가하지 않는다.

`audit_strength_inputs.py <Runtime> <Normalized> <연도,연도> --team <원본 팀명>`은
Core25의 선수별 원기록·능력치·합성 여부를 읽기 전용으로 비교한다.
`--reference-output <출력 JSON>`은 승률 검증용 실제 승패 목록을 생성한다.
`--leaders-only`를 함께 쓰면 연도별 최고 승률 팀(공동 선두 포함)만 검증 대상으로 남긴다.
이 파일은 외부 검증 전용이며 경기 입력에는 연결하지 않는다.

## 단일 능력치 반응 검증

연도별 선두 비교는 `summarize_leaders.py <시뮬레이션 JSON> <선두 참조 JSON> <출력.md>`로 만든다.
`--supplement <보충 JSON>`을 주면 같은 콘텐츠·밸런스·엔진·회전 규칙과 공통 시드 checksum이
같은 실행만 합친다. 실제 승률은 검증 전용이며 최소 32시드 게이트를 낮추지 않는다.
여러 연도 묶음은 `--supplement`를 반복해서 지정한다. 겹치는 연도의 시드를 줄이는 보충은 거부한다.
Markdown과 같은 이름의 JSON에 개별 판정·입력 SHA-256을 기록한다.
44개 연도 예비 진단과 4개 연도 보충의 최신 결과는 `docs/reports/historical-leaders-hbp-20260911.md`를 따른다.

`dotnet run --project Tools/SimulationDiagnostics -- ability-response 3000`은 동일 상대·시드·구종에서
한 선수의 Contact/Power/Speed/Mental/Control/Stuff/Breaking/Velocity를 각각 30·50·80으로 바꾼다.
선택적으로 뒤에 `Control`처럼 능력치 이름과 후보 공통 경기 JSON 경로를 지정할 수 있다.
투수는 ERA·BB/9·K/9·HR/9·IP/G를 함께 읽어야 한다. 더 긴 이닝을 맡은 투수의 경기당 실점만
보고 능력치 역전으로 결론 내리지 않는다. 타자는 AVG·OBP·SLG·HR·BB%·HBP%·삼진·SB/G·CS/G를 분리한다.
`verify-match-balance` 명령은 공통 경기 JSON과 Core 기본값의 일치를 검사한다.
`MiniGameBalance.json`의 `hitByPitchMinimumInsideLocation`, `hitByPitchMaximumHeight`,
`hitByPitchContactProbability`는 몸쪽 위험구 영역과 접촉 확률을, `aiMentalChaseWeight`는
상세 경기 AI의 Mental별 추격 확률을 저작한다. 능력치 반응·중립 전력·역사 로스터·간이 경로를 함께 검증한다.
`analyze_team_metrics.py`는 실제/시뮬레이션 팀 경기당 사구와 연도 내 상관도 비교한다.
원본 사구 0과 기록 미확보는 구분하며, 미확보는 평균·상관 표본에서 제외한다.
사구 조정의 전후 근거와 채택하지 않은 Mental 후보는 `docs/reports/plate-discipline-20260911.md`에 기록한다.

같은 시드 집합으로 실행한 전후 결과를 비교하려면 프로젝트 루트에서 다음을 실행한다.

```powershell
uv run --project Tools/KBOImporter python Tools/HistoricalSeasonDiagnostics/compare.py `
  --before .tmp/team-strength/before_seasons.json --after .tmp/team-strength/fixed_rotation_seasons.json `
  --normalized Tools/KBOImporter/.cache/KBOImport/Normalized `
  --output .tmp/team-strength/comparison.json
```

모든 원본 팀을 Source Franchise ID로 연결한다. 승률 MAE, 연도별 평균을 제거한 상관,
동일 시드의 승률 차이와 정규시즌 타격·투구·실책을 집계하고 입력 파일 해시를 함께 저장한다.
반복 수가 다른 실행은 `--maximum-repeats 8`처럼 명시해서 연도별 앞쪽 동일 시드만 비교한다.
부분 비교의 `comparedSeasons`와 원본 실행의 총 경기 수는 구분한다.
검증 대상 생성기의 `--leaders-only`는 당시 공표 승률과 `W/(W+L)` 선두의 합집합이다.
원본 `rank`에는 포스트시즌·양대리그 순위가 섞이므로 정규시즌 선두 판정에 쓰지 않는다.

모든 시즌의 각 경기 입력에서 실제 선발이 Core25의 1~5선발 순서인지 검증한다.
올스타를 제외하고 정규시즌에서 포스트시즌까지 팀별 순번을 이어서 검사한다.
각 결과의 `rotations.regularStarts`에 정규시즌 선발별 등판 횟수를 기록한다.

## 로스터 단일 요인 대조

`--roster-ablation <Runtime> <출력 JSON> <반복 수> <TeamSeasonKey,Key>`는 실제 시즌의 경기 전 입력을
잠그고 기준·대상 타순 AI·양팀 타순 AI·대상 수비 적응도100·대상 보직 불일치 제거를 각각 재생한다.
기준 재생은 원 경기 전체 직렬화와 일치해야 한다. 수비 실험은 주 포지션을 유지하고 부포지션
적응도만 추가한다. 투수·선수·배치·경기 전 피로·Seed를 바꾸는 실험과 혼합하지 않는다.

변경된 경기 결과의 투구 수를 다음 날에 전달하지 않으므로 이는 경기 단위 직접 효과 실험이지
수정 후 전체 시즌을 재현한 결과가 아니다. `BothOrder`도 대상 경기에서 양팀 타순을 바꿀 뿐
모든 구단의 별도 시즌을 완성하는 것은 아니다. 능력치·Cost·생산용 Archive는 수정하지 않는다.

```powershell
dotnet run --project Tools/HistoricalSeasonDiagnostics -c Release -- --roster-ablation Assets/10.Datas/HistoricalSimulation/1982-2025 .tmp/research-roster/precision-roster.json 32 FRANCHISE_a23e5d3c82759518a0e1_1985,FRANCHISE_d759ac5f30c5df8a5e19_1992,FRANCHISE_1b36b987034cef53c24a_2010
```

상세 검토와 적용하지 않은 후보는 `docs/reports/historical-strength-precision/README.md`를 따른다.

## 구단주 선수 수집 기간

실제 `ScoutRoller`·`ShopDefaultPools`·`ScoutEconomyBalance` 기본값으로 모든 구단 연도의 1군 25인을
정밀 Scout로 모으는 시즌 수를 잰다. SP는 모두 목표 구단 정밀 Scout에 쓰고 보장 영입은 즉시 쓴다고 가정한다.
`--facility`는 스카우트 시설을 가장 빠르게 올린 주간 생산을, `--legacy`는 이전 규칙(전체 명단·Pity 없음·
시작 SP 10,000·시설 SP만)을 쓴다. 수치 해석은 `BaseballManager_PROJECT.md` 43.9절을 따른다.

```powershell
dotnet run --project Tools/HistoricalSeasonDiagnostics -c Release -- `
  --scout-collection Assets/10.Datas/HistoricalSimulation/1982-2025 .tmp/scout-collection/current.json 30 0.5 [--facility] [--legacy]
```
