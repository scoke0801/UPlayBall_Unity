# 타이틀 버튼·프레임 스킨

타이틀은 선수 모드와 구단주 모드의 공용 진입점이므로 어느 모드의 전용 자산도 재사용하지 않는다.
구단주 UI와 동일하게 ImageGen으로 제작한 역할별 9-slice 프레임을 사용하되, 민트 포인트와
밝은 업무면으로 중립적인 타이틀 시각 언어를 유지한다.

- Mode: 선수 모드와 구단주 모드의 동등한 선택 카드 및 타이틀 패널·확인 Popup.
- Secondary: 카드 디자인 보기, 설정, 크레딧, 취소 같은 보조 동작.
- Primary: 확인처럼 흐름을 전진시키는 주요 동작.
- Danger: Secondary 프레임에 붉은 의미 Tint를 적용하고 밝은 라벨 대비를 사용한다.

`TitleUiButtonSkin`은 원본 `Image.color`와 생성 프레임을 분리한다. 프레임 자식 Image가 버튼의
`targetGraphic`과 전체 Raycast 영역을 맡으므로 빈 기본 Label을 사용하는 모드 선택 카드도 클릭할 수 있다.
공용 `CareerUiSkin`은 활성화된 타이틀 스킨을 발견하면 다시 일반 카드 버튼으로 바꾸지 않는다.
프레임은 `Assets/10.Datas/Resources/UI/TitleSkin/`에 저장한다.

## 생성 방식

Codex 내장 `image_gen`을 사용했다. 기존 구단주 Primary/Secondary 자산은 품질과 재질 언어를 위한
스타일 레퍼런스로만 전달했고 결과물은 타이틀 전용으로 새로 생성했다.

### Mode

```text
Use case: ui-mockup
Asset type: production raster 9-slice MODE SELECTION CARD frame for the shared title screen of a polished Korean baseball career management PC game.
Input images: Image 1 and Image 2 are style references only; match their restrained front-office craft quality and family resemblance, but create a distinct title-screen asset rather than copying either.
Primary request: Generate exactly ONE empty wide rectangular mode-selection card, straight-on orthographic. Warm pale ivory/light silver matte center for dark Korean overlay text, with a thin brushed slate-silver outer perimeter, a very thin midnight-navy inner hairline, tiny chamfered corners, and one restrained muted mint accent hairline along the lower inside edge to connect player-career and club-owner modes neutrally.
Composition/framing: Fill the entire 1536x1024 canvas edge-to-edge with the card; all four borders touch the image edges; quiet uniform center occupying at least 90 percent; border details confined to outer 3 percent; designed for aggressive Unity 9-slice stretching to wide cards.
Materials/textures: Subtle premium paper/enamel grain only, no large gradients.
Text: none.
Constraints: exactly one empty frame; perfectly front-on; crisp symmetric border; no external margin; no cast shadow; no perspective.
Avoid: text, letters, numbers, logos, icons, people, baseballs, stitching, rivets, screws, ornaments in the center, sci-fi HUD styling, glow, montage, multiple variants.
```

### Secondary

```text
Use case: ui-mockup
Asset type: production raster 9-slice SECONDARY BUTTON frame for the shared title screen of a polished Korean baseball career management PC game.
Input images: Image 1 is a style reference only; preserve its restrained professional front-office quality but create a distinct title-screen control.
Primary request: Generate exactly ONE empty rectangular warm ivory/light silver matte button, with a thin brushed slate-silver perimeter, very thin midnight-navy inner hairline, tiny chamfered corners, and a restrained muted mint highlight on the lower inner edge.
Composition/framing: Fill the entire 1536x1024 canvas edge-to-edge; all borders touch canvas edges; quiet uniform center occupying at least 92 percent; border details confined to outer 2 percent; intended for Unity 9-slice stretching into wide 150x42 to 280x52 controls.
Materials/textures: subtle premium paper/enamel grain only.
Text: none.
Constraints: exactly one empty button, perfectly front-on, crisp symmetric border, no external margin, no cast shadow, no perspective.
Avoid: text, letters, numbers, logos, icons, baseballs, stitching, rivets, screws, central ornaments, sci-fi HUD style, glow, montage, multiple variants.
```

### Primary

```text
Use case: ui-mockup
Asset type: production raster 9-slice PRIMARY ACTION BUTTON frame for the shared title screen of a polished Korean baseball career management PC game.
Input images: Image 1 is a style reference only; preserve its restrained professional front-office craft quality but create a distinct title-screen control.
Primary request: Generate exactly ONE empty rectangular deep midnight-navy enamel button, with a thin brushed silver outer perimeter, a restrained muted mint inner hairline, and tiny chamfered corners. The center must stay quiet and dark enough for ivory Korean overlay text.
Composition/framing: Fill the entire 1536x1024 canvas edge-to-edge; all borders touch canvas edges; uniform center occupying at least 92 percent; border details confined to outer 2 percent; intended for Unity 9-slice stretching into wide confirmation controls.
Materials/textures: subtle premium enamel grain only, no large gradients.
Text: none.
Constraints: exactly one empty button, perfectly front-on, crisp symmetric border, no external margin, no cast shadow, no perspective.
Avoid: text, letters, numbers, logos, icons, baseballs, stitching, gold ornament, rivets, screws, central ornaments, sci-fi HUD style, glow, montage, multiple variants.
```
