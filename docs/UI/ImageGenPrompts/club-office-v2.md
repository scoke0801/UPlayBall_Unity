# 구단 업무 화면 이미지와 적용 기록

시설·코칭스태프·계약·트레이드를 구단 정보 화면의 흰 작업면, 청색 구분선, 짙은 본문 글자에 맞췄다.
수치와 버튼은 uGUI로 표시하며 생성 이미지에는 게임 정보를 굽지 않는다.

- 생성 방식: 내장 ImageGen.
- 자산: `Assets/Resources/UI/Generated/club_office_artwork_v2.png`
- 사용: `UIClubOfficeStyle.LoadArtwork`에서 3×3 아틀라스를 분리해 캐시한다. 경계의 흰 선이 카드에 섞이지 않도록 각 타일 안쪽 2px를 사용한다.
- 읽기 순서: 스카우팅, 훈련, 회복 / 분석, 전술, 팬샵 / 구장, 코칭 사무실, 협상실.
- 시설: 이미지·단계·현재 효과·비용·업그레이드를 묶은 가변 1/2열 카드.
- 스태프: 현재 역할 카드, 스크롤 가능한 후보/상세, 제안 ID 기준 선택 유지.
- 계약/트레이드: 두 줄 선수 목록, 청색 선택 표시선, 계약 조건 요약, 스크롤 가능한 상세, 고정 최소 너비 실행 버튼.
- 시뮬레이션과 밸런스 수치는 변경하지 않았다.

## 검증

- 현재 Presentation 소스 전체 및 기존 Presentation.Tests 보조 컴파일: 경고 0, 오류 0.
- 새 partial 파일의 생성 csproj 반영 지연은 임시 `.tmp/ClubOfficeCompile.targets`로 소스 전체를 포함해 확인했다.
- 변경 파일의 공백 오류 검사 통과.
- Unity EditMode 별도 실행은 초기 종료되어 결과 XML을 얻지 못했다. 테스트 실행 통과로 간주하지 않는다.
- 생성 아틀라스는 시각 확인했다. 실제 Unity 화면 렌더링·해상도별 입력 QA는 미완료다.

## 최종 생성 프롬프트

Use case: stylized-concept. Asset type: production environment artwork atlas for a Korean baseball management game's facility, staff and player negotiations UI. Create ONE image as a precise seamless 3 columns by 3 rows contact sheet of NINE equal-sized rectangular illustrations, no gutters, no borders. Overall landscape 1536x1024. Each tile is independently composed, no objects cross tile edges. Read order: top left scouting office with binoculars, baseball scouting reports and field beyond window; top center indoor batting and strength training center; top right modern sports recovery treatment room; middle left baseball data analysis room with abstract blue charts without readable text; middle center baseball tactical planning room with diamond board; middle right ballpark fan merchandise shop with generic caps and jerseys; bottom left immaculate baseball stadium viewed from stands in daylight; bottom center coaching staff office overlooking field with jackets, clipboard and baseball equipment, no people; bottom right elegant contract negotiation desk with two chairs, blank papers, pen and baseball. Style: polished restrained semi-realistic game environment illustration, soft daylight, clean architectural details, navy and steel blue accents, warm white interiors, green ballfield accents. Matches crisp white and blue rectangular sports management UI. Environments fill each tile, clear silhouettes at thumbnail sizes. NO text, letters, numbers, logos, watermark, UI widgets or graphic frames.
