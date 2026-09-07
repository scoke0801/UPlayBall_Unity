# 특수 카드 프레임 ImageGen 프롬프트

내장 ImageGen 편집 도구로 생성한 실제 UI용 PNG 원화 8종이다. 기존 `docs/art/OwnerPlayerCardFrames.md` 프롬프트와 `PlayerCard_Mini_Normal_v2.png`, `PlayerCard_Full_Normal_v2.png`를 공통 판형 기준으로 재사용했다. 기존 원화는 덮어쓰지 않았다.

## 산출물과 배치 계약

- 저장: `Assets/Resources/UI/PlayerCards/PlayerCard_{Mini|Full}_{Rare|Ex|CareerHigh|Legend}_v2.png` 및 Sprite meta.
- Mini: 상단 기준 명찰 72~82%, COST·포지션용 하단 82~100%. 원본 명찰 약 y=1073~1223px.
- Full: 상단 기준 명찰 51~61%, 능력치 61~93%, 하단 93~99%. 기존 곡면 명찰을 유지한다.
- 인물·이름·포지션·COST 별·수치·등급 라벨은 굽지 않았다. 실제 카드 공통 오버레이가 배치한다.
- Rare는 백금 이중 테두리, Ex는 건메탈과 교차 각인, CareerHigh는 앰버 상승선, Legend는 고금색 모서리 세공으로 구별한다.
- 레퍼런스 `docs/디자인/ref/레전드_ref.png`, `커리어하이_ref.png`는 해당 등급의 색감과 소재만 참고했다. UI 배치는 기존 카드 원화를 따른다.
- 생성 원화 8종을 육안 확인했다. CareerHigh Mini 초안의 명찰 이동은 재생성으로 보정했다. Unity 임포트·화면·빌드 테스트는 사용자 요청에 따라 실행하지 않았다.

## 최종 프롬프트

### Mini Rare

입력 원화: `Assets/Resources/UI/PlayerCards/PlayerCard_Mini_Normal_v2.png`.

```text
Use case: style-transfer. Edit the attached existing MINI baseball card FRAME production texture into RARE edition. Preserve exact canvas aspect ratio and layout, every boundary and position. One portrait 1060x1484 frame fills canvas. NAME STRIP y=72% to82% from top, portrait ends at72%, empty footer82%-100%. Preserve original straight name strip, thin outline and all region heights with pixel fidelity. Change only border material and restrained edition motifs: subtle pale platinum sheen and fine etched double-line border, tiny geometric corner cuts. Overall neutral charcoal/silver grayscale matching original sports management UI. Keep portrait interior quiet graphite gray. No sci-fi or fantasy, no large decorations. No player, silhouette, text, numbers, COST stars, logos, data, badges, panel subdivisions. Blank frame texture only.
```

### Mini Ex

입력 원화: `Assets/Resources/UI/PlayerCards/PlayerCard_Mini_Normal_v2.png`.

```text
Use case: style-transfer. Edit the attached existing MINI baseball card FRAME production texture into EX edition. Preserve exact canvas aspect ratio and layout, every boundary and position. One portrait 1060x1484 frame fills canvas. NAME STRIP y=72% to82% from top, portrait ends at72%, empty footer82%-100%. Preserve original straight name strip, thin outline and all region heights with pixel fidelity. Change only border material and restrained edition motifs: dark brushed gunmetal with thin brighter silver beveled edges and restrained etched angular corner accents. A faint embossed crossing-line motif only at portrait outer corners. Overall neutral charcoal/silver grayscale matching original sports management UI. Keep portrait interior quiet graphite gray. No sci-fi or fantasy, no large decorations. No player, silhouette, text, numbers, COST stars, logos, data, badges, panel subdivisions. Blank frame texture only.
```

### Mini CareerHigh

입력 원화: `Assets/Resources/UI/PlayerCards/PlayerCard_Mini_Normal_v2.png`.

