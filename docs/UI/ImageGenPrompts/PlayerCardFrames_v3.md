# 상위 선수 카드 프레임 v3

내장 ImageGen으로 노말 원화를 편집해 MVP·Rare·EX·Legend·CareerHigh의 Full/Mini 원화를 생성한다. 프레임 장식과 소재를 강화하되 이름·Cost·포지션 영역의 기존 배치를 유지한다. 라벨은 이미지에 굽지 않고 UI로 배치한다.

- 저장 위치: `Assets/Resources/UI/PlayerCards/PlayerCard_{Mini|Full}_{MVP|Rare|Ex|Legend|CareerHigh}_v3.png`.
- MVP: 루비 레드·금색 월계 장식. Rare: 코발트·백금. EX: 크림슨·티타늄. Legend: 고금색 세공. CareerHigh: 에메랄드·샴페인 골드 상승 장식.
- 공통 렌더러와 디자인 팝업은 v3를 우선 사용하며, 없는 등급은 기존 v2를 사용한다.
- 팝업에서 일반/미니 모아보기, 원화/정보 표시 전환, 선택 등급의 일반 카드와 실제 라인업 미니 카드 비교를 제공한다.
- Full 등급 표기는 포지션 반대편 작은 배지로 이동한다. 팀명은 별도로 유지한다.
- Unity 테스트·빌드·Bake는 실행하지 않는다.

## 최종 프롬프트

### MVP Mini

편집 대상: `Assets/Resources/UI/PlayerCards/PlayerCard_Mini_Normal_v2.png`

```text
Use case: style-transfer. Edit target is the attached Mini Normal baseball player card frame. Produce one full-canvas flat front-view production card texture, portrait aspect ratio 5:7 matching original. Preserve exact panel geometry: straight name ribbon from exactly 72.3% to82.4% of canvas height, dark empty footer from82.4% to99%. Upgrade to MVP: champion ruby red enamel and polished gold; broad sculpted beveled outer rail, gold laurel clusters and faceted ruby corner insets. Recognizably an MVP championship trophy design. Make the premium materials and raised border ornaments clearly visible at a small thumbnail size, considerably richer than the plain gray original. Ornament width at side edges up to6% of card width; keep central portrait area clean dark neutral charcoal with subtle radial studio sheen. Name ribbon remains light pale metal for black text readability, lower stats/footer panels remain dark for white overlays. No portrait, person, text, letters, numbers, logos, stars or extra panels; all labels supplied by actual Unity UI. Do not move name strip, stats, footer or top header. Frame fills entire image, no outside background, no presentation mockup. Opaque texture, sharp metallic detailing and real depth, luxury baseball collectible, no fantasy props.
```

### MVP Full

편집 대상: `Assets/Resources/UI/PlayerCards/PlayerCard_Full_Normal_v2.png`

```text
Use case: style-transfer. Edit target is the attached Full Normal baseball player card frame. Produce one full-canvas flat front-view production card texture, portrait aspect ratio 5:7 matching original. Preserve exact panel geometry: curved name ribbon from exactly51% to61% of canvas height, dark empty stats panel61%-93%, bottom cost strip93%-99%. Preserve top inset header silhouette. Upgrade to MVP: champion ruby red enamel and polished gold; broad sculpted beveled outer rail, gold laurel clusters and faceted ruby corner insets. Recognizably an MVP championship trophy design. Make the premium materials and raised border ornaments clearly visible at a small thumbnail size, considerably richer than the plain gray original. Ornament width at side edges up to6% of card width; keep central portrait area clean dark neutral charcoal with subtle radial studio sheen. Name ribbon remains light pale metal for black text readability, lower stats/footer panels remain dark for white overlays. No portrait, person, text, letters, numbers, logos, stars or extra panels; all labels supplied by actual Unity UI. Do not move name strip, stats, footer or top header. Frame fills entire image, no outside background, no presentation mockup. Opaque texture, sharp metallic detailing and real depth, luxury baseball collectible, no fantasy props.
```

### Rare Mini

편집 대상: `Assets/Resources/UI/PlayerCards/PlayerCard_Mini_Normal_v2.png`

```text
Use case: style-transfer. Edit target is the attached Mini Normal baseball player card frame. Produce one full-canvas flat front-view production card texture, portrait aspect ratio 5:7 matching original. Preserve exact panel geometry: straight name ribbon from exactly 72.3% to82.4% of canvas height, dark empty footer from82.4% to99%. Upgrade to Rare: cool platinum and deep cobalt blue enamel; faceted geometric silver corner plates and fine blue inlaid double rails, crisp collectible finish. Make the premium materials and raised border ornaments clearly visible at a small thumbnail size, considerably richer than the plain gray original. Ornament width at side edges up to6% of card width; keep central portrait area clean dark neutral charcoal with subtle radial studio sheen. Name ribbon remains light pale metal for black text readability, lower stats/footer panels remain dark for white overlays. No portrait, person, text, letters, numbers, logos, stars or extra panels; all labels supplied by actual Unity UI. Do not move name strip, stats, footer or top header. Frame fills entire image, no outside background, no presentation mockup. Opaque texture, sharp metallic detailing and real depth, luxury baseball collectible, no fantasy props.
```

