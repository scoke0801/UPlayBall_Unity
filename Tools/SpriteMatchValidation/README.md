# 스프라이트 경기 표현 검증

현재 경기용 9클립의 승인·연결 근거는
[RuntimeConnectionReport](../../docs/design/sprite_sheet_ingame/RuntimeConnectionReport.md)를 따른다.
아래 A/B 임시 매핑과 경기용 0클립 설명은 최초 검수 당시 기록이다.
현재 실행기는 경기용 카탈로그가 준비되면 승인값을 바꾸는 복사본 없이 그 카탈로그로 합성한다.
`owner-<width>x<height>.png`는 실제 관전 UI의 4개 해상도 정적 합성이고,
`motion-Right/Left/*.png`는 좌우 투구의 36프레임 연속 캡처다.
`replay-<타구 종류>/*.png`는 실제 상세 시뮬레이션 이벤트로 구동한 `MatchPlayVisualizer` 연속 캡처다.
동일 Seed 이벤트 일치와 1·2·4배속 전체 재생 결과는 `real-match-replay.txt/csv`에 남는다.
검증기는 관전 세션과 같이 타석 종료 사건까지 읽어 완성된 타구 데이터를 찾는다.
`python Tools/SpriteMatchValidation/build_motion_previews.py`로 GIF를 만든다.
`owner-session-replay.csv`는 실제 관전 세션과 화면 갱신을 연결한 3개 관전 모드 × 3개 배속 결과다.
HUD 공개 경계, 일시정지 중 사건·재생 시간 정지, 최종 BoxScore 일치, 결과·홈 복귀 표시를 검사한다.
EditMode에서 갱신 함수를 직접 호출하므로 실제 입력·Unity Update를 사용하는 Play Mode와 구분한다.
이닝 점수표의 자식 정리는 EditMode에서는 즉시 삭제해 반복 재생 중 객체가 누적되지 않게 한다.

하이라이트 삽입 이미지 6종의 리소스·공개 사건 매핑·배속·표시 해제 검증도 기본 필터에 포함한다.
현재 기본 필터는 74/74 통과했다. `highlight-inset-replay.csv`는 실제 경기에서 6종이 발생한
공개 사건을 기록하며, 공개 이전 결과 누출·일시정지·즉시 결과 전환·종료 후 잔류 여부를 검사한다.
`inset-replay-*.png`는 실제 사건 재생 화면이고 `inset-<종류>-<해상도>.png` 24개는
4개 해상도에서 우측 영역 경계·상세 정보 숨김을 확인하는 고정 합성이다.
`python Tools/SpriteMatchValidation/verify_highlight_assets.py`는 6종 원본과 런타임 PNG의
해시·비율·불투명 알파를 검사해 `highlight-image-qa.json`을 만든다(Pillow 필요).
자세한 근거는 [하이라이트 적용 기록](../../docs/design/sprite_sheet_ingame/highlight-insets-v1/README.md)을 따른다.

열려 있는 원본 Unity 프로젝트와 씬·Library를 공유하지 않고 Unity 6의 실제 엔진에서 검증한다.
`output/sprite-sheet-validation/UnityProject`에 코드·플러그인·테스트를 복사하고 설치된 로컬 패키지 캐시를 참조한다.
원본 프로젝트의 PackageCache가 준비되어 있어야 한다.

```powershell
& Tools/SpriteMatchValidation/Invoke-SpriteMatchValidation.ps1 -Mode Tests
& Tools/SpriteMatchValidation/Invoke-SpriteMatchValidation.ps1 -Mode Visual
```

각 명령은 숨겨진 Unity 프로세스를 시작하고 PID를 출력한다. 이전 실행이 종료된 뒤 다음 명령을 실행한다.
`Visual`은 먼저 `Tools/SpriteSheetPipeline`으로 `output/sprite-sheet-ingame/Processed`를 생성해야 한다.
Unity 설치 위치가 다르면 `-UnityPath`를 지정한다.

- `editmode-results.xml`: 스프라이트 런타임·Importer EditMode 결과.
- `unity-import-qa.json`: 두 차례 Import 후 파일 내용·GUID가 동일한지, 미검수 모션 차단 여부.
- `review-*.png`: 1280×720 Unity RenderTexture 합성. 좌우 투구/접촉 및 땅볼/뜬공을 점검한다.
- `unity-tests.log`, `unity-visual.log`: 각 Unity 실행 로그.
- `unity-update-profile.json`: 예열 후 무대 갱신 600회의 CPU 시간과 관리 힙 할당. Canvas/GPU 렌더링을 제외하므로 전체 경기 60fps의 증거는 아니다.

검수 합성은 저장되지 않는 Catalog 복사본에서 수행한다. 타자 A는 화면 오른쪽의 L 후보, B는 화면 왼쪽의 R 후보로
배치해 시안과 비교한다. 이전 반대 매핑은 배트가 홈 바깥으로 향해 철회했다. 두 원본의 손잡이는 여전히 미확정이다.
**검수 이미지 생성은 손잡이 확정·Production 승인이나 실제 경기 검증 통과를 의미하지 않는다.**
원본 검수/경기 Catalog의 승인 값은 변경하지 않는다. 미검수 원본이 있으면 `isRuntimeReady`는 false여야 한다.

생성 에셋은 격리 프로젝트에 들어간다. 실제 프로젝트 등록은 통합 툴 런처의 `스프라이트 시트 처리`에서 같은 처리 폴더를 가져온다.
새 자산을 복사할 때는 PNG·SO·Atlas와 `.meta`를 함께 보존해야 Sprite GUID 참조가 유지된다.

이 검증은 실제 Unity UI 렌더링과 정적 연출 입력을 사용한다. 전체 선수 커리어/구단주 경기의 Play Mode 회귀,
실제 그래픽 장치에서 60fps·프레임 할당 측정, 시트의 아트 승인은 별도 완료 조건이다.
