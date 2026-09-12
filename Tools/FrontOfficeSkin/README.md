# 프런트 오피스 V2 이미지 스킨 제작

## 실제 화면 연결 현황

V2는 이제 구단주 화면의 공통 팩토리·버튼 역할·홈 상태 갱신에 연결된다.
아래의 검수 전용 설명은 최초 에셋 제작 당시 기록이다. 현재 적용 범위와 유지한 기존 표현,
이번 검증 결과는 [런타임 연결 보고서](../../docs/reports/owner-front-office-v2-runtime.md)를 따른다.
Unity를 실행하지 않는 임포트 계약 확인은 `Verify-RuntimeIntegration.ps1`로 실행한다.

현재 프런트 매니저 원화의 부드러운 반실사·새틴 피규어 셰이딩을 재질 기준으로 삼는다.
기존 더그아웃·캐릭터·글자·아이콘은 유지한다. PNG에는 텍스트나 아이콘을 구워 넣지 않는다.

생성 원본과 최종 프롬프트는 `output/imagegen/baseball-front-office-v2`, Unity 에셋은
`Assets/10.Datas/Resources/UI/BaseballFrontOffice/V2`에 새 버전으로 보존한다.
기존 에셋을 덮어쓰지 않으며 검수용 적용과 실제 기본 스킨 교체를 분리한다.

상세 분석·Border 표·실제 UI 프리뷰·적용 컴포넌트는
[산출물 보고서](../../docs/reports/owner-front-office-v2.md)를 참고한다.
전체 생성 프롬프트는 [Prompts.md](Prompts.md), PNG 명세는 [AssetManifest.json](AssetManifest.json)에 있다.

## 재가공

Windows PowerShell 5와 System.Drawing을 사용한다. Python이나 별도 이미지 생성 API는 필요하지 않다.

```powershell
& Tools/FrontOfficeSkin/Build-Assets.ps1
& Tools/FrontOfficeSkin/Verify-Assets.ps1
```

ImageGen 원본 29개가 `output/imagegen/baseball-front-office-v2/*-source.png`에 있어야 한다.
이 도구는 이미지 생성기가 아니다. 기존 생성 원본을 다음 순서로 후처리한다.

1. 프로젝트의 `Tools/ImageBackground/Remove-ImageBackground.ps1 -Mode ChromaKey`로 초록 배경 제거.
   실제 알파를 가진 Count/Important 배지 원본은 그대로 보존한다.
2. 투명 여백 절삭, 고해상도 모서리 유지, 단순 중앙을 9-slice로 정규화.
3. 동일 종류의 상태를 Normal 알파로 정렬하고 Hover/Pressed 중앙 휘도 조정.
4. 프레임 제목선을 콘텐츠 위에 고정하고 본문 알파를 부드럽게 조정.
5. 잔여 초록색 제거와 투명 가장자리 RGB 확장. PNG 및 Unity 메타데이터 기록.
6. `AlphaReport.csv`와 `Verification.json`에 실제 픽셀 검수 수치 기록.

최종 에셋은 ImageGen 원본에서 파생되며 생성 원본·키 제거본·정렬본을 모두 남긴다.
Build는 이 작업에서 만든 V2 출력만 다시 기록하며 기존 OwnerSkin은 읽기만 한다.
키 제거본이 이미 있으면 재사용한다. 생성 원본을 바꿀 때는 새로운 제작 버전 경로를 사용한다.

## Unity 검수

`OwnerFrontOfficeSkinTests`는 29 Sprite의 Full Rect/PPU/Clamp/Alpha/Mipmap 및 상태 크기를 검사하고,
실제 UI 버튼 상태를 전환한다. 모든 상태·프레임·배지와 9-slice 150/200/250%를 Unity 카메라로 캡처하고
`Assets/03.Prefabs/FrontOfficeV2/FrontOfficeSkinPreview.prefab`을 생성한다.
프리팹은 이 도구가 소유하는 정적 검수 산출물이므로 재검수 시 같은 이름으로 갱신한다.

`OwnerMainDashboardRevisionTests`의 `imageSkin = true` 케이스는 기존 홈 생성·Bind·리포트 전환 후
`UIOwnerFrontOfficeSkin.Apply(home.GuideDockTarget)`를 호출한다. 좌표·클릭·콜백은 기존 화면을 사용한다.
V2는 기본 홈에 자동 적용되지 않으므로 최종 선택 전까지 기존 화면을 보존한다.

검수 PNG는 프로젝트 루트의 `FrontOfficeScreenshots/`에 생성되며, 납품 사본은
`Assets/04.Images/UI/FrontOfficeV2/Preview/`에 둔다. 원본과 검수 결과는 런타임 Resources에 넣지 않는다.
