# 선수 카드 프레임 v4 제작 기록

## 목적

원작 프로야구매니저 카드 레퍼런스를 기준으로 AllStar, GoldenGlove, MVP, Rare, Ex, Legend,
CareerHigh의 Full/Mini 프레임을 다시 제작했다. 사용자의 후속 지정에 따라 등급명은 카드 장식에
포함하고, 선수명·구단명·연도·능력치·COST는 런타임 UI가 표시한다.

## 적용 규칙

### 이름·문장 겹침 수정

- 위에서 내려오는 원화 좌표의 공통 명찰 구역은 제작 가이드이며, 실제 텍스트는
  `OwnerPlayerCardFrames.GetNameRect`의 등급별 안전 영역을 사용한다.
- Mini EX·레전드·레어·커리어하이는 이름과 연도를 실제 리본 안으로 올린다.
  MVP Mini는 중앙 문장이 명찰 안까지 내려오므로 왼쪽 빈 영역에 이름을 배치한다.
- MVP·레전드는 초상 하단을 문장 위로 제한한다. 커리어하이 이름·연도는 흰색으로 표시한다.
- 갤러리도 8종 모두 실제 `PlayerCardEdition`을 전달해 공통 렌더러의 배치와 색을 소비한다.
- 검증: Presentation 및 Presentation.Tests 보조 컴파일 통과(경고·오류 0).
  Unity EditMode와 캡처는 같은 프로젝트가 다른 Unity 인스턴스에서 열려 있어 실행 전에 중단됐다.
  실제 UI에서 80×120 Mini와 일반 카드의 최종 시각 확인은 남아 있다.

- 공통 로더는 v4 → v3 → v2 순서로 최신 원화를 선택한다.
- v4가 없는 Normal은 v2를 유지한다.
- Full은 이름 영역 y=51%~61%, 기록 영역 y=61%~93%, COST 영역 y=93%~99%를 기준으로 한다.
- Mini는 이름 영역 y=72%~82%, COST/상태 영역 y=82%~100%를 기준으로 한다.
- 카드 프레임은 1060×1484 불투명 PNG이며, 초상은 런타임에서 별도 합성한다.

## 디자인 정본

- AllStar: 청색 바탕, 작은 별과 원형 점선, 상단 은색 리본의 `ALL STAR`.
- GoldenGlove: 무채색 바탕, 양옆의 가는 금색 줄기·잎, 이름 영역 아래 `Golden Glove`.
- MVP: 자주색 바탕, 상단 가장자리의 작은 분홍·보라 광점, 이름 영역 위 야구공·짧은 날개의 `MVP` 문장.
- Rare: 주황색 방사형 바탕, 상단 작은 은색 플레이트의 `RARE`.
- Ex: 청색 방사형 바탕, 얕은 금색 날개 장식과 `EX`.
- Legend: 낡은 양피지·갈색 프레임, 원형 문장과 이름 영역 위 `LEGEND`.
- CareerHigh: 어두운 연무와 불꽃, 주황색 상승 화살표와 `CAREER HIGH`.

## 최종 ImageGen 프롬프트 구조

내장 ImageGen의 `precise-object-edit` 경로를 사용했다. 각 Full/Mini v3 프레임은 배치 기준,
사용자가 제공한 원작 캡처는 색·문양·등급명 위치 기준으로 사용했다.

```text
Use case: precise-object-edit
Asset type: production baseball card frame texture
Input images: Image 1 is the current 1060x1484 Full or Mini asset and is authoritative for canvas and runtime overlay geometry. Image 2 is the original Pro Baseball Manager reference and is authoritative for visual language and edition-title placement.
Primary request: redesign Image 1 to follow Image 2 closely and make its edition title part of the printed card decoration.
Full layout: portrait area above; blank player-name band y=51%-61%; blank stats y=61%-93%; blank footer y=93%-99%.
Mini layout: portrait area ends at y=72%; blank player-name band y=72%-82%; blank cost/status y=82%-100%.
Composition: straight-on flat full-bleed card; central portrait area remains clear for a runtime player cutout.
Constraints: preserve a blank player-name band for runtime name/year; lower information panels remain blank; typography must be exact and readable at thumbnail size; use thin flat 2010s sports-game graphics.
Avoid: player, silhouette, team name, team logo, year, jersey number, stats, stat bars, COST stars, watermark; any other words; invented motifs; excess glow or texture.
```

등급별 요청은 다음과 같다.

```text
AllStar: muted steel-blue portrait backdrop, small pale stars and fine dotted circular arcs, a shallow white/silver top ribbon with exact text "ALL STAR", and two tiny muted pink star accents.
GoldenGlove: restrained charcoal background, thin neutral border, flat light silver name band, slender warm-gold branches along both portrait sides, and exact text "Golden Glove" immediately below the name band.
MVP: deep burgundy-to-charcoal portrait backdrop, sparse curved dotted light trails and pink-violet sparkle streaks, thin gunmetal/silver border, and a compact gold baseball-and-short-wing crest with exact text "MVP" immediately above the blank name band.
Rare: burnt orange-red portrait backdrop with a fine circular radial line pattern, thin charcoal/silver border, and a compact silver inverted-chevron plate with exact text "RARE".
Ex: dark teal-blue portrait background, thin pale radial rays, thin aged bronze border, a shallow wing-like top ornament, and exact text "EX" centered below the ornament.
Legend: muted antique parchment-gold portrait background, one low-contrast circular seal, thin dark brown/antique brass border, restrained corner curls, and exact text "LEGEND" inside the small crest above the name band.
CareerHigh: charcoal-black smoky portrait background, fine amber sparks, burnt-orange outlined arrows, and exact text "CAREER HIGH" near the top center.
```

## 산출물과 검증

- 위치: `Assets/Resources/UI/PlayerCards/PlayerCard_{Full|Mini}_{Variant}_v4.png`
- 대상 Variant: `AllStar`, `GoldenGlove`, `MVP`, `Rare`, `Ex`, `Legend`, `CareerHigh`
- 14장 모두 1060×1484 PNG이며 Unity Sprite import 메타와 독립 GUID를 가진다.
- 원화의 등급명 철자와 Full/Mini 쌍을 육안 확인했다.
- `Baseball.Presentation.csproj`와 `Baseball.Presentation.Tests.csproj` 컴파일은 경고·오류 없이 통과했다.
- Unity EditMode 실행과 실제 UI 캡처는 Editor license 부재로 시작 전에 중단됐다. 테스트 코드와
  캡처 경로는 준비되어 있으며 license가 활성화된 Editor에서 최종 확인한다.
