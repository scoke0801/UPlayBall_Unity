# 스프라이트 경기 표현 구현 기록

> **현재 연결 상태:** 검수·보완한 9클립 70프레임을 경기용 카탈로그에 등록했다.
> 구단주 관전의 실제 리소스 로딩과 네 투타 조합, 주루 전환을 Unity EditMode에서 확인했다.
> 아래 미승인·0클립 설명은 이전 단계 기록이며,
> [RuntimeConnectionReport.md](RuntimeConnectionReport.md)가 현재 상태의 정본이다.

> **최신 아트 재제작:** 사용자가 다시 지정한 홈 뒤→마운드 구도를 기준으로 좌·우 타격 시트를
> 각각 6프레임으로 다시 만들었다. 이전 A/B는 활성 처리 매니페스트에서 제외했고,
> `Batter.DuelSwing.R/L`과 포수·주루 후보를 등록했다. 현재 활성 처리 대상은 13클립 116프레임이다.
> 아래 11시트/112프레임 표와 검증 수치는 이전 구현 시점 기록이며, 새 원본·프롬프트·RGBA·검수 상태는
> [generated-v4/README.md](generated-v4/README.md)를 따른다. 새 아트의 경기 승인 상태는 아직 올리지 않았다.

## 현재 상태

**부분 완료.** 이미지 처리·Unity 가져오기/검수 도구·두 경기 모드의 표현 연결을 구현했다.
현재 제공 시트는 모션/카메라 적합성 검수가 끝나지 않아 경기용으로 승인하지 않았다.
따라서 승인 Catalog를 갖추기 전 실제 경기에는 기존 표현이 유지된다.
검수용 16:9 합성은 미저장 복사본으로 확인하며 Production 승인을 바꾸지 않는다.

## 구현 범위

- `Tools/SpriteSheetPipeline`: 11시트 처리 manifest, 원본 해시 검증, 외곽 key 추정,
  기존 `Remove-ImageBackground.ps1 -Mode ChromaKey` 재사용, HSV/녹색 우세/키 거리 검사,
  soft alpha·edge despill, 정규화 grid 경계, 순서·제외·프레임별 시간·공 마스크,
  원본 cell/pivot/root/bounds 메타데이터, 개별 RGBA·Atlas·GIF·밝고 어두운 배경 합성·alpha/spill·QA.
- `Assets/02.Scripts/Editor/SpriteSheets`: idempotent importer, SpriteAtlas,
  검수/Production Catalog 분리, 순서/시간/이벤트/손잡이/pivot/원본 접점 편집,
  1x/2x/4x 미리보기와 경기장 anchor 클릭 보정. 통합 툴 런처의 `경기 표현`에서 실행한다.
- `Assets/02.Scripts/Presentation/Match/Sprites`: 카탈로그, 프레임 시계, 선수와 그림자,
  독립 공, 깊이 투영, 카메라, 결과를 재판정하지 않는 공통 무대.
- 구단주 `MatchPlayVisualizer`와 선수 커리어 `PlayResolutionPresenter` 연결.
  시뮬레이션·확률·능력치·밸런스는 변경하지 않는다.
- 구단주 중계 1/2/4배 버튼을 제공하고 기존 5배 값은 이전 설정의 호환을 위해 유지한다.
- `Tools/SpriteMatchValidation`: 원본 Unity 작업을 건드리지 않는 격리 EditMode·Import·16:9 검수 실행.

## 원본과 처리 결과

각 결과는 `output/sprite-sheet-ingame/Processed/<clipId>/`에 있다.
정확한 순서·시간·마스크·이벤트는 [source manifest](../../../Tools/SpriteSheetPipeline/sprite_sheet_sources.json),
처리 후 좌표는 해당 clip JSON이 정본이다. 이벤트 번호는 재생 순서 기준 0부터 센다.

