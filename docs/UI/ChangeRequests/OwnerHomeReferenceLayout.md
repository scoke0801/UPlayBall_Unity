# 구단주 홈 참고 레이아웃

사용자 참고 이미지의 컨테이너 사무실과 우측 하단 정보 집중 구성을 적용한다. 빨간 X로 표시한 좌측 하단 채팅 영역은 만들지 않는다.

- 상단: 기존 실제 메뉴와 구단 상태를 유지한다. 홈에서는 중복 제목 행을 숨긴다.
- 중앙: 사무실 배경을 전체 표시한다.
- 우측 하단: 다음 경기 및 분석·준비, 구단 이름·리그·날짜, 선수단·자원, 저장·진행·모드 선택 순서다.
- 경기 버튼은 기존 로스터 검증과 일정 유무를 따른다. 온라인 미션이나 가짜 경기 진행 상태는 추가하지 않는다.
- 세부 화면에 진입하면 공용 작업 프레임과 제목 행을 복원한다.

생성 도구: built-in ImageGen. 생성 배경: `Assets/Resources/UI/Generated/bg_owner_container_office_v2.png`. 기존 v1 이미지는 보존한다.

최종 생성 프롬프트:

```text
Use case: stylized-concept. Asset type: full-screen 16:9 baseball management game lobby background, no UI baked in. Create a detailed nostalgic early-2000s Korean baseball club's modest temporary office inside a corrugated metal container. Wide interior camera from front left toward rear right. Left wall has a window, wall ventilation fan, hanging navy baseball jacket, water cooler, worn blue upholstered chair, baseball bats and gloves. Back wall has a cork noticeboard with unreadable paper shapes and old wall air conditioner. Center ceiling exposed ribs and one hanging bare bulb. Right foreground has a used manager desk, office chair, folders, telephone; standing fan near center. Realistic textured 3D game environment, muted charcoal gray and dusty navy, gentle warm bulb light and soft window daylight, clearly readable midtones, lived-in but welcoming. Match the spatial layout of a vintage baseball manager lobby. Entire image is the room, extending to all edges, no framing. Lower left remains visible room and floor, lower right will receive separate game UI overlays. No people, no logos, no readable text, no UI, no buttons, no watermark. Landscape 16:9.
```

검증: Presentation 보조 컴파일과 Unity 화면 검증은 작업 결과 보고에서 각각 구분한다. 시뮬레이션 및 밸런스 변경은 없다.
