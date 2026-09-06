# 전력보강 레퍼런스 UI

사용자 첨부 화면을 기준으로 선수 목록 6열×3행, 등록대 5열×2행,
등록하기·등록취소·초기화와 하단 보강 버튼을 배치했다.
카드는 기존 PlayerMiniCardView와 실제 보유 정보를 사용한다.
강화는 기존 대상 카드 + 동일 카드 재료 1장 규칙을 유지하며 나머지 슬롯은 빈칸이다.
강화 소비자가 없는 다른 카드 유형 탭은 비활성으로 표시한다.
선수방출은 중복 판매 창을 열며 수량·SP와 최종 확인을 거친다.
공용 Shell 내비게이션은 현재 프로젝트의 Route 구성을 유지한다.

## 생성 이미지

- 도구: 내장 ImageGen
- 리소스: Assets/10.Datas/Resources/UI/OwnerPowerUp/reinforcement_paper_v1.png
- 용도: 실제 UI 뒤에 놓는 회백색 야구 음각 배경
- 프롬프트:

> Use case: ui-mockup. Asset type: subtle background texture for an existing Korean vintage baseball management game reinforcement workspace. Generate a landscape 3:2 plain near-white pearl silver background with extremely faint embossed oversized baseball stitching and abstract concentric baseball club seal shapes near the bottom right only, like a 2008 desktop sports management interface. Center and left almost flat white, contrast very low, no text, no letters, no UI, no buttons, no cards, no borders, no gradients to dark colors. This is a background texture consumed underneath functional Unity UI, not a screenshot.

## 검증

- Presentation 및 Presentation.Tests 보조 컴파일 통과.
- 등록 전 차단 → 등록 → 확인 전 Command 미발행 → 확정 → 등록취소 후 차단 회귀 테스트 추가.
- Unity 프로젝트가 에디터에서 열려 있어 EditMode 실행과 실제 UI 렌더 캡처는 미수행.
- 이미지 리소스 육안 검수만 수행했으므로 픽셀 단위 일치로 판정하지 않는다.
