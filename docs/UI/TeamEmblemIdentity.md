# 구단 이름과 엠블럼 연결

`Assets/10.Datas/Resources/TeamEmblems/TeamEmblemIdentities.json`에서 구단 별칭과 표시 이미지 ID를
연결한다. 지역명이나 리그 접두사를 제외한 마지막 단어가 조회 키다. 표기 변형은 `names`에 함께
등록한다. 구단 이름에 따른 분기를 코드에 추가하지 않는다.

기존 월드의 `EmblemId`는 월드 전체의 유일성 검증에 쓰이므로 그대로 보존한다. 표시할 때
카탈로그를 우선 조회하여 과거 구단 번호·난수 배정의 의미 불일치를 교정한다. 미등록 구단은
명시된 ID로 표시하고 유효한 ID도 없으면 이미지를 비운다. 구단 정보 화면의 이름 해시는 제거했다.

기존 4개 아틀라스의 ID 1~128은 유지한다. 추가 이미지
`Assets/10.Datas/Resources/TeamEmblems/TeamEmblems_Identity_01.png`는 4열 2행이며,
왼쪽 위부터 129 수호기사, 130 왕관, 131 타이탄, 132 티라노사우루스,
133 검치호, 134 해적, 135 삼지창, 136 용사 순이다. 원본 투명도를 보존한다.

새 별칭을 추가하면 카탈로그에 등록하고 이미지의 의미와 아틀라스 위치를 함께 확인한다.
`TeamEmblemSpritesTests`는 카탈로그 전체 이미지 로딩, 슬롯과 무관한 이름 매칭,
지역·리그 접두사, 미등록 구단의 대체 ID, Image 재바인딩을 검증한다.

검증: Unity 6000.3.21f1의 임시 분리 프로젝트에서 동일 코드·Resources·EditMode 테스트
16건 통과. 전체 Presentation 보조 컴파일은 작업 중인 경기 연출과 구단 시설 코드의
미해결 참조 19건으로 실패했다. 실제 게임 화면 Play Mode 검수는 수행하지 않았다.
이번 변경은 표시 이미지에 한정되며 시뮬레이션이나 밸런스 수치는 변경하지 않았다.

추가 아틀라스는 내장 image_gen 도구로 생성했다. 생성 프롬프트는 아래와 같다.

```text
Use case: logo-brand. Asset type: ONE production sprite atlas for fictional baseball club emblems. Generate a square PNG with genuinely transparent background, exactly 8 separate colorful polished mascot sports shield emblems in a strict 4 columns x 2 rows equal-cell grid. Each emblem centered in its cell with generous transparent padding, no touching neighboring cells. Top row left to right: 1 green and silver guardian knight helmet with prominent defensive shield (Guardians), 2 purple and gold royal crown (Crowns), 3 bronze giant titan warrior helmet (Titans), 4 green roaring Tyrannosaurus rex head (Rex). Bottom row left to right: 5 teal and gold saber-toothed tiger head with very long fangs (Sabers), 6 red and navy pirate raider with crossed cutlasses (Raiders), 7 ocean blue and gold trident (Tridents), 8 red and gold brave warrior helmet with crossed swords (Braves). Crisp bold dark outlines, metallic thin shield borders, flat shaded detailed illustration readable at 48 pixels. Match classic vivid sports mascot badges. All silhouettes similar size; each icon occupies at most 78% of cell width and 70% of cell height. No text, letters, numbers, captions, grid lines, watermark, shadows outside badges. Transparent background, not a checkerboard painted into the image.
```
