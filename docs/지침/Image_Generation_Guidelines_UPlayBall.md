# 이미지 생성 지침

이 프로젝트의 인물 일러스트(프런트 매니저·선수 초상화 등) 생성 규칙이다. 배경 제거 도구의
파라미터·검수 절차는 [Tools/ImageBackground/README.md](../../Tools/ImageBackground/README.md)를 따른다.

## 생성 수단

- 이미지는 **Imagegen으로 생성한다. 이 절은 Codex 전용이다** — Imagegen에 접근할 수 없는 에이전트는
  이미지를 생성하지 말고, 생성이 필요하다는 사실만 보고한다. 다른 이미지 생성 경로로 대체하지 않는다.

## 공통 · 단일 이미지 제작 프롬프트

Image 1은 원본 성인 여성 매니저 일러스트, Image 2는 **재질·셰이딩 레퍼런스 전용**이다. Image 2의
정체성·의상·모자·치비 비율은 절대 가져오지 않는다. 아래 본문을 그대로 쓰고, 대괄호 자리에
아래 강도 블록 중 **하나만** 넣는다.

```text
Edit Image 1, the original adult female manager illustration. Image 2 is ONLY a reference for soft premium painted-resin figurine MATERIAL and shading. Never copy its identity, costume, cap, or chibi proportions.
Preserve Image 1 facial identity, adult slender proportions, head and eye sizes, hairstyle silhouette, expression, pose, costume, accessories, prop graphics, colors, black background, and original above-knee portrait framing. Output a single 2:3 portrait.
Apply the selected figurine surface treatment chiefly to face and hair. Use satin-matte skin, delicate warm blush, softly modeled cheek, nose and eyelid planes, refined painted facial details, and restrained broad highlights. Harmonize exposed skin without redesigning the outfit or props.
[INSERT ONE INTENSITY BLOCK BELOW]
No enlarged head or eyes, no baby face, no chibi, no character merging, no new objects, no wet waxy gloss, no toy joints, seams or pedestal. No added text or labels. Keep the original elegant illustration identity.
```

### 강도 블록

**25% · 원화 중심 · 가벼운 표면 정리**

```text
25 PERCENT influence: original illustration remains dominant. Retain fine linework and painterly details. Slightly soften skin highlights and cheek/nose shadows, add a subtle satin-matte finish, and gently soften hair highlights. Shape changes must be minimal.
```

**50% · 원화와 피규어의 중간**

```text
50 PERCENT influence: a balanced blend of illustration and premium resin figurine rendering. Smooth satin-matte skin, moderately rounded sculptural facial shading, delicate painted details, and softly sculpted hair locks. Retain some original linework and natural adult facial proportions.
```

**75% · 피규어 조형감 강조**

```text
75 PERCENT influence: predominantly collectible figurine material on face and hair. Use coherent sculptural cheek, nose and eyelid planes, airbrushed blush, painted brows and lashes, satin molded hair locks, and reduced facial linework. Preserve the same face, eye size, expression and body proportions.
```

**100% · 재질 표현을 피규어로 전환**

```text
100 PERCENT influence: fully premium hand-painted resin figurine surface rendering, especially face and hair. Clearly sculpted facial volumes, solid refined hair locks, soft-touch satin resin skin, painted eyes and lips, broad diffused studio highlights and gentle ambient occlusion. This is full MATERIAL transfer, not a change to chibi geometry or character identity.
```

## 배경 처리

생성 모델로 투명 배경을 직접 얻기는 어렵다. **투명 배경이 필요하면 배경을 크로마키 단색으로 덮어
생성한 뒤 `Tools/ImageBackground`로 제거한다.**

- 기본 키 색은 **초록 `#00FF00`**. 전경(의상·소품·머리색)에 키 색과 겹치는 색이 있으면 마젠타
  `#FF00FF` 등 겹치지 않는 색을 프롬프트에 명시해 다시 생성한다.
- 배경은 완전한 균일 단색으로 지정한다. 그라데이션·질감·그림자·체크무늬·전경 색 번짐은 금지한다.
- 제거는 `-Mode ChromaKey`로 실행하고, `KeyColor`·`KeyTolerance`·`KeyEdgeRadius`는 이미지마다
  README의 설명에 맞춰 조정한다. 다른 이미지의 파라미터·보호 다각형을 그대로 재사용하지 않는다.

```powershell
powershell -NoProfile -File Tools/ImageBackground/Remove-ImageBackground.ps1 -InputPath source.png -OutputPath transparent.png -Mode ChromaKey -KeyColor '#00FF00'
```

- 생성 원본(`*-source.png`)은 지우지 말고 보존한다. 도구는 기존 출력을 덮어쓰지 않는다.
- 결과는 `Review-ImageBackground.ps1`로 밝고 어두운 배경에 합성해 **눈으로** 확인한다. 통계만으로
  전경 손실을 판정하지 않는다 — 얼굴·머리카락 경계·의상·소품과 내부 틈을 본다.

## 선수 초상화 양산

선수 초상화·유니폼·외형 양산은 [docs/art/PlayerPortraitProduction/README.md](../art/PlayerPortraitProduction/README.md)를
따른다. 고정 마스터 프롬프트와 앵커 레퍼런스를 사용하고 유니폼·외형은 별도 변주한다. 정면
무동작 프로필과 실제 투명 배경을 유지하며, 이전 화풍을 섞거나 마스터를 임의로 바꾸지 않는다.
