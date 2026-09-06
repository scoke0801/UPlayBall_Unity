# 프야매 메인 UI 하위 메뉴 TODO

> 선수단 01~04는 2026-09-06 구현을 마치고
> [`docs/지침/Owner_Roster_UI_Guidelines_UPlayBall.md`](../../지침/Owner_Roster_UI_Guidelines_UPlayBall.md)로
> 정규화했다. 이 폴더의 01~04 문서는 레퍼런스 조사와 완료 이력을 보존한다.
>
> 구단 12~18도 2026-09-06 구현을 마치고
> [`docs/지침/Owner_Club_UI_Guidelines_UPlayBall.md`](../../지침/Owner_Club_UI_Guidelines_UPlayBall.md)로
> 정규화했다. 이 폴더의 12~18 문서는 레퍼런스 조사와 완료 이력을 보존한다.

## 목표

구단주 모드의 실제 Navigation Route를 메뉴 하나당 하나의 TODO로 관리한다. 디자인 정본은 [인벤 「첫화면 대기실! 하나하나 뜯어 보자」](https://m.inven.co.kr/webzine/wznews.php?idx=86747&searchwhere=writer&site=bm&svalue=%EA%B3%B5%EB%AF%BC%ED%99%98)이다. 이 기사에서 확인되는 정보 밀도, 회백색 업무 Canvas, 작은 상단 탭, 카드/표 비율, 진입·복귀 흐름을 가능한 동일하게 재현한다.

현재 UI는 정본이 아니다. 레퍼런스와 충돌하면 Presentation 레이아웃, 계층, 간격, 색, 카드 크기, CTA 위치를 과감히 수정한다. 실제 Snapshot·Query·Preview·Validate·Command, 공용 Shell 소유권, Simulation/Presentation 분리 계약은 유지한다.

이 폴더는 구단주 모드 메인 UI와 하위 메뉴 구현의 정규 지침이다. 덕아웃의 감독·수석코치 세부 규칙은 `docs/todo/역사시뮬레이션_구단주모드/17_프야매_덕아웃_감독_수석코치_역기획.md`를 보조 규격으로 사용하되, Route·화면 책임·레이아웃·상태 표현이 충돌하면 이 폴더의 08~11 문서를 우선한다.

전력보강 세 Route의 구현 계약과 수치 기준은 [구단주 모드 전력보강 UI 구현 지침](../../지침/Owner_PowerUp_UI_Guidelines_UPlayBall.md)을 따른다.

## 공통 조사·디자인 계약

- 각 TODO에 적힌 로컬 비교 이미지가 docs/디자인 또는 docs/디자인/ref 아래에 존재하면 구현 전에 반드시 view_image로 실제 이미지를 열어 확인한다. 파일명만 보고 추정하지 않는다.
- 기준 URL은 모든 TODO에서 사용자가 지정한 위 인벤 기사 하나로 통일한다.
- Global Top Bar와 아이콘 Navigation 위치는 모든 일반 화면에서 고정한다.
- 하위 메뉴는 작은 직사각형 탭으로 표시하며 선택 상태는 하나만 둔다.
- 본문은 회백색 Canvas, 얇은 청회색 Border, 조밀한 표와 작은 선수 카드 비율을 따른다.
- 원본 4:3을 단순 확대하지 않는다. 16:9 확장분은 정보 영역에 배분하되 원본 요소의 상대 위치와 시각 우선순위를 유지한다.
- Panel, Button, Table, Label, Scroll, 수치와 상태는 Native uGUI로 구현한다.
- ImageGen은 배경, 인물/선수 실루엣, 카드 삽화, Pack, Badge, 장식 Key Frame에만 사용한다. 생성 이미지에 한글·수치·버튼을 굽지 않는다.
- 새 자산은 built-in ImageGen으로 자산별 생성하고 최종 연결본은 프로젝트 내부에 저장한다. 투명 자산은 실제 Alpha를 확인한다.
- 잠긴 기능은 Backend 계약이 완성되기 전까지 잠금을 유지한다.
- 렌더링 및 시뮬레이션 테스트는 이 문서 묶음의 작업 범위에 포함하지 않는다.

## Route 목록

| 번호 | 전역/Context | 하위 메뉴 | Route | 현재 상태 | TODO |
|---:|---|---|---|---|---|
| 01 | 선수단 | 라인업 | Owner.Roster.Lineup | 구현됨 | [01_선수단_라인업.md](./01_선수단_라인업.md) |
| 02 | 선수단 | 투수진 | Owner.Roster.Pitching | 구현됨 | [02_선수단_투수진.md](./02_선수단_투수진.md) |
| 03 | 선수단 | 보유선수 | Owner.Roster.Collection | 구현됨 | [03_선수단_보유선수.md](./03_선수단_보유선수.md) |
| 04 | 선수단 | 컨디션·궁합 | Owner.Roster.Condition | 구현됨 | [04_선수단_컨디션궁합.md](./04_선수단_컨디션궁합.md) |
| 05 | 전력보강 | 스카우트 | Owner.PowerUp.Scout | 구현됨 — 전용 PowerUp View·실제 확률 Preview | [05_전력보강_스카우트.md](./05_전력보강_스카우트.md) |
| 06 | 전력보강 | 카드훈련 | Owner.PowerUp.Training | 구현됨 — 전용 PowerUp View·훈련 Preview | [06_전력보강_카드훈련.md](./06_전력보강_카드훈련.md) |
| 07 | 전력보강 | 강화·판매 | Owner.PowerUp.EnhancementSale | 구현됨 — 전용 PowerUp View·수량별 Preview | [07_전력보강_강화판매.md](./07_전력보강_강화판매.md) |
| 08 | 덕아웃 | 덕아웃 | Owner.Dugout.LineupNotes | 구현됨 | [08_덕아웃_덕아웃.md](./08_덕아웃_덕아웃.md) |
| 09 | 덕아웃 | 팀컬러 | Owner.Dugout.TeamColor | 구현됨 — 전용 View/상세 Snapshot/원자적 Command | [09_덕아웃_팀컬러.md](./09_덕아웃_팀컬러.md) |
| 10 | 덕아웃 | 작전 | Owner.Dugout.Tactics | 구현됨 — 전용 View/상세 Snapshot/원자적 Command | [10_덕아웃_작전.md](./10_덕아웃_작전.md) |
| 11 | 덕아웃 | 감독방침 | Owner.Dugout.ManagerPolicy | 구현됨 — 전용 View/6축 Command | [11_덕아웃_감독방침.md](./11_덕아웃_감독방침.md) |
| 12 | 구단 | 구단주 | Owner.Club.Owner | 구현됨 — ClubInformation Owner Tab | [12_구단_구단주.md](./12_구단_구단주.md) |
| 13 | 구단 | 구단 | Owner.Club.Information | 구현됨 — ClubInformation Club Tab | [13_구단_구단정보.md](./13_구단_구단정보.md) |
| 14 | 구단 | 재정 | Owner.Club.Finance | 구현됨 | [14_구단_재정.md](./14_구단_재정.md) |
| 15 | 구단 | 시설 | Owner.Club.Facility | 구현됨 | [15_구단_시설.md](./15_구단_시설.md) |
| 16 | 구단 | 코칭스태프 | Owner.Club.Staff | 구현됨 | [16_구단_코칭스태프.md](./16_구단_코칭스태프.md) |
| 17 | 구단 | 계약 | Owner.Club.Contract | 구현됨 — 계약 조회/Preview/갱신/시즌 연봉/Save v11 | [17_구단_계약.md](./17_구단_계약.md) |
| 18 | 구단 | 트레이드 | Owner.Club.Trade | 구현됨 — 1:1 제안/가치·로스터 검증/확정/Save v11 | [18_구단_트레이드.md](./18_구단_트레이드.md) |
| 19 | 리그 | 순위표 | Shared.League.Standings | 구현됨 | [19_리그_순위표.md](./19_리그_순위표.md) |
| 20 | 리그 | 구단 성적 | Shared.League.TeamResults | 구현됨 | [20_리그_구단성적.md](./20_리그_구단성적.md) |
| 21 | 리그 | 대전 결과 | Shared.League.Matchups | 구현됨 | [21_리그_대전결과.md](./21_리그_대전결과.md) |
| 22 | 리그 | 순위 변화 | Shared.League.RankHistory | 구현됨 | [22_리그_순위변화.md](./22_리그_순위변화.md) |
| 23 | 리그 | 일정 | Shared.League.Schedule | 구현됨 | [23_리그_일정.md](./23_리그_일정.md) |
| 24 | 리그 | 선수 기록 | Shared.League.SeasonRecords | 구현됨 | [24_리그_선수기록.md](./24_리그_선수기록.md) |
| 25 | 리그 | 역사 기록 | Shared.League.Records | 구현됨 | [25_리그_역사기록.md](./25_리그_역사기록.md) |
| 26 | 상점 | 상점 | Owner.Shop | 구현됨 — 전역 단일 Route | [26_상점.md](./26_상점.md) |
| 27 | 경기 준비 | 상대 분석 | Owner.MatchCenter.Analysis | 구현됨 | [27_경기준비_상대분석.md](./27_경기준비_상대분석.md) |
| 28 | 경기 준비 | 우리 라인업 | Owner.MatchCenter.Lineup | 구현됨 — RosterLineup 재사용 | [28_경기준비_우리라인업.md](./28_경기준비_우리라인업.md) |
| 29 | 경기 준비 | 컨디션·궁합 | Owner.MatchCenter.Condition | 구현됨 — Condition 재사용 | [29_경기준비_컨디션궁합.md](./29_경기준비_컨디션궁합.md) |
| 30 | 경기 준비 | 전술카드 | Owner.MatchCenter.Tactics | 구현됨 — RosterLineup 재사용 | [30_경기준비_전술카드.md](./30_경기준비_전술카드.md) |
| 31 | 경기 | 경기 관전 | Owner.Match.Spectator | 구현됨 — Context Route | [31_경기관전.md](./31_경기관전.md) |

## 구현 순서

1. 활성 Route의 레이아웃과 Back/선택 상태를 정본에 맞춘다.
2. 같은 View를 재사용해 문맥이 흐려진 Route를 전용 View State로 분리한다.
3. 잠긴 Route는 Backend Query/Preview/Validate/Command를 먼저 완성한 뒤 활성화한다.
4. 구현 후 해당 TODO의 현재 플로우, ImageGen 프롬프트·저장 경로, 남은 제한을 갱신한다.

## 덕아웃 08~11 구현 검증 기록

- `Baseball.Simulation`, `Baseball.Game.Unity`, `Baseball.Presentation`에서 이번 변경 파일을 포함한 보조 컴파일은 각각 오류 0개로 통과했다.
- 마지막 전체 의존성 재컴파일은 동시에 추가 중인 선수 계약·트레이드 코드의 `OwnerPlayerMarketResolver`, `OwnerContractRenewalPreview`, `OwnerTradePreview`가 생성 csproj에 아직 포함되지 않아 중단됐다. 덕아웃 변경 파일의 컴파일 오류는 아니다.
- 사용자 지시에 따라 EditMode, PlayMode, 렌더링, 대량 시뮬레이션 테스트는 실행하지 않았다. 따라서 화면 육안 QA와 밸런스 통계 검증은 완료로 간주하지 않는다.

## 구단 12~18 구현 검증 기록

- `Baseball.Core`, `Baseball.Simulation`, `Baseball.Game`, `Baseball.Game.Unity`, `Baseball.Presentation` 보조 컴파일 오류 0개다.
- `OwnerPlayerMarketResolverTests` 4건과 `ManagerHistoricalSaveTests` 19건을 Headless EditMode Runner로 실행해 모두 통과했다.
- Unity 배치 실행은 로컬 License Client 연결 실패로 Import 전에 중단했다. 사용자 지시에 따라 화면 렌더링·육안 QA는 수행하지 않았다.
