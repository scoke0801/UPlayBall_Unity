# TeamColor Card Plates V2

## 목적

`Owner.Roster.TeamColor`의 공통·타자·투수 효과를 색만 바꾼 복제본이 아니라 서로 다른 형태 언어로
구분한다. 사용자 제공 화면은 Layout Context, 작은 레퍼런스 이미지는 효과 유형 분리 참고,
`team_color_card_plate_v1.png`는 교체 대상 비교 자료로만 사용했다.

생성은 built-in `imagegen`으로 수행했다. 세 자산 모두 Text·Grade·Progress·State를 포함하지 않으며
Unity Text가 Runtime 데이터로 합성한다.

## 공통 제약

```text
Use case: stylized-concept
Asset type: Unity game UI horizontal TeamColor card plate
Composition: one isolated wide plate; empty circular grade socket near 18% width;
empty name/meta field from about 30% to 78%; empty status zone at right.
Style: restrained baseball broadcast-management UI, crisp at small size,
brushed metal and matte enamel, controlled studio highlights, no glow.
Constraints: no text, letters, numbers, logos, trademarks, watermark, team marks,
characters, players, baseball objects, stadiums, particles, or rarity sparkle.
Avoid: copying the legacy red arrow silhouette, batter silhouette, chrome-heavy frame,
or making the three variants simple recolors.
```

## 공통 효과 Plate

```text
Express a cohesive team-wide effect through two interlocking bands and a subtle woven
chevron motif. Use deep navy, desaturated teal, cool silver, charcoal, and small ivory
highlights. Keep the center and right fields visually quiet for runtime text.
```

## 타자 효과 Plate

```text
Express hitter-side energy through a compact home-plate-shaped left module, two forward
slash cuts, and one restrained swing-arc line without literal baseball pictograms.
Use burnt orange, deep oxblood, dark walnut-charcoal, muted brass, and warm ivory.
Change the outer corners and panel seams so this is not a recolor of the common plate.
```

## 투수 효과 Plate

```text
Express pitcher-side control through a layered circular orbit around the empty grade
socket, a restrained curved release trail, and two thin horizontal precision rails.
Use deep cobalt, slate blue, blue-black charcoal, cool platinum, and ice-blue highlights.
Use stepped rails and a rounded taper distinct from both common and hitter geometry.
```

## Alpha 보정

초기 생성본의 Checkerboard가 실제 Pixel로 포함되어 다음 편집 Prompt를 각 자산에 적용했다.

```text
Use case: background-extraction
Remove only the white/light-gray checkerboard background and replace it with genuine
full alpha transparency. Preserve the entire card exactly, create a clean antialiased
outer cutout with no white fringe, and do not crop, resize, repaint, add elements, or text.
```

## Unity 정규화

최종 Plate는 Center의 비장식 Body만 수평 확장하는 3-Slice 후처리로 원형 Grade Socket과 양 끝 장식을
왜곡하지 않고 2048×720 Canvas에 맞췄다.

| Asset | Alpha Bounds | Visible Ratio | Corner Alpha |
|---|---:|---:|---:|
| `team_color_card_plate_common_v2.png` | `(170, 190)–(2030, 530)` | 5.47:1 | 0 |
| `team_color_card_plate_hitter_v2.png` | `(190, 190)–(2030, 530)` | 5.41:1 | 0 |
| `team_color_card_plate_pitcher_v2.png` | `(165, 190)–(2030, 530)` | 5.49:1 | 0 |

Unity `RawImage.uvRect`는 세 자산 공통으로 `(0, 0.25, 1, 0.50)`을 사용한다.
