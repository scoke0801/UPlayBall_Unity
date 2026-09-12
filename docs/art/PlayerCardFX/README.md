# 선수 카드 디자인 전용 플립북 광채

## 자산과 재생

- 사용 시트: `Assets/Resources/UI/PlayerCards/PlayerCardFX_GoldHolographic_Flipbook_v2.png`.
- Imagegen 원본 v1의 **첫 칸 하나**를 고정 원화로 삼아 Unity 셰이더로 광량만 바꿔 베이크한다.
- 프레임마다 다른 원화를 사용하지 않는다. 원화 UV·방사광 모양·반짝임 중심·크기·회전은 고정한다.
- 1024×1536, 4열×4행, 각 프레임 256×384. 좌상단부터 행 우선 순서다.
- 12fps, `0→15→1→0` 왕복 루프. 끝 프레임 중복 대기 없이 2.5초마다 반복한다.
- 불투명 배경이며 미리보기 선수 초상 아래에 놓는다. v1은 원본으로 보존하며 재생에는 사용하지 않는다.
- [브라우저 애니메이션 미리보기](preview.html).

## 적용 범위

Skin / Layout Reference는 기존 `OwnerPlayerCardFrames`, `PlayerMiniCardView`, `UI_Popup_OwnerPlayerCard.BuildFrontCard`다.
셸·패널·카드 크기와 텍스트 지표는 기존 값을 재사용한다.
**새 게임의 ‘선수 카드 디자인’ 갤러리에서 FX ON일 때만** 초상 뒤 광채를 표시한다.
FX OFF와 ‘프레임 원화만 보기’에서는 배경 광채를 붙이지 않는다. 기존 갤러리 테두리 FX는 기존 토글을 따른다.
실제 구단주 목록·상세 카드·특수 영입 등 공용 카드에는 붙이지 않는다.

`UIPlayerCardFlipbook`은 사진 영역 안에서만 RawImage의 UV를 바꾼다. 상세 카드는 기존 PortraitWindow 마스크를 재사용하고,
미니 카드는 기존 프레임 안쪽 사진 창에 맞춘다. 카드 장식은 기존 메시를 통해 초상 위에 유지한다.
부착 호출은 `UI_Scene_NewGame.CardGallery.AttachGalleryFx`에만 있다.
`UIPlayerCardFlipbook` 자체도 `UNITY_EDITOR`로 제한해 Player 빌드에서 제외한다.

컴포넌트의 Atlas / Columns / Rows / Frames Per Second는 직렬화 필드다.
동일 시트 Texture를 공유하고 Sprite·Material을 카드마다 만들지 않는다. UV는 프레임이 바뀔 때만 갱신한다.
비활성 카드와 CanvasRenderer에서 잘린 카드는 갱신하지 않는다. Time.unscaledDeltaTime은 Presentation에서만 사용한다.
리소스 누락 시 FX Image를 숨겨 기존 카드 배경을 유지한다. Raycast를 차단하지 않아 카드 클릭·포커스 경로는 기존과 같다.
갤러리의 기존 Render 재구성이 토글·종류 변경·닫기 시 FX 수명을 관리한다.
HTML 미리보기는 정수 좌표의 `drawImage`로 같은 크기의 Canvas에 프레임을 그려 CSS 배경 위치 반올림을 피한다.

## 고정 원화 베이크

`powershell -NoProfile -File Tools/CardFxValidation/Invoke-Bake.ps1`로 독립적인 소형 Unity 프로젝트에서 베이크한다.
원화는 Imagegen 산출물이며 셰이더는 기존 원화 위의 조명 FX만 렌더링한다. 프레임을 이미지 생성으로 다시 그리지 않는다.
강도·폭은 `Tools/CardFxValidation/StableFlipbook.json`, 광량 계산은 `PlayerCardFxBake.shader`에 있다.
성공한 결과는 `output/card-fx-stable-bake/PlayerCardFX_GoldHolographic_Flipbook_v2.png`에 나온다.
검증에 성공한 PNG를 Resources에 반영한다. 베이크 실패 시 배포용 시트를 내보내지 않는다.

## 검증

검증 명령은 `powershell -NoProfile -File Tools/CardFxValidation/Invoke-Validation.ps1`이다.
FX 합성을 폰트 설정 저작과 독립적으로 확인하도록 테스트가 프로젝트 원본
`esamanru Medium.ttf`를 임시 주입하고 테스트 종료 시 복원한다. 제품의 기본 폰트 연결 검증을 대신하지 않는다.
검증 출력은 `output/card-fx-validation/results.xml`과 `screenshots/`에 저장한다.

v2 베이크 검사 통과: 16프레임×4구역의 gradient 정렬 검사에서 모두 이동량 (0,0).
14개 중간 프레임은 광량 변화가 있으며 첫·마지막 프레임은 모든 RGB 픽셀이 같다.
일부러 1px 이동시킨 비교 대조군을 검출해 정렬 검사 자체도 확인했다. [상세 결과](alignment-v2.txt).
갤러리 범위·FX OFF·일반 카드 미적용·왕복 UV·입력 비차단은 `PlayerCardFlipbookTests`가 검사한다.
Unity 6000.3.21f1에서 최신 코드 컴파일과 EditMode 5/5 통과 (`output/card-fx-validation/results.xml`).
공용 카드 생성 직후 FX 0개, 갤러리 FX OFF에서도 0개, FX ON에서만 상세·미니 각 1개임을 확인했다.
카드 단위 캡처는 1280×720·1920×1080·2560×1440·3440×1440에서 0·7·15프레임을 저장하고
각 해상도의 초상·프레임·이름·능력치 경계와 광채 위치를 시각 확인했다.
전체 갤러리 팝업의 실시간 Play Mode 조작은 이번 검증에 포함하지 않았다.
브라우저 자동 검수는 도구 연결 오류로 미실시다. 시뮬레이션·밸런스 변경은 없다.

## Imagegen 원본 v1의 생성 프롬프트

```text
Create a production animation SPRITE SHEET of the gold holographic player card background from the previous image. EXACTLY 4 columns by 4 rows = 16 equally sized frames, no gutters, no borders, no labels. Total image portrait 2:3, 1024x1536 preferred, each tile portrait 2:3. Read frames left-to-right then top-to-bottom. This is ONE coherent stationary golden radial backlight with pastel rainbow foil glints, repeated in IDENTICAL camera, composition and light-ray geometry across all sixteen tiles. Animate ONLY a broad iridescent highlight sweeping gradually from left in frame 1 to right in frame 16 and small star sparkle brightness evolving smoothly. Small incremental progression, no random redesign per frame, no camera shake, no rotation, no flicker, no abrupt whiteout. A soft luminous warm ivory center behind future player's head, sharper champagne gold rays and pink cyan lavender holographic facets at perimeter. Restrained brightness variation. Opaque full-bleed background in every tile. No person, silhouettes, text, frame numbers, logos or actual card frames. Entire output is precisely tiled sixteen animation frames. Will play forward then backward as a seamless ping-pong loop.
```
