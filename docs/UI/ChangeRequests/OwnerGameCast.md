# 구단주 경기 관전: 상공 2D GameCast

## 결정과 적용 범위

투타 인물 포즈를 포함한 한 장의 배경에서 상공 구장 지도 중심 화면으로 전환한다.
목표는 액션 재현이 아니라 누가 처리한 타구인지, 주자가 어디로 이동했는지,
어떤 투구와 감독 판단이 누적되었는지를 읽을 수 있게 하는 것이다.

생성 이미지 한 장에는 사람·공·UI를 넣지 않는다. 배경과 경기 상태를 분리하면 타자 좌우,
수비·주자 상태마다 다른 완성 이미지를 제작할 필요가 없다. 기존 인물 이미지와 셰이더는
삭제하지 않았지만 새 관전 화면에서는 참조하지 않는다.

## 실제 연결

- 왼쪽: 3:2 상공 구장, 포지션 마커 9개, 주자 이름과 베이스 경유 이동, 공과 타구 경로.
- 오른쪽: 현재 투타, 공개된 투구 수·타석·안타, 실제 투구 통과점과 최근 투구 기록,
  타구 수비 구역·담당 야수, 이벤트에 기록된 감독 판단 사유.
- 기존 점수·B/S/O·이닝·관전 밀도·일시정지·배속·즉시 결과·결과 기록 스크롤을 유지한다.
- `OwnerMatchSpectatorSession`은 다음 사건 준비와 결과 공개를 분리한다. 배속은 표현 시간만 바꾸며
  `DetailedMatchEngine`, 난수, 경기 결과, 감독의 운영 권한과 밸런스는 변경하지 않는다.
- 중요 순간은 결과 종류를 단순 나열하지 않는다. 타석 전후 Win Expectancy 변화, 동점·역전,
  후반 접전 득점·감독 판단·수비 사건, 다득점, HomeRun/Triple과 마지막 타석을 선별하고,
  선택된 타석의 승부처 표식 또는 첫 투구부터 공식 결과까지 하나의 하이라이트로 재생한다.
  매 이닝 종료와 평범한 초반 Single/Strikeout은 중요 순간 경계에서 제외한다.
- `MatchPlayVisualizer`는 고정 개수 마커를 재사용한다. 선수 모드와 공유하는
  `PlayResolutionFieldLayout`의 의미 좌표를 이미지에 맞게 보정한다.

## 데이터의 의미와 한계

투구 표시는 `PitchPlayData`의 실제 통과점·구종·구속을 사용한다. 데이터가 없는 투구에는 가짜 위치를
만들지 않는다. 타구 경로의 끝점은 `FieldZone`을 시각화한 대표 지점이며 실제 측정 좌표나 공 물리가 아니다.
주자 이동은 `RunnerAdvance`/`RunnerThrownOut`의 출발·도착 베이스를 따른다.
모든 송구·중계 플레이를 완전한 수비 애니메이션으로 재현하는 버전은 아니다.
감독의 사유는 `ReasonCode`가 있는 공식 사건만 표시하고 확률 설명을 추측하지 않는다.

## 이미지 및 설정

내장 ImageGen으로 새로 생성했다. 첨부된 구도나 인물 이미지를 재사용·편집하지 않았다.

- 이미지: `Assets/10.Datas/Resources/UI/OwnerMatch/gamecast_field_v1.png` (1536×1024)
- 좌표·시간 설정: `Assets/10.Datas/Resources/UI/OwnerMatch/GameCastPresentation.json`
- 프롬프트: `docs/UI/ImageGenPrompts/OwnerGameCast.md`

## 검증

최신 소스 전체를 포함한 Presentation 보조 컴파일: 경고 0, 오류 0.
자동 생성 csproj에서 다른 동시 작업의 신규 partial 파일이 누락되어 임시 MSBuild 설정으로
Presentation 소스 목록만 갱신해 검사했다. 원본 csproj와 다른 작업 파일은 수정하지 않았다.

Unity 6000.3.21f1 별도 프로젝트에서 대상 EditMode 테스트 23/23 통과.
실제 `UI_Scene_OwnerMatchSpectator`를 테스트 데이터 경기의 공식 이벤트 864건으로 재생하여
경기 종료와 결과 화면까지 확인했다. 모든 순간/중요 순간의 최종 점수 일치, 미리보기의 HUD
비공개, 일시정지 중 결과 공개 방지도 검사했다. 플레이어 세이브는 사용하지 않았다.
1440×810 실제 View 캡처는 `docs/reports/owner-gamecast/{pitch,contact,runner,result}.png`,
결과 요약은 같은 폴더의 `runtime-validation.txt`, NUnit 원본은 `editmode-results.xml`이다.
현재 열린 원본 프로젝트에서의 PlayMode 조작과 최종 Player Build는 수행하지 않았다.
화면 검증은 격리 시점 소스 기준이므로 동시 작업의 후속 UI 변경까지 보증하지 않는다.
밸런스 변경이 없어 대량 통계 시뮬레이션은 이번 표현 변경 검증에 포함하지 않는다.

중요 순간 선별 개선본은 `Baseball.Presentation.Tests.csproj --no-restore` 보조 컴파일과 열린
Unity Editor의 asmdef 컴파일을 오류 없이 통과했다. 평범한 초반 Single/Strikeout 제외와
승부처 표식부터 역전 타석 종료까지의 구간 결합을 EditMode 회귀 테스트로 추가했다.
원본 프로젝트가 Editor에서 실행 중이어서 개선 후 Unity Test Runner 재실행과 실제 경기 조작 검증은 남아 있다.
