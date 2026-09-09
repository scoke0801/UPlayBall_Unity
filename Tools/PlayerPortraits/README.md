# 선수 초상화 발급

후속 [구단 계보별 유니폼 발급](../PlayerUniforms/README.md)을 적용했다. 기존 얼굴 배정은
유지하고 시즌·카드에는 원본 구단 계보의 의상을 사용한다. 후속 Unity EditMode 4개와
최신 Presentation 소스 보조 컴파일은 통과했으며, 아래 재개 당시 검증 제한은 과거 기록이다.

576개 기존 초상을 역사 인물 3,565명에게 고정 배정한다. 같은 인물의 시즌·카드 등급·소속이
달라져도 얼굴은 유지된다. 생성 커리어 선수는 선수 ID의 고정 해시로 같은 풀을 사용하며,
생성 선수의 분포는 역사 인물의 6~7명 균등 배정 보장에 포함하지 않는다.

## 현재 발급 결과

- 역사 인물 3,565명, 시즌 별칭 18,326건: 누락 0건.
- 576개 모두 사용: 467개는 각각 6명, 109개는 각각 7명.
- 구단·시즌 363개: 서로 다른 인물의 얼굴 중복 0건.
- 등록 PNG 576개: 제작 정본 SHA256 일치, 고유 GUID·Single Sprite 설정·알파 채널·상단 투명 배경 확인.
- 공용 미니 카드, 카드 상세 앞·뒷면, 도감, 선수 커리어 프로필은 선수 ID로 초상을 조회한다.

이전 세션의 배정과 이미지를 유지하고 독립 대조 도구를 추가해 현재 콘텐츠 전체와 재검증했다.
발급 정본은 `Assets/Resources/UI/Portraits/player_portrait_assignments.json`,
이미지는 같은 폴더의 `Players/face-0001.png`~`face-0576.png`다.

## 중복 범위의 제한

기존 기획의 **구단 전체 역사 계보 내 중복 금지**는 충족하지 않는다. 현재 최대 계보가
586명이어서 기존 576개만으로는 불가능하다. 이전 세션의 `team-season` 배정을 유지하여
현재 요청의 전체 발급과 균등 사용을 충족한다. 구단 전체 역사 및 여러 연도 혼합 로스터에서는
다른 인물이 같은 얼굴을 사용할 수 있다. 계보 전체 무중복은 추가 외형 확보와 이적 관계를
포함한 재배정 검증이 필요한 후속 작업이다. 유니폼 10종 결합도 이번 발급에 포함되지 않는다.

## 검증 실행

```powershell
powershell -NoProfile -File Tools/PlayerPortraits/Verify-Issuance.ps1
powershell -NoProfile -File Tools/PlayerPortraits/Run-UnityTests.ps1
```

첫 명령은 기존 발급 파일을 수정하지 않고 현재 콘텐츠·배정·이미지를 대조하며
`output/portrait-validation/issuance-audit.json`에 결과를 저장한다.
두 번째는 별도 Unity 프로젝트에서 Resources 로드와 동일 인물·생성 선수 얼굴 유지 테스트를 실행한다.

이전 세션의 Unity EditMode 결과는 **3개 통과, 0개 실패**
(`output/portrait-validation/test-results.xml`, 2026-09-09 22:46 KST)다.
이번 재개에서 데이터·이미지 감사는 통과했으나 Unity 재실행은 Licensing Client 연결 실패로
새 테스트 결과를 얻지 못했다. 이전 XML을 이번 실행의 성공으로 간주하지 않는다.
`dotnet build Baseball.Presentation.csproj --no-restore`는 다른 경기 표현 작업의
`OwnerMatchPlaybackGroup` 참조 누락(CS0246)으로 실패했다. 전체 프로젝트 컴파일 및
실제 게임 화면 검증 완료를 의미하지 않는다. 경기 확률·밸런스는 변경하지 않았다.

발급을 다시 생성해야 할 때는 Python 환경에서 다음을 실행한다. 콘텐츠가 바뀌면 기존 인물의
배정도 달라질 수 있으므로 일상 검증에는 재발급 대신 위 대조 명령을 사용한다.

```text
python Tools/PlayerPortraits/issue_portraits.py --scope team-season
python Tools/PlayerPortraits/issue_portraits.py --scope team-season --publish
```
