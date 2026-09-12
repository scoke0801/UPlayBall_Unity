# 스킬 블록 공용 타일

사용자 레퍼런스에 따라 얇은 베벨과 중앙 문양으로 교체했다. 일반·레어는 원형 홈,
엘리트·유니크·전설은 별 문양을 사용한다. 등급 색은 기존 `SkillBlockVisual.GetRarityColor`를 유지한다.

내장 ImageGen으로 생성했으며 `skill_tiles_v3_source.png`가 채택한 원본이다.
`skill_tile_v2_source.png`는 최초 원형 시안으로 보존하며 런타임에서는 사용하지 않는다.
최종 자산은 `Assets/10.Datas/Resources/UI/OwnerPowerUp/skill_tile_circle_v3.png`와
`skill_tile_star_v3.png`다. `SkillBlockVisual.ApplyTile`이 두 자산을 캐시하고 등급을 해석한다.
장착 화면의 성장판·인벤토리·회전 미리보기와 선수 카드 뒷면이 이 계약을 공유한다.

## 생성과 추출

원본의 균일한 초록 배경을 `Tools/ImageBackground/Remove-ImageBackground.ps1 -Mode ChromaKey
-KeyColor '#00FF00'`로 제거했다. 1774×887 원본에서 투명 790,337픽셀, 부분 투명 5,458픽셀을 확인했다.
좌우 절반 각각에서 alpha > 20인 영역을 잘라 256×256 PNG에 2픽셀 여백으로 축소했다.
밝고 어두운 배경에서 92px·24px 등급별 합성을 검사했다 (`output/skill-tiles-v3-preview.png`).
카드 뒷면의 배치 회전과 별 문양의 정방향을 분리해 회전해도 별이 뒤집히지 않는다.

## 채택 프롬프트

내장 ImageGen에 전달한 원문은 `prompt.txt`에 보존한다. 단색 배경 이외의 투명 표현을
생성기에 맡기지 않고 후처리했으며, 패널·텍스트·입력은 기존 Native UI를 사용한다.

## Unity 검증

- Unity 6000.3.21f1 격리 프로젝트 컴파일 성공. 관련 60개 테스트 중 55개 통과, 5개 실패.
- 카드 뒷면 34개 전부 통과. 표준 7종 모양의 등급별 텍스처·색상·회전·문양 정방향을 검증했다.
- 스킬/유학 화면 26개 중 21개 통과. 스크롤 유지 등은 통과했지만 선택 선수 기대값,
  미니 카드 이름 위치, 배치 버튼 활성화 2개, 야수 유학 핀 겹침 1개는 실패했다.
- 검수 복사본의 스킬 타일을 기존 v1 경로로 돌린 비교 실행에서도 같은 5개가 동일하게 실패했다.
  원본의 범위 밖 기능은 수정하지 않았다. 따라서 전체 화면의 입력·포커스 품질이 완료됐다고 보지 않는다.
- 장착 화면과 원형/별 혼합 4×4 카드 뒷면을 1280×720, 1920×1080, 2560×1440,
  3440×1440에서 캡처해 확인했다. 새 타일의 배경 잔색·잘림·프레임 침범은 보이지 않는다.
  전체 Shell·모든 입력 장치의 수동 조작은 미검증이다.
- 결과: `output/skill-block-ux-validation/tiles-v3-results.xml`, 비교 결과 `tiles-v3-baseline.xml`.
  캡처: 같은 폴더의 `tiles-v3-screenshots/Skills-1920.png`, `hitter-1920x1080.png` 등.
- 경기 밸런스·저장 구조는 변경하지 않았으며 대량 경기 통계 검증 대상이 아니다.
