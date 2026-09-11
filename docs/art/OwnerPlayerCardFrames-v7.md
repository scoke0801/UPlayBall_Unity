# 카드 이름표 정렬 v7

- MVP 미니 원화만 Imagegen으로 재제작했다. 은색 이름표를 노멀·올스타·골든글러브 미니와 같은 높이로 내리고 MVP 문장을 바로 위에 배치했다.
- 적용 리소스: `Assets/Resources/UI/PlayerCards/PlayerCard_Mini_MVP_v7.png`. 생성 원본 사본은 `output/imagegen/player-card-frames-v7/`에 보존했다.
- 메인 원화는 변경하지 않았다. `OwnerPlayerCardFrames.GetNameRect`에서 등급별 명찰 중앙으로 이름과 연도 영역을 함께 정렬한다. 레전드는 문장 아래의 빈 명찰 영역을 기준으로 한다.
- 모든 등급의 기존 공통 초상 크기와 배경 → 초상 → 장식 순서를 유지한다.

## 검증

- `dotnet build Baseball.Presentation.Tests.csproj --no-restore -v quiet`: 경고 0, 오류 0.
- Unity 6000.3.21f1 격리 프로젝트의 `OwnerPlayerCardFrameTests`: 24개 통과, 실패·생략 0.
- 실제 UI 합성 캡처에서 메인 8종의 이름 정렬과 MVP 미니 이름표 높이를 확인했다. 실행 중인 본 프로젝트의 Play Mode 검증은 수행하지 않았다.
- 결과 XML: `output/CardNameV7-EditMode.xml`.
- 캡처: [MVP 미니 비교](../reports/card-name-alignment-v7/Mini_AllEditions_640.png), [MVP 메인](../reports/card-name-alignment-v7/Full_MVP_456.png), [레전드 메인](../reports/card-name-alignment-v7/Full_Legend_456.png).
