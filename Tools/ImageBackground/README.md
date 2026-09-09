# 생성 이미지 배경 제거

향후 배경 제거용 생성 원본은 균일한 단색 크로마키를 사용한다. 기본 녹색 `#00FF00`, 전경과 겹치면
마젠타 `#FF00FF` 등 다른 색을 프롬프트에 명시한다. 배경 질감·그라데이션·그림자·체크무늬와 전경 색 번짐은 금지한다.
크로마키 제거와 경계 보정을 지원한다. 프런트 매니저 03은 민트 장식 보존을 위해 마젠타를 사용한다.
아래 첫 명령은 기존 무채색 원본용이며, 신규 단색 원본은 아래 크로마키 작업 순서를 따른다.

Windows PowerShell 5.1과 System.Drawing만 사용하는 로컬 도구다. 외부 업로드·모델·API 키가 필요 없다.
이미지 생성 후 투명 배경이 필요하면 이 도구를 실행한다. 체크무늬가 그려진 RGB 이미지도 실제 RGBA PNG로 변환한다.

```powershell
powershell -NoProfile -File Tools/ImageBackground/Remove-ImageBackground.ps1 -InputPath source.png -OutputPath output/imagegen/portrait.png
```

- `MaxChroma` (기본 12): RGB 최댓값과 최솟값 차이가 이 값 이하인 배경 후보.
- `MinBrightness` (기본 105): RGB 최솟값의 배경 후보 하한.
- `ProtectPolygonPath`: 원본 픽셀 좌표 `[[x,y], ...]`의 JSON 다각형. 해당 영역은 전경으로 보존한다.
- 얼굴·의상 등 보호 영역이 떨어져 있으면 `[[[x,y], ...], [[x,y], ...]]`처럼 다각형 배열을 지정한다. 좌표는 각 원본 이미지에 맞춰 작성한다.
- `RemoveEnclosedBackground`: 머리카락·팔 사이에 둘러싸여 외곽과 연결되지 않은 배경 후보도 제거한다. 흰 눈·치아·의상을 함께 지우지 않도록 `ProtectPolygonPath`가 필수다. 보호 영역 밖의 저채도 전경도 제거될 수 있으므로 합성 검증을 반드시 수행한다.
- 무채색 모드는 가장 큰 연결 전경과 보호 다각형에 걸친 연결 전경을 보존한다.
- 복잡한 장면의 의미 기반 분할 기능은 없다. 무채색 배경과 겹치는 의상은 보호 다각형이 필요하다.
- 기존 알파는 증가시키지 않는다. `EdgeMatteRadius`(0~8)를 지정하면 무채색 경계 혼합색도 보정한다. `MaxBrightness`는 배경 밝기 상한, `ProtectBrightThreshold`는 보호 다각형에서 이어지는 밝은 의상 확장 기준이다(보호 다각형 필수).
- 출력이 이미 있으면 중단한다. 원본 및 기존 결과를 덮어쓰지 않는다.

## 이번 선수 초상화

`figurine-shirt-protection.json`은 **1254×1254인 이번 입력만을 위한** 흰 유니폼 보호 영역이다.
다른 이미지에 재사용하지 않는다. 생성 원본은 `output/imagegen/player-figurine-profile-source.png`에 보존한다.

```powershell
powershell -NoProfile -File Tools/ImageBackground/Remove-ImageBackground.ps1 -InputPath output/imagegen/player-figurine-profile-source.png -OutputPath output/imagegen/player-figurine-profile-transparent.png -ProtectPolygonPath Tools/ImageBackground/figurine-shirt-protection.json
```

검증: 출력이 RGBA인지, 외곽 알파가 0인지, 얼굴·모자·흰 유니폼 내부가 불투명인지 확인한다.
밝고 어두운 배경에 합성해 경계 잔여와 의상 손실을 확인한다. 이번 시안은 원본 크기로 출력하며 Unity 자산 등록은 별도 작업이다.

