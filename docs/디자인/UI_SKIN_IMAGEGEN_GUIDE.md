# ImageGen UI 스킨 적용 가이드

## 결론

현재 UI는 대부분 런타임 C#에서 `Image`, `Button`, `Slider`를 생성하므로 ImageGen 리소스를 적용할 수 있다.
다만 화면별 코드에 Sprite를 직접 연결하면 같은 스타일 로직이 반복되므로, 공통 `CareerUiSkin`이 UI 생명주기에서
버튼·슬라이더·정보 패널을 일괄 스타일링하는 구조를 사용한다.

## 시각 원칙

- 기본 재질: matte navy scorebook, warm ivory chalk line, muted baseball red stitch
- 조형 언어: 프로야구 중계 그래픽처럼 평면적이고 얇게 유지하며, SF HUD형 네온·장갑판·뾰족한 엔드캡은 사용하지 않는다.
- 강조 순서: 다음 경기 CTA → 현재 역할 → 내 선수 → 최근 성적
- 금색은 수상·최종 결과처럼 의미가 있는 순간에만 사용하고, 발광 대신 ivory 테두리와 상태 마커로 구분한다.
- 텍스트, 숫자, 구단 로고와 야구 아이콘은 이미지에 굽지 않는다. 현지화와 상태 변경이 가능한 Unity Text/Image로 유지한다.
- 정보 밀도가 높은 표와 목록에는 범용 얇은 프레임을 쓰고, 영웅 프레임은 다음 경기·최종 결과처럼 큰 패널에만 쓴다.

## 리소스

| 파일 | 용도 | 적용 방식 |
|---|---|---|
| `ui_panel_universal_v2.png` | 최상위 주요 정보 패널 | 투명 배경 9-slice |
| `ui_panel_hero_v2.png` | 다음 경기·중요 결과·대형 선택 카드 | 투명 배경 9-slice |
| `ui_button_states.png` | Normal / Focused / Pressed | 런타임 Sprite 분할 + SpriteSwap |
| `ui_selected_point.png` | 레거시 선택 포인트 | 신규 UI에서는 사용하지 않음 |
| `ui_slider_parts.png` | Track / Fill / Handle | 런타임 Sprite 분할 |
| `ui_fx_atlas.png` | CTA light sweep 등 | 저빈도 unscaled-time 연출 |

PNG는 UI 번짐을 막기 위해 mipmap을 끄고, PC 메모리 점유를 줄이기 위해 최고 품질의 기본 texture compression을
사용한다. Sprite 영역과 9-slice border는 `CareerUiSkin` 한 곳에서 관리한다. 화면 코드는 리소스 좌표를 알지 않는다.

## 적용 범위와 안전장치

- `UIBase.Initialize`와 `UIBase.Show` 뒤에 적용하므로 런타임 재구성 화면에도 같은 스타일을 적용한다.
- 기존 Sprite가 있는 선수 초상·구장·뉴스·은퇴 회고 일러스트는 교체하지 않는다.
- 장식 패널은 화면의 최상위 `*Panel`/`*Modal` 한 단계에만 적용한다. 그 안의 카드·표·섹션에는 같은 프레임을 반복하지 않는다.
- 기존 `Panel > Surface` 구조는 `Surface`에만 투명 9-slice를 적용하고 바깥 cyan backplate는 투명화한다.
- 내부 `Card`/`Frame`/`Surface`는 짙은 navy 평면과 1px steel 경계로 통일해 정보 밀도를 유지한다.
- 높이 260px 이상의 대형 선택지만 짧은 버튼 아틀라스를 늘리지 않고 패널 규격을 사용한다. 220px 이하의 커리어·선수 유형·스타일 선택지는 표준 버튼 아틀라스를 사용해 패널 장식이 갈라지지 않게 한다.
- 너비 180px 이하이면서 높이도 46px 이하인 실제 필터·증감 버튼만 red stitch 엔드캡을 반복하지 않는 평면형 compact 규격을 사용한다. `이전`, 투타 선택, 게임 속도처럼 높이가 확보된 버튼은 너비가 좁아도 표준 프레임을 유지한다.
- 선택된 항목은 금색 Focused 프레임과 ColorTint로 구분한다. 별도 홈플레이트 Point는 프레임의 red stitch 및 화면 코드의 `✓` 텍스트와 중복되므로 사용하지 않는다.
- 버튼 라벨은 좌우 안전 여백, 자동 축소, 줄바꿈과 RectMask2D 클리핑을 공통 적용한다.
- Danger/Success 등 화면별 의미 색은 원래 tint를 약하게 보존한다.
- 반복 표시 시 tint가 누적되지 않도록 적용을 멱등 처리한다.

## ImageGen이 적합하지 않은 영역

- 한글·숫자가 포함된 버튼과 표: 텍스트 정확성·현지화 문제
- 구단 로고와 작은 픽토그램: 일관된 실루엣과 픽셀 정렬이 중요하므로 벡터 원본이 우선
- B/S/O, 주자, 능력치처럼 상태가 자주 바뀌는 정보: 코드와 단순 Sprite 조합이 우선
- 모든 패널의 강한 장식: 정보 계층을 무너뜨리고 여러 시즌 반복 플레이의 피로도를 높인다.

ImageGen은 분위기·재질·큰 프레임·컷 이미지·저빈도 연출에 사용하고, 정확한 정보 표현은 Unity UI가 소유한다.

## 기준 규격

| 역할 | 판정 | 표현 |
|---|---|---|
| Primary panel | 최상위 `*Panel`/`*Modal`, 260×140 이상 | transparent universal/hero 9-slice |
| Nested surface | Primary panel 내부 `Card`/`Frame`/`Surface` | flat navy + 1px steel |
| Card button | 높이 260px 이상 | universal/hero panel, 별도 선택 포인트 없음 |
| Standard button | 위 두 범위 사이 | 3-state button atlas |
| Compact control | 너비 180px 이하이면서 높이 46px 이하 | flat navy + 상태별 outline |

화면별 예외 좌표를 추가하기 전에 이 다섯 역할 중 잘못 분류된 원인을 먼저 수정한다. 같은 문제가 세 화면에서 반복되면
이름 예외를 늘리지 않고 명시적 역할 컴포넌트 도입을 검토한다.
