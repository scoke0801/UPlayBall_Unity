# 오프시즌 일정표 원화

- 생성: 내장 ImageGen. `owner_offseason_camp_v1.png`, 1536×1024, 불투명 배경.
- 화풍 기준: `../PlayerPortraitProduction/references/player-figurine-anchor-v1.png`.
- 원본 보존: 이 폴더의 PNG.
- Unity 자산: `Assets/10.Datas/Resources/UI/OwnerPowerUp/owner_offseason_camp_v1.png`.
- 연결: `UI_Popup_OwnerOffseason`의 `TrainingCampIllustration/Artwork`. 텍스트·일정·버튼은 모두 native uGUI.
- 초상화나 유니폼 양산을 변경하지 않는다. 기존 선수 초상화는 재질·치비 비율·색조 레퍼런스로만 사용했다.
- 불투명 환경 그림이므로 크로마키 제거를 적용하지 않는다. 원본과 적용 자산은 동일하다.

## 생성 프롬프트

```text
Create one wide 3:2 premium baseball management game offseason training camp illustration, meant for the upper left artwork of a native UI calendar modal. The reference image is ONLY a style reference: match its soft hand-painted resin figurine material, cute chibi baseball player proportions, sculpted brown hair, satin skin, large brown eyes, blue baseball cap and white baseball jersey with blue trim. Show two distinct adult Korean baseball players in this exact figurine art style quietly preparing training equipment together, one holding a glove and one holding a small baseball bat at their side, no active pitch or action sequence. Place the two players in the right half of the image, full bodies entirely visible, next to a tidy rack of bats and practice balls. Set them on a sunlit baseball practice field in late summer, with a small elegant training pavilion in the far background, restrained navy and warm ivory colors, gentle soft sunlight. Leave the left third relatively calm, landscaped field. Premium polished cohesive illustration, soft ambient occlusion, broad restrained highlights. Opaque fully illustrated background, no transparency. No words, text, letters, UI panels, borders, logos, badges, watermark or numbers. This is a new environmental illustration, not an edit to the portrait. Save the generated image so it can be copied into the Unity project.
```

## 검수

원본에서 얼굴·손·유니폼·장비와 배경의 화풍 일치를 확인했다. 적용 UI의 네 해상도 검수는
`Tools/OffseasonValidation/Invoke-OffseasonValidation.ps1`과
`docs/reports/owner-reference-systems-20260912.md`에 기록한다.
