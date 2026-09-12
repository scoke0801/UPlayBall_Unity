# 구단 기록실 트로피

## 현재 적용: v2 캐릭터 화풍

사용자가 새 디자인 3종을 승인한 뒤 그림풍만 캐릭터와 맞추도록 요청했다.
금색 날개 손잡이 컵·청색 에나멜 우승 컵·월넛 기념패의 실루엣과 장식을 유지하고,
선수 피규어 고정 앵커를 재질 참조로 사용해 반사·명암·잔무늬를 부드럽게 바꿨다.

- 생성/편집: 내장 Imagegen, 3종 각각 생성 후 재질 편집.
- 승인 디자인과 화풍 수정 원본: `v2/*-approved-design.png`, `v2/*-figurine-source.png`.
- 이전 게임 자산 보존: `v1/`. 이전 시트와 생성 원본도 보존한다.
- 최종 적용: 기존 `Assets/10.Datas/Resources/UI/ClubHistory/{pennant,champion,runner-up}.png`.
  기존 meta와 Resources 경로를 유지한다.
- [디자인 프롬프트](trophies-v2-prompts.md), [화풍 편집 프롬프트](trophies-v2-style-prompt.md).
- 크로마키: `#00FF00`, 기본 KeyTolerance=24, KeyOpaqueDistance=180, KeyEdgeRadius=6.
- 1254×1254 RGBA 3종 모두 네 모서리 alpha=0. 반투명 경계 픽셀은 각각 8275 / 10019 / 5635개.
- 밝고 어두운 배경의 합성에서 손잡이·왕관 내부 틈, 금색 테두리·기념패 외곽을 직접 확인했다.
  검수: `output/club-history-validation/trophies-v2/review.png` 및 `.json`.
- 교체 후 독립 Unity 검증 37개 통과(09:41:21~09:41:34 UTC), 4개 해상도 렌더링.
  `screenshots/history-3-1920x1080.png`에서 새 3종의 실제 표시를 직접 확인했다.
  별도 AI 파일의 잘못된 33자리 GUID 때문에 검증 복사본에서만 해당 메타데이터를 보정했다.
  원본 전체 컴파일은 이 오류가 남아 있으며, 이 결과는 원본 PlayMode 검증이 아니다.

## 이전 v1 제작 기록

- 생성 수단: 내장 ImageGen. 피규어 재질과 navy·gold·pearl 색상으로 프로젝트 화풍을 따른다.
- 원본: `trophies-source.png` (1536×1024, 단색 크로마키 배경).
- 최종: `Assets/10.Datas/Resources/UI/ClubHistory/`의 `pennant.png`, `champion.png`, `runner-up.png`.
- `trophies.png`는 배경 제거 시트이며, 연결된 전경을 분리한 세 PNG를 실제 UI에서 사용한다.
- 배경 제거: `Tools/ImageBackground/Remove-ImageBackground.ps1 -Mode ChromaKey -KeyColor '#00FF00'`.
- 분리: `Tools/ClubHistoryValidation/Export-Trophies.ps1`. 원본의 색·형태를 보존하고 전경 연결 영역만 추출한다.
- 검수: `output/club-history-validation/individual-review.png`와 JSON. 밝은 배경과 어두운 배경에서 세 트로피의 손잡이·내부 틈·금속 외곽을 확인했다. 네 모서리 알파 0, 반투명 경계 포함.

## 최종 생성 프롬프트

Use case: stylized-concept. Create one game asset sprite sheet with exactly THREE separate baseball honors, evenly spaced left center right in three equal-width columns, no overlap, complete objects with generous green margins. Left: elegant gold regular-season pennant flag on short gold stand with pearl baseball and dark navy round base. Center: grand gold championship cup with two curved handles, pearl baseball finial, deep navy enamel body, sculpted gold laurel and dark navy base. Right: smaller silver and champagne gold runner-up award plaque with sculpted baseball laurel on navy base. Style: premium hand-painted resin collectible figurine, softly rounded sculptural modeling, satin surfaces, broad diffused studio highlights, refined gold metallic details, matches a polished Korean chibi baseball management game. Front facing, slightly elevated camera, consistent scale and lighting. Backdrop MUST be perfectly uniform solid chroma green #00FF00, no gradient, no texture, no floor shadow, no green spill on objects. No lettering, no numbers, no logos, no UI, no watermark. Landscape 1536x1024.
