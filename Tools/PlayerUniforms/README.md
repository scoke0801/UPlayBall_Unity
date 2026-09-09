# 선수 유니폼 변환

## 구단 계보별 발급

현재 게임은 아래 10개 유니폼 계보를 사용한다. `lineage-uniforms.json`이 분류 정본이며,
런타임은 표시 이름이 아닌 원본 시즌의 Franchise ID로 조회한다. 쌍방울·SK·SSG 및
현대 이전 역사·키움의 통합은 이 게임의 유니폼 분류 규칙이다.

| 계보 | 유니폼 | 디자인 |
| --- | --- | --- |
| OB → 두산 | uniform-01 | 네이비 홈 |
| 해태 → KIA | uniform-02 | 버건디 아이보리 |
| NC | uniform-03 | 틸 골드 |
| 빙그레 → 한화 | uniform-04 | 블랙 오렌지 |
| 삼성 | uniform-05 | 로열블루 원정 |
| MBC → LG | uniform-06 | 레드 핀스트라이프 |
| 쌍방울 → SK → SSG | uniform-07 | 포레스트 크림 |
| 삼미 → 청보 → 태평양 → 현대 → 우리·히어로즈 → 넥센 → 키움 | uniform-08 | 퍼플 라벤더 |
| 롯데 | uniform-09 | 네이비 스카이 |
| KT | uniform-10 | 브라운 샌드 |

기존 가상 디자인 10종을 배정한 것으로, 실제 구단 유니폼을 재현한 배색이라는 뜻은 아니다.
같은 선수의 얼굴은 기존 PersonId 배정을 유지한다. 다른 계보에서 뛴 시즌은 옷만 달라지고,
카드 등급·강화·현재 보유 구단은 원본 시즌 유니폼을 바꾸지 않는다. 시즌 ID가 없는 인물 단독
조회와 생성 커리어 선수는 기존 기본 의상을 유지하며 임의의 역사 계보를 배정하지 않는다.

완성된 `uniforms-v3` 이미지 5,760개를 그대로 `Assets/Resources/UI/Portraits/Uniforms/`에
Single Sprite로 등록했다. 원본 12개 Franchise ID를 10개 의상 계보로 묶고 시즌 18,326건을
`player_uniform_assignments.json`에 연결한다. Core/Simulation·선수 능력·경기 RNG·세이브는 바꾸지 않는다.
초상은 `PlayerPortraitSprites`에서 필요한 외형·유니폼 조합만 로드하고 재사용한다.
미니 카드·도감·라인업·상세 앞뒷면은 원본 시즌 ID를 전달한다.

```powershell
# 처음 등록하거나 분류 정책·시즌 콘텐츠를 갱신할 때 실행한다.
powershell -NoProfile -File Tools/PlayerUniforms/Publish-LineageUniforms.ps1
# 파일을 다시 발급하지 않고 현재 정본과 등록 결과를 대조한다.
powershell -NoProfile -File Tools/PlayerUniforms/Publish-LineageUniforms.ps1 -VerifyOnly
powershell -NoProfile -File Tools/PlayerPortraits/Run-UnityTests.ps1 -ValidationDirectory output/uniform-validation
```

검증 결과: 5,760개 제작 원본·등록 PNG 해시 일치, 조합 누락 0건, 고유 GUID·Sprite 설정 통과,
시즌 18,326건 전체 의상 발급 확인. `output/portrait-validation/uniform-issuance-audit.json`에 기록한다.
별도 Unity 6000.3.21f1 프로젝트에서 EditMode **4개 통과, 0개 실패**
(`output/uniform-validation/test-results.xml`). 전체 시즌·Normal/Rare/Legend의 실제 Resources
조회와 얼굴 유지, 10개 계보·역사 구단 통합, 생성 선수 얼굴 유지가 검증 대상이다.
최신 Presentation 소스 목록을 임시 MSBuild targets로 보충한 보조 컴파일도 오류·경고 0건이다.
전체 게임 Play Mode 화면 검증은 수행하지 않았다. 비교판은 `uniforms-v3/previews/uniforms-10.png`를 확인했다.

## 이미지 제작 도구

