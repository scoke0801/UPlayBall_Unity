# 올스타·골든글러브 v3 제작 기록

내장 ImageGen으로 전체/미니 4장을 제작했다. 원본 v2와 다른 등급은 유지한다.
기존 OwnerPlayerCardFrames의 v3 우선 로딩으로 연결되며 코드 변경은 없다.

## 원작 조사

- https://www.inven.co.kr/webzine/news/?news=86969&site=bm : 원작 골든글러브 도입과 올스타와의 구분 확인. 기사에서 내려받은 이미지는 카드 앞면이 아닌 팩 구매 화면이다.
- https://www.inven.co.kr/webzine/news/?news=86752&site=bm : 원작 선수 카드의 정보 배치와 일반/레어 등급 설명 확인.
- 실제 문양의 시각 기준은 저장된 docs/디자인/ref/카드디자인_골든글러브_ref.png 및 카드디자인_올스타_ref.png이다. 웹 조사와 로컬 캡처의 역할을 구분한다.

## 최종 방향

골든글러브는 가는 양옆 줄기·작은 잎에만 은은한 금색을 허용한다. 사용자 후속 지시에 따른 무채색 규약의 예외다. 배경·테두리·명찰은 무채색이며 읽기 쉬운 밝기의 명찰을 사용한다. 올스타는 작은 별의 반복과 밝은 명찰을 사용하며 상단 리본은 없다. 굵은 월계관·글러브 부조·큰 별 시안은 채택하지 않았다.

## 산출물

- Assets/Resources/UI/PlayerCards/PlayerCard_Full_GoldenGlove_v3.png
- Assets/Resources/UI/PlayerCards/PlayerCard_Mini_GoldenGlove_v3.png
- Assets/Resources/UI/PlayerCards/PlayerCard_Full_AllStar_v3.png
- Assets/Resources/UI/PlayerCards/PlayerCard_Mini_AllStar_v3.png

## 프롬프트

검증: 4장 모두 1060×1484 PNG이며 Sprite 가져오기 설정과 독립 GUID를 확인했다.
원화에서 문양·명찰·빈 초상 영역을 시각 확인했고 기존 v3 우선 로더를 확인했다.
Unity 에디터의 실제 카드 합성·Play Mode 검증은 수행하지 않았다.

### Full GoldenGlove

Use case: precise-object-edit. Produce ONE Full blank baseball player card production texture, exactly 1060x1484. Image 1 is the layout edit target; Image 2 is the ORIGINAL Korean Pro Baseball Manager GoldenGlove card screenshot, the authoritative reference for understated visual language. The previous attempted redesign was rejected as far too ornamental. Follow ORIGINAL screenshot's flat 2010s sports-game print graphics, NOT luxury fantasy trading cards. Preserve Image 1 exact outer shape and region heights: top blank recessed tab; name band y=51%-61%; blank black stats y=61%-93%; footer y=93%-99%. Keep the card completely blank for runtime player/text overlays. Faithfully adapt image 2's very slender climbing leaf stems framing the portrait, with small sparse rhombus leaves and delicate flat silver highlights. Stem/leaf decoration mostly in outer 8% left/right, thin organic lines, no thick laurel branches. Plain narrow double-line border, NO ornate corner curls. Distinguish from Rare with a smooth dark graphite NAME BAND with fine pale silver upper/lower rules (no bright broad silver ribbon), dark graphite vignette and understated silver leaf flecks near upper side edges. No glove icons, trophies or medallions. STRICT monochrome grayscale across every pixel; do NOT copy screenshot's blue/gold/team colors. Dark smooth neutral portrait background; no linen, no noisy texture. Center kept quiet and clear for portrait. Restraint and faithful original game atmosphere are paramount. No invented symbols, no sculpted metal, no ornate corner pieces, no radial burst, no logos, no text, no numbers, no player or silhouette, no COST stars. No external background; opaque full bleed straight-on rectangle. Regions and overlay positions identical to image 1.

### Mini GoldenGlove

Use case: precise-object-edit. Produce ONE Mini blank baseball player card production texture, exactly 1060x1484. Image 1 is the layout edit target; Image 2 is the ORIGINAL Korean Pro Baseball Manager GoldenGlove card screenshot, the authoritative reference for understated visual language. The previous attempted redesign was rejected as far too ornamental. Follow ORIGINAL screenshot's flat 2010s sports-game print graphics, NOT luxury fantasy trading cards. Preserve Image 1 exact outer shape and region heights: portrait ends 72%; straight name band y=72%-82%; blank cost/status below 82%; no added top tab. Keep the card completely blank for runtime player/text overlays. Faithfully adapt image 2's very slender climbing leaf stems framing the portrait, with small sparse rhombus leaves and delicate flat silver highlights. Stem/leaf decoration mostly in outer 8% left/right, thin organic lines, no thick laurel branches. Plain narrow double-line border, NO ornate corner curls. Distinguish from Rare with a smooth dark graphite NAME BAND with fine pale silver upper/lower rules (no bright broad silver ribbon), dark graphite vignette and understated silver leaf flecks near upper side edges. No glove icons, trophies or medallions. STRICT monochrome grayscale across every pixel; do NOT copy screenshot's blue/gold/team colors. Dark smooth neutral portrait background; no linen, no noisy texture. Center kept quiet and clear for portrait. Restraint and faithful original game atmosphere are paramount. No invented symbols, no sculpted metal, no ornate corner pieces, no radial burst, no logos, no text, no numbers, no player or silhouette, no COST stars. No external background; opaque full bleed straight-on rectangle. Regions and overlay positions identical to image 1.

