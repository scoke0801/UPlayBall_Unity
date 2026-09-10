# 후면 경기 구도 재제작

## 기준

사용자가 다시 첨부한 `../generated-v3/user-duel-camera-reference.png`가 카메라·타자 준비 자세의 정본이다.
홈 뒤에서 타자의 모자 뒷면과 등/옆면을 보고, 타자의 시선은 화면 안쪽 투수 마운드를 향한다.
화면 오른쪽을 바라보는 정면·측면 초상으로 대체하지 않는다.

내장 ImageGen으로 제작했다. API/CLI 생성은 사용하지 않았다. 각 원본과 함께 실제 사용한 프롬프트를 보존한다.
우타자 최종 시트는 `generated-v3/batter-ready-duel-source.png`의 준비 자세에서 출발해
`batter-duel-right-motion-draft.png`로 동작을 확장하고, 최종 프롬프트로 셀 간격을 보정했다.
잘못된 얼굴 방향과 팔 자세를 가진 v2 및 v3 초기 타격 시트는 사용하지 않는다.

## 산출물

| 원본 | 역할 | 격자 | 재생 순서 | 사건 후보 |
|---|---|---|---|---|
| `batter-duel-right-source.png` | 우타자 후면 타격 | 3×2 | 0,1,2,3,4,5 | SwingWindowOpen 2 / BatContact 3 |
| `batter-duel-left-source.png` | 좌타자 후면 타격 | 3×2 | 0,1,2,3,4,5 | SwingWindowOpen 2 / BatContact 3 |
| `catcher-idle-source.png` | 포수 준비 후보 | 4×2 | 0..7 | 없음 |
| `runner-loop-source.png` | 배트 없는 주루 후보 | 4×2 | 0..7 | 없음 |

타격은 준비→체중 이동→보폭/회전 시작→접촉→팔 뻗기→마무리의 6단계다.
프레임 시간은 180 / 120 / 70 / 55 / 80 / 160ms다. 접촉 이후에는 팔만 뒤로 돌리지 않고
몸통·어깨가 함께 회전하는 자세로 변경했다. 두 손 그립과 앞발 방향은 좌우 각각 작성했다.

원본은 녹색 배경 RGB 시트다. `Tools/SpriteSheetPipeline/process_sheets.py`가 저장소의
`Remove-ImageBackground.ps1 -Mode ChromaKey`를 거쳐 실제 RGBA 개별 프레임·Atlas와 밝고 어두운 배경 합성을 만든다.
결과 위치는 `output/sprite-sheet-ingame/Processed/<Clip ID>/`다.
이 폴더에도 배경이 제거된 `*-rgba.png`, 밝고 어두운 배경의 `*-contact.png`,
연속 재생용 `*-preview.gif`를 함께 보존한다. RGBA Atlas는 셀 크기를 그대로 유지한다.

타격의 두 번째 행은 첫 행과 그림의 지면 위치가 달라 `framePivots`로 접지 원점을 보정했다.
셀 이미지 자체를 잘라 재중앙 정렬하거나 프레임마다 크기를 바꾸지 않는다.
정확한 해시·접점·접지 좌표는 `Tools/SpriteSheetPipeline/sprite_sheet_sources.json`을 따른다.

## 검수 상태

새 시트는 검수 카탈로그에 등록한다. 경기 승인 상태는 `NeedsReview`로 유지한다.
사용자가 지적한 관절 오류를 반복해서 통과 처리한 이력이 있어, 자동 알파·잘림 검사를
인체 구조나 전체 타격 모션의 최종 승인으로 간주하지 않는다.
포수 후보는 글러브손을 재확인해야 하고, 주루 후보는 양 다리 교대와 루프 연결을 추가 검수해야 한다.
최종 타격 후보는 첨부 구도와 맞춘 버전이며, v2/v3 초기 오류 후보와 구분한다.

자동 검증은 Python 8/8 통과, 활성 13클립 재실행 캐시 일치다. 새 28프레임은 모두 RGBA이며
배경 후보 평균 알파 0, 반투명 경계 최대 녹색 우세 8이다.
셀 경계까지의 최소 여백은 우타자 42px, 좌타자 34px, 포수 26px, 주루 25px다.
모든 새 프레임의 알파 영역이 셀 내부에 들어오는 것을 확인했다. 수치는 `alpha-grid-qa.json`에 기록했다.

Unity 6000.3.21f1에서 새 매니페스트를 두 번 가져와 검수 13클립, 파일 307개의 내용·GUID 일치를 확인했다.
1280×720 합성에서 준비 자세와 배트 접점을 확인했으며 `duel-*-review.png`로 보존했다.
새 4클립 28프레임은 `Assets/04.Images/SpriteMatch/`와 검수 카탈로그에 등록했다.
Production 카탈로그는 0클립으로 유지하며 전체 경기 적용 완료를 뜻하지 않는다.
GIF는 원본 셀 순서 미리보기이며, 프레임별 접지 보정은 메타데이터를 읽는 Unity 합성에 적용된다.
