# 레전드·커리어 하이 영입 FX

각 영입에 별도의 ImageGen 4×4 플립북을 연결했다.

| 영입 | 연출 | 재생 |
|---|---|---|
| 레전드 | 금빛 월계관 형성 → 왕관 섬광 → 금빛·붉은 조각 확산 | 16프레임, 10fps, 1.6초 |
| 커리어 하이 | 푸른 상승 궤적 → 다이아몬드 섬광 → 결정 조각 확산 | 16프레임, 12fps, 약 1.33초 |

`RecruitSpecialCard`가 정상 반환한 카드 ID로 실제 보유 Snapshot을 조회한다.
결과 카드의 Edition이 FX 종류를 결정한다. 예외 경로에는 성공 팝업을 연결하지 않는다.
영입 규칙·재료 소비·저장 계약은 변경하지 않았다.

## 공용 결과 화면

합성까지 세 종류의 결과를 `UI_Popup_OwnerCardResult`와 `OwnerCardResultStyle`로 공유한다.
기존 `UI_Popup_OwnerCardSynthesis.Show`는 공용 화면을 호출하는 진입점으로 남겼다.
결과 카드·영입 완료 제목·선수 이름을 표시하고, 확인·취소로 즉시 닫을 수 있다.
합성과 동일하게 호스트 대비 폭 30%·높이 74%의 공통 배치를 사용하고 카드 크기는 유지한다.
하단 획득·잠금 지급 안내와 예약 여백은 제거했다. 기존 ManagerReport 패널 스킨과 Primary 버튼을 재사용한다.
특수 영입 화면은 결과가 열린 동안 뒤쪽 입력을 막으며 닫으면 입력과 도움말 버튼 포커스를 복구한다.
화면을 숨길 때 결과도 정리한다. 애니메이션은 unscaled time을 사용한다.

## 자산과 재현

- `Legend-source.png`, `CareerHigh-source.png`: ImageGen 원본.
- `*-keyed.png`: 프로젝트 `Remove-ImageBackground.ps1 -Mode ChromaKey` 중간 결과.
- `Build-Atlas.ps1 -Kind Legend|CareerHigh`: 원본의 초록 혼합색 역산, 14px 프레임 경계 감쇠, 1024×1024 RGBA 변환.
- 최종 자산: `Assets/10.Datas/Resources/UI/SpecialRecruitFx/*Atlas.png`.
- `Review-Atlas.ps1 -Kind Legend|CareerHigh`, `*-AlphaReview.png`: 밝고 어두운 배경 합성 검수.

생성 프롬프트는 각각 고정 중심·16프레임의 시간 순서·초록 단색 배경·글자/카드/인물 제외를 지정했다.
레전드는 금빛 월계관·왕관·붉은 조각, 커리어 하이는 푸른 상승 궤적·흰 다이아몬드·보라 결정으로 구분했다.
초기 키 제거에서 발광부의 어두운 매트 흔적이 발견되어, 최종 atlas는 원본의 초록 혼합색을 역산했다.

## 검증

팝업 축소 수정 후 외부 Presentation 컴파일 오류·경고 0개. 제목·카드·이름·버튼의 앵커 영역 분리를 확인했다.
이번 수정의 Unity 실화면·해상도별 Bounds·입력 검수는 미실행이다.

외부 `Baseball.Presentation` 컴파일 오류·경고 0개. Unity 생성 csproj에 새 파일만 임시 targets로 추가했다.
레전드 완전 투명 픽셀 795,224 / 1,048,576, 커리어 하이 815,250 / 1,048,576, 두 자산 최대 알파 255.
밝고 어두운 배경에서 월계관·왕관·결정·발광 경계를 직접 확인했다.
사용자의 앞선 지시에 따라 에디터 테스트는 실행하지 않았다. 실제 Unity 화면의 레이아웃·입력·재생 검수는 남아 있다.
