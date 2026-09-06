# 구단 라인업 조회

경기 준비의 컨디션·궁합 탭을 상대 라인업 탭으로 교체한다.
리그 / 순위표에서 구단 행을 클릭해 같은 카드 보드를 열 수 있다.
닫기 또는 ESC는 원래 순위표를 복원하고, 다른 메뉴를 선택하면 상세 보드를 숨긴다.

- 야수 9명과 벤치 5명, 선발 5명과 중계 4명·셋업·마무리를 두 줄로 배치한다.
- 내 구단은 선택 프리셋, AI 구단은 실제 경기 서비스의 공개 등록 역할 기반 기본 편성을 조회한다.
- 상대의 내부 컨디션·궁합·피로·숨은 전술은 공개하지 않는다. AI 팀컬러는 실제 경기 서비스와 같은 현재 로스터 자동 선택 결과를 표시한다. 발동 후보가 없는 슬롯만 적용 없음으로 표시하며 카드 등급을 발명하지 않는다.
- 선수 표시는 Runtime 가상 Identity를 사용한다. 기존 카드 표면과 초상화를 재사용한다.
- 편성 비용은 기존 RosterCostResolver 결과다. 공개 근거가 없는 상대 Cost 상한은 표시하지 않는다.
- 기존 Owner.MatchCenter.Condition 링크는 새 Owner.MatchCenter.OpponentLineup으로 이관한다.
- 구단주 AI 팀컬러 자동 선택은 실제 경기 능력치에도 반영한다. 화면은 경기 서비스 Query만 소비하며 저장 형식은 변경하지 않는다.

## ImageGen

내장 image_gen 도구로 생성했다. 최종 에셋:
`Assets/10.Datas/Resources/UI/TeamLineup/team_color_banner.png`

프롬프트:

> Use case: product-mockup. Asset type: reusable raster UI banner background for a Korean baseball management game, used behind runtime team-color text. Create a single wide horizontal rectangular silver and violet team synergy plaque, early 2010s Korean PC sports management UI style. Brushed silver thin rectangular outer bevel, dark charcoal horizontal inset nameplate, small luminous purple circular medallion inset on left with no symbol. Restrained violet glow along left edge, mostly flat dark center and right area with ample empty space for UI text. Straight-on orthographic, crisp texture, no perspective. Aspect ratio 3:1. Full canvas is the rectangular banner, no surrounding scene. No text, no letters, no numbers, no stars, no rank, no logos, no watermark. This is a UI asset, not an entire screen.

생성 이미지는 하단 팀컬러 배너의 바탕이며, 구단명·선수·비용·팀컬러 문구는 실제 UI 텍스트로 표시한다.
참조는 사용자가 제공한 두 번째 이미지의 조밀한 두 줄 카드 배열과 하단 배너다.

## 검증

검증 결과와 실제 uGUI 렌더는 `docs/reports/owner-team-lineup/`에 기록한다.
