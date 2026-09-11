# 관전 모션 보완 원본

Imagegen으로 생성한 크로마키 원본을 보존한다. 생성 원본은 실제 알파 PNG가 아니다.
경기용 RGBA 프레임과 접점 메타데이터는 `Tools/SpriteSheetPipeline/sprite_sheet_sources.json` 및
`Assets/04.Images/SpriteMatch/`를 따른다.

| 원본 | 용도 | 상태 |
| --- | --- | --- |
| catcher-idle-source.png | 후면 포수 대기, 4열 × 2행 | 왼손 글러브·오른손 보호 자세, 경기 연결 |
| ground-fielding-source.png | 땅볼 포구·송구, 4열 × 2행 | 셀 잘림 보완, 경기 연결 |
| runner-loop-source.png | 주루 반복, 2열 × 2행 | 팔·다리 교대 보완, 경기 연결 |
| runner-idle-source.png | 베이스 대기, 2열 × 2행 | 양발 접지·빈손 대기, 알파·경기 합성 검수 후 연결 |
| runner-rejected-source.png | 초기 8프레임 주루 후보 | 같은 쪽 다리가 반복되어 미사용 |
| runner-gait-draft.png | 주루 보정 전 후보 | 배경·의상 보완 전, 미사용 |

각 `*-prompt.txt`는 생성·보정 요청 기록이다. 하이라이트는 별도
[제작 기준](../HighlightSpriteProduction.md)을 따르며, 위 기본 모션의 승인으로 하이라이트까지 승인한 것은 아니다.

초기 연결 뒤 사용자 검수에서 포수 크기·원근 문제가 확인되어 `FieldLayout.asset`과
공통 투영 코드를 다시 조정하고 있다. 원본 시트 검수와 실제 경기 연속 재생 검수는 구분한다.
