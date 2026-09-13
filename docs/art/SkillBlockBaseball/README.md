# 야구공 스킬 블록

사용자 최종 선택은 **야구공 아래 C/B/A/S/SS/SSS 문자**다. 블록마다 별도 등급 탭을 붙이지 않고,
방패·월계·보석을 제거했다. 타입은 바탕색, 등급은 문자, 같은 블록의 인접 칸은 흰 연결선으로 읽는다.

## 자산과 구현

- ImageGen 원본 `Source.png`, 크로마키 제거 `Transparent.png`, 추출 `Extract.ps1`을 보존한다.
- `Tools/ImageBackground/Remove-ImageBackground.ps1 -Mode ChromaKey -KeyColor '#FF00FF'`로 배경을 제거했다.
- 실제 Resources는 `UI/OwnerPowerUp/SkillBaseball/Plate`, `Ball`이다. 256px RGBA이며 런타임 합성을 위해 Read/Write를 사용한다.
- `Alpha-Review.png`는 밝고 어두운 배경에서 검수한 실제 추출 에셋이다.
- `SkillBlockVisual`은 ImageGen 바탕에 계통 Tint를 적용하고, 흰 연결선과 색상이 유지된 야구공을 합성한다.
  계통색·연결 마스크별 128px 텍스처를 공유하고 합성 후 CPU 픽셀 사본을 해제한다. 등급별 텍스처는 복제하지 않는다.
- `UISkillTileGradeLabel`은 프로젝트 Medium 폰트로 공 아래에 문자를 표시한다. 회전하는 것은 점유 칸과 연결 방향이며 문자는 정방향이다.
  원래 RawImage의 알파·Button 페이드·비활성을 따른다. Raycast는 원래 칸만 받는다.
- 구단주 배치·합성·연구·카드 뒷면, 선수 성장의 블록 미리보기·선수 화면, 상점 지급 결과에 공통 경로를 연결했다.
  추첨 전 계통 상점의 일반 모양 예시는 확정 등급 문자를 표시하지 않는다.
- `OwnerSkillBlockPlacementSnapshot`, `ShopSkillBlockRevealModel`에 계통을 전달한다. 표시 스냅샷 변경이며 저장 포맷은 바꾸지 않는다.

## UI 기준

- Skin Reference: 기존 `SkillBlockVisual`, `UIProjectFonts.Default`, 구단주 셸·버튼·툴팁.
- Layout Reference: 기존 성장판·블록 인벤토리·회전 미리보기·카드 뒷면.
- Metrics: 기존 칸 크기·패널·안전 영역 유지. 문자 영역은 칸 내부 x=8~92%, y=7~37%, 공은 y=39~81%다.
- 새로운 메뉴·상태·입력·포커스 경로는 없다. 기존 선택·빈 상태·미리보기·저장 실패 안내를 재사용한다.
- 타입의 실제 이름은 기존 한국어 라벨을 유지한다. 비슷한 색만으로 12계통을 외우게 하지 않는다.
- 기존 금장 90종 및 이전 시안은 원본 보존을 위해 삭제하지 않았다. 공용 블록 렌더러는 새 원화를 사용한다.

## 검증

- 추출 PNG 2개 모두 투명 모서리 확인. 밝고 어두운 합성 육안 확인.
- `dotnet build Baseball.Presentation.csproj --no-restore -v quiet` 보조 컴파일 오류·경고 0개.
  이 환경에서 Windows SDK 경로 조회 권한 오류가 생기면 `-p:TargetPlatformSdkPath=C:/unused -p:TargetPlatformDisplayName=Unity`를 덧붙인다.
  Unity가 갱신한 csproj로 확인했으며 자동 생성 csproj는 직접 편집하지 않았다.
- `SkillBlockBaseballPresentationTests`: 등급/타입 분리, 칸 재사용, 회전, 알파, 60·36·24px 칸 간격의 실제 Unity 캡처.
- 별도 프로젝트 `output/skill-block-ux-validation/UnityProject`에서 Unity 검수를 수행한다.
- **전용 EditMode 테스트 최종 6/6 통과**(`Grade-Results.xml`). `Unity-Grades.png`는 실제 Unity 렌더링이며 색상·등급 문자·연결선을 육안 확인했다.
- `Unity-Growth-{1280,1920,2560,3440}.png`: 1280×720, 1920×1080, 2560×1440, 3440×1440에서
  실제 성장 화면의 장착 4개·6계통·6등급을 캡처하고 육안 확인했다. 새 타일과 문자는 칸 안에 유지되며 패널을 침범하지 않는다.
  목록 하단은 기존 ScrollRect 마스크에서 정상적으로 잘린다. 캡처용 데이터이며 사용자의 세이브는 변경하지 않았다.
  [1920×1080 적용 화면](Unity-Growth-1920.png).
- Play Mode 수동 조작과 키보드·게임패드 전체 순회는 미검수다. 신규 그래픽의 Raycast 비활성은 전용 테스트로 확인했다.
- 기존 `OwnerGrowthPresentationTests`는 **27건 중 9통과·18실패**다(`Existing-Growth-Results.xml`).
  현재 목록 이름은 `BlockStack_*`인데 기존 테스트·캡처는 `Block_1`/`Block_10`을 찾는다.
  그 외 선수 선택 기대값·이름 영역·유학 지도 핀 중첩 실패도 있다. 이 작업에서 성장 화면 전체 테스트를 통과했다고 보고하지 않는다.
  해당 테스트/화면 전반의 수정은 요청 범위 밖이므로 임의로 고치지 않았다.
- 확률·능력치·경제 수치는 변경하지 않아 밸런스 대량 시뮬레이션 대상이 아니다.
