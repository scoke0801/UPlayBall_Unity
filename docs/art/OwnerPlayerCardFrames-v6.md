# 카드 등급 마크 위치 v6

레어·올스타·EX의 Full/Mini 원화 6장을 ImageGen으로 편집했다.
상단의 등급 마크·날개·리본을 원화에서 제거하고, 이름표 바로 위 중앙에 마크 하나만 배치한다.
이 규칙이 v5의 상단 마크와 레어 UV 이동 규칙보다 우선한다.

- 원화의 명찰·하단 정보 영역·프레임 크기는 보존한다.
- 상단은 구단 정보와 초상 공간으로 비운다.
- RARE 은색 플레이트, ALL STAR 은색 리본·분홍 별, EX 금색 날개를 유지한다.
- 코드의 장식 UV와 실제 렌더 좌표를 같게 유지한다. 상단 원화를 남긴 채 마크만
  다른 위치로 복사하던 `_verticalOffset`은 제거했다.
- 공통 초상 크기와 이름 중앙 정렬, 배경 → 초상 → 장식 → 정보 순서는 유지한다.
- 최종 원화: `Assets/Resources/UI/PlayerCards/PlayerCard_{Full|Mini}_{Rare|AllStar|Ex}_v6.png`.
  생성 원본 사본은 `output/imagegen/player-card-frames-v6/`에 보존한다.
- 생성 프롬프트는 기존 단일 원화를 편집 대상으로 지정하고, 상단 마크를 완전히 지우며
  Full은 위 기준 y≈710px, Mini는 각 명찰 상단에 작은 마크 하나만 두도록 요청했다.
  선수·구단명·이름·연도·기록·Cost를 생성하지 않도록 지정했다.

원화 6장 모두 상단 마크 제거, 등급명 1회, 이름 영역의 비어 있음과 1060×1484 규격을 확인한다.
실제 Runtime Builder의 캡처는 `docs/reports/card-badges-v6/`,
Unity EditMode 결과는 `output/CardBadgeV6-EditMode.xml`에 기록한다.

검증 결과: Presentation 및 테스트 보조 컴파일 경고·오류 0.
독립 Unity 6000.3.21f1 프로젝트의 카드 EditMode 24개 전부 통과(캡처 포함, 제외 0).
수정된 6종의 실제 카드 캡처에서 상단 중복 제거와 이름표 위 단일 마크를 육안 확인했다.
