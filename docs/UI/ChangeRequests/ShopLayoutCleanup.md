# 상점 버튼·상품 목록·팩 이미지 정리

사용자 제공 상점 스크린샷의 대형 탭·구매 버튼, 잘리는 오른쪽 상품 열,
팩 배경의 체크무늬와 검은색 영역을 수정한다.

- 고정 높이 LayoutElement에 flexibleHeight 0을 명시한다. 내부 HorizontalLayoutGroup의
  확장값이 높이 계산에 참여하던 원인을 제거하고 탭 36, 구매 동작 38 높이를 사용한다.
- 상품 목록은 ScrollRect Content에 직접 2열 Grid를 배치한다. 레이아웃 확정 이후와
  화면 크기 변경 시 Viewport의 실제 너비에서 여백·간격을 빼 동일 너비로 나눈다.
- 인기 배지는 표시 문자열을 비운다. 신규·할인 배지는 유지한다.
- ImageGen으로 선수 팩·스킬 팩을 각각 편집하고 기존 Resources의
  `UI/Shop/shop_player_pack`, `UI/Shop/shop_skill_pack` PNG를 교체했다.
  기존 meta와 GUID를 보존하므로 목록과 선택 상품 미리보기에 함께 반영된다.
  첫 투명 배경 시도는 체크무늬가 다시 생성되어 채택하지 않았다.
  최종 파일은 밝은 회녹색 배경의 불투명 PNG이며 실제 알파 투명 이미지가 아니다.
  이미지 프레임 색은 #E8ECE9를 사용한다.

생성 원본은 `.codex/generated_images/01a07607-4717-74f1-aca6-cd500e12e5cb`의
`exec-a2a9e822-7798-4669-a5e6-fe0a6c2d562f.png`와
`exec-8ceab7f2-273e-4236-a358-e21418673d0b.png`다.
각 기존 팩 디자인을 유지하며 체크무늬·검정 배경을 단색 #E8ECE9로 바꾸도록 요청했다.

검증: 최종 이미지 두 장 육안 확인, 인기 배지의 기존 테스트 기대값 변경,
diff 공백 검사 통과. 중간 Presentation 및 Presentation.Tests 보조 컴파일은
경고 0·오류 0으로 통과했으나 최종 재실행은 작업 도중 달라진 구단 재정 화면의
BindFinanceDashboard·BuildFinanceDashboard·StyleFinanceTicket 미해결 오류 3개로 실패했다.
따라서 최종 통합 컴파일 통과로 보고하지 않는다. 상점 범위 밖의 해당 파일은 수정하지 않았다.
Unity EditMode 테스트 실행과 실제 상점 화면의 해상도별 시각 검증은 수행하지 못했다.
게임 규칙·가격·확률은 수정하지 않았다.