네이비 모자·흰 셔츠의 정면 피규어 시트에서 인물별 의상 마스크를 추출하고,
얼굴·머리·피부를 유지한 채 유니폼 10종을 출력한다. 외부 모델이나 API를 사용하지 않는다.
색과 핀스트라이프는 `uniforms.json`에서 변경한다.

```powershell
powershell -NoProfile -File Tools/PlayerUniforms/Test-Uniforms.ps1
powershell -NoProfile -File Tools/PlayerUniforms/Build-Uniforms.ps1 -FirstSheet 1 -LastSheet 36 -OutputRoot output/imagegen/player-portraits/uniforms-v3
powershell -NoProfile -File Tools/PlayerUniforms/Review-Uniforms.ps1 -OutputRoot output/imagegen/player-portraits/uniforms-v3
```

기존 출력이 있으면 새 버전 폴더를 지정한다. 완료 시트는 `manifests/sheet-NNN.json`으로 확인한다.
중단 시 `FirstSheet`/`LastSheet`로 미완료 범위만 새 출력 폴더에서 재실행할 수 있다.
입력 선택은 `production-576-v1/manifests/generation-576-resume-audit.json`을 사용하며 해시 불일치를 차단한다.

## 산출물

- `portraits/uniform-01/face-0001__uniform-01.png`: `AppearanceId × UniformId`로 조회하는 RGBA 원본 해상도 이미지.
- `sources/raw`, `sources/transparent`: 의상 변환 전 인물별 원본과 투명화 결과.
- `masks/face-NNNN.png`: 빨강 채널은 모자, 초록은 셔츠, 파랑은 파이핑·이너 가중치. 검정은 보존 영역.
- `catalog.json`: 전체 조합의 상대 경로, 해시, 보호 영역 검증 결과.
- `index.html`: 선수 번호와 배경을 바꾸며 10종을 비교하는 로컬 갤러리. 브라우저에서 바로 연다.
- `previews/uniforms-10.png`: 같은 얼굴 세 명으로 비교하는 10종 유니폼.
- `previews/review-01.png`~`review-06.png`: 576명 전체를 밝고 어두운 배경에 배치한 검수판.

배경 제거는 기존 `Tools/ImageBackground/Remove-ImageBackground.ps1 -Mode ChromaKey`를
인물별로 호출한다. 이번 원본은 전경에 녹색이 없어 `KeyTolerance 48`, `KeyOpaqueDistance 255`,
`KeyEdgeRadius 12`를 사용한다. 색 변환은 **투명화 이후** 수행하므로 녹색 유니폼도 제거되지 않는다.

## 보존과 한계

마스크는 각 인물의 픽셀에서 별도로 추출한다. 정규화 높이 0.48~0.74의 얼굴 중앙 영역과
따뜻한 피부·머리 색상은 변환 시 독립적으로 보호 검사한다. 눈의 흰색을 셔츠로 해석하지 않는다.
저장 PNG를 다시 읽어 마스크 밖 RGBA와 전체 알파의 변경이 있으면 실패한다.
명암은 원본 밝기로 유지하며 의상만 색을 바꾼다. 핀스트라이프는 셔츠 마스크 내부에 적용한다.

이 도구는 이번 정면·동일 유니폼 원본에 맞춘 색/위치 규칙이며 범용 의상 분할기가 아니다.
다른 화풍·포즈·머리색·조명·재단은 새 마스크 검수가 필요하다. 크로마키 제거 전후의
가장자리 픽셀은 매팅으로 달라질 수 있다. 의상 변환의 픽셀 보존 기준은 투명화한 원본이다.
표준 크기 리샘플링은 하지 않아 원본 셀에 따라 313~314 × 310~313px 크기를 유지한다.
전체 얼굴 고유성·계보 배정·Unity 임포트/카드 표시 검증은 이 도구의 검증 대상과 별도다.

이번 디자인은 모자와 챙을 같은 색, 몸판과 소매를 같은 색으로 유지한다.
소매 절개나 챙 경계를 임의 추정해 선을 만드는 대신 작은 카드에서 읽히는 색 조합과
파이핑·핀스트라이프로 구단을 구분한다. 제작 당시 미배정 디자인 10종을 위 계보 표로 연결했다.
