# ImageGen 최종 프롬프트

기준 원화: Assets/10.Datas/Resources/FrontManager/FM_01_NEUTRAL.png. Windows 참조 경로 전달 오류로 대화에 표시한 참조 이미지를 사용했다. 상태 편집은 각 종류의 Normal을 첫 번째 기준 이미지로 전달했다. 생성 원본은 별도 보존하고 후처리는 크로마키 제거·정렬·9-slice 정규화·알파·명도에 한정한다.

## MainDashboard

```text
Use case: stylized-concept. Create ONE production-ready orthographic 2D game UI asset: MainDashboardFrame, NOT a mockup, NOT a sprite sheet. Image 1 is ONLY a MATERIAL AND SHADING style reference, the game's existing adult manager illustration. Do NOT draw any character, face, clothes, objects, text, numbers, icons, logo or watermark. Match her premium soft hand-painted semi-realistic / satin painted-resin illustration aesthetic: gently modeled edges, refined soft shading and broad subdued highlights. Do NOT use photographic brushed metal, gritty noise, neon, sci-fi, holograms, glossy web-game chrome or ornate mobile fantasy decoration. 
Single horizontal rounded rectangle panel, about 2.3:1 aspect ratio, requested output 2048x1024. Panel nearly fills image with only a narrow uniform exterior margin. Deep navy #091827 interior, blue-charcoal #10171D rim, thin subdued blue-grey #365269 outline. Tiny warm muted brass #DDB45C corner details entirely confined to the outer 24px at final 744px display width. Radius about 8px at display scale. Thin muted gold header separator near top, short soft inset shadow. Large totally empty low-contrast central content area. Not thick metal framing, no rivets or bolts. Long edges simple and straight for Unity 9-slice stretching, no center ornament, no inner vertical dividers. 
BACKGROUND: perfectly uniform solid chroma key green #00FF00 everywhere OUTSIDE the panel, absolutely no green light spill, shadows, texture or checkerboard outside. Panel interior is solid navy at this source stage; real alpha and controlled interior opacity will be applied in production postprocessing. Draw the front-facing flat panel only. Preserve clean silhouette and subtle painted material.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-fe55b67b-db6a-4555-8248-72e1861c6467.png`

## ManagerCard

```text
Use Image 1 as the exact visual family reference. Produce a NEW single ManagerCardFrame asset, no character included. Soft hand-painted satin/resin illustration finish matching the manager character, not photographic metal. Orthographic horizontal 2.15:1 panel, output 2048x1024, nearly edge-to-edge with a narrow uniform pure #00FF00 chroma green exterior margin. Same deep navy #091827, restrained blue-grey thin rim, tiny softly painted warm brass corner details, small radius, low contrast blank interior. Left quarter should provide unobtrusive space for a portrait, without any portrait image, placeholder silhouette, circle, frame division or hard separator. Right area totally empty for live text and buttons. Reduce brass corner emphasis by 30% compared with reference, no glow or glossy bevel. A fine muted gold line only at the top inner edge, no fixed interior horizontal or vertical division. Unity 9-slice compatible simple edges and empty stretchable center. No words, numbers, icons, logos, watermarks, objects, checkerboard or perspective. Keep outside perfectly flat key green with no shadows or spill.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-94ee5b8a-e0a7-40ea-9860-657ef8a85af6.png`

## ManagerReport

```text
Image 1 is a visual-family reference only. Generate ONE separate ManagerReportFrame for the same premium soft painted semi-realistic baseball management UI. Output 2048x2048, a near-square frame suitable for final 744x620 display. Match navy satin painted-resin shading and restrained warm brass of reference; less corner ornament. Very thin blue-grey rim and tiny muted gold corner tips, small softly modeled radius. Totally empty deep navy #091827 content field, no header stripe, no internal separators, no portrait, no objects. Top, left-list and right-detail regions will be native content, DO NOT draw them. All decoration confined to outermost 32px, plain long edges suitable for 9-slice. NOT a screenshot, not a sheet, no text, numbers, icons, badges, logos or perspective. Narrow perfectly uniform #00FF00 exterior chroma key background, no green spill or outer shadow. Quiet premium character-illustration shading, never photographic metal or glossy chrome.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-d89192af-c406-4d4b-ab3e-01f8b7b493d1.png`

