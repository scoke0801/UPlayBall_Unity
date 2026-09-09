# 프런트 매니저 03 크로마키 납품 기록

기존 인물·의상·상황별 포즈를 참조해 1024×1536 마젠타 배경 원본을 생성하고, 로컬 도구로 실제 RGBA PNG 8종을 만들었다. 민트색 장식과 키 색이 겹치지 않도록 `#FF00FF`를 선택했다.

- 최종 이미지: `Assets/10.Datas/FrontManager/Art/FrontManager_Default_03_{표정}.png`
- 표정: Neutral, Analysis, Calm, Celebrate, Concerned, Surprised, Warning, Welcome
- 생성 원본: [sources](sources), 공통 편집 프롬프트: [chroma-prompt.txt](chroma-prompt.txt). 각 호출 끝에 `Variant: {표정}.`을 추가했다.
- 제거 설정과 원본·결과 SHA256: [chroma-manifest.json](chroma-manifest.json)
- 실제 프로젝트 PNG 합성: [review.png](review.png), 알파 수치: [review.png.json](review.png.json)
- 도구 회귀 검사: [tool-tests.txt](tool-tests.txt)

전체 8종의 32비트 RGBA, 네 모서리 투명, 불투명 전경과 반투명 경계를 확인했다. 밝은 배경·어두운 배경 합성과 원본 크기 주요 이미지에서 얼굴·의상·손·태블릿과 머리카락 틈을 검수했다. 동일 원본과 설정을 다시 실행한 8종의 파일 해시가 납품 결과와 일치한다.

마젠타·녹색 합성 입력으로 외곽/내부 배경, 흰 의상·민트색·갈색 머리, 분리된 소품, 반투명 경계, 기존 알파, 잘못된 키 거부, 기존 출력과 원본 보존, 결정론을 검사했다. Unity 실행과 게임 내 선택 화면 검증은 하지 않았다. 이번 납품은 이미지와 제작 도구이며 매니저 03의 런타임 선택 등록은 포함하지 않는다.

프로젝트 루트의 PowerShell에서 다음 명령으로 재현한다. 출력 폴더에 같은 이름의 파일이 있으면 중단한다.

```powershell
& docs/art/FrontManager03/Rebuild.ps1 -OutputDirectory output/imagegen/front-manager-03/rebuilt
```

기존 프로젝트 PNG와 메타 백업은 `output/imagegen/front-manager-03/before-chroma-delivery/`에 있다. 프로젝트의 기존 `.meta` GUID는 유지했다. `generation-manifest.json`과 `*-protection.json`은 이전 무채색 제거 실험 기록으로, 이번 크로마키 재현에는 사용하지 않는다. 기존 체크무늬 생성 원본도 `output/imagegen/front-manager-03/*-source.png`에 남아 있다.

도구의 색 분리는 전경에 키 색이 없다는 전제다. 다른 의상에는 키 색과 설정을 다시 선택하고 합성 검수를 반복한다. 사용법은 [배경 제거 도구](../../../Tools/ImageBackground/README.md)를 따른다.
