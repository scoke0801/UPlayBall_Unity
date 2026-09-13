# 구단주 UI 리뉴얼 ImageGen 제작물

화면 시안과 실제 자산을 별도로 생성했다. 시안의 선수 그림·영문 라벨은 설계 참고용이며 게임에 넣지 않았다.
기존 선수·매니저·구단 로고는 교체하지 않는다.

| 파일 | 규격·용도 | Border (L/B/R/T) | 사용처 |
|---|---|---|---|
| training-design-study.png | 1672×941 화면 설계 참고 | 해당 없음 | 문서 전용 |
| UI_Frame_Surface.png | 1536×1024 불투명 본문 프레임 | 24/24/24/24 | 공용 ManagerReport·CompactStrip 역할 |
| UI_Frame_Speech.png | 1536×1024 실제 RGBA 말풍선 | 128/208/160/184 | 구단주 정보의 매니저 안내 |
| speech-alpha-composite.png | 1152×384 밝고 어두운 배경 합성 | 해당 없음 | 이미지 파일 검수, Unity 화면 아님 |

프로덕션 PNG와 meta는 `Assets/10.Datas/Resources/UI/BaseballFrontOffice/V2/Frames/`에 있다.
PPU 100, Full Rect, Clamp, Mipmap Off. Surface multiplier 2, Speech multiplier 8.
최초 ImageGen 원본은 `output/imagegen/owner-ui-renewal/`에도 별도 보존했다.
말풍선 원본에는 초록 배경이 있고 최종본은 `Remove-ImageBackground.ps1 -Mode ChromaKey -KeyColor '#00FF00'`로 제거했다.
말풍선 1,572,864픽셀 중 완전 투명 631,914, 부분 투명 3,074. 밝고 어두운 합성에서 외곽과 꼬리를 확인했다.
불투명 Surface에는 알파 제거가 필요 없다. 실제 Unity 9-Slice·폰트·합성은 실행하지 않았다.

기존 V2 29종 버튼·탭·배지·Hero 자산은 재사용했다. 이전 프롬프트는
[FrontOfficeSkin/Prompts.md](../../../Tools/FrontOfficeSkin/Prompts.md)에 있다.

## 생성 프롬프트

### 화면 설계

Use case: ui-mockup / game UI asset. Create a UI DESIGN STUDY ONLY for UPlayBall PC baseball management owner front office, wide 16:9 desktop dashboard. Sleek Korean PC sports management, charcoal #07111C page, midnight navy #0D1B2A main surfaces, #122638 inputs, thin #29465D keylines, sparse warm gold #DCB86A, ivory #EDF3F8 text. Show horizontal slim top navigation band, left dense 3-column baseball card collection with abstract portrait placeholders, right selected card and training comparison horizontal bars, costs and one gold CTA. Surface hierarchy only 3 levels, no nested boxes around every statistic. Background extremely subdued clubhouse. Materials subtle painted satin texture and crisp 1px edges, corners gently rounded, no fantasy ornaments, no neon, no watermark. No actual player likenesses or logos. Use minimal English labels for the design study. This is NOT a production sprite.

### Surface

Use case: game UI asset, SINGLE production sprite for UPlayBall PC baseball management. Output one flat front-facing rectangular dark navy Surface texture, fills ENTIRE image edge to edge, landscape 3:2. Completely opaque. NOT a mockup, NOT a scene, NO text, NO icons, NO characters, NO logo, NO objects. Center #0D1B2A nearly uniform with extremely subtle painted satin grain (3% contrast), slightly lighter #122638 at top, restrained thin blue-gray #29465D keyline exactly along all four image edges, no outer margins. NO gold corner ornaments, NO bands, NO inset boxes, NO reflections, NO perspective, NO glow, NO cast shadow. Crisp square corners. 9-slice friendly: all surface detail confined to outer 24 pixels, central 90% visually uniform, barely visible texture. This is a reusable calm information panel background underneath live UI text. High quality premium sports front-office visual material, minimal.

### Speech

Use case: SINGLE production game UI speech bubble asset for UPlayBall PC baseball management front office. Blank wide 3:2 rectangle navy #122638 speech panel with very small triangular speech tail at middle RIGHT edge pointing right toward a speaker portrait. Entire bubble centered with 8% margin on flat uniform pure chroma key green #00FF00 background. Background MUST be solid green without gradient texture shadow or checkerboard. Foreground fully opaque midnight navy painted satin, thin muted steel blue #29465D border, modest radius 8px, center uniform near #0D1B2A, 3% subtle texture. ZERO text, icons, letters, numbers, logos, objects. No cast shadow outside, no gold ornaments, no glow, no green spill on foreground. Crisp frontal 2D sprite, nine slice friendly corners and tail confined to outer edge, preserve a large uniform center. This is not a scene or mockup.
