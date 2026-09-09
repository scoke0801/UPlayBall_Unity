# 스프라이트 경기 표현 검증

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