## CompactStrip

```text
Using Image 1 only for the same softly painted satin navy UI material, generate ONE separate CompactStripFrame asset. Very wide low horizontal strip, aspect 5:1, nearly fill a wide canvas with a narrow uniform pure #00FF00 chroma green margin. Deep #091827 navy blank interior, thin #365269 blue-grey outline, small rounded corners. MINIMAL ornament: remove all large brass corner flourishes from reference, retain only a tiny muted brass line at upper left and upper right inside the corner border. No top header subdivision or other internal lines. Quiet illustrated finish to match an elegant hand-painted manager character, not photorealistic chrome. No text, icons, numbers, labels, objects, people, checkerboard, perspective, glow, rivets or shadows in background. Straight simple sides and empty center suitable for Unity 9-slice. Output high resolution width at least 1600 pixels.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-b78e7bc3-d1aa-4964-afb8-852c2058861e.png`

## Primary_Normal

```text
Generate ONE PrimaryButton NORMAL state background for a quiet premium baseball club management game. Image 1 is only the reference for softly illustrated satin/resin edges, NOT the navy fill. This button has a warm muted gold #DDB45C face, very subtle painted satin tonal variation, dark navy #091827 slim lower rim, small radius, very restrained edge highlights. Flat orthographic wide 5:1 rounded rectangle, use at 360x68, no chunky chrome, no bright gloss band, no grainy metal, no ornament. The large center is entirely empty and low contrast for a dark navy live text label. Exact symmetrical silhouette; simple stretchable edges for 9-slice. Render at least 1600px wide on uniform #00FF00 key green background with no color spill. Narrow margin, no outside cast shadows. No text, icons, numbers, logos, symbols, checkerboard, perspective or people. Material should fit an elegant softly painted manager character, not a photograph.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-02275152-0ef6-4904-af11-6bf5693b9023.png`

## Primary_Hover

```text
Edit Image 1, the NORMAL PrimaryButton background, to make its HOVER state. Preserve exactly its canvas size, bounding box, rounded silhouette, rim thickness, corner geometry, empty center and perfectly flat #00FF00 chroma green exterior. Change ONLY the gold face to about 10% brighter and add a hairline soft ivory inner highlight. Keep the same soft painted satin illustration material; no additional gloss, ornaments, text, icons, symbols or shadow outside. One button, not a comparison sheet.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-dfe12d0b-b3b8-42fc-ac91-462f27847bca.png`

## Primary_Pressed

```text
Edit Image 1 (the earlier NORMAL gold button), not Image 2 (the brighter hover comparison), into PrimaryButton PRESSED state. Keep the NORMAL canvas, silhouette, corner radius, rim, position and empty center identical. Darken gold face by about 12%, reduce top highlight, add a very short soft inner shadow at the top so it feels depressed only 1-2 display pixels. Preserve muted hand-painted satin material; no gloss or glow. No changes to uniform #00FF00 green exterior. No words, icons, numbers, logos or ornament. Output only one pressed button, not a sheet.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-54294a03-9717-4049-a6d2-59d8e1732f55.png`

## Primary_Disabled

```text
Edit Image 1, the earliest NORMAL gold PrimaryButton (other images are state references), into its DISABLED state. Lock canvas size, placement, outline, rounded corners and all geometry to NORMAL. Only change material colors: desaturate and darken gold to subdued grey-brown taupe (#766F5F direction), remove bright highlights and any glow. Same soft painted satin finish and blank center. Maintain pure uniform #00FF00 outside with no spill or shadows. No text, numbers, icons or symbols. One button only.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-c68cf881-8097-4283-8868-f01112f1df4b.png`

## Secondary_Normal