### Full AllStar

Use case: precise-object-edit. Produce ONE Full blank baseball player card production texture, exactly 1060x1484. Image 1 is the layout edit target; Image 2 is the ORIGINAL Korean Pro Baseball Manager AllStar card screenshot, the authoritative reference for understated visual language. The previous attempted redesign was rejected as far too ornamental. Follow ORIGINAL screenshot's flat 2010s sports-game print graphics, NOT luxury fantasy trading cards. Preserve Image 1 exact outer shape and region heights: top blank recessed tab; name band y=51%-61%; blank black stats y=61%-93%; footer y=93%-99%. Keep the card completely blank for runtime player/text overlays. Faithfully adapt image 2's tiny pale five-point stars receding in curved dotted arcs on either side behind the player, like a subtle printed starfield. Small stars only, maximum diameter 1.5% of card width; no oversized stars, no embossed stars, no diagonal beams. Give name strip a flat pearl light gray finish, with only a few faint tiny stars at its outer ends. Simple fine border without Rare's ornate corner curls. Omit original ALL STAR top ribbon entirely to respect existing UI constraint. STRICT monochrome grayscale across every pixel; do NOT copy screenshot's blue/gold/team colors. Dark smooth neutral portrait background; no linen, no noisy texture. Center kept quiet and clear for portrait. Restraint and faithful original game atmosphere are paramount. No invented symbols, no sculpted metal, no ornate corner pieces, no radial burst, no logos, no text, no numbers, no player or silhouette, no COST stars. No external background; opaque full bleed straight-on rectangle. Regions and overlay positions identical to image 1.

### Mini AllStar

Use case: precise-object-edit. Produce ONE Mini blank baseball player card production texture, exactly 1060x1484. Image 1 is the layout edit target; Image 2 is the ORIGINAL Korean Pro Baseball Manager AllStar card screenshot, the authoritative reference for understated visual language. The previous attempted redesign was rejected as far too ornamental. Follow ORIGINAL screenshot's flat 2010s sports-game print graphics, NOT luxury fantasy trading cards. Preserve Image 1 exact outer shape and region heights: portrait ends 72%; straight name band y=72%-82%; blank cost/status below 82%; no added top tab. Keep the card completely blank for runtime player/text overlays. Faithfully adapt image 2's tiny pale five-point stars receding in curved dotted arcs on either side behind the player, like a subtle printed starfield. Small stars only, maximum diameter 1.5% of card width; no oversized stars, no embossed stars, no diagonal beams. Give name strip a flat pearl light gray finish, with only a few faint tiny stars at its outer ends. Simple fine border without Rare's ornate corner curls. Omit original ALL STAR top ribbon entirely to respect existing UI constraint. STRICT monochrome grayscale across every pixel; do NOT copy screenshot's blue/gold/team colors. Dark smooth neutral portrait background; no linen, no noisy texture. Center kept quiet and clear for portrait. Restraint and faithful original game atmosphere are paramount. No invented symbols, no sculpted metal, no ornate corner pieces, no radial burst, no logos, no text, no numbers, no player or silhouette, no COST stars. No external background; opaque full bleed straight-on rectangle. Regions and overlay positions identical to image 1.

### 골든글러브 최종 금색 보정 (Full/Mini 공통)

Use case: precise-object-edit. Edit this exact blank baseball Golden Glove card texture with minimal changes. User correction: the fine tree stems and leaves along BOTH portrait sides should be slightly more GOLD in color. Color only these existing slender stems and small leaves a restrained warm champagne antique gold, softly visible, e.g. muted #B69B58 midtone and #D6C18A highlights. Keep their exact shapes, counts, positions and thinness; no new ornaments or symbols. All background, outer border, top tab, lower stats panel and footer remain strictly neutral grayscale. Also ensure the existing blank NAME BAND is medium-light neutral silver gray (#999999 to #BBBBBB) so existing near-black runtime name text is readable; keep its flat restrained finish and original exact geometry. Do not add text or symbols. Preserve all region positions and the exact 1060x1484 canvas. Do not introduce gold elsewhere, do not thicken branches, no medallions, no glove icons, no leaves added, no lighting effects, no colored background. Every other detail identical.
