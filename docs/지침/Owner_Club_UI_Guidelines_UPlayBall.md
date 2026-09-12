# 구단주 모드 구단 UI 구현 지침

## 선수 재계약 제거 — 우선 규칙

구단 메뉴는 구단주·구단·재정·시설·코칭스태프의 다섯 Route다.
`Owner.Club.Contract` 메뉴와 수동 재계약 명령은 제거했다. 시즌 전환 시 만료 여부로
진행을 막거나 선수를 해제하지 않으며 재계약금도 받지 않는다. 기존 연봉과 급여 정산 이력은
계속 저장하고 매 시즌 급여를 정산한다. 잔여 계약 연수는 과거 저장 정보로만 보존한다.
아래 계약 화면·기간 선택·갱신 비용·만료 차단 관련 내용은 과거 구현 기록이며 더 이상 적용하지 않는다.

이 문서는 `Owner.Club.*` 여섯 Route의 정규 UI·시스템 계약이다. 최초 레이아웃 조사와 완료 이력은
`docs/todo/프야매_메인UI_하위메뉴/12~18`에 보존하되, 이후 구현·리팩터링 판단은 이 문서를 우선한다.
공용 Shell과 화면 제작 규칙은 `Unity_UI_Production_Guidelines_UPlayBall.md`를 함께 따른다.

## 1. 게임적 목표

구단 화면은 정보 열람용 도감이 아니라 다음 경기와 다음 시즌의 선택 비용을 설명하는 운영실이다.
시설·스태프·선수 계약은 같은 Money를 두고 경쟁해야 하며, 확정 전에는
비용·효과·차단 근거를 보여준다. 플레이어가 결과를 보고 “왜 이 선택이 가능하거나 거절됐는지”를
설명할 수 없는 Command는 노출하지 않는다.

## 2. 공통 화면 계약

- Global Top Bar와 `구단주/구단/재정/시설/코칭스태프/계약` Local Navigation을 고정한다.
- 회백색 Canvas, 얇은 청회색 Border, 검정·은색 상태 Bar, 작은 직사각형 Tab, 조밀한 Table을 쓴다.
- 수치·버튼·표는 Native uGUI로 만들고 이미지에 굽지 않는다. 생성 이미지는 배경 장식으로만 쓴다.
- 모든 변경은 `Snapshot/Query → Preview → Validate → Command → RuntimeChanged → Rebind`를 거친다.
- 화면은 비용·효과를 재계산하지 않는다. 계산된 Snapshot과 차단 사유만 표시한다.
- 목록 선택과 계약 기간은 Route를 오가는 동안 Coordinator가 보존한다.
- Loading, Empty, Locked, Error를 빈 패널로 대체하지 않는다. 실행 불가능 CTA는 이유와 함께 비활성화한다.

## 3. Route별 책임

| Route | 화면 책임 | Production Consumer |
|---|---|---|
| `Owner.Club.Owner` | 구단주 Identity, 구단 명성·운영 목표, Front Manager | `OwnerClubInformationPresentationModel` |
| `Owner.Club.Information` | 구단 Identity, 시즌 성적·기록·일정 요약 | `OwnerClubInformationPresentationModel` |
| `Owner.Club.Finance` | 보유 Money, 주간·시즌 수입/지출, 티켓 정책과 예상 관중 | `ClubOperationState`, `SetTicketPolicy` |
| `Owner.Club.Facility` | 구장·시설 Level, 효과, 다음 비용·조건, 업그레이드 | `ClubUpgradeResolver`, `UpgradeFacility`, `UpgradeStadium` |
| `Owner.Club.Staff` | 현재 5역할, 시장 후보, 전문성·철학·효과·계약 비용 | `StaffMarketResolver`, `StaffContractService` |
| `Owner.Club.Contract` | 25인 계약, 잔여 연수, 연봉 총액, 1~3년 갱신안 | `OwnerPlayerMarketService` |

재정 Route는 계정과 티켓 정책에 전체 작업면을 사용한다. 시설 Route는 좌측 구장·팬 기반 요약과
우측 시설 목록·효과·업그레이드 CTA를 함께 둔다. 계약은 Shell의 Right Inspector를
의사결정 근거 영역으로, Action Bar를 기간 선택과 최종 확정 영역으로 사용한다.

### 3.1 프런트 매니저 선택

`Owner.Club.Owner`의 사진 아래 `매니저 교체`는 공용 Popup Host에서 분석형·현장형·활력형
초상과 타입을 표시한다. 현재 선택은 `현재 매니저`로 구분하고 다른 매니저의 `선택하기`로 즉시 교체한다.
닫기·취소는 선택을 유지하고 원래 버튼으로 포커스를 돌려준다. OwnerModeManager가 지원 ID를 검증한 뒤
기존 OwnerProfile.FrontManagerId를 저장하고 RuntimeChanged로 다시 바인딩한다. 저장 실패 시 이전 ID를
복원하고 재시도 안내를 표시한다. 외형 선택이므로 비용·경기 능력치 효과는 없으며 저장 형식은 유지한다.