| 원본 | Clip ID | 손잡이 | 이벤트 프레임 후보 |
|---|---|---|---|
| 우투수_피치.png | Pitcher.Pitch.R | R | BallRelease 7 |
| 좌투수_피치.png | Pitcher.Pitch.L | L | BallRelease 7 |
| 우투수_준비.png | Pitcher.FollowReady.R | R | 없음 |
| 좌투수_준비.png | Pitcher.Idle.Unreviewed | 미확정 | 없음 |
| 투수_수비.png | Pitcher.FollowReady.L | L | 없음 |
| 우타자_히트.png | Batter.DuelSwing.A | 미확정, L 배치 후보 | SwingWindowOpen 5 / BatContact 7 |
| 좌타자_히트.png | Batter.DuelSwing.B | 미확정, R 배치 후보 | SwingWindowOpen 5 / BatContact 7 |
| 수비.png | Fielder.InfieldGrounder | R | GloveContact 3 / Transfer 6 / ThrowRelease 10 |
| 플라이수비.png | Fielder.OutfieldFlyCatch | R | GloveContact 6 / ThrowReady 9 |
| 상위 docs/design의 …09_27_54.png | Batter.HitToRun | 미확정 | BatDrop 3 / RunStart 4 |
| 상위 docs/design의 …09_27_51.png | Batter.HighlightReaction | 미확정 | Miss 7 / Reaction 9 |

빈 `경기장.png`만 런타임 배경으로 등록한다. `경기장_투수_타자_시안.png`는 합성 참고다.
타격 A/B는 모션 카메라 적합성 승인 후 `Batter.DuelSwing.R/L` 계약으로 등록한다.

현재 모든 시트의 재생 순서는 원본 셀 0부터 마지막 셀까지이며 제외 셀은 없다.
이는 검수 후보 순서다. 전체 모션이 시간적으로 자연스럽다는 승인으로 해석하지 않는다.
추가한 접점 후보는 원본 셀 좌상단 기준이며 각각 실제 프레임 그림에서 읽었다.

| 모션 / 사건 | 셀 내 픽셀 좌표 | 셀 크기 |
|---|---|---|
| Batter.DuelSwing.A / BatContact | (35, 151) | 362×362 |
| Batter.DuelSwing.B / BatContact | (300, 150) | 362×362 |
| Fielder.InfieldGrounder / GloveContact | (203, 317) | 362×362 |
| Fielder.InfieldGrounder / ThrowRelease | (176, 166) | 362×362 |
| Fielder.OutfieldFlyCatch / GloveContact | (234, 70) | 355×443 |

## 검증 및 남은 조건

검증 명령·로그 위치는 [검증 도구 README](../../../Tools/SpriteMatchValidation/README.md)를 따른다.
최종 실행 수치와 생성 에셋 목록은 이 문서의 후속 검증 절에 기록한다.

- 모든 시트의 검수 상태는 `NeedsReview`다. 손잡이 판독만으로 모션 전체를 승인하지 않았다.
- 우타자 시트의 얼굴/배트는 화면 왼쪽을 향한다. 시안의 우타석에 배치했을 때
  홈 바깥으로 배트가 뻗는 문제를 합성에서 확인해야 한다. 단순 mirror는 손잡이까지 바꾸므로 해결로 간주하지 않는다.
- 일부 소스 프레임이 정규 grid 경계를 침범한다. 타격 A의 접촉 후보 셀은 배트 끝이 인접 셀에 들어가
  crop 손실 위험이 있다. 투수 다음 행 모자 조각도 별도 마스크 검수 대상이다.
- 원본의 발 위치 차이는 고정 root를 써도 남는다. 모션별 실제 접지·시간 순서·BatContact 후보를 승인해야 한다.
- 포수/주루용 시트가 부족하다. 승인된 주루 모션이 없으면 의미를 잘못 전달하는 타격 자세 대신 기존 주자 표식을 사용한다.
- 실제 양 모드 전체 경기 Play Mode, 60fps·GC 할당 측정, 최종 아트 승인은 미완료다.
  대량 시뮬레이션 밸런스 검증은 이번 수치 변경 대상이 아니다.

## 중단 후 재개한 구현과 검증

- `SpriteMatchStage`: 포수 전용 승인 모션이 없으면 위치 표식을 표시한다. 이전 내야수 시트 확대가
  타자와 홈 접점을 가리던 문제를 수정했다. 향후 `Catcher.Idle` 승인 모션을 가져오면 자동으로 사용한다.
- `SpriteAnimationCatalog`: Inspector에서 직접 편집한 모션도 중복·알 수 없는 사건,
  포구 전 송구·스윙창 전 접촉·배트 투하 전 주루 등 인과 순서 위반을 차단한다.
