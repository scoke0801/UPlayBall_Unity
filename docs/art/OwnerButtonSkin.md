# 구단주 버튼 스킨

ImageGen 내장 도구로 새로 제작한 구단주 전용 프레임이다. 선수 모드의 UI/Skin 프레임을 재사용하지 않는다.

- Navigation: 네이비·슬레이트 메뉴. 현재 메뉴는 Primary 프레임과 기존 선택 표시선을 함께 사용한다.
- Secondary: 아이보리 작업면의 패널 버튼과 보조 동작.
- Primary: 다음 경기, 스카우트 파견, 배치 저장, 덕아웃 결정. 네이비·샴페인 테두리로 행동 위계를 강조한다.
- Detail: 작은 상세 동작과 방침 선택기. Secondary 리소스에 좁은 라벨 여백을 사용한다.
- Tab: 선택 여부를 포인터 포커스와 독립적으로 유지한다.

OwnerUiButtonSkin은 원본 Image.color의 의미 상태를 보존하고 전용 자식 Image에 9-slice 프레임을 그린다. 자식은 레이아웃 계산에서 제외하며 버튼 전체를 클릭할 수 있다. CareerUiSkin 재적용은 전용 컴포넌트를 존중한다. 빈 Label의 카드/목록 클릭면에는 적용하지 않는다. 전용 프레임은 Resources/UI/OwnerSkin에 저장하며 원본 픽셀을 후처리하지 않고 Sprite 영역으로 메뉴 여백을 제외한다.

## 검증

Presentation 및 Presentation.Tests 컴파일: 오류 0, 경고 0.
분리된 Unity 6000.3.21f1 프로젝트에 실제 빌드 어셈블리와 리소스를 로드하여 EditMode 테스트 메서드 8건을 실행했고 모두 통과했다.
테스트는 재적용·역할별 리소스·탭 선택·모드 복귀·카드 제외·프레임 클릭 전달·비활성 클릭 차단·방침 색상 보존을 다룬다.
덕아웃 실제 View를 1920×1080, 1280×720으로 렌더링하여 버튼 프레임과 텍스트를 확인했다.
전체 게임 Play Mode 흐름 검증과 전체 EditMode 스위트 실행은 수행하지 않았다. 시뮬레이션 및 밸런스 수치는 변경하지 않았다.

- [버튼 역할별 렌더](OwnerButtonSkinPreview.png)
- [덕아웃 적용 렌더](OwnerButtonSkinDugout.png)
- [테스트 결과](OwnerButtonSkinTests.txt)

## 생성 프롬프트 원문

내장 image_gen 사용. 최종 채택 프롬프트 원문:

### Navigation

Use case: ui-mockup. Asset type: production raster UI button frame for a polished Korean baseball club management PC game, not a screenshot or mockup. Generate ONE empty wide rectangular NAVIGATION BUTTON, straight-on orthographic, centered, width 90% of canvas and height 65% of canvas, transparent background outside frame. Target canvas 1536x1024. Premium restrained baseball front-office aesthetic: deep midnight navy enamel inset, subtle fine material texture, thin brushed slate-silver double perimeter, tiny chamfered corners, very subtle cool top edge highlight and restrained muted brass bottom hairline. Beautiful crisp crafted finish, quiet center with no ornament so Korean text and a separate icon remain legible. Border detail limited to outer 4 percent, central 85 percent nearly uniform navy, intended for Unity nine-slice stretching. NO text, NO symbols, NO logo, NO ball, NO stitching, NO extra objects, NO large shadows, NO glow, NO perspective, NO rounded pill, NO frame variants. Genuine transparent alpha outside button.

### Secondary

Use case: ui-mockup. Production raster SECONDARY BUTTON frame for premium baseball management game. ONE empty rectangular warm ivory porcelain/light silver matte inset button, thin brushed slate silver perimeter, very thin midnight navy inner hairline, tiny chamfered corners. Straight-on flat graphic UI asset, refined pro sports front-office aesthetic. FILL ENTIRE CANVAS EDGE TO EDGE with button: top border touches top image edge, bottom border bottom image edge, left/right borders touch left/right edges. No margins, no background outside button. Target 1536x1024. Central 90 percent almost uniform warm off-white for dark navy labels. Minimal delicate material grain. Border limited to outer 2 percent with quiet center for Unity nine-slice. No text, logos, icons, symbols, ball, stitching, montage, perspective, heavy bevel, glow, shadows. Exactly one empty button.

### Primary

Use case: ui-mockup. Production raster PRIMARY ACTION BUTTON frame for premium baseball management game. ONE empty rectangular cobalt navy enamel button, brushed silver thin straight perimeter with restrained champagne gold inner edge, small chamfered corners. Front-on flat UI asset. Very quiet deep blue center with faint matte finish for ivory Korean text. Refined professional sports front office aesthetic, matching ivory/slate secondary controls. Border confined to outer 3 percent. Fill entire canvas edge-to-edge with this button, NO margins or external background. Target wide canvas 1536x1024, used as Unity nine-slice with very thin rendered borders. No text, icons, symbols, baseball, stitches, rivets, screws, montage, perspective, glow, shadows. Blue center slightly brighter than midnight navy navigation. The only object is the empty primary action button.