```text
Use case: style-transfer. Edit the first attached existing MINI baseball card FRAME production texture into CAREER HIGH edition. Preserve exact canvas aspect ratio and layout, every boundary and position. One portrait 1060x1484 frame fills canvas. NAME STRIP y=72% to82% from top, portrait ends at72%, empty footer82%-100%. Preserve original straight name strip, thin outline and all region heights with pixel fidelity. Change only border material and restrained edition motifs: warm amber bronze fine border and dark charcoal base. Very subtle rising diagonal amber trajectory etched near portrait lower sides only, conveying a career peak. Name strip light champagne silver-gold for almost-black RGB18,20,24 UI text readability. Supporting image 2 supplies ONLY warm amber material direction; never copy its layout, UI, player or typography. Keep portrait center quiet neutral graphite gray. Korean classic baseball manager UI. No sci-fi or fantasy, no large decorations. No player, silhouette, text, numbers, COST stars, logos, data, badges, panel subdivisions. Blank frame texture only. CRITICAL PIXEL COORDINATES: 1060x1484 canvas. Straight name strip starts y=1073 and ends y=1223, precisely as IMAGE 1. Portrait MUST end at1073. Footer begins1223. Do not put name strip at y1120 or lower. Only recolor the original template and etch small amber upward lines at side edges. Original panel boundary positions are immutable.
```

### Mini Legend

입력 원화: `Assets/Resources/UI/PlayerCards/PlayerCard_Mini_Normal_v2.png`.

```text
Use case: style-transfer. Edit the first attached existing MINI baseball card FRAME production texture into LEGEND edition. Preserve exact canvas aspect ratio and layout, every boundary and position. One portrait 1060x1484 frame fills canvas. NAME STRIP y=72% to82% from top, portrait ends at72%, empty footer82%-100%. Preserve original straight name strip, thin outline and all region heights with pixel fidelity. Change only border material and restrained edition motifs: antique gold fine border over deep brown charcoal base. Delicate archival gold filigree confined to thin outer corners and tiny laurel etching along portrait lower side edges, historical baseball collector card dignity. Name strip brushed subdued old gold. Supporting image 2 supplies ONLY antique gold/brown material and historical ornament direction; never copy its layout, UI, player or typography. Keep portrait center quiet neutral graphite gray. Korean classic baseball manager UI. No sci-fi or fantasy, no large decorations. No player, silhouette, text, numbers, COST stars, logos, data, badges, panel subdivisions. Blank frame texture only. CRITICAL: name strip exactly y=1073 to1223 pixels in the 1060x1484 canvas, matching first input. Do not lower name strip.
```

### Full Rare

입력 원화: `Assets/Resources/UI/PlayerCards/PlayerCard_Full_Normal_v2.png`.

```text
Use case: style-transfer. Edit the attached existing FULL baseball card FRAME production texture into RARE edition. Preserve exact canvas aspect ratio and layout, every boundary and position. One portrait 1060x1484 frame fills canvas. NAME STRIP y=51% to61% from top, portrait ends at51%, empty stats area61%-93%, footer93%-99%. Preserve original curved name strip, thin outline and all region heights with pixel fidelity. Change only border material and restrained edition motifs: subtle pale platinum sheen and fine etched double-line border, tiny geometric corner cuts. Overall neutral charcoal/silver grayscale matching original sports management UI. Keep portrait interior quiet graphite gray. No sci-fi or fantasy, no large decorations. No player, silhouette, text, numbers, COST stars, logos, data, badges, panel subdivisions. Blank frame texture only.
```

### Full Ex

입력 원화: `Assets/Resources/UI/PlayerCards/PlayerCard_Full_Normal_v2.png`.

```text
Use case: style-transfer. Edit the attached existing FULL baseball card FRAME production texture into EX edition. Preserve exact canvas aspect ratio and layout, every boundary and position. One portrait 1060x1484 frame fills canvas. NAME STRIP y=51% to61% from top, portrait ends at51%, empty stats area61%-93%, footer93%-99%. Preserve original curved name strip, thin outline and all region heights with pixel fidelity. Change only border material and restrained edition motifs: dark brushed gunmetal with thin brighter silver beveled edges and restrained etched angular corner accents. A faint embossed crossing-line motif only at portrait outer corners. Overall neutral charcoal/silver grayscale matching original sports management UI. Keep portrait interior quiet graphite gray. No sci-fi or fantasy, no large decorations. No player, silhouette, text, numbers, COST stars, logos, data, badges, panel subdivisions. Blank frame texture only.
```

### Full CareerHigh

입력 원화: `Assets/Resources/UI/PlayerCards/PlayerCard_Full_Normal_v2.png`.