### Rare Full

편집 대상: `Assets/Resources/UI/PlayerCards/PlayerCard_Full_Normal_v2.png`

```text
Use case: style-transfer. Edit target is the attached Full Normal baseball player card frame. Produce one full-canvas flat front-view production card texture, portrait aspect ratio 5:7 matching original. Preserve exact panel geometry: curved name ribbon from exactly51% to61% of canvas height, dark empty stats panel61%-93%, bottom cost strip93%-99%. Preserve top inset header silhouette. Upgrade to Rare: cool platinum and deep cobalt blue enamel; faceted geometric silver corner plates and fine blue inlaid double rails, crisp collectible finish. Make the premium materials and raised border ornaments clearly visible at a small thumbnail size, considerably richer than the plain gray original. Ornament width at side edges up to6% of card width; keep central portrait area clean dark neutral charcoal with subtle radial studio sheen. Name ribbon remains light pale metal for black text readability, lower stats/footer panels remain dark for white overlays. No portrait, person, text, letters, numbers, logos, stars or extra panels; all labels supplied by actual Unity UI. Do not move name strip, stats, footer or top header. Frame fills entire image, no outside background, no presentation mockup. Opaque texture, sharp metallic detailing and real depth, luxury baseball collectible, no fantasy props.
```

### Ex Mini

편집 대상: `Assets/Resources/UI/PlayerCards/PlayerCard_Mini_Normal_v2.png`

```text
Use case: style-transfer. Edit target is the attached Mini Normal baseball player card frame. Produce one full-canvas flat front-view production card texture, portrait aspect ratio 5:7 matching original. Preserve exact panel geometry: straight name ribbon from exactly 72.3% to82.4% of canvas height, dark empty footer from82.4% to99%. Upgrade to Ex: black titanium and vivid crimson red with polished silver; bold angular machined corner armor and diagonal red inset rails, premium competitive sports design. Make the premium materials and raised border ornaments clearly visible at a small thumbnail size, considerably richer than the plain gray original. Ornament width at side edges up to6% of card width; keep central portrait area clean dark neutral charcoal with subtle radial studio sheen. Name ribbon remains light pale metal for black text readability, lower stats/footer panels remain dark for white overlays. No portrait, person, text, letters, numbers, logos, stars or extra panels; all labels supplied by actual Unity UI. Do not move name strip, stats, footer or top header. Frame fills entire image, no outside background, no presentation mockup. Opaque texture, sharp metallic detailing and real depth, luxury baseball collectible, no fantasy props.
```

### Ex Full

편집 대상: `Assets/Resources/UI/PlayerCards/PlayerCard_Full_Normal_v2.png`

```text
Use case: style-transfer. Edit target is the attached Full Normal baseball player card frame. Produce one full-canvas flat front-view production card texture, portrait aspect ratio 5:7 matching original. Preserve exact panel geometry: curved name ribbon from exactly51% to61% of canvas height, dark empty stats panel61%-93%, bottom cost strip93%-99%. Preserve top inset header silhouette. Upgrade to Ex: black titanium and vivid crimson red with polished silver; bold angular machined corner armor and diagonal red inset rails, premium competitive sports design. Make the premium materials and raised border ornaments clearly visible at a small thumbnail size, considerably richer than the plain gray original. Ornament width at side edges up to6% of card width; keep central portrait area clean dark neutral charcoal with subtle radial studio sheen. Name ribbon remains light pale metal for black text readability, lower stats/footer panels remain dark for white overlays. No portrait, person, text, letters, numbers, logos, stars or extra panels; all labels supplied by actual Unity UI. Do not move name strip, stats, footer or top header. Frame fills entire image, no outside background, no presentation mockup. Opaque texture, sharp metallic detailing and real depth, luxury baseball collectible, no fantasy props.
```

### Legend Mini

편집 대상: `Assets/Resources/UI/PlayerCards/PlayerCard_Mini_Normal_v2.png`

