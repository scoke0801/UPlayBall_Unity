# 특수 카드 큐레이션과 EX Cost 충돌

## 현재 발급 우선순위 (2026-09-12)

커리어하이 발급 자격을 충족하는 `playerPersonId`는 레전드에서 제외한다. 다른 연도·다른 계보도
동일 선수로 판정하며, 보유 카드나 Bake의 Edition 선택 범위에 따라 결과가 달라지지 않는다.
아래 v2 확장의 동시 발급 설명은 이전 정책이다. 큐레이션 원본은 후보 근거로 보존하고 최종 Bake에서
제외 카드와 레시피를 함께 제거한다. 자격 미달 커리어하이 후보만 있는 선수는 레전드 심사를 유지한다.

현 정본은 커리어하이 116장, 레전드 19장(중복 86장 제외), EX 73장, 레어 363장이다.
연습경기 기존 Bake 영향과 검증은 [변경 기록](../reports/special-card-exclusivity-20260912.md)을 따른다.

> 2026-09-10 후속 완료: 일반 카드 Cost 평가 계약을 수정해 EX 88건 모두 Cost 10을 충족했다.
> 레전드 20·커리어하이 122·레어 363장을 포함한 총 593장과 142개 레시피를 Unity에 연결했다.
> 아래 52건 차단 및 미연결 내용은 이전 분석 기록이다. 최신 근거는
> `docs/reports/고코스트_평가_특수카드_연결.md`를 따른다.

## 레전드 확장 (legend-curation-v2)

2026-09-11, 번트 전환 평가(`BuntPrimaryStatSpecialCardsSchema6`) 기준으로 레전드를 20명에서 **105명**으로 늘렸다.
기존 20명과 모든 EX·레어·커리어하이 카드·레시피는 바이트 단위로 그대로이며, 레전드 85장·레시피 85개만 추가됐다.

- 대상: 기존 레전드가 아닌 선수 가운데, 계보 유효 시즌이 8년 이상(NC·KT는 5년 이상)이고 Cost 9~10 유효 시즌이 있는 선수. 계보 누적 Source 성과 순으로 계보당 최대 10명.
- 베이스: Cost 9~10 유효 시즌 중 발급 전력이 가장 높은 시즌(가능한 Peak). 발급 전력 Peak가 Cost 8 이하인 14명만 `basePlayerSeasonId`로 지정하고 `PeakCostEligibleSeason` 태그를 붙였다. 기존 관례에 따라 같은 해 커리어하이 카드와 공존한다. 기존 김동주 2000 지정(`UserConfirmedRepresentativeSeason`)은 유지했다.
- 고유 선수당 레전드 1장. 두 계보에서 후보가 된 박석민(삼성/NC)·최형우(삼성/KIA)·송지만(한화/키움)은 누적 성과가 큰 계보만 남겼다.
- 계보별 인원: 두산·한화·KIA·LG·롯데·삼성·SSG 각 12, 키움 10, KT 6, NC 5 (기존 2명 포함).
- 검증: 파이프라인 재현(기준 발급본과 바이트 일치), 동일 입력 재베이크 바이트 일치, `--validate-content` Fast/Full 651장·219레시피 참조 통과, Headless EditMode 845개 통과.
- 레전드는 Scout 대상이 아니라 특수 영입 전용이므로 스카우트 확률은 바뀌지 않는다. 획득 경제(재료 소모량 대비 공급) 검증은 아직 하지 않았다.

## 현재 결과

2026-09-09 기준, 레전드 초기 20명과 영입 재료 160그룹을 확정했다. 기존 커리어 하이 112장과 함께 **132장·132개 레시피의 Offline Bake 및 Canonical 검증을 통과**했다. EX는 기존 Cost 10 규칙을 유지하여 52건이 계속 차단된다. EX 제외 범위는 발급 파일의 `editionScope`에 기록하며 전체 특수 카드 발급 완료로 간주하지 않는다.

이번 작업은 원본 선수 시즌의 Cost·능력치·경기 계수를 변경하지 않는다. 발급 파일은 `output/special-cards/`에 있으며 Unity 콘텐츠 로드·획득 UI에는 아직 연결하지 않았다. 대량 경기 밸런스나 Production E2E 완료를 뜻하지 않는다.

