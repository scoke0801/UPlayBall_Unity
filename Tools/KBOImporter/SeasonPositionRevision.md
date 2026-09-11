# 전체 역사 시즌 포지션 보완

1982~2025 전체 Runtime의 포지션 결측 3,199건을 조사했다. 246개 구단·시즌의 공개 선수단
목록에서 시즌·소속·이름이 유일하게 연결되는 3,121건을 공식 SourcePlayerId에 연결했다.
결측은 78건으로 줄고, 포지션 값 2,365건·로스터 역할 1,434건·기본 Core25 120개가 바뀐다.
새 포지션 증거가 있는 선수도 실제 경기·수비 이닝을 추정해서 채우지 않는다.

## 근거와 남은 결측

정본은 `season_position_research_all.json`이다. 각 선수에 연도·원본 ID·구단·포지션·출처 ID를,
각 출처에 URL·조회일·원본 HTML SHA-256을 저장한다. 원본 HTML은 `.cache/SeasonPositions`에만 둔다.
위키백과의 해당 시즌 선수단 목록을 B급 보조 근거로 사용하며 다음 우선순위를 유지한다.

1. 해당 시즌의 실제 수비 기록.
2. 기존 `season_position_evidence.json`의 검토된 포지션 근거.
3. 새로 조사한 시즌 선수단 목록. 기존 자료와 충돌한 5건은 덮어쓰지 않고 `conflicts`에 기록.

시즌 통합 문서로 리다이렉트되는 MBC 등의 페이지는 **요청한 연도 절만** 읽는다.
수상·드래프트·퓨처스·타 시즌의 목록은 포지션 증거로 사용하지 않는다.
명칭 목록과 포지션별 표를 지원하며, 복수 포지션·동명이인·명칭 미연결은 자동 확정하지 않는다.
재실행 시 이미 조사한 대상을 유지하므로 재베이크 후 결측이 줄었다고 보충 정본을 지우지 않는다.

미해결 78건은 `deferred`에 전체 목록을 남겼다. 이 중 일부는 원본에서 타자로 생성됐지만
해당 시즌 선수단에는 투수로만 나타나는 사례다. 투구 기록 없이 타자 카드를 투수 카드로 바꾸거나
능력치를 만들어 넣지 않았다. 이 유형 분류 문제는 포지션 자료 보완과 별도의 원본 검토 대상이다.