```text
Create ONE new SecondaryButton NORMAL state asset, using Image 1 only as a reference for the softly painted satin shape family. Replace gold/taupe with dark navy #10283D face, charcoal #10171D lower rim and thin blue-grey #365269 outline. Match an elegant semi-realistic painted manager illustration. Very low contrast broad shading, almost matte; no strong glossy stripe, no brass corners. Wide rectangular button around 4:1, small rounded radius, large blank center, 9-slice simple edges. Render high-resolution at least 1600px wide, only narrow uniform pure #00FF00 green exterior margin, no outer shadow/spill. No text, icons, numbers, logo, decoration, people, checkerboard or perspective. This is a subtle secondary action, much quieter than a gold primary button.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-0a6f7613-b14b-495a-a86c-5d6657ff8688.png`

## Secondary_Hover

```text
Edit Image 1 SecondaryButton NORMAL into HOVER. Exactly preserve canvas dimensions, rectangular silhouette, corner radius, rim thickness and placement. Brighten navy face only slightly, and make its thin blue-grey inner outline softly ivory-tinted, NOT gold-filled. Same restrained painted satin material, no gloss or glow. Preserve perfectly uniform #00FF00 exterior. No text, icons, numbers or added ornaments. One button only.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-8698b760-b957-4cab-a093-01bff52ada32.png`

## Secondary_Pressed

```text
Edit Image 1 (earlier Secondary NORMAL, not brighter Image 2) into PRESSED state. Preserve exact silhouette, corners, dimensions, rim and position. Darken navy face slightly and add only a 1-2 display-pixel soft top inset shadow, reducing the upper highlight. Keep painted satin material subdued. No new ornament, text, symbols, icons or numbers. Flat pure #00FF00 exterior unchanged, one orthographic button only.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-80ea3b44-f9ab-43e9-bf56-54403095ecf5.png`

## Secondary_Disabled

```text
Edit Image 1 (earliest Secondary NORMAL; other two are state examples) into DISABLED. Lock exact dimensions, location, silhouette and rounded corners. Desaturate navy face to very dark charcoal #20272C, lower border contrast and eliminate highlights. Preserve soft hand-painted finish but extremely subtle. Outside remains uniform pure #00FF00. No text, icons, labels, numbers or decoration. One blank button only.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-be0f685a-37bf-4448-a6af-cb5056ffb336.png`

## Secondary_Selected

```text
Edit Image 1, earliest Secondary NORMAL, into SELECTED state. Preserve its exact dimensions, silhouette, placement, radius and geometry. Keep dark navy face, add a very subtle dark warm-gold tint inside and change ONLY thin inner outline to muted #DDB45C brass gold. No gold-filled face, no glow, no gloss, no new ornaments. Match same soft painted satin illustration. Outside pure #00FF00 unchanged. No text, numbers, icons, marks or labels. Single button only; later images are other state references, not the base.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-b3d21ac5-382c-4901-bf29-b23b93b8a77a.png`

## Utility_Normal

```text
Create ONE UtilityIconButton NORMAL background, a small square rounded button without any icon. Use Image 1 ONLY for the same soft painted satin navy material. Square 1:1 orthographic silhouette, small radius (about 6px at 64px display), almost-flat deep charcoal #10171D face, very subtle blue-grey rim, no gold. Low presence because a live icon will be overlaid. No strong bevel, glossy highlights or thick frame. Large empty central area. High resolution 1024x1024 with a narrow uniform pure #00FF00 chroma green exterior margin. No text, numbers, symbols, icons, people, screws, ornaments, checkerboard or perspective. One standalone asset, not a sheet.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-62001990-4360-4848-9da5-75f20f15cc24.png`

## Utility_Hover

```text
Edit Image 1 UtilityIconButton NORMAL into HOVER. Preserve exact square silhouette, radius, canvas size, placement and blank center. Brighten dark navy face modestly and make the thin rim a restrained warm ivory grey; still no gold fill. Match its soft painted satin shading, no hard chrome gloss. Background stays pure flat #00FF00, no spill. No icon, text, symbol, number, logo or ornament. Single square button only.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-44f57821-bce7-46e0-9d1e-d0b68e87b73f.png`

## Utility_Pressed