## 레전드 초기 선정

원작 관측 자료에 등장한 인물만으로 후보를 제한하면 삼성 이승엽·KIA 선동열 같은 전체 Canonical 경력의 후보를 검토할 수 없었다. 따라서 `legendShortlist`는 참고 자료로 유지하고, **명시적으로 저작한 Person/Lineage를 전체 경력의 Peak 목록에서 검증**하도록 변경했다. 이름은 검수용이며 실제 선택과 발급은 Person ID·Lineage ID로 한다. 동명이인을 이름만으로 연결하지 않는다.

초기 구성은 10개 계보마다 타자·투수 각 1명이다. 대표성은 아래의 명시적 기획 선정이며, 자동 성적 순위가 역사성을 입증한다고 주장하지 않는다. 장기 소속 경력과 원작 관측 여부는 정본 평가에서 검증한다. NC·KT에는 각각 자기 계보 선수만 배치했다. 후보 확장은 같은 큐레이션 파일로 저작한다.

| 계보 | 타자 | Peak 연도 / Cost | 투수 | Peak 연도 / Cost |
|---|---|---|---|---|
| 두산 | 김동주 | 1999 / 9 | 김상진 | 1995 / 10 |
| 한화 | 장종훈 | 1991 / 10 | 정민철 | 1994 / 10 |
| KIA | 이종범 | 1997 / 10 | 선동열 | 1993 / 10 |
| 키움 | 박병호 | 2015 / 10 | 정민태 | 1995 / 9 |
| KT | 강백호 | 2021 / 9 | 고영표 | 2021 / 10 |
| LG | 이병규 | 1999 / 10 | 이상훈 | 1994 / 10 |
| 롯데 | 이대호 | 2010 / 10 | 최동원 | 1986 / 9 |
| NC | 나성범 | 2020 / 9 | 이재학 | 2013 / 10 |
| 삼성 | 이승엽 | 1997 / 10 | 임창용 | 1999 / 9 |
| SSG | 최정 | 2011 / 10 | 김원형 | 1997 / 10 |

여기서 Peak는 실제 수상 시즌이나 널리 알려진 최고 시즌을 임의 지정한 것이 아니다. 기존 정책대로 최종 BaseAttributes에 역할별 가중치를 적용한 발급 전력 1위이며, 신뢰도·표본·이른 연도·ID 순으로 동률을 해소한다. 예를 들어 최동원은 정본에서 1986년 Cost 9가 Peak다. 다른 시즌을 선택하거나 능력치·Cost를 올리지 않았다.

저작 정본: `Tools/KBOImporter/legend_curation_v1.json`. 이름과 이유 태그는 도구용 데이터이며 Runtime 발급 파일에는 포함하지 않는다.

## 레전드 재료

- 각 Recipe는 타자 4명·투수 4명, 서로 다른 Person 8명으로 구성한다.
- 같은 계보의 Normal 카드만 사용한다. 대상 Legend 본인은 재료에서 제외한다.
- Cost 5·6·7·8에서 타자·투수 각각 한 그룹을 저작한다. Cost 합계는 52이며 실제 획득 확률을 뜻하지 않는다.
- 그룹마다 한 Person의 조건에 맞는 시즌 후보를 최대 3장 제공하며, 플레이어는 그중 1장만 사용한다. 전체 160그룹의 후보 참조는 347개다.
- 기존 평가의 신뢰도 0.45 이상·역할별 표본 비율 0.25 이상을 재료의 초기 유효 시즌 기준으로 사용한다.
- 재료 저작은 아직 사용하지 않은 시대·포지션, 계보 내 유효 경력, 발급 전력, 연도·ID 순으로 결정론적으로 수행한다. 이는 이미 선정한 Legend의 재료 저작에만 적용하며 Legend 대상 선정을 자동화하지 않는다.
- 그룹을 채울 수 없으면 실패한다. 다른 계보·다른 Cost로 조용히 대체하지 않는다.

`compile_legend_curation.py`가 최종 ID 목록을 `special_card_bake_policy.json`에 저장한다. 평가 입력 해시·큐레이션 파일 해시·컴파일러 해시를 함께 기록한다. 정본이 변경되면 오래된 재료 정책을 그대로 Bake할 수 없다.

