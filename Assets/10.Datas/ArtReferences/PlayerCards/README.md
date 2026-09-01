# 선수 카드 아트 레이어 가이드

## 런타임 합성 순서

```text
Neutral Base
→ Team Color Overlay
→ Special Card Overlay
→ Player Portrait
→ Common Top Meta / Player Data
→ Grade Effect
```

- `Team Color`는 구단 정체성, `Special Card`는 수상·선정 이력, `Grade Effect`는 희귀도다.
- 대표 `Special Card`는 한 번에 하나만 사용한다. 다른 수상 이력은 동적 `AwardSlot`에 표시한다.
- 일반 카드와 특수 카드는 1024×1536, 같은 외곽 실루엣과 정보 좌표를 공유한다.
- 런타임 Sprite는 `Assets/Resources/UI/PlayerCards/`에 있고, 이 폴더의 Preview는 검수 전용이다.

## 공통 상단 메타 계약

`PlayerCard_TopMeta_Common.png` 한 장을 모든 Edition이 공유한다.

- 좌측 첫 칸: 실제 `PlayerPosition` 코드(`SS`, `CF`, `SP` 등)
- 좌측 나머지 세 칸: 대표 카드 외 수상 이력을 넣는 동적 `AwardSlot`
- 우측 Rounded-Octagonal Medal: 소속 구단의 동적 `TopTeamEmblem`
- Edition별로 RectTransform·Shape·칸 수를 변경하지 않는다.
- Normal·All-Star·MVP·Golden Glove는 동일 Alpha Geometry를 사용하고 Meta 소재색만 바꾼다.
- 기존 Golden Glove 전용 `PositionBadge`는 실제 Position과 중복되므로 사용하지 않는다.
- Editor 갤러리는 수상 슬롯에 `★ / M / G` 더미를, 우측 Medal에는 임의 구단 엠블럼을 표시한다.

`PlayerCard_Edition_4Up_Preview.png`는 최종 런타임 합성 순서로 네 Edition의 상단 정렬을 비교한다.

## 특수 카드 문법

| 유형 | 소재 | 패턴 | 광원 |
|---|---|---|---|
| All-Star | Pearl / Silver | 작은 별 면과 방사선 | 넓고 약한 축제성 광선 |
| MVP | Pale Champagne / Pearl | 추상 월계 곡선 | 선수 중앙을 향한 수직 Spotlight |
| Golden Glove | Antique Brass / Warm Leather | Glove Web, Stitch, Defense Diamond | 따뜻하고 절제된 선광 |

`PlayerCard_Special_3Up_Preview.png`는 같은 네이비 팀색으로 세 문법을 비교한다.
`PlayerCard_MVP_TeamColor_Preview.png`는 동일 MVP 문법에 Red·Blue·Green 팀색을 적용한 호환성 검사다.

## ImageGen 재생성 Prompt Set

공통 Front Prompt:

> 1024×1536 Korean PC baseball management player-card additive overlay. Preserve the existing neutral card silhouette, photo/name/stat/cost geometry. Pure black working background for black-to-alpha conversion. Draw only sparse special-effect pixels; no player, text, number, logo, team color, stat boxes, fantasy, sci-fi, HUD, neon, heavy metal or broad fill. Keep more than 88% pure black.

- All-Star: `pearl-white and cool-silver hairlines, subtle starburst, no more than three abstract four-point star facets, quiet celebratory foil`.
- MVP: `pale champagne double hairline, abstract symmetric laurel curves, narrow vertical spotlight, cold refined season-best prestige; no trophy or crown`.
- Golden Glove: `antique-brass and muted warm-leather stitch lines, abstract glove-web arcs and defensive diamond; no literal glove or brown surface fill`.

공통 Top Meta Prompt:

> Exact 1024×1536 pure-black additive overlay. Draw only two fixed components: one compact left meta plate with one wider position cell and three equal award cells, plus one empty rounded-octagonal badge at the right. Neutral printed silver/graphite hairlines, no text or symbols. This exact geometry is reused by every Edition; material tint changes at runtime.

Edition Front 수정 시에는 반드시 다음을 추가한다.

> Remove every edition-specific top-left widget and top-right badge. Do not invent replacement components. Preserve the exact card chassis and change only material, lighting and portrait-background motifs; Unity supplies the shared CommonTopMeta separately.

공통 Back Prompt:

> Use `PlayerCard_Back_Neutral.png` only as exact geometry reference. Create a quieter 1024×1536 additive back overlay on pure black. Keep the central emblem area empty and use thin partial edge accents only. Do not reproduce the neutral base.

- All-Star Back: `subtle circular star arc, fine radial rays, sparse pearl-silver facets`.
- MVP Back: `geometric laurel curves and one restrained vertical champagne prestige line`.
- Golden Glove Back: `abstract glove-web geometry, baseball seam arc and defense-diamond corner cues in brass/leather linework`.

생성본은 검은 배경의 최대 RGB 채널을 Alpha로 변환하고 색 채널을 역프리멀티플라이해 RGBA PNG로 저장한다. Unity Import는 `Sprite (2D and UI)`, `Single`, `Alpha Is Transparency`, mipmap 비활성으로 맞춘다.