## 이전 무채색 프런트 매니저 03 실험

생성 원본과 이미지별 얼굴·유니폼 보호 다각형은 `output/imagegen/front-manager-03/`에 보존한다.
내부 체크무늬 제거 예시:

```powershell
powershell -NoProfile -File Tools/ImageBackground/Remove-ImageBackground.ps1 -InputPath output/imagegen/front-manager-03/Concerned-source.png -OutputPath output/imagegen/front-manager-03/Concerned-transparent.png -ProtectPolygonPath output/imagegen/front-manager-03/Concerned-protection.json -RemoveEnclosedBackground
```

`alpha-review.png`는 밝고 어두운 배경 합성 검토용이며 납품 PNG는 별도 투명 이미지다.
도구 회귀 검증은 `powershell -NoProfile -File Tools/ImageBackground/Test-ImageBackground.ps1`로 실행한다.

## 크로마키 작업 순서

원본 생성 시 전경에 없는 채도 높은 단색을 지정한다. 녹색 의상에는 마젠타를 쓴다.
전경과 키 색이 겹치면 구분할 수 없으므로 다른 키로 생성한다. 단색 배경에 생긴 약한 색 오차는 허용값으로 흡수한다.

```powershell
powershell -NoProfile -File Tools/ImageBackground/Remove-ImageBackground.ps1 -InputPath source.png -OutputPath transparent.png -Mode ChromaKey -KeyColor '#FF00FF' -KeyTolerance 48 -KeyOpaqueDistance 255 -KeyEdgeRadius 12
powershell -NoProfile -File Tools/ImageBackground/Test-ImageBackground.ps1
# 아래 명령은 PowerShell 안에서 호출한다.
& Tools/ImageBackground/Review-ImageBackground.ps1 -InputPaths @('transparent.png') -OutputPath review.png
```

- `Mode` 기본값은 기존 호출 호환을 위한 `Neutral`. 신규 단색 원본에는 `ChromaKey`를 명시한다.
- `KeyColor`: RGB 16진수 키 색. 기본 마젠타 `#FF00FF`. 회색·흰색 배경은 Neutral 모드를 사용한다.
- `KeyTolerance`(기본 24): 각 RGB 채널의 최대 오차. 이 범위는 내부 틈까지 완전 투명화한다. 전경색과 겹치게 과도하게 늘리지 않는다.
- `KeyOpaqueDistance`(기본 180): 키에서 이 거리 이상 떨어진 색은 경계 복원 대상에서 제외한다. 255는 전경에 키 색이 없는 이번 마젠타 원본용 설정이다. 녹색 키와 민트 전경에 그대로 재사용하지 않는다.
- `KeyEdgeRadius`(기본 6, 최대 16): 배경 주위 경계 보정 폭. 0이면 단순 제거만 한다. 키 색이 강한 내부 틈도 주변 전경을 찾아 처리한다.
- 키 색이 섞인 경계는 근처 전경과 배경의 혼합 비율로 알파를 추정한다. 저알파에서 색 오차가 증폭되지 않도록 근처 전경색을 사용한다. 의미 기반 정밀 헤어 매팅은 아니므로 가는 머리카락은 합성으로 확인한다.
- 분리된 소품을 크기로 버리지 않으며 보호 다각형은 필요 없다. 무채색 보호 옵션과 혼용하면 중단한다.
- 출력 파일이 있거나 키 배경·전경을 찾지 못하면 실패한다. 원본은 보존한다.
- 검수 도구는 32비트 RGBA, 투명/반투명/불투명 픽셀 수, 네 모서리를 검사하고 밝고 어두운 배경 합성 및 JSON을 저장한다. 통계만으로 전경 손실을 판정할 수 없으므로 얼굴·의상·손·소품과 내부 틈을 눈으로 확인한다.

완성한 8종, 원본과 결과 해시, 재현 스크립트는 [프런트 매니저 03 기록](../../docs/art/FrontManager03/README.md)을 따른다.