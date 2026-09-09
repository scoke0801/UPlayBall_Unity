# 1985 삼성 Reference 보정

조사: [Research 보고서](../../Research/PyaMaeCardDb/1985-Samsung/README.md).
정본: `annual_reference_1985_samsung.json`. 원본 카드 행·출처 URL·해시·미확정 판본도 포함한다.

프야매 일반 카드18명의 관측 능력치·Cost와 김일융의 기사 Cost10을 연결했다.
김시진·김일융에게 붙은 MVP 베이스볼 온라인 기사 Cost 근거는 각각 제외·교체했다.
공통 산식·팀별 보너스는 변경하지 않는다. 타자 번트 값을 Arm으로 쓰지 않고,
미확보 능력치는 기존 산식을 유지한다. 2014 보존 일반 카드와 2015 재평가 EX는 구분한다.

## 게시 결과

- Runtime ContentHash: `f00962c16a9bd79be96f369c6919204efd0c52a9393669da965fd2cd7a608275`.
- 전체18,326시즌·삼성36명 유지. Core25 Cost144→155, 기본 전력61.02→61.9733.
- 삼성 외 구단·43개 연도·기존 기록·신원 변화 없음. 직전939개 연구 보충도 유지.
- 전후 각24시즌(10,609 / 10,607경기), 첫 시드 재실행 결정론 검사 통과.
  삼성 합산 승률.50887→.52575(6팀 중4→2위), 리그 AVG .27795→.27780,
  ERA3.42993→3.44808, 팀 경기당 득점3.54456→3.56192,
  경기당 HR1.29900→1.32128, BB/SO .47672→.47681.
- 신규5건·연구 보충9건·원본 기반 최종 bake5건 테스트 통과.
  최신 C# 진단 프로젝트 빌드 경고0·오류0. Unity EditMode/PlayMode는 미실행.
- 실제.706 시즌의 완전 복원은 아니다. 김시진·장효조·외야·선발의 일반 카드 및
  재평가 후 자료, 김순철·송상진의 신원 연결은 남아 있다.
- 같은 타 게임 기사에 연결된 다른 연도10건의 근거는 범위 밖으로 보고하고 유지했다.

게시 전 파일 중 변경된 것만 `.tmp/research-roster/BeforeSamsung1985`에 백업했다.
기존 `.meta`와 캐시 카탈로그는 그대로다. WorldHistory 캐시는 아직 재생성하지 않았으며,
Unity 통합 도구의 **데이터 → World History Bake**로 구단주·커리어5개 캐시를 다시 만들어야 한다.
기존 캐시 해시만 바꾸면 안 된다. 실제 NewGameDefinition의 밸런스를 써야 하므로
기본 BalanceTable을 쓰는 진단 출력으로 Production 캐시를 대체하지 않는다.

## 재현

```powershell
uv run --project Tools/KBOImporter Tools/KBOImporter/compile_archived_reference.py --cards Research/PyaMaeCardDb/1985-1985/archive-cards.json --article-cards Research/PyaMaeCardDb/1985-Samsung/article-cards.json --editor-year 'Assets/Editor Default Resources/HistoricalSimulation/1982-2025/Years/1985.json' --team 삼성 --research-output Research/PyaMaeCardDb/1985-Samsung --output Tools/KBOImporter/annual_reference_1985_samsung.json
uv run --project Tools/KBOImporter Tools/KBOImporter/synthetic_bake.py --input-dir Tools/KBOImporter/.cache/KBOImport/Normalized --years 1982-2025 --seed 0 --research-supplement Tools/KBOImporter/research_roster_supplement.json --editor-assets-dir .tmp/research-roster/Samsung1985Stage --verify-editor-assets
uv run --project Tools/KBOImporter python -m unittest discover -s Tools/KBOImporter -p test_archived_reference.py
dotnet run --project Tools/HistoricalSeasonDiagnostics -c Release --no-build -- .tmp/research-roster/Samsung1985Stage/Runtime .tmp/research-roster/samsung1985-after.json 24 1985
```

출처 파일의 내용이 바뀌면 `derivation_balance.json`의 별도 Source 해시도 검토 후 갱신한다.
무조건 최신값 우선으로 덮어쓰지 않는다. 중복 시즌의 교체는 `supersedes`가 기존 카드 전체와
정확히 일치할 때만 가능하며, 제외 역시 기존 카드 스냅샷과 사유를 요구한다.
