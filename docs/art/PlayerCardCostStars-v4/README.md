# 선수 카드 COST 별 v4

## 결과

한 장의 4×2 마스터 시트에서 등급별 별 8종을 잘라 사용한다.

```text
위: Normal | Rare | AllStar | GoldenGlove
아래: MVP | Ex | Legend | CareerHigh
```

- 생성 원본: `PlayerCard_CostStars_v4_source.png`
- 배경 제거 마스터: `PlayerCard_CostStars_v4_rgba.png`
- 밝고 어두운 배경 검수: `PlayerCard_CostStars_v4_review.png`
- 분할 스크립트: `Slice-CostStars.ps1`
- Unity Sprite: `Assets/Resources/UI/PlayerCards/PlayerCard_CostStar_{Variant}_v4.png`

마스터는 내장 ImageGen으로 만들었다. 기존 v2 별 4종은 오각형 실루엣과 faceted bevel의
스타일 참고로만 사용했다. 배경은 전경과 겹치지 않는 균일한 녹색 `#00FF00`을 사용했고,
`Tools/ImageBackground/Remove-ImageBackground.ps1 -Mode ChromaKey`로 RGBA 변환했다.

## 디자인

- Normal: 무채색 흰색·밝은 은색.
- Rare: 무채색 중간 회색·graphite.
- AllStar: 은색 외곽과 rainbow facet.
- GoldenGlove: 내부가 완전히 빈 금색 outline.
- MVP: champagne gold와 중앙 baseball.
- Ex: 가장 깨끗하고 밝은 순금색.
- Legend: 긁힘과 갈색 tarnish가 있는 낡은 금색.
- CareerHigh: 붉은 반사가 도는 red-gold.

## 최종 ImageGen 프롬프트

```text
Use case: stylized-concept
Asset type: master sprite sheet for eight baseball-card COST star icons
Input images: Images 1-4 are style references only for the clean symmetrical five-point star silhouette, centered front view, crisp faceted bevel, and polished sports-game icon finish. Do not copy their existing colors or internal designs.
Scene/backdrop: one absolutely uniform solid chroma-key green #00FF00 across every background pixel, including all spaces between icons and the hollow interior of the GoldenGlove star. No gradient, texture, vignette, grid, dividers, shadows, reflections, ambient light, or color spill on the green.
Composition: one wide 2-row by 4-column sprite sheet, exactly eight icons in equal cells with generous empty green gutters. Each icon is centered in its cell, identical scale and outer five-point silhouette shape, straight-on, fully contained, with at least 15% green margin on every side. Top row left-to-right: Normal, Rare, AllStar, GoldenGlove. Bottom row left-to-right: MVP, Ex, Legend, CareerHigh.
Icon designs:
1 Normal: solid white achromatic star, pearl-white center with light neutral-silver bevel, strictly grayscale.
2 Rare: solid medium-gray star, graphite center with darker gray bevel, strictly grayscale and clearly darker than Normal.
3 AllStar: solid iridescent rainbow star, distinct red-orange-yellow-green-cyan-blue-violet facets, polished silver outer bevel.
4 GoldenGlove: hollow gold outline star, thick elegant polished gold rim only; the entire inner star-shaped area is empty #00FF00 background. No solid fill.
5 MVP: warm champagne-gold star with one small realistic white baseball embedded as a centered circular badge; visible red stitches, ball no larger than 32% of star width, star remains recognizable.
6 Ex: pristine bright solid 24-karat gold star, strongest clean golden shine, simple faceted center and gold bevel.
7 Legend: aged antique-gold solid star, worn brushed metal, restrained scratches and dark brown tarnish in recesses; no green patina, no broken edges.
8 CareerHigh: rich red-gold solid star, polished rose-gold/copper-red center with golden bevel and warm crimson reflections.
Style/medium: cohesive 2010s Korean baseball management game UI icon set, sharp readable silhouettes at 16-32 px, modest 3D depth, consistent top-left studio highlight.
Constraints: all eight outer silhouettes, rotations, sizes, margins, bevel thicknesses, and camera angles must match exactly. Each icon must be isolated from every other icon. The sheet must contain exactly eight stars in the specified order.
Avoid: all text, letters, numbers, labels, logos, trophies, crowns, wings, laurels, bats, gloves, extra baseballs, cell borders, touching icons, cast shadows, glow outside icon edges, green spill on icon edges, transparent/checkerboard background, black background, additional objects, watermark.
```

## 재현

```powershell
powershell -NoProfile -File Tools/ImageBackground/Remove-ImageBackground.ps1 `
  -InputPath docs/art/PlayerCardCostStars-v4/PlayerCard_CostStars_v4_source.png `
  -OutputPath docs/art/PlayerCardCostStars-v4/PlayerCard_CostStars_v4_rgba.png `
  -Mode ChromaKey -KeyColor '#00FF00' -KeyTolerance 48 -KeyOpaqueDistance 255 -KeyEdgeRadius 12

& docs/art/PlayerCardCostStars-v4/Slice-CostStars.ps1 `
  -InputPath docs/art/PlayerCardCostStars-v4/PlayerCard_CostStars_v4_rgba.png `
  -OutputDirectory Assets/Resources/UI/PlayerCards
```

분할 결과는 모두 443×443 RGBA다. 검수 시 8장 모두 네 모서리 alpha 0이며,
밝은/어두운 배경에서 녹색 잔여와 전경 손실이 없음을 확인했다.
