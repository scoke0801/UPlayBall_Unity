# 하이라이트 스프라이트 제작 기준

사용자가 제시한 하이라이트 프롬프트를 실제 게임 리소스 제작과 연결할 때의 기준이다. 기본 관전의 원근·타격 접점·공 소유권을 먼저 검증한다. 확대 연출을 기본 장면의 오류를 가리는 수단으로 사용하지 않는다.

## 공통 프롬프트

```text
Use Image A as the character identity and rendering reference.
Preserve the same cute chibi proportions, soft youthful face, brown hair,
large expressive eyes, blue baseball cap, and white-and-blue baseball uniform.
Create a production-ready gameplay HIGHLIGHT sprite sheet for a 2D baseball game.
Use polished 2.5D chibi rendering: soft rounded figurine-like forms, gentle shading,
subtle volume, and a clear sporty silhouette. Match Image A's materials and costume.
The framing should feel closer and more dramatic than a normal gameplay sprite.
Keep the camera angle, lighting, character scale, and proportions fixed across frames.
Show the entire body, bat or glove in every frame. Never crop moving equipment.
Use a uniform grid in row-major playback order with generous equal cell margins.
Do not draw grid lines, text, labels, UI, decorative effects, or an environment.
Use a single pure flat bright green #00FF00 chroma key background: no gradient,
texture, checkerboard, cast shadow, reflected green light, or green edge contamination.
Do not include a baseball in ANY frame. The game renders one independent ball.
This is NOT a collection of unrelated baseball poses. It must look like one
continuous highlight animation captured frame by frame from a fixed camera.
The face, hair, cap, costume, proportions, glove, and bat must remain consistent.
This must look like a usable gameplay highlight sprite sheet for an actual baseball
game, not a poster or decorative illustration: dramatic, consistent, clean, and
easy to extract as a real in-game asset.
```

공을 생략할 수 있다는 원안을 강화해 모든 시트에서 공을 제외한다. 접촉·포구·송구 시점의 공은 사건 앵커와 독립 공 표시 하나로 연결한다. 원본과 크로마키 제거 후 PNG를 모두 보존한다. 배경 제거는 프로젝트 이미지 지침의 도구를 사용한다.

## 모션별 요청 블록

10프레임은 5열 × 2행, 8프레임은 4열 × 2행이다. 아래 블록을 공통 프롬프트 뒤에 하나만 붙인다.

| 리소스 | 연속 동작 | 사건 앵커 |
| --- | --- | --- |
| 정타, 10프레임 | loaded stance → stride/coil → hip rotation → hands accelerating → pre-contact → impact → extension → strong follow-through → full finish → held finish | 6번째 프레임 BatContact |
| 홈런, 10프레임 | loaded stance → stride → rotation → pre-contact → contact → extension → follow-through → admire → confident hold → first trot step | 5번째 프레임 BatContact |
| 다이빙 캐치, 10프레임 | ready → react → first move → accelerate → dive start → glove extension → catch → landing → recovery → secured finish | 7번째 프레임 GloveContact |
| 강한 송구, 10프레임 | ready → secure → transfer → plant → cock → release → follow-through → complete → hold → recover | 2번째 GloveContact, 3번째 Transfer, 6번째 ThrowRelease |
| 도루 주자, 10프레임 | lead → first step → accelerate → sprint → sprint → lower → slide start → full slide/reach → finish → completed slide hold | 실제 진루/아웃 사건과 연결, 성공 판정은 시트에 포함하지 않음 |
| 도루 태그 수비, 8프레임 | receive ready → present glove → receive → turn → tag → full extension → complete → hold | 3번째 GloveContact, 태그 접점 별도 저작 |

정타는 몸통 회전과 배트 궤적의 힘, 홈런은 팔로스루 뒤 타구를 바라보는 동작, 다이빙은 연속적인 몸의 이동과 글러브의 뻗음, 도루는 가속과 낮은 슬라이딩을 강조한다. 송구와 태그는 서로 다른 동작이므로 한 시트 안에서 임의로 섞지 않는다.

## 손잡이와 연결 계약

- 타격은 우타·좌타를 별도로 제작한다. 우타는 왼손이 배트 손잡이 아래, 오른손이 위이며 왼발이 앞발이다. 좌타는 반대다. 반전만으로 검수 없이 승인하지 않는다.
- 수비는 우투(왼손 글러브)·좌투(오른손 글러브)를 명시한다. 주자에는 배트와 글러브를 넣지 않는다.
- 확대는 시트 내부 프레임마다 달리하지 않는다. 배경·선수·공을 함께 움직이는 Presentation 카메라가 담당한다.
- 경기 결과·카운트·득점의 공개 경계는 기존 관전 세션이 소유한다. 시트가 홈런·세이프·아웃을 새로 판정하지 않는다.
- 다이빙·태그는 공식 사건이 뒷받침하는 경우만 연결한다. 수비 성공 일반 기록만으로 다이빙을 임의로 연출하지 않는다.
- 제작 상태와 런타임 승인 상태를 구분한다. 생성만 끝난 시트는 카탈로그에 자동 승인하지 않는다.

## 검수 순서

1. 원본 1장 안의 체격·손잡이·도구·연속성을 확인한다.
2. 크로마키 제거 뒤 알파와 밝고 어두운 합성을 확인한다.
3. 셀 단위 고정 배율·발 기준점·접점 메타데이터를 저작한다.
4. 실제 경기장에 합성해 포수·투수·야수와 체격 및 원근을 비교한다.
5. 실제 이벤트 스트림의 투구 → 타격 → 타구 → 수비 → 주루 → 다음 타석을 1·2·4배속에서 검증한다.
6. 카메라 전환, 공 중복·잔류, 주자 중복, 결과의 선공개가 없을 때 승인한다.