## EX 52건의 원인

전체 44개 연도 × 타자·투수 2명 = 성적 1위 88건을 유지했다. 이 중 36건은 Cost 10이고 52건은 아래와 같다.

| 분류 | 건수 |
|---|---:|
| Cost 9 | 38 |
| Cost 8 | 13 |
| Cost 7 | 1 |
| 최종 가격이 RecordGradientBoosting에서 결정됨 | 33 |
| 최종 가격이 AnnualReferenceOverride에서 결정됨 | 19 |
| 같은 연도·역할에 유효 표본의 Cost 10 후보 자체가 없음 | 16 |

성적 1위는 `costDerivationTrace.continuousValue`의 Source 시즌 성과 순위다. 최종 Cost는 그 뒤 적용하는 기록 학습 모델 또는 연도별 참조 보정으로 결정된다. 서로 다른 두 기준을 쓰므로 성적 1위가 반드시 Cost 10이라는 보장은 없다. 52건에 Cost 10을 강제 부여하면 기존 가격 데이터를 왜곡하며, 차순위 Cost 10으로 교체하면 최고 성적 선수 규칙을 위반한다. 16건은 그 대체 후보조차 없다.

현재 정책은 엄격한 Gate를 유지한다. 해결 제안은 **성적 1위와 원본 Cost를 유지하면서 EX의 Cost 10 한정을 완화하는 것**이다. 이는 기존 사용자 확정 규칙의 변경이므로 질문으로 제시했고, 답변 없이 적용하지 않았다. Cost 10 규칙을 유지한다면 현재 정본에서는 해당 연도의 EX 발급을 보류하거나 가격/성과 규약을 별도 개정해야 한다.

개별 52건의 원래 Cost Trace·성과·Cost 10 후보 유무·차순위 대체 시 성과 손실은 `output/special-cards/ex-cost-conflicts.json`에 있다. `audit_ex_cost_conflicts.py`로 재생성한다.

## 재현

프로젝트 루트에서 Python 3.9 이상으로 실행한다. 평가에는 로컬 Research 관측 자료가 필요하다.

```powershell
python Tools/KBOImporter/evaluate_special_cards.py --output Research/PyaMaeCardDb/SpecialCardCurationEvaluation
python Tools/KBOImporter/compile_legend_curation.py --evaluation Research/PyaMaeCardDb/SpecialCardCurationEvaluation/evaluation.json --curation Tools/KBOImporter/legend_curation_v1.json --policy Tools/KBOImporter/special_card_bake_policy.json --report output/special-cards/legend-curation.json
python Tools/KBOImporter/bake_special_cards.py --evaluation Research/PyaMaeCardDb/SpecialCardCurationEvaluation/evaluation.json --policy Tools/KBOImporter/special_card_bake_policy.json --editions Legend CareerHigh --output output/special-cards/BakedLegendCareerHighCards.json
python Tools/KBOImporter/audit_ex_cost_conflicts.py --evaluation Research/PyaMaeCardDb/SpecialCardCurationEvaluation/evaluation.json --output output/special-cards/ex-cost-conflicts.json
```

`--editions`를 생략하면 EX까지 포함한 전체 발급을 검사하며 현재는 Cost Gate 52건으로 실패한다. 검증용 부분 Bake를 전체 성공으로 보고하지 않는다.

## 검증 범위

- Python 레전드 큐레이션 5개 및 Bake 9개 테스트 통과.
- 실제 132장·132개 Recipe의 Base Cost, Normal 존재, Person, 계보, 연도, 본인 재료 금지·재료 인물 중복 검증 통과.
- 동일 입력으로 두 번 Bake한 파일 SHA-256 일치: `FA7B2FC1AC70B9DEF4A4474BB06C8198598A8BAA88E3E0580177415D8AA1BD64`.
- Canonical 입력 SHA-256 검증으로 원본 선수 시즌 파일 불변 확인.
- Unity 실행·획득 UI·경기 대량 시뮬레이션은 이번 범위에서 실행하지 않았다. 발급 파일을 실제 게임에 배포하기 전 카드 효과와 획득 경제 검증이 남아 있다.