94LG는 타자 32명 중 결측 14명 → 0명이다. 김정민·당신상 C, 최동수 1B, 조양근 SS,
윤찬 3B, 김태민 LF, 서효인 CF가 보완됐다. 김영직·허문회 등은 해당 시즌 목록의 DH를 유지한다.
김재현 LF·노찬엽 CF·박준태 RF는 기존 기사 기반 자료를 우선한다.
[94LG 시즌 선수단](https://ko.wikipedia.org/wiki/1994년_LG_트윈스_시즌)을 일반적인 커리어 주 포지션과
동일시하지 않는다. 주전 9명의 포지션은 유지되고 김정민이 백업 포수로 Core25에 남는다.

## 포지션 재저작과 가치 평가 분리

발급된 일반 카드 18,326장의 신원·구단·연도·능력치·성장 상한·Cost·원기록 수치·수상자는 유지한다.
Full Bake는 포지션별 가격 가중치도 재평가하여 36장 Cost와 특수 카드 Peak 선정을 바꿨다.
따라서 포지션 수정은 `prepare_position_revision.py`로 기존 발급 가치 평가를 보존한 Archive를 만든다.
능력치나 신원이 달라진 후보는 이 도구가 거부한다. 보존한 Cost Trace는 이전 발급 평가이며,
새 위치로 다시 평가했다고 표시하지 않는다. 전체 카드 가치 재평가는 별도 작업이다.

이 경로의 최종 Runtime 변경 필드는 `position`, `isPositionEvidenceMissing`, `rosterRole`뿐이다.
Person 대표 위치와 원기록의 포지션 메타데이터는 함께 갱신되지만 원기록 숫자는 바뀌지 않는다.
Core25 선정은 기존 능력치·표본·포지션 적격성으로 재실행하며 Cost를 주전 점수로 사용하지 않는다.
기존 결측 선수의 전 수비 적응도 100 정책은 바꾸지 않는다. 확인된 선수에게는 정상 위치 제약이 적용된다.

특수 카드 594장(EX 88·레전드 20·커리어하이 123·레어 363)의 카드 정의는 동일하다.
현재 위치 근거로 143개 레시피를 재검증·발급했고 이 중 16개의 재료 후보 구성이 갱신됐다.
발급 조건을 완화하거나 이전 시뮬레이션 결과의 Hash만 교체하지 않는다.

## 표시 경로

`IsPositionEvidenceMissing`를 보유 카드·새 게임·도감·선수단·컨디션·영입 표시까지 전달한다.
미해결 선수는 **포지션 미확인**으로 표시하며 DH 검색·필터와 구분한다.
카드 뒷면은 미확인 선수를 DH 수비 지점에 그리지 않는다.
실제 라인업의 배치 위치는 주 포지션과 별개의 정보이므로 지정된 슬롯을 그대로 표시한다.
세이브 형식이나 Core/Simulation 어셈블리 경계 변경은 없다.

## 검증

동일 엔진 7·동일 경기 밸런스·동일 Seed, 44개 연도 각각 2시즌의 실제 상세 경기 경로를 비교했다.
전후 각각 정규시즌 **52,272경기**, 연도별 결정론 재실행 44건이다. 추가 결정론 재실행 경기는
통계 표본에 중복 합산하지 않았다. 아래 수치는 최종 포지션 전용 후보 기준이다.

| 지표 | 변경 전 | 변경 후 |
|---|---:|---:|
| 타율 | .27350 | .27327 |
| ERA | 4.02826 | 4.01636 |
| 팀 경기당 득점 | 4.11588 | 4.10577 |
| 경기당 홈런 | 1.79813 | 1.79419 |
| BB/SO | .51591 | .51242 |

- Python: 조사·가치 보존 6, 기존 포지션/보직 11, 부포지션 2, Runtime Bake 12, 최종 Bake 5 통과.
- C# 표시 소스와 테스트 보조 컴파일 통과. 표시 모델 검사 4건, 기존 경기 전달 EditMode 9건,
  도감 조회 EditMode 8건 통과. 도감 API도 Unknown 필터와 실제 DH 필터를 구분한다.
- 실제 Editor 검증 소스 전체 검사: 오류 0·기존 경고 34. 연구 원기록 회귀 5건 통과.
- 실제 Runtime Full 로더와 WorldCardCatalogBuilder: 특수 카드 594장·레시피 143개 전체 참조 통과.
  다른 BaseContentHash를 가진 특수 카드 입력을 거부하는 것도 확인했다.
- Unity Test Runner·Play Mode·실제 카드 렌더링은 미실행이다. 표는 리그 총량 회귀이며
  구단별 승률의 역사 재현이나 장기 경제 밸런스 통과를 의미하지 않는다.

최종 ContentHash: `b7d8c82a9967ef018ef47e7edbbb7203c1bad598df7f6cc389076f16dfa14fff`.
비교 원본: `.tmp/position-repair-all/before-simulation.json`, `final-simulation.json`, `final-audit.json`.
게시 전 백업과 게시 검사: `.tmp/position-repair-all/PublicationBackup`.

## 재현과 Unity 반영

아래 명령은 프로젝트 루트에서 실행한다. `uv run --project Tools/KBOImporter python`을 사용한다.
기존 산출물을 후보로 덮어쓰지 말고 새 디렉터리를 사용한다.

```powershell
uv run --project Tools/KBOImporter python Tools/KBOImporter/research_season_positions.py --output Tools/KBOImporter/season_position_research_all.json
uv run --project Tools/KBOImporter python Tools/KBOImporter/synthetic_bake.py --input-dir Tools/KBOImporter/.cache/KBOImport/Normalized --years 1982-2025 --research-supplement Tools/KBOImporter/research_roster_supplement.json --editor-assets-dir .tmp/position-next/full --verify-editor-assets
uv run --project Tools/KBOImporter python Tools/KBOImporter/prepare_position_revision.py --before-editor "Assets/Editor Default Resources/HistoricalSimulation/1982-2025" --before-runtime Assets/10.Datas/HistoricalSimulation/1982-2025 --candidate .tmp/position-next/full --output .tmp/position-next/final
uv run --project Tools/KBOImporter python Tools/KBOImporter/bake_pipeline_special_cards.py --editor-root .tmp/position-next/final
```

대량 경기와 전체 검증 후 `publish_position_repair.py <final> <기존 ContentHash> <새 백업 경로>`로 반영한다.
게시 도구는 능력치·Cost·신원·원기록 보존과 동시 수정 여부를 검사하고,
Editor Source·Editor Runtime·게임 Runtime·특수 카드 파일·Unity Catalog Hash를 함께 갱신한다.
기존 `.meta`와 GUID는 보존한다. 포지션만 수정할 때 Full Bake를 바로 게시하지 않는다.
게시 후 최종 후보·Editor Runtime·게임 Runtime의 47개 파일이 바이트 단위로 동일하며,
실제 Runtime 로더 재검증과 Unity Catalog의 특수 카드 파일 Hash 일치도 확인했다.

현재 반영본으로 Unity 파이프라인을 재개할 때는 **2단계부터** 진행한다.
새 ContentHash에 대해 World History를 재베이크해야 한다. 기존 역사 캐시 Hash만 바꿔 재사용하지 않는다.
열린 게임이 기존 Definition을 캐시 중이면 세션을 다시 시작해야 새 위치가 보인다.
