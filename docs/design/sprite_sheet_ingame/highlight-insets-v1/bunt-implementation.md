# 번트 하이라이트

구단주 관전에서 공개된 `BuntAttempted`에 `OwnerMatchHighlightKind.Bunt`를 연결한다.
그림은 번트 준비 자세이고 공·접촉·성공 판정을 포함하지 않는다. 이후 희생번트의 송구·포구,
번트 안타·실패 중계는 기존 공개 사건대로 이어진다. 타석의 모든 번트를 핵심 장면으로
강제 선택하는 변경은 아니며, 관전 세션이 재생하는 구간 안에서 표시한다.

## 이미지

- 생성 수단: 내장 Imagegen. 채택 프롬프트: [bunt-prompt.txt](bunt-prompt.txt).
- 주 레퍼런스: `docs/design/sprite_sheet_ingame/우타자_히트.png`의 현재 경기 캐릭터.
- 보조 레퍼런스: 기존 `slide.png`의 부드러운 피규어 셰이딩과 구장 배경.
- 큰 머리·짧은 팔다리·갈색 머리·파란 모자·흰색과 파란색 유니폼을 유지한다.
- 생성 원본: [bunt-source.png](bunt-source.png).
- 런타임: `Assets/10.Datas/Resources/UI/OwnerMatch/Highlights/bunt.png`.
- 1672×941, 불투명 완성 장면. 삽입 컷 제작 기준에 따라 크로마키 제거는 적용하지 않는다.

## 기존 UI 재사용

`UI_Scene_OwnerMatchSpectator.Highlights`의 기존 Panel·Image·Caption과 JSON 로딩을 그대로 쓴다.
Skin/Layout Reference는 기존 `GameCastSidebar/HighlightInset`이며 그림 영역은 472×265.5다.
새 메뉴·조작·포커스 대상은 없다. 기존 입력 비차단, 일시정지, 배속별 최소 표시 시간,
종료·화면 숨김·즉시 결과 전환의 해제 경로를 재사용한다. Placeholder는 없다.

## 검증

- System.Drawing 검사: 전체 7종 원본/런타임 SHA-256 일치, 16:9 허용 오차, 불투명 픽셀 확인.
  결과: `output/sprite-sheet-validation/bunt-image-qa.json`.
- 신규 이미지 육안 검수: 현재 캐릭터 비율·모자·유니폼·손과 배트 자세·배경 일치 확인.
- Unity 6000.3.21f1 격리 프로젝트 컴파일 및 `OwnerMatchHighlightInsetTests` 25/25 통과.
  리소스 로드·번트 공개 사건 선택·결과 반복 방지·기존 도루/수비 매핑·배속과 해제 경로 검증.
- Unity Visual 실행 종료 코드 0. 1280×720, 1920×1080, 2560×1440, 3440×1440의
  `output/sprite-sheet-validation/inset-Bunt-*.png`를 육안 확인했다. 그림·설명 잘림 없음,
  우측 패널 경계 준수, 투구 상세와의 중첩 없음, 입력 비차단·종료 후 상세 복원을 자동 확인했다.
- 실제 경기의 3개 관전 모드×3개 배속 재생에서 최종 이벤트 954개·점수 10:11 일치,
  재생 모드의 일시정지·즉시 결과 전환 검증 통과. 이 고정 Seed 경기에는 번트 시도가 없어
  번트 사건 선택은 EditMode 사건 테스트, 그림 표시는 실제 UI 정적 합성으로 각각 검증했다.
- 원본 프로젝트의 Play Mode 수동 입력·게임패드 검증은 수행하지 않았다.
- 시뮬레이션 규칙·밸런스·세이브 구조는 변경하지 않는다. 대량 밸런스 검증 대상이 아니다.