```text
Edit Image 1 earliest Utility NORMAL, not Image 2 hover, into PRESSED. Exact same square dimensions, silhouette, rounded radius and placement. Darken navy and lower the top highlight, slight 1-2px-at-display soft inset shadow only. Empty center for native icon. Same subdued painted satin finish. Outside uniform #00FF00 unchanged. No icon, text, number, symbol, ornament or glow.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-b3ad6c87-ef6e-4489-87db-7cf015c98653.png`

## Utility_Disabled

```text
Edit Image 1 earliest Utility NORMAL into DISABLED; other images are state references. Exact same square geometry, dimensions, corners and position. Face very dark desaturated charcoal, rim barely visible, no highlight. Keep soft painted material low contrast. Uniform #00FF00 outside unchanged. No text, icon, number, symbol, logo, glow or ornament. One blank square only.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-5a5be6f1-1cde-4838-855c-be4545390c6f.png`

## ListItem_Normal

```text
Create one separate ListItemFrame NORMAL asset for report rows in same softly hand-painted navy baseball management UI. Image 1 is material reference only. Make an extremely wide 9:1 LOW rectangular row, small 4px display radius, near-flat dark navy #10283D interior and subtle 1px blue-grey border, no thick raised rim, no bevel, no gold. Simple long edges, empty low contrast center for two lines of live text. Match soft illustrated character shading but quieter than buttons. Wide high resolution canvas at least 1800px, narrow uniform #00FF00 exterior green margin. No text, icons, numbers, badges, people, ornaments, perspective, gloss, glow or checkerboard. One row only, not a sheet.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-dac42929-9012-43e6-9586-4528acde4049.png`

## ListItem_Hover

```text
Edit Image 1 ListItem NORMAL into HOVER. Exact same canvas, silhouette, radius, row height and position. Slightly brighten navy face by 8% and thin blue-grey border, no brass/gold, no glow. Keep center empty and subtle painted satin texture. Exterior #00FF00 unchanged. No text, icons, numbers, badges or decoration. One row only.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-67e23891-78b4-47ea-8947-934abee95af8.png`

## ListItem_Selected

```text
Edit Image 1 earliest ListItem NORMAL (Image 2 is hover) into SELECTED state. Lock exact canvas, row dimensions, placement and small corner radius. Replace the thin outline with muted brass #DDB45C; interior should be dark warm gold-charcoal, extremely low saturation and dark enough for ivory text, NOT a bright filled gold button. Keep understated painted material. No icon, dot, text, numbers, glow, ornament or divider. Outside uniform #00FF00 unchanged. One row only.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-bc5b565f-dbfd-4611-85d6-a045907b029f.png`

## ListItem_Unread

```text
Edit Image 1 earliest ListItem NORMAL into UNREAD state, not selected or hover examples. Exact same silhouette, dimensions, radius and position. Keep navy face, raise interior brightness very slightly (about 4%) and make left edge a subtly lighter blue-grey only. Do NOT add any dot, badge, exclamation, icon, text or number: unread badge will be a separate asset. No gold border or glowing highlight. Soft painted satin material, empty center, #00FF00 exterior unchanged. One row only.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-87ed9603-08fb-4776-b7de-c3acc310de80.png`

## ListItem_Disabled

```text
Edit Image 1 earliest ListItem NORMAL into DISABLED. Lock exact row silhouette, corners, canvas, dimensions and placement. Desaturate face to dark charcoal and reduce rim contrast. Same subtle soft-painted texture, no gold, no bright highlight. Perfect #00FF00 exterior remains. No text, icons, numbers, badge, decoration, glow or perspective. One row only; other images are state references.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-c63f6f53-7eda-4868-ac3c-91765fc8e4e6.png`

## Tab_Normal

