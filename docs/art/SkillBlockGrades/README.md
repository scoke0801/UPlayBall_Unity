# 스킬 블록 등급 원화

ImageGen에서 신규 타일 30개와 등급 배지 6개를 생성했다. 상세 설계·게임 연결·검증은
[스킬 블록 등급 개편](../../reports/skill-block-grades.md)을 따른다.

- `Tiles-Source.png`, `Badges-Source.png`: 생성 원본. 생성기 원본 경로의 파일도 보존했다.
- `Tiles-Transparent.png`, `Badges-Transparent.png`: 실제 알파를 보존한 작업 원본.
- `Build-Tiles.ps1`: 타일 90개·배지 6개·Unity 메타·합성 검수 이미지를 추출한다.
- `Tiles-Review.png`, `Badges-Review.png`: 밝고 어두운 배경, 작은 표시 크기 검수.
- `Alpha-Validation.txt`: 배지별 투명·부분 투명·불투명 픽셀 수.
- `Fusion-Validation.txt`, `Grade-Validation.txt`, `Match-Validation.txt`, `Match-SingleBlock-Validation.txt`: 에디터 밖 콘솔 결과.

생성 방향: 정면 새틴 금속·에나멜 UI 자산. C 은색/아이보리 → B 사파이어 → A 자수정 → S 금색 →
SS 루비·금장 → SSS 남색·오팔·금은 세공. 타일은 6열×5행(고립, 오른쪽 연결, 오른쪽/아래 꺾임,
좌우 직선, 좌우/아래 T 연결), 배지는 6열×1행 육각 형태와 C/B/A/S/SS/SSS 각인.
초록 단색 배경을 요청했으나 결과에 실제 알파가 있어 크로마키 제거를 추가 실행하지 않았다.
