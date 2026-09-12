# 덕아웃 인물 원화 프롬프트

내장 ImageGen 사용. 선수 figurine-v1 앵커를 참조하며 남성·여성 감독과 수석코치 원화는 각각 별도 생성했다.
원본은 output/imagegen/dugout에 보존하고 크로마키 제거본은 Assets/10.Datas/Resources/UI/OwnerDugout에서 사용한다.

## managerPrompt

Use case: stylized-concept. Asset type: baseball player card profile.
STYLE VERSION: figurine-v1. PROMPT VERSION: single-chroma-v2. This block is immutable across the production batch.
Create or edit ONE fictional adult Korean male baseball player depicted as a rounded collectible figurine.
The supplied approved player-figurine anchor is the authoritative rendering and proportion reference.
Match its oversized rounded head, compact torso, softly sculpted cheeks, warm painted skin, rounded brown eyes, curved sculpted hair clumps, smooth matte-to-satin vinyl/resin material, gentle highlights and soft studio lighting.
Keep the reference's head-to-body ratio, camera distance, eye level, face placement, cap silhouette, framing and overall color rendering consistent.
Square canvas. Entire cap visible with headroom. Both ears and shoulders visible. Straight front-facing card profile. Head upright, shoulders level, arms resting down, hands outside the upper-body crop. No gesture, head tilt, torso turn, action pose or props.
Calm neutral expression or a very small symmetrical closed-mouth smile as specified. No smirk, sneer, raised one-sided mouth corner or exaggerated grin.
Short dark hair under a baseball cap. Baseball jersey and undershirt. Clothing cut, buttons and sleeve length follow the anchor unless the supplied production specification explicitly changes them.
Only change the attributes authorized in the task block and variation data. Retain all other visual properties.
Use a perfectly uniform solid chroma-key green background (#00FF00) for later removal. If this color occurs in the foreground, use a non-overlapping solid key color such as magenta (#FF00FF) and state the selected key color in the appended record. No background texture, gradient, shadow, checkerboard or scene. No green or key-color spill, reflections or tint on the subject. White clothing and eye whites remain opaque. Do not request transparency for this generation source.
No text, letters, numbers, branding, logos, packaging, display stand, border or UI.
Avoid sharp black contours, spiky angular shapes, hard polygonal facial shadows, realistic pores, stubble, gritty skin, live-action realism, flat vector rendering, adult realistic body proportions, fashion posing and ornate hair.
Preserve the soft rounded figurine aesthetic. This is a stylized adult athlete collectible, not a child portrait.

Task: Create the MANAGER card portrait for the dugout, a fictional veteran Korean male baseball manager, approximately 55 years old. Preserve the anchor figurine proportions and material. Broader oval face, thicker straight eyebrows, kind focused brown eyes, short sculpted salt-and-pepper hair at the temples, subtle mature eyelid shaping without realistic wrinkles. Closed mouth calm confident expression. Plain deep navy cap and navy baseball warmup jersey with ivory piping. Upper body portrait front facing, no hands, no props. Solid green #00FF00 background. Save the generated source image inside C:/UsingProject/UnityProject/UPlayBall_Unity/output/imagegen/dugout/manager-source.png if supported.

## coachPrompt

Use case: stylized-concept. Asset type: baseball player card profile.
STYLE VERSION: figurine-v1. PROMPT VERSION: single-chroma-v2. This block is immutable across the production batch.
Create or edit ONE fictional adult Korean male baseball player depicted as a rounded collectible figurine.
The supplied approved player-figurine anchor is the authoritative rendering and proportion reference.
Match its oversized rounded head, compact torso, softly sculpted cheeks, warm painted skin, rounded brown eyes, curved sculpted hair clumps, smooth matte-to-satin vinyl/resin material, gentle highlights and soft studio lighting.
Keep the reference's head-to-body ratio, camera distance, eye level, face placement, cap silhouette, framing and overall color rendering consistent.
Square canvas. Entire cap visible with headroom. Both ears and shoulders visible. Straight front-facing card profile. Head upright, shoulders level, arms resting down, hands outside the upper-body crop. No gesture, head tilt, torso turn, action pose or props.
Calm neutral expression or a very small symmetrical closed-mouth smile as specified. No smirk, sneer, raised one-sided mouth corner or exaggerated grin.
Short dark hair under a baseball cap. Baseball jersey and undershirt. Clothing cut, buttons and sleeve length follow the anchor unless the supplied production specification explicitly changes them.
Only change the attributes authorized in the task block and variation data. Retain all other visual properties.
Use a perfectly uniform solid chroma-key green background (#00FF00) for later removal. If this color occurs in the foreground, use a non-overlapping solid key color such as magenta (#FF00FF) and state the selected key color in the appended record. No background texture, gradient, shadow, checkerboard or scene. No green or key-color spill, reflections or tint on the subject. White clothing and eye whites remain opaque. Do not request transparency for this generation source.
No text, letters, numbers, branding, logos, packaging, display stand, border or UI.
Avoid sharp black contours, spiky angular shapes, hard polygonal facial shadows, realistic pores, stubble, gritty skin, live-action realism, flat vector rendering, adult realistic body proportions, fashion posing and ornate hair.
Preserve the soft rounded figurine aesthetic. This is a stylized adult athlete collectible, not a child portrait.

Task: Create the HEAD COACH card portrait for the dugout. A fictional Korean male senior baseball coach, about 43 years old. Same approved player figurine proportions and material. Slightly narrower rectangular rounded face, straight dark eyebrows, warm attentive brown eyes, short neatly sculpted dark brown hair, gentle symmetrical closed mouth smile, mature cheek shaping. Plain ivory cap with navy brim, ivory baseball jersey with navy undershirt and muted gold piping. Front facing upper body profile, hands outside crop, no props. Green #00FF00 background. The input image is only the authoritative style and framing reference, not an identity to copy.

## managerFemalePrompt

Use case: stylized-concept. Asset type: baseball player card profile.
STYLE VERSION: figurine-v1. PROMPT VERSION: single-chroma-v2. This block is immutable across the production batch.
Create or edit ONE fictional adult Korean female baseball manager depicted as a rounded collectible figurine.
The supplied approved player-figurine anchor is the authoritative rendering and proportion reference.
Match its oversized rounded head, compact torso, softly sculpted cheeks, warm painted skin, rounded brown eyes, curved sculpted hair clumps, smooth matte-to-satin vinyl/resin material, gentle highlights and soft studio lighting.
Keep the reference's head-to-body ratio, camera distance, eye level, face placement, cap silhouette, framing and overall color rendering consistent.
Square canvas. Entire cap visible with headroom. Both ears and shoulders visible. Straight front-facing card profile. Head upright, shoulders level, arms resting down, hands outside the upper-body crop. No gesture, head tilt, torso turn, action pose or props.
Calm neutral expression or a very small symmetrical closed-mouth smile as specified. No smirk, sneer, raised one-sided mouth corner or exaggerated grin.
Neatly sculpted dark hair under a baseball cap. Baseball jersey and undershirt. Clothing cut, buttons and sleeve length follow the anchor unless the supplied production specification explicitly changes them.
Only change the attributes authorized in the task block and variation data. Retain all other visual properties.
Use a perfectly uniform solid chroma-key green background (#00FF00) for later removal. If this color occurs in the foreground, use a non-overlapping solid key color such as magenta (#FF00FF) and state the selected key color in the appended record. No background texture, gradient, shadow, checkerboard or scene. No green or key-color spill, reflections or tint on the subject. White clothing and eye whites remain opaque. Do not request transparency for this generation source.
No text, letters, numbers, branding, logos, packaging, display stand, border or UI.
Avoid sharp black contours, spiky angular shapes, hard polygonal facial shadows, realistic pores, stubble, gritty skin, live-action realism, flat vector rendering, adult realistic body proportions, fashion posing and ornate hair.
Preserve the soft rounded figurine aesthetic. This is a stylized adult athlete collectible, not a child portrait.

Task variation: Female manager, fictional Korean woman approximately 42 years old, calm confident symmetrical smile, rounded oval face and refined gently arched eyebrows, warm brown eyes the same size as the anchor, dark brown sculpted bob-length hair tucked behind both ears, simple navy baseball cap and navy warmup jersey with ivory piping. Keep the SAME rounded oversized head and compact torso figurine profile style as the player anchor. This user-authorized adult female variation replaces the male identity of the master only. No cosmetics emphasis, no jewelry, no hand gestures or props. Uniform solid green background #00FF00.

## coachFemalePrompt

Use case: stylized-concept. Asset type: baseball player card profile.
STYLE VERSION: figurine-v1. PROMPT VERSION: single-chroma-v2. This block is immutable across the production batch.
Create or edit ONE fictional adult Korean female baseball head coach depicted as a rounded collectible figurine.
The supplied approved player-figurine anchor is the authoritative rendering and proportion reference.
Match its oversized rounded head, compact torso, softly sculpted cheeks, warm painted skin, rounded brown eyes, curved sculpted hair clumps, smooth matte-to-satin vinyl/resin material, gentle highlights and soft studio lighting.
Keep the reference's head-to-body ratio, camera distance, eye level, face placement, cap silhouette, framing and overall color rendering consistent.
Square canvas. Entire cap visible with headroom. Both ears and shoulders visible. Straight front-facing card profile. Head upright, shoulders level, arms resting down, hands outside the upper-body crop. No gesture, head tilt, torso turn, action pose or props.
Calm neutral expression or a very small symmetrical closed-mouth smile as specified. No smirk, sneer, raised one-sided mouth corner or exaggerated grin.
Neatly sculpted dark hair under a baseball cap. Baseball jersey and undershirt. Clothing cut, buttons and sleeve length follow the anchor unless the supplied production specification explicitly changes them.
Only change the attributes authorized in the task block and variation data. Retain all other visual properties.
Use a perfectly uniform solid chroma-key green background (#00FF00) for later removal. If this color occurs in the foreground, use a non-overlapping solid key color such as magenta (#FF00FF) and state the selected key color in the appended record. No background texture, gradient, shadow, checkerboard or scene. No green or key-color spill, reflections or tint on the subject. White clothing and eye whites remain opaque. Do not request transparency for this generation source.
No text, letters, numbers, branding, logos, packaging, display stand, border or UI.
Avoid sharp black contours, spiky angular shapes, hard polygonal facial shadows, realistic pores, stubble, gritty skin, live-action realism, flat vector rendering, adult realistic body proportions, fashion posing and ornate hair.
Preserve the soft rounded figurine aesthetic. This is a stylized adult athlete collectible, not a child portrait.

Task variation: Female head coach, fictional Korean adult woman approximately 35 years old. Gentle confident closed mouth smile, softly rounded face distinct from the manager, straight eyebrows, attentive brown eyes same size as anchor, dark sculpted hair tied into a low ponytail mostly behind shoulders with neat side fringe. Ivory baseball cap with navy brim and ivory baseball jersey with navy undershirt and muted gold piping. SAME rounded oversized head and compact torso figurine profile aesthetic as the player anchor. This user-authorized female variation replaces the male identity only. Both ears and shoulders visible, level head, no jewelry, no hands or props. Uniform green #00FF00 background.