```text
Create one TabFrame NORMAL-state underline asset, in the same softly illustrated satin navy UI family as Image 1. NOT a boxed button or panel: ONLY a very thin horizontal low pedestal/underline, long and simple with tiny gently rounded ends. Muted blue-grey #365269, very low contrast, almost flat. Thickness about 3px at 228px display width; no tall side walls or outline rectangle. Completely empty background ABOVE and BELOW this line, pure uniform #00FF00 chroma green. Wide high-resolution canvas, a single centered horizontal line only; no text, icon, number, logo, badge, people, ornaments, glow, gradient rays or perspective. This will be used beneath live filter-tab text with Unity 9-slice.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-9e907dc5-1c29-46f2-8116-d949833ef3ca.png`

## Tab_Hover

```text
Edit Image 1 Tab NORMAL underline to HOVER. Lock exact canvas, thin line silhouette, length, thickness and rounded ends. Change only blue-grey to slightly brighter cool ivory-grey, softly painted and subdued. NO box or added shape, no glow. Pure #00FF00 background unchanged. No text, icons, numbers or symbols. One thin line only.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-85cfbff1-5d98-4ea6-a68d-de5197e83f8d.png`

## Tab_Selected

```text
Edit Image 1 earliest Tab NORMAL underline into SELECTED. Preserve exact canvas and thin line silhouette, dimensions, position and small rounded ends. Change only its color to muted warm gold #DDB45C with extremely subtle softly painted highlight. NO surrounding box, glow, rays or ornament. Uniform #00FF00 exterior unchanged, no text, icons, numbers or symbols. One thin underline only; Image 2 is hover reference.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-2950a505-8c14-40bc-9c99-a048b0e3772d.png`

## Tab_Disabled

```text
Edit Image 1 earliest Tab NORMAL underline to DISABLED. Preserve exact dimensions, line silhouette, rounded ends and position. Change only line color to very dark desaturated charcoal-grey, low contrast with navy UI. No box, glow or new geometry. Outside pure #00FF00 unchanged, no text, icons, numbers or symbols. Single thin line only.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-ad8d505d-98a1-4abf-aafc-775936a27284.png`

## Badge_Count

```text
Create ONE circular NotificationBadge background for live notification counts in a premium baseball club management game. An empty circular disc, muted warm red #B65353, thin dark navy rim, softly hand-painted semi-realistic satin/resin shading to match an elegant illustrated manager character. Very restrained broad highlight, no glossy button sheen or glow. Front orthographic perfect circle, near edge-to-edge on square 1024x1024 with narrow perfectly uniform #00FF00 chroma green margin. No number, text, punctuation, icon, logo, symbol, character, watermark or checkerboard. Center totally blank for Unity to overlay a number. No outer cast shadow or key-green spill.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-18d10df5-a102-4a07-befc-76b89d56ee98.png`

## Badge_Unread

```text
Create ONE separate tiny UnreadDot UI asset in the same family as Image 1. It is a simple perfect circular muted warm red #B65353 dot, no surrounding dark ring, no icon, no number, no text. Nearly flat satin painted illustration surface, extremely subtle soft shading, not shiny, no glow. Intended display size 8-10px, so avoid fine detail. Orthographic circle on uniform solid #00FF00 chroma green background with narrow margin, no shadow or spill. Square high-resolution output. Do not reproduce the large count-badge rim. No checkerboard, logo or watermark.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-c425121e-1b06-47d9-b367-e54cb01186d1.png`

## Badge_Important

```text
Create ONE separate ImportantNotificationBadge background for a premium baseball management UI. A small softly rounded diamond-shaped lozenge, muted terracotta red #B65353 face, very thin deep navy edge. Blank center for a native Unity importance symbol; DO NOT draw any symbol or punctuation. Soft semi-realistic hand-painted satin/resin shading matching an elegant illustrated manager, restrained broad highlight, no shiny plastic or glow. Orthographic front view, symmetrical, narrow pure uniform #00FF00 chroma green margin around asset on square high resolution canvas. No text, digits, exclamation mark, icon, logo, watermark, checkerboard, perspective or outer shadow. Image 1 is only the red painted material reference.
```

원본: `C:\Users\scoke\.codex\generated_images\01a094b5-40d2-74b2-a2cb-ba9b2f9e5405\exec-3ad27a56-6779-484c-9812-064c9e19f6d9.png`

