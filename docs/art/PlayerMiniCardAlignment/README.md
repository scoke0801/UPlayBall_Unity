# 선수 미니 카드 명찰 정렬

EX·Legend·Rare·CareerHigh 원화의 명찰이 Normal·AllStar보다 높고, 코드도 이름 위치를
두 그룹으로 나누어 같은 편성 행에서 이름·포지션·연도가 어긋났다.

## 자산과 연결

- Skin Reference: `Assets/Resources/UI/PlayerCards/PlayerCard_Mini_*` 기존 등급 원화.
- Layout Reference: `PlayerMiniCardView`의 선수 목록·편성 슬롯 공용 레이아웃.
- Metrics: 기존 151×212 / 편성 슬롯 80×120과 프레임 높이 비율 0.89 유지.
- ImageGen으로 Ex·Legend·Rare·CareerHigh 4종을 재생성해 `_v8.png`로 추가했다.
- `OwnerPlayerCardFrames`가 v8을 우선 선택한다. 기존 이미지와 GUID는 보존했다.
- 미니 이름 영역은 모든 등급에서 `(0.23, 0.19, 0.54, 0.075)`를 사용한다.
  이름·포지션·연도 중심은 카드 하단 기준 22.75%다. 등급별 텍스트 높이 분기를 제거했다.
- EX·Legend·Rare 문장의 전경 메시 범위도 이동한 원화에 맞췄다.
- 이름 띠의 테두리·장식 두께에는 원화별 미세 차이가 있지만 텍스트 중심은 공통이다.
- 상세 카드의 이름 좌표·초상 크기·입력·포커스·공통 셸은 기존 계약을 사용한다.

## 생성 원본

ImageGen 저장 디렉터리:
`C:/Users/scoke/.codex/generated_images/01a09930-be86-7f03-870d-8e72daec733c/`

| 등급 | 최종 생성 파일 |
|---|---|
| Ex | exec-20d45854-ccab-4bea-b41a-9678960bb013.png |
| Legend | exec-12243486-36fd-4aeb-b1fb-9cd273cf7f78.png |
| Rare | exec-6dff6552-0bd7-4c9a-850c-98cc4ccbdc08.png |
| CareerHigh | exec-249cde03-8f88-46e0-a4c9-5bd1fd804c96.png |

생성 원본은 보존하고 Resources에 복사했다. 불투명 카드 배경이므로 크로마키 제거는 하지 않는다.
일반 카드의 명찰 위치를 기하 기준으로 제공하고 기존 등급의 재질·색상·문장을 유지하며
빈 이름 띠를 내리도록 요청했다. 재생성 후 위치가 남아 어긋난 결과는 추가 보정했다.

## 검증

- `name-alignment-review.png`: 8개 등급을 151×212로 배치하고 같은 좌표에 한국어 이름을 합성했다.
  이름 정렬과 명찰 내부 배치를 육안 확인했다. Unity 실행 화면이 아닌 원화 합성 검수다.
- `dotnet build Baseball.Presentation.csproj --no-restore -v quiet`: 오류 0, 경고 0.
- 기존 리소스 버전 확인 테스트의 기대값만 v8로 갱신했다. 테스트는 실행하지 않았다.
- 사용자 요청에 따라 에디터 테스트 미실행. Unity 실제 화면 및 해상도별 검수는 미실행이다.
