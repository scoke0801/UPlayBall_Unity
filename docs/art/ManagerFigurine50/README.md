# 프런트 매니저 3종 50% 피규어 재질 납품 기록

`docs/todo/manager-figurine-material-plan.html`의 50% 기준을 매니저 3명과 감정 8종, 총 24장에 적용했다. 원화 선을 일부 유지하고 얼굴·노출 피부·머리카락에 새틴 무광 레진 조형과 부드러운 볼륨 음영을 더했다. 성인 비율, 얼굴 정체성, 표정, 포즈, 손, 의상과 소품은 원본을 기준으로 유지했다.

- 생성 방식: Codex 내장 ImageGen 편집
- 재질 레퍼런스: [material-reference-383.jpg](references/material-reference-383.jpg)
- 공통 프롬프트와 인물별 보존 블록: [prompt.txt](prompt.txt)
- 크로마키 생성 원본: [sources](sources)
- 실제 투명 납품 PNG: 아래 `Assets/10.Datas/` 적용 경로
- 원본·생성·납품 SHA256과 제거 설정: [manifest.json](manifest.json)
- 교체 전 8종 비교판: `input-review-01.png`, `input-review-02.png`, `input-review-03.png`
- 밝고 어두운 배경 합성 검수: `review-01.png`, `review-02.png`, `review-03.png`

## 적용 경로

- 저작 자산: `Assets/10.Datas/FrontManager/Art/FrontManager_Default_0{1..3}_{Expression}.png`
- 런타임 자산: `Assets/10.Datas/Resources/FrontManager/FM_0{1..3}_{EXPRESSION}.png`
- 이전 로딩 경로 호환 별칭: `Assets/10.Datas/Resources/FrontManager/FM_{EXPRESSION}.png`는 매니저 01 결과와 동일하다.

기존 `.meta` 파일은 수정하지 않아 Unity GUID를 유지했다.

## 배경 제거와 검수

생성 원본은 1024×1536의 균일한 마젠타 `#FF00FF` 배경으로 출력했다. 다음 설정으로 프로젝트 도구를 사용해 RGBA PNG로 변환했다.

```powershell
powershell -NoProfile -File Tools/ImageBackground/Remove-ImageBackground.ps1 `
  -InputPath <source.png> -OutputPath <final.png> `
  -Mode ChromaKey -KeyColor '#FF00FF' `
  -KeyTolerance 48 -KeyOpaqueDistance 255 -KeyEdgeRadius 12
```

24장 모두 1024×1536, 32비트 RGBA, 네 모서리 투명, 투명 배경·불투명 전경·반투명 경계를 확인했다. 세 인물의 합성 검수판에서 밝고 어두운 배경의 경계 잔여, 얼굴·의상·손·태블릿·사원증 손실 여부를 확인했다. 코드 변경이 없는 이미지 교체이므로 컴파일 및 시뮬레이션 밸런스 테스트 대상은 아니다. Unity Editor의 실제 대화 화면 확인은 별도다.