```text
Use case: style-transfer. Edit target is the attached Mini Normal baseball player card frame. Produce one full-canvas flat front-view production card texture, portrait aspect ratio 5:7 matching original. Preserve exact panel geometry: straight name ribbon from exactly 72.3% to82.4% of canvas height, dark empty footer from82.4% to99%. Upgrade to Legend: rich antique gold and obsidian black; substantial sculpted gold filigree corners, embossed laurel side rails and heritage ornamental engraving, highest prestige archival trophy. Make the premium materials and raised border ornaments clearly visible at a small thumbnail size, considerably richer than the plain gray original. Ornament width at side edges up to6% of card width; keep central portrait area clean dark neutral charcoal with subtle radial studio sheen. Name ribbon remains light pale metal for black text readability, lower stats/footer panels remain dark for white overlays. No portrait, person, text, letters, numbers, logos, stars or extra panels; all labels supplied by actual Unity UI. Do not move name strip, stats, footer or top header. Frame fills entire image, no outside background, no presentation mockup. Opaque texture, sharp metallic detailing and real depth, luxury baseball collectible, no fantasy props.
```

### Legend Full

편집 대상: `Assets/Resources/UI/PlayerCards/PlayerCard_Full_Normal_v2.png`

```text
Use case: style-transfer. Edit target is the attached Full Normal baseball player card frame. Produce one full-canvas flat front-view production card texture, portrait aspect ratio 5:7 matching original. Preserve exact panel geometry: curved name ribbon from exactly51% to61% of canvas height, dark empty stats panel61%-93%, bottom cost strip93%-99%. Preserve top inset header silhouette. Upgrade to Legend: rich antique gold and obsidian black; substantial sculpted gold filigree corners, embossed laurel side rails and heritage ornamental engraving, highest prestige archival trophy. Make the premium materials and raised border ornaments clearly visible at a small thumbnail size, considerably richer than the plain gray original. Ornament width at side edges up to6% of card width; keep central portrait area clean dark neutral charcoal with subtle radial studio sheen. Name ribbon remains light pale metal for black text readability, lower stats/footer panels remain dark for white overlays. No portrait, person, text, letters, numbers, logos, stars or extra panels; all labels supplied by actual Unity UI. Do not move name strip, stats, footer or top header. Frame fills entire image, no outside background, no presentation mockup. Opaque texture, sharp metallic detailing and real depth, luxury baseball collectible, no fantasy props.
```

### CareerHigh Mini

편집 대상: `Assets/Resources/UI/PlayerCards/PlayerCard_Mini_Normal_v2.png`

```text
Use case: style-transfer. Edit target is the attached Mini Normal baseball player card frame. Produce one full-canvas flat front-view production card texture, portrait aspect ratio 5:7 matching original. Preserve exact panel geometry: straight name ribbon from exactly 72.3% to82.4% of canvas height, dark empty footer from82.4% to99%. Upgrade to CareerHigh: champagne gold and deep teal emerald enamel; bright stepped art-deco gold corner rays and upward chevron rail ornament, luminous career pinnacle trophy. Make the premium materials and raised border ornaments clearly visible at a small thumbnail size, considerably richer than the plain gray original. Ornament width at side edges up to6% of card width; keep central portrait area clean dark neutral charcoal with subtle radial studio sheen. Name ribbon remains light pale metal for black text readability, lower stats/footer panels remain dark for white overlays. No portrait, person, text, letters, numbers, logos, stars or extra panels; all labels supplied by actual Unity UI. Do not move name strip, stats, footer or top header. Frame fills entire image, no outside background, no presentation mockup. Opaque texture, sharp metallic detailing and real depth, luxury baseball collectible, no fantasy props.
```

### CareerHigh Full

편집 대상: `Assets/Resources/UI/PlayerCards/PlayerCard_Full_Normal_v2.png`

```text
Use case: style-transfer. Edit target is the attached Full Normal baseball player card frame. Produce one full-canvas flat front-view production card texture, portrait aspect ratio 5:7 matching original. Preserve exact panel geometry: curved name ribbon from exactly51% to61% of canvas height, dark empty stats panel61%-93%, bottom cost strip93%-99%. Preserve top inset header silhouette. Upgrade to CareerHigh: champagne gold and deep teal emerald enamel; bright stepped art-deco gold corner rays and upward chevron rail ornament, luminous career pinnacle trophy. Make the premium materials and raised border ornaments clearly visible at a small thumbnail size, considerably richer than the plain gray original. Ornament width at side edges up to6% of card width; keep central portrait area clean dark neutral charcoal with subtle radial studio sheen. Name ribbon remains light pale metal for black text readability, lower stats/footer panels remain dark for white overlays. No portrait, person, text, letters, numbers, logos, stars or extra panels; all labels supplied by actual Unity UI. Do not move name strip, stats, footer or top header. Frame fills entire image, no outside background, no presentation mockup. Opaque texture, sharp metallic detailing and real depth, luxury baseball collectible, no fantasy props.
```

