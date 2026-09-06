# 선수 카드 원본 레퍼런스 아트

- 제작: 내장 ImageGen 도구. 최초 푸른 구장 시안은 사용자 피드백에 따라 폐기했다.
- 기준: `docs/디자인/ref/타자_카드_ref.png`, `docs/디자인/ref/선수오더_ref.png`.
- 원본의 전면 은색 사선 명찰·검은 능력치 표, 뒷면 좌측 프로필·우측 구종/수비 배치, 미니 카드의 초상·이름·비용·포지션 비율을 따른다.
- 선수·구단 이름, 연도, 능력치, 비용, 장착 블록과 구종 등급·구속은 런타임 uGUI로 표시한다.
- 전면 생성물의 예시 막대는 불투명 StatsPanel로 가린 뒤 실제 능력치만 그린다.

## 전면

저장: `Assets/Resources/UI/PlayerCards/PlayerCard_Front_Reference.png`

```text
Use case: style-transfer. Input image is the EXACT visual design reference from Professional Baseball Manager. Make a production FRONT card skin, not a redesign. The source contains two cards stacked: reproduce ONLY THE UPPER CARD's front layout and aesthetic at 1024x1536. Faithfully preserve its proportions: upper approximately 54% large portrait area, slim slanted silver team-name tab at very top, satin SILVER angular nameplate across 54%-66% with a small separate polygon year badge on its right, BLACK lower stats area from 67%-92% with six fine dark gray horizontal tracks and bright white bars (these bars will be covered by live UI), slim SILVER cost ribbon across bottom 93%-99%. Very thin straight dark outer border. Portrait area backdrop is a subtle warm burgundy team-color panel with faint checker texture, as in reference, NO stadium, NO blue lights, NO broad metallic perimeter. The nameplate has the SAME angular cut edges and brushed silver finish as source, not a simple rectangular strip. Preserve the original game's dense functional layout, not modern trading card style. Remove all photo/person, all letters/text/numbers, team logos, MVP wreath, upgrade icons, locks, stars and effects from this reusable skin. Empty portrait area and empty silver nameplate. No real team marks. Neutral black/silver for bottom, burgundy portrait backdrop. Full bleed exactly one front card filling the canvas; do not include the back card or white separator from source. Do not improvise a blue arena design.
```

## 미니 카드

저장: `Assets/Resources/UI/PlayerCards/PlayerCard_Mini_Reference.png`

```text
Use case: style-transfer. Reference image: an actual Professional Baseball Manager player-order screen. Reproduce ONE of its miniature rectangular player cards as a reusable blank skin at 1024x1536. Closely MATCH the tiny cards visible in the source: simple straight black rectangle with a ONE pixel white/silver outline at thumbnail scale; upper 68% is a plain LIGHT GRAY neutral photo placeholder, NOT a stadium scene. It will receive a tightly cropped headshot. Bottom 32% is an uninterrupted solid BLACK info strip for native UI name/year, with a narrow flat medium BLUE cost strip across its middle, and a final BLACK position strip at bottom. These cards are modest compact roster thumbnails, NOT ornate trading-card frames. Precisely flat simple rectangular edges, no corners cut, no beveled metal, no sci-fi border, no decorative silver nameplate. Absolutely no stadium, no spotlights, no blue sky, no grunge. No person or silhouette, no text/numbers/logos/icons/stars on this blank skin. Thin outline, light gray portrait space, black name strip, flat blue cost strip, black position strip. One isolated full bleed skin fills entire canvas, not a whole screenshot. MATCH reference style rather than redesigning it.
```

## 뒷면

저장: `Assets/Resources/UI/PlayerCards/PlayerCard_Back_Reference.png`

```text
Use case: style-transfer. Production Unity baseball card BACK skin. Source is an authentic Professional Baseball Manager reference with front and back vertically stacked. Recreate ONLY THE LOWER CARD's BACK structure at 1024x1536 full bleed, no outside margin. Keep reference's thin straight black outline, silver warm gray angular identity header at top right; light warm gray LEFT PROFILE COLUMN occupying x0%-28% and y0%-56%; burgundy subtle checker pattern RIGHT MAIN PANEL x29%-100%, y12%-56%; very slim silver gray RECORD HEADER full width y57%-62%, dark charcoal RECORD BODY full width y62%-70%; BLACK bottom SKILL BLOCK SECTION y71%-100%. Straight tight joins, dense early 2010s baseball manager interface, fine silver separating hairlines. Remove all players/photos, baseball field diagram, all text, names, letters, digits, all stars/skill blocks/icons/logos and borders inside main panels. These elements will be dynamic Unity UI. Leave the gray profile panel, silver identity header, burgundy main panel, silver record header, charcoal record body and black bottom panel EMPTY. No blue gradients, no stadium, no ornate heavy outer metal border, no 3D perspective, no whole screen, only one blank back card. Match source panel proportions and material closely.
```