## 4. 선수 계약 규칙

- 계약은 구단주 모드 전용 `OwnerPlayerContractState`이며 선수 커리어 계약 상태와 공유하지 않는다.
- 현재 25인 로스터의 모든 카드가 정확히 하나의 계약을 가진다.
- 초기 계약 기간은 첫 시즌에 강제 갱신이 발생하지 않도록 `CardId`의 고정 Hash로 2~3년에 분산한다.
  같은 Save 입력은 항상 같은 결과다.
- 연봉은 Cost와 Edition, 계약 기간을 `OwnerPlayerMarketBalanceTable`로 계산한다.
- 다년 계약은 연봉을 조금 낮추지만 장기 재정 유연성을 사용한다. 갱신 시 계약금은 가용 Money부터 차감한다.
- 시즌 마감 시 선수단 연봉과 Staff 연봉을 합산해 현금으로 정산하고 부족액은 미지급금으로 이월한다.
- 시즌 전체 종료 후 만료 임박 선수의 갱신 계약금도 부족액 이월을 허용한다. Preview는 전체 비용과
  이월액을 표시한다. 시즌 중 갱신과 만료 임박이 아닌 계약의 추가 연장에는 현금이 필요하다.
- 모든 Money 수입은 미지급금을 우선 상환하며, 잔액만 가용 현금으로 제공한다. 구매·시설 투자·신규
  Staff 계약에 외상을 허용하지 않는다. 홈·재정에서 미지급 급여·계약금 잔액과 상환 규칙을 표시한다.
- SaveVersion 21은 미지급금을 보존한다. 개별 계약의 마지막 급여 정산 표시는 현금 지급 또는
  미지급금 인수 완료를 의미하며, 저장 복원·재시도 시 동일 급여를 다시 청구하지 않는다.
- 잔여 1년 계약을 갱신하지 않은 상태에서는 다음 시즌 진행을 막고 `ContractRenewalRequired`를 반환한다.
- 계약과 마지막 연봉 지급 시즌은 SaveVersion 11부터 저장한다. v10 이하는 현재 25인 기준으로 결정론적으로 생성한다.
- SaveVersion 14는 v11~v13의 1군 등록 변경에서 생길 수 있던 계약 `CardId` 불일치를 이행한다. 계약 수가 25인과 같은 알려진 결함만 기존 계약을 보존해 복구하며, 일부 계약이 유실된 일반 손상 상태는 계속 거부한다.
- 보유 카드의 새 1군 등록 계약은 1년이다. 기존 1군 계약은 그대로 보존하며, 등록 해제·재등록으로 2~3년 초기 계약이나 계약 갱신 비용을 무료로 다시 받지 않는다.

## 5. Balance와 성능

`OwnerPlayerMarketBalanceTable`이 Cost별 연봉, Edition 배율, 계약금 비율, 다년 할인,
계약 상한을 소유한다. 핫패스 경기 확률은 바꾸지 않으므로 이 UI 작업만으로 대량 경기 밸런스
재측정을 요구하지 않는다. 계약 후보는 고정 순서로 정렬하며 전역 난수와 컬렉션 순회 순서에
의존하지 않는다.

## 6. 생성 장식 자산

다음 자산은 UI 문구·버튼을 포함하지 않는 배경 장식이다.

- `Assets/Resources/UI/Generated/bg_owner_club_facilities_v1.png` — 구장·훈련·회복·스카우트·팬 시설 Campus
- `Assets/Resources/UI/Generated/bg_owner_contract_v1.png` — 계약서·펜·계산기가 놓인 Front Office Desk

모두 16:9, 청회색·은색 기반, 한글·수치·로고 없음으로 생성했다. 정보 Panel 가독성이 장식보다 우선한다.

## 7. 완료 게이트

- Core/Simulation/Game/Presentation asmdef 보조 컴파일 오류 0개
- 계약 결정론·자금 부족 EditMode 테스트 통과
- SaveVersion 11 저장/복원 회귀 통과
- 실제 Unity 화면 배치·해상도·입력 QA는 사용자가 수행한다.

### 7.1 잔여 작업 감사 기록

- 계약 Route는 `CanManagePlayerContracts` 권한을 가진 Production Route로 유지한다.
- `Owner.Club.Trade` Route와 구단주 트레이드 Query·Preview·Command·저장 데이터는 제공하지 않는다.
- 계약 Aggregate는 현재 25인 로스터와 같은 수의 CardId를 정확히 가져야 한다. 일부 계약만 복원된 손상 상태는 자동 보정하지 않고 진입 시 실패시킨다.
- 만료된 계약의 갱신 Preview는 `ContractExpired`로 구분하며 Runtime을 변경하지 않는다.