- 처리기: 체류 시간 누락·비유한 수·0 이하 값, 잘못된 그리드·손잡이·중복 사건을 생성 전에 차단한다.
  실행법과 회귀 테스트는 `Tools/SpriteSheetPipeline/README.md`에 추가했다.
- 검수 실행기: 접촉 캡처가 헛스윙 경로를 호출하던 오류를 수정했다. 땅볼·뜬공의 포구 시점과
  땅볼의 송구 전·중·후 캡처, 예열 후 무대 갱신 할당 측정을 추가했다.
- Importer: Unity JSON 역직렬화가 생략된 접점 객체에 기본 좌표를 생성하던 문제를 수정했다.
  처리 메타데이터 schemaVersion 2의 `hasSourcePosition`으로 실제 지정된 접점만 가져온다.
  이 문제로 공이 타자의 머리로 향하던 오류를 확인했으며, 원본 셀의 배트·글러브·송구손 접점 후보도 기록했다.
- 타격 A/B의 이전 R/L 판독을 철회하고 `NeedsReview`로 되돌렸다. 시안과 같은 홈 방향 배치를
  비교하기 위해 검수 복사본에서만 A=L 후보, B=R 후보로 사용한다. 원본 이미지 반전은 하지 않는다.

검증 결과:

- Unity 6000.3.21f1 격리 프로젝트의 코드 컴파일 및 선택 EditMode **39/39 통과**.
  스프라이트 런타임·Importer·MatchGameCast·구단주 화면 구성/경기장 로드 검증이며 전체 테스트 스위트는 아니다.
- Python 회귀 **7/7 통과**. 11개 원본 해시, 정규화 그리드, 입력 계약과 실제 크로마키 제거 도구를 거친
  흰 유니폼·파란 모자·1픽셀 갈색 배트·혼합 경계 알파 보존을 확인했다.
- 11시트 **112프레임** 생성. 배경 후보 픽셀의 평균 알파는 모두 **0**, 반투명 경계의 최대 녹색 우세는
  모두 **8**. 이는 설정된 수치 검증이며 모든 자세의 아트 적합성을 뜻하지 않는다.
- 11시트 재실행의 출력 해시 검증 및 캐시 재사용 통과. Unity 두 차례 Import의
  **247개 파일 내용·GUID 동일**. 검수 Clip 11개, Production Clip 0개, `isRuntimeReady=false`.
- 예열 후 무대 Reset·투구·주자 갱신 **600회, 관리 힙 할당 0바이트, 평균 0.117ms/회**.
  Canvas/GPU 렌더링을 제외한 격리 환경 측정이며 전체 경기의 60fps 검증은 아니다.
- 1280×720 합성 13장을 생성했다. 좌우 배치 후보에서 공이 배트에 닿고 포구 뒤 외부 공이 숨는 화면,
  포수 대체 표식으로 타석이 가려지지 않는 화면을 확인했다. A의 잘린 배트와 일부 수비 시트의
  인접 셀 모자 조각·발 위치 차이는 여전히 아트 보완 대상이다.
- 격리 Unity에서 생성한 검수 에셋 247개 파일과 폴더 meta 3개를 아래 실제 프로젝트 경로에 등록하고
  원본과 복사본의 파일 해시 일치를 확인했다. 빈 Production Catalog로 인해 실제 경기는 기존 표현을 사용한다.
  - `Assets/04.Images/SpriteMatch/`: 112개 RGBA 프레임, 빈 경기장 PNG, 각 meta.
  - `Assets/10.Datas/SpriteMatch/`: ReviewCatalog, FieldLayout, SpriteAtlas, ImportReceipt.
  - `Assets/Resources/UI/SpriteMatch/`: 승인 모션이 없는 AnimationCatalog.
- `git diff --check` 통과. 이번 재개 작업에서 Core/Simulation 및 밸런스 변경 없음.

캡처와 로그는 `output/sprite-sheet-validation/`에 있다. `review-Right-pitch-100.png` 등은
검수 후보를 임시 활성화한 합성이며 실제 승인 모션을 사용한 전체 경기 화면이 아니다.
알파 합성에서 A의 접촉 셀 왼쪽 배트 끝 잘림과 프레임별 발 위치 차이가 남아 있다.
손잡이·모션 순서·접지·포수/주루 리소스의 검수 및 보완 없이 Production 승인을 올리지 않는다.
