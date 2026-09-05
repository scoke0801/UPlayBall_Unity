# Generated UI Assets

ImageGen으로 실제 생성하고 프로젝트에 반입한 자산을 기록한다. 실제 Panel, Button, Tab, Table, Text는 이 이미지에 굽지 않고 Unity UI로 구성한다.

## `bg_player_clubhouse_v1.png`

- **경로:** `Assets/Resources/UI/Generated/bg_player_clubhouse_v1.png`
- **용도:** Player Home 뒤에 배치하는 저대비 환경 배경. 중앙과 우측의 여백 위에 선수 상태, 다음 경기, 감독 기용 정보 Panel을 올리는 구성을 전제로 한다.
- **생성 방식:** Codex built-in ImageGen
- **원본 해상도:** 1672×941, RGB, 약 16:9 landscape
- **9-Slice:** 아니오
- **Unity Import 권장값:** Texture Type `Sprite (2D and UI)`, Sprite Mode `Single`, Mesh Type `Full Rect`, sRGB `On`, Alpha Is Transparency `Off`, Read/Write `Off`, Generate Mip Maps `Off`, Wrap Mode `Clamp`, Filter Mode `Bilinear`, Max Size `2048`, Compression `Normal Quality`. 화면 비율이 다르면 중앙 UI-safe 영역을 유지하도록 Aspect Fill로 가장자리만 Crop한다.
- **적용 대상:** `UI_Scene_CareerDashboard.RenderBackgroundAccents`의 Player Home 장식 배경. Runtime에서 Texture를 한 번 읽고 Sprite를 캐시하며, Native uGUI Panel 아래에 낮은 대비로 합성한다.

### 최종 Prompt

```text
Use case: stylized-concept
Asset type: project-bound 16:9 game UI background for a desktop baseball player-career home screen
Primary request: an original fictional professional baseball clubhouse transition space that connects a quiet locker room to a generic dugout corridor; the room is completely empty, prepared before a game, believable but not tied to any real team
Scene/backdrop: restrained clubhouse interior with simple unlabeled lockers and benches on the far left, a short open passage suggesting a dugout and a narrow glimpse of muted field grass at the far edge; no featured props or focal memorabilia
Style/medium: polished semi-realistic game environment background, grounded PC sports-management game aesthetic, subtle painterly realism, not a UI mockup and not concept-art spectacle
Composition/framing: exact 16:9 landscape; wide eye-level architectural view; keep the central 55% and most of the right side visually quiet, low-detail, and low-contrast for dense Unity UI panels and text; environment interest stays near outer edges; stable straight perspective; no foreground object crossing the UI-safe area
Lighting/mood: soft overcast daylight mixed with gentle practical interior light, calm professional preseason mood, subdued highlights, no dramatic sun shafts
Color palette: neutral graphite, charcoal, off-white, muted navy, metallic gray, with only a small localized low-saturation grass green accent; avoid dominant team colors
Materials/textures: softly worn painted metal lockers, matte concrete, restrained wood bench grain, subtle realistic wear without grime
Constraints: background artwork only; absolutely no people; no text, numbers, letters, nameplates, signage, scoreboard, watermark, logos, emblems, mascots, flags, UI panels, UI buttons, UI frames, player cards, uniforms, jerseys, helmets, trophies, photographs, branded equipment, real stadium identifiers, KBO or MLB references; no actual team identity; maintain excellent dark/light separation for overlaid off-white text and graphite UI panels
Avoid: cyberpunk, science-fiction HUD, neon glow, glassmorphism, mobile-game dashboard, dramatic cinematic action, clutter, saturated red or blue, lens flare, shallow depth of field, exaggerated vignette
```

## `bg_owner_front_office_v1.png`

- **경로:** `Assets/Resources/UI/Generated/bg_owner_front_office_v1.png`
- **용도:** Owner Home 뒤에 배치하는 저대비 프런트 오피스 배경. 중앙 관리 Dashboard를 방해하지 않으면서 오른쪽 가장자리의 구장 일부로 구단 운영 맥락을 전달한다.
- **생성 방식:** Codex built-in ImageGen
- **원본 해상도:** 1672×941, RGB, 약 16:9 landscape
- **9-Slice:** 아니오
- **Unity Import 권장값:** Texture Type `Sprite (2D and UI)`, Sprite Mode `Single`, Mesh Type `Full Rect`, sRGB `On`, Alpha Is Transparency `Off`, Read/Write `Off`, Generate Mip Maps `Off`, Wrap Mode `Clamp`, Filter Mode `Bilinear`, Max Size `2048`, Compression `Normal Quality`. 화면 비율이 다르면 중앙 UI-safe 영역을 유지하도록 Aspect Fill로 가장자리만 Crop한다.
- **적용 대상:** `UI_Scene_OwnerHome`의 실제 Runtime 배경. `OwnerWorkspaceUiFactory`가 Resources 경로로 한 번 로드해 Native uGUI Panel 뒤에 배치한다.

### 최종 Prompt

```text
Use case: stylized-concept
Asset type: project-bound 16:9 game UI background for a desktop baseball club-owner management home screen
Primary request: an original fictional professional baseball club front office overlooking part of a generic ballpark, completely empty and ready for a workday, conveying practical long-term team management rather than luxury or spectacle
Scene/backdrop: restrained front-office operations room with graphite built-in cabinetry and a modest off-white work surface near the far left edge, simple architectural wall sections, and a broad window at the far right showing only a quiet partial view of generic green baseball field, seating, and structural canopy; no featured desk objects
Style/medium: polished semi-realistic game environment background, grounded PC sports-management game aesthetic, subtle painterly realism, not a UI mockup and not cinematic concept-art spectacle
Composition/framing: exact 16:9 landscape; wide eye-level architectural view; reserve the central 60% and much of the right-center as visually quiet, low-detail, low-contrast space for dense Unity UI panels and text; keep room details and window framing near outer edges; stable straight perspective; no foreground object crossing the UI-safe area
Lighting/mood: soft neutral morning daylight through the window with subdued interior practical lighting, focused and professional, gentle shadows, no dramatic sun shafts
Color palette: neutral graphite, charcoal, off-white, muted navy, metallic gray, with a small localized low-saturation grass green visible only through the window; avoid dominant team colors
Materials/textures: matte painted cabinetry, finely brushed dark metal, restrained wood or laminate, subtle concrete texture, realistic but quiet surface wear
Constraints: background artwork only; absolutely no people; no text, numbers, letters, documents with writing, signage, scoreboard, watermark, logos, emblems, mascots, flags, UI panels, UI buttons, UI frames, player cards, uniforms, jerseys, helmets, trophies, photographs, branded office objects, real stadium identifiers, KBO or MLB references; no actual team identity; maintain excellent dark/light separation for overlaid off-white text and graphite UI panels
Avoid: cyberpunk, science-fiction command center, neon glow, glassmorphism, mobile-game dashboard, executive luxury cliché, dramatic cinematic action, clutter, saturated red or blue, lens flare, shallow depth of field, exaggerated vignette
```

## 검수 결과

- 인물, 실제 선수 얼굴, 유니폼, 실제 구단 식별 요소 없음
- Text, 숫자, Logo, Watermark, UI Button/Panel bake 없음
- 공통 Palette인 graphite/off-white/muted navy와 국소적인 저채도 grass green 사용
- 중앙 정보 Panel 영역은 저대비·저디테일로 확보
- 두 자산 모두 배경 전용이므로 9-Slice 미사용