```text
Use case: style-transfer. Edit the first attached existing FULL baseball card FRAME production texture into CAREER HIGH edition. Preserve exact canvas aspect ratio and layout, every boundary and position. One portrait 1060x1484 frame fills canvas. NAME STRIP y=51% to61% from top, portrait ends at51%, empty stats area61%-93%, footer93%-99%. Preserve original curved name strip, thin outline and all region heights with pixel fidelity. Change only border material and restrained edition motifs: warm amber bronze fine border and dark charcoal base. Very subtle rising diagonal amber trajectory etched near portrait lower sides only, conveying a career peak. Name strip light champagne silver-gold for almost-black RGB18,20,24 UI text readability. Supporting image 2 supplies ONLY warm amber material direction; never copy its layout, UI, player or typography. Keep portrait center quiet neutral graphite gray. Korean classic baseball manager UI. No sci-fi or fantasy, no large decorations. No player, silhouette, text, numbers, COST stars, logos, data, badges, panel subdivisions. Blank frame texture only.
```

### Full Legend

입력 원화: `Assets/Resources/UI/PlayerCards/PlayerCard_Full_Normal_v2.png`.

```text
Use case: style-transfer. Edit the first attached existing FULL baseball card FRAME production texture into LEGEND edition. Preserve exact canvas aspect ratio and layout, every boundary and position. One portrait 1060x1484 frame fills canvas. NAME STRIP y=51% to61% from top, portrait ends at51%, empty stats area61%-93%, footer93%-99%. Preserve original curved name strip, thin outline and all region heights with pixel fidelity. Change only border material and restrained edition motifs: antique gold fine border over deep brown charcoal base. Delicate archival gold filigree confined to thin outer corners and tiny laurel etching along portrait lower side edges, historical baseball collector card dignity. Name strip brushed subdued old gold. Supporting image 2 supplies ONLY antique gold/brown material and historical ornament direction; never copy its layout, UI, player or typography. Keep portrait center quiet neutral graphite gray. Korean classic baseball manager UI. No sci-fi or fantasy, no large decorations. No player, silhouette, text, numbers, COST stars, logos, data, badges, panel subdivisions. Blank frame texture only.
```

CareerHigh Mini 보정은 Normal Mini 한 장을 입력으로 사용했다. 프롬프트의 두 번째 이미지 설명은 초기 생성에서 사용했던 색감 지시를 보존한 문구다.

## 검정 명찰 글씨 대비 보정

실제 카드의 명찰 글씨는 RGB(18,20,24)이므로 CareerHigh와 Ex의 Mini/Full 명찰을 밝은 샴페인/백금색으로 재편집했다. 명찰 경계와 다른 영역은 유지하도록 지시했다.

### Mini CareerHigh 명찰 보정

```text
Use case: precise-object-edit. Change ONLY the blank horizontal NAME BAND at y=72%-82% of this MINI card: recolor its dark bronze fill to bright light champagne silver-gold, consistent uniform lightness across entire width so almost-black RGB18,20,24 game UI text will be clearly readable on top. Do not add text. Preserve name band position, dimensions, exact outline, entire rest of card image including amber borders and rising diagonal side motifs pixel-identically. Keep all portrait, footer, COST position regions unchanged. Output one frame 1060x1484 pixels.
```

### Mini Ex 명찰 보정

```text
Use case: precise-object-edit. Change ONLY the blank horizontal NAME BAND at y=72%-82% of this MINI card: recolor its dark gunmetal fill to bright light neutral platinum silver, consistent uniform lightness across entire width so almost-black RGB18,20,24 game UI text will be clearly readable on top. Do not add text. Preserve name band position, dimensions, exact outline, entire rest of card image including silver borders and etched cross-line corner motifs pixel-identically. Keep all portrait, footer, COST position regions unchanged. Output one frame 1060x1484 pixels.
```

### Full CareerHigh 명찰 보정

```text
Use case: precise-object-edit. Change ONLY the blank curved NAME BAND at y=51%-61% of this FULL card: recolor its dark bronze fill to bright light champagne silver-gold, consistent uniform lightness across entire width so almost-black RGB18,20,24 game UI text will be clearly readable on top. Do not add text. Preserve name band position, dimensions, exact outline, entire rest of card image including amber borders and rising diagonal side motifs pixel-identically. Keep all portrait, footer, COST position regions unchanged. Output one frame 1060x1484 pixels.
```

### Full Ex 명찰 보정

```text
Use case: precise-object-edit. Change ONLY the blank curved NAME BAND at y=51%-61% of this FULL card: recolor its dark gunmetal fill to bright light neutral platinum silver, consistent uniform lightness across entire width so almost-black RGB18,20,24 game UI text will be clearly readable on top. Do not add text. Preserve name band position, dimensions, exact outline, entire rest of card image including silver borders and etched cross-line corner motifs pixel-identically. Keep all portrait, footer, COST position regions unchanged. Output one frame 1060x1484 pixels.
```

