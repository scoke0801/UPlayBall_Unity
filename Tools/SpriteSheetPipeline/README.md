# 경기 스프라이트 시트 처리

저장소 루트에서 Python 3.10 이상과 Pillow를 사용한다. 기존 설치가 없다면
`python -m pip install --target output/sprite-sheet-ingame/python-deps Pillow`로 작업 폴더에 설치한다.
배경 제거는 `Tools/ImageBackground/Remove-ImageBackground.ps1 -Mode ChromaKey`를 호출하므로 Windows PowerShell이 필요하다.

```powershell
python Tools/SpriteSheetPipeline/process_sheets.py --validate-only
python -m unittest discover -s Tools/SpriteSheetPipeline -p test_process_sheets.py -v
python Tools/SpriteSheetPipeline/process_sheets.py
```

`sprite_sheet_sources.json`이 원본 경로·SHA256·손잡이·재생 순서·체류 시간·사건·공 마스크의 정본이다.
사건의 `frameIndex`는 재생 순서 기준이고 마스크의 `cellIndex`는 원본 셀 기준이다.
`framePivots`는 원본 셀 번호를 키로 하는 선택적 접지 좌표 보정이다. 검수자가 기록한 `x`와
`groundY`를 해당 프레임의 pivot·root·사건 접점 투영에 함께 적용하며, 없는 셀은 clip 공통 pivot을 사용한다.
원본이 바뀌면 손잡이·접점·그리드를 다시 확인하고 해시를 갱신한다.
`Approved`는 실제 모션 검수가 끝난 시트에만 사용한다.

처리 결과는 `output/sprite-sheet-ingame/Processed/<clipId>/`에 생성한다.
각 프레임 PNG는 실제 RGBA이며 원본 셀 크기와 공통 root를 보존한다.
밝고 어두운 배경을 번갈아 쓴 contact sheet, GIF, alpha, spill, QA JSON을 함께 만든다.
`build.json`은 입력·설정·처리기 해시와 출력 파일 해시를 기록한다. 재실행에서 모든 해시가 일치할 때만 캐시를 사용한다.
원본 PNG는 수정하지 않는다.

처리 메타데이터 schemaVersion 2는 사건마다 `hasSourcePosition`을 기록한다.
Unity JSON 역직렬화는 생략된 중첩 객체에도 기본값을 만들 수 있으므로, 이 플래그가 참인 접점만 가져온다.
기존 schemaVersion 1 출력은 처리기를 재실행해 갱신한다.

검증은 잘못된 사건 순서·손잡이·체류 시간 차단, 나누어떨어지지 않는 그리드,
11개 원본 해시, 실제 제거 도구를 이용한 흰 유니폼·파란 모자·1픽셀 갈색 배트·soft alpha 보존을 포함한다.
자동 통과가 손잡이·모션·카메라의 시각 검수를 대신하지 않는다.

Unity에서는 통합 툴 런처의 `경기 표현 → 스프라이트 시트 처리`에서 `Processed` 폴더를 가져온다.
격리 Unity 검증과 캡처 절차는 [SpriteMatchValidation](../SpriteMatchValidation/README.md)을 따른다.
