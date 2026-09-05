# UI Asset 생성 계획

## 원칙

- 실제 Panel/Button/Tab/Table/Text/Graph는 uGUI와 공통 Sprite/Token으로 구현한다.
- ImageGen은 분위기와 재질이 필요한 배경만 사용한다.
- 실제 구단·리그 로고, 실제 선수, UI Text/숫자, 완성 화면을 이미지에 넣지 않는다.
- 최종 사용 자산만 Runtime에서 직접 해석 가능한 `Assets/Resources/UI/Generated/`에 저장하고 Prompt와 Import 설정을 기록한다.

## 계획 자산

| Asset | 목적 | 목표 해상도 | 9-Slice | 상태 |
|---|---|---:|---:|---|
| `bg_player_clubhouse_v1.png` | Player Home 저대비 배경 | 1672×941 생성, 1920×1080 Canvas scale 검증 필요 | 아니오 | 생성·Player Home 적용 |
| `bg_owner_front_office_v1.png` | Owner Home 저대비 배경 | 1672×941 생성, 1920×1080 Canvas scale 검증 필요 | 아니오 | 생성, Owner Runtime 연결 대기 |
| `bg_match_stadium_neutral_v1.png` | Match 공용 가상 구장 배경 후보 | 1920×1080 이상 | 아니오 | 기존 경기 배경 평가 후 결정 |

정밀 Panel/Slot Frame은 먼저 현재 `CareerUiSkin`을 단순화하거나 코드 기반 1px border로 구성한다. AI 생성 9-Slice는 가장자리 일관성이 검증되지 않으면 사용하지 않는다.

## 검수

- 텍스트/숫자/워터마크 0건
- 실제 KBO/MLB 구단 연상 마크 0건
- 중앙/패널 배치 영역의 대비가 낮음
- Team Color가 화면 전체를 지배하지 않음
- 선수 카드보다 배경이 먼저 보이지 않음
- 실제 Unity 화면 적용 뒤 1280×720, 1920×1080, 2560×1440 확인

생성 서비스가 반환한 두 원본은 1672×941이다. 16:9 비율과 2048 Max Size 정책은 충족하지만 목표 Canvas보다 작으므로, Unity 실기 검증에서 확대 선명도가 부족하면 같은 Prompt로 고해상도 변형을 다시 생성한다.
