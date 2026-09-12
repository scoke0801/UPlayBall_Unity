# 스카우터 이미지 재제작 상태

## 최신 적용: 부드러운 v3 연결

`output/scout-soft-v3/final/`에 별도로 남아 있던 청년 남성·청년 여성·중장년 남성의
부드러운 v3 투명 PNG를 기존 Resources 파일에 적용했다. 아래 v2 확인 기록은 과거 상태이며,
v3 폴더를 누락한 채 최신본이라고 판단했던 기록이다. 중장년 여성은 v3 생성 원본·최종본이
해당 폴더에 없어 v2를 유지한다.

- 원본: `output/scout-soft-v3/raw/*-source.png`
- 최종본: `output/scout-soft-v3/final/*-transparent.png` (3종)
- 교체 전 백업: `output/scout-soft-v3/previous/`
- 적용: 기존 `scout_{young_male,young_female,senior_male}_v1.png` 내용 교체. 메타와 Resources 키 유지.
- 기본 화면과 방침 창은 기존 공통 로딩 경로로 v3를 사용한다.
- 적용 파일과 v3 최종본 SHA256 3종 일치. 1254×1254 RGBA 및 투명 모서리 확인.
- 밝고 어두운 배경 합성: `output/scout-soft-v3/review-connected.png`와 동일 이름 JSON.
- 사용자 요청에 따라 에디터 실행·컴파일·EditMode·Play Mode 테스트는 수행하지 않았다.

## 현재 적용본

남녀·연령별 4종의 v2 제작본이 모두 생성·배경 제거·Resources 교체까지 완료되어 있다.
재개 시 생성 원본, 투명 결과, 기존 이미지 백업을 확인했고, 적용 자산과 투명 결과의 SHA256이
4종 모두 일치했다. 추가 생성 없이 적용본의 검수와 기록을 마무리했다.

- 적용 폴더: `Assets/10.Datas/Resources/UI/OwnerPowerUp/Scouts/`
- 파일: `scout_young_male_v1.png`, `scout_young_female_v1.png`, `scout_senior_male_v1.png`, `scout_senior_female_v1.png`
- 제작 방식: 내장 ImageGen. [최종 프롬프트 4종](prompts-v2.md).
- 생성 원본: `output/scout-refresh/art/*-source.png`
- 투명 결과: `output/scout-refresh/art/*-transparent.png`
- 교체 전 백업: `output/scout-refresh/previous/*.png`

제작 버전은 v2지만 기존 Resources 참조를 유지하기 위해 자산 파일명의 `_v1`은 유지한다.
선수 피규어 앵커의 재질을 따르고 눈·홍조·몸 비례를 정리한 정면 상반신 구도다.
네 유형 모두 네이비 재킷과 밝은 셔츠를 사용한다.

## 재개 후 검수

실제 Resources PNG를 `Review-ImageBackground.ps1`로 다시 검사했다.
4종 모두 1254×1254이고 투명·반투명·불투명 픽셀이 존재하며 네 모서리는 투명하다.
밝고 어두운 배경 합성을 직접 확인했고 얼굴·의상 내부 손실과 눈에 띄는 녹색 테두리는 보이지 않았다.
검수 이미지와 픽셀 통계는 `output/scout-refresh/art/review-installed.png` 및
`review-installed.png.json`에 보존한다.

`UI_Scene_OwnerPowerUp.Scout.cs`의 네 Resources 키가 실제 자산과 일치하며,
기본 화면과 방침 화면이 같은 Texture2D를 소비한다. 네 메타 파일의 `alphaIsTransparency`는 1이다.

이번 재개에서는 이미지·연결 정적 검수만 수행했다. Unity 재임포트 후 화면 렌더링,
컴파일·EditMode·Play Mode 테스트는 새로 실행하지 않았다.
기존 UI 보고서의 10/10 테스트 결과는 이전 실행 기록이며 이번 v2 검증 결과로 취급하지 않는다.
