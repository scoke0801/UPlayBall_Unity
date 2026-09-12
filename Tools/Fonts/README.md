# 프로젝트 기본 폰트

`Assets/08.Fonts`의 이사만루 TTF 세 굵기에서 동적 TMP SDF 에셋을 생성한다. OTF는 동일 굵기의 대체 원본으로 보존한다.

- TMP 기본: `TMP Settings` → `esamanru Medium SDF`
- 기존 uGUI Text 기본: `UIProjectFonts.Default` → `Fonts/DefaultFonts` → `esamanru Medium.ttf`
- uGUI 생성 컴포넌트: `UIProjectText` (`Text` 상속). 부모 패널의 확대 배율을 동적 아틀라스 생성 밀도에 반영한다. 기본 Medium TTF는 작은 글자의 획 정렬을 위해 Hinted Smooth를 사용한다.
- SDF: 90pt, padding 9, 2048×2048, Dynamic, Multi Atlas. 생성 시 ASCII와 대표 한글의 추가를 검증한다. Unity의 동적 데이터 정리 이후에도 원본 TTF에서 필요한 글자를 다시 추가한다.
- Light/Medium/Bold는 TMP의 300/400·500/700 굵기에 연결한다.

`ProjectFontAssetBuilder.cs`는 분리된 Unity 작업 프로젝트의 `Assets/Editor`에 복사하여 실행하는 생성 진입점이다. 원본 폰트와 `UIProjectFonts.cs` 및 동일 GUID의 meta를 복사하고 uGUI 패키지의 TMP Essential Resources를 먼저 가져온 다음 `-executeMethod Baseball.Editor.Fonts.ProjectFontAssetBuilder.Build`로 실행한다. 생성 결과의 폰트 폴더와 TMP 리소스를 meta와 함께 프로젝트에 반영한다. 기존 에셋을 덮어쓰는 재생성 도구가 아니므로 빈 작업 프로젝트에서 실행한다.

## 검증

- Unity 6000.3.21f1: 세 굵기 생성, ASCII·대표 한글 추가, 저장 후 TMP/uGUI Medium 참조 검증 통과.
- `ProjectFontAssetVerifier.Verify`: 1280×720 표본 렌더링을 육안 확인했다. 결과는 `output/font-generation/font-preview.png`에 있다.
- 최신 Presentation 소스를 임시 빌드 목록에 포함한 `dotnet build`: 오류·경고 0건. 생성된 csproj의 누락 파일은 원본 csproj 수정 없이 보조 targets로 포함했다.
- 반영된 기본 에셋 GUID·Dynamic·Multi Atlas 설정과 기존 내장 폰트 참조 제거를 확인했다.
- 공통 폰트만 교체했으며 셸·Skin·레이아웃·입력 흐름은 기존 구현을 재사용한다. 전체 게임 화면의 해상도별 잘림·배치 검증은 수행하지 않았다.
