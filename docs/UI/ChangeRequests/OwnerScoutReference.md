# 스카우트 화면 레퍼런스 반영

사용자가 제공한 파견 지도 화면과 스카우터 선택창을 기준으로 초록 도트 지도 / 중앙 스카우터 / 오른쪽 선수 카드의 3열 구성을 적용한다. 1000×460 기준 좌표를 Workspace에 비례 축소·확대한다.

- 지도 마커는 실제 `General/Franchise/Year/YearFranchise/Award` Scout Pool의 파견 범위에 연결한다. 별도 지리 확률을 발명하지 않고 `WorldCardCatalog`의 구단·연도 필터를 지역/방침 표현으로 사용한다.
- `스카우트 방침` 창은 실제 상품 목록을 `파견 범위 · 탐색 방침 · 탐색 인원 · SP 비용`으로 보여준다. 취소는 기존 선택을 보존하고, 결정해야 메인 파견 정보와 구매 대상이 바뀐다.
- 비용, 영입 수, 집중 영입 게이지와 실제 후보 확률은 기존 Snapshot을 사용한다. 확률 정보는 스크롤 가능한 별도 창으로 표시한다.
- 스카우트 파견 → 비용 확인 → 확정 → 기존 `ScoutPurchaseRequested` 순서를 유지한다. 즉시 지급 계약은 바꾸지 않고 확정 결과를 오른쪽 `영입대기 선수` 카드 슬롯에 표시한다.
- 스카우터는 감독·수석코치와 같은 무채색 익명 실루엣 한 장으로 표시한다. 외형이 확률·비용에 영향을 주지 않으므로 별도 외형 선택창은 제공하지 않는다. 참조 이미지의 스카우터 능력치·이용권·타이머는 해당 게임 데이터가 없어 발명하지 않는다.
- 빈 화면·조회 오류는 실행 버튼을 비활성화하고 본문에 사유를 표시한다.

생성 이미지는 내장 ImageGen으로 제작했다. 지도와 단일 실루엣을 `Assets/10.Datas/Resources/UI/OwnerPowerUp/scout_korea_map_v1.png`, `OwnerScout_Silhouette_V1.png`에 저장하고 RawImage로 연결한다. 기존 3×3 실사풍 초상 아틀라스는 폐기한다.

최종 프롬프트는 `docs/UI/ImageGenPrompts/OwnerScoutReference.md`에 기록한다.

## 검증

- Unity 6000.3.21f1 격리 EditMode 검증 대상에는 생성 이미지 로드, 외형 선택 UI 제거, 방침 취소/결정 경계, 실제 확률 목록 높이/스크롤, 비용 확인 전 명령 차단, 확정 후 1회 명령, 지급 결과 표시가 포함된다.
- 기존 1200×680 RenderTexture 캡처의 지도·정보·슬롯 배치는 유지한다. 스카우터 영역은 새 단일 실루엣으로 교체되고 기존 선택창 캡처는 폐기 대상이다. 테스트 상품을 주입한 Workspace 캡처이며 전체 게임 Shell/PlayMode 검증은 아니다.
- `Baseball.Game.Tests.csproj`는 경고·오류 0으로 통과했다. Presentation 생성 csproj가 동시 추가된 `OwnerTeamLineup` 두 파일을 아직 포함하지 않아 첫 빌드가 실패했지만, 해당 파일을 검증용 Compile Item에 임시 포함한 전체 `Baseball.Presentation.Tests.csproj` 빌드는 경고·오류 0으로 통과했고 생성 csproj는 원상 복구했다.
- 격리 Unity Test Runner는 로컬 Licensing Client가 `com.unity.editor.headless`를 찾지 못해 이번 변경본 실행을 완료하지 못했다. 테스트 소스와 캡처 경로는 새 방침 흐름으로 갱신했으며 실제 Editor/Player 조작 검증은 남아 있다.
- 확률·비용·밸런스 수치는 변경하지 않았다.
