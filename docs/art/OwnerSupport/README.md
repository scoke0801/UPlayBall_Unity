# 서포트 배지

ImageGen으로 제작한 구단주 서포트 카드용 공용 아이콘이다. 캐릭터 원화의 무광 피규어 재질과 부드러운 셰이딩에 맞춰 네이비·아이보리·골드의 날개 달린 야구 방패를 사용했다. 글자와 숫자는 이미지에 넣지 않고 UI가 남은 경기 수를 표시한다.

- 생성 원본: `support_badge_source_v1.png` — 균일한 `#00FF00` 크로마키 배경.
- 최종 PNG: `Assets/10.Datas/Resources/UI/PlayerGrowthBadges/Support_v1.png`.
- 후처리: `Tools/ImageBackground/Remove-ImageBackground.ps1 -Mode ChromaKey -KeyColor '#00FF00'`.
- 검수 합성: `review_v1.png`, 알파 검수: `review_v1.png.json`.
- 크기 1254×1254. 완전 투명 898,098픽셀, 부분 알파 3,768픽셀, 불투명 670,650픽셀. 모서리 네 곳은 투명하다.
- 밝은 배경·어두운 배경 합성을 열어 외곽선과 방패 내부 틈을 확인했다. 생성 원본과 최종 투명 파일을 함께 보존한다.
- `PlayerCardGrowthBadgesView`가 활성 서포트와 남은 1~2경기를 읽어 미니 카드·상세 카드에 연결한다.
