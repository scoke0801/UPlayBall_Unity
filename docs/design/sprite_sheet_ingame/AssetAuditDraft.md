# 스프라이트 시트 초기 감사 — Gate A

이 기록은 구현 경계를 정하기 위한 초안이다. 최종 시트별 판정은 AssetAuditReport.md와 source manifest를 따른다.

- 정본: docs/design/sprite_sheet_ingame의 HTML과 PNG 11개. 경기장 1개, 합성 참고 1개, 시트 9개.
- HTML이 설명한 HitToRun/HighlightReaction PNG 2개는 상위 docs/design에 존재하며 감사 대상에 포함한다.
- 경기장_투수_타자_시안.png는 16:9 합성 참고로 실제 확인했다. 이 이미지에 선수가 이미 있으므로 배경 자산으로 사용하지 않는다.
- 우투수_피치.png는 실제 시각 확인했다. 4×3이며 3~6번 셀에서 선수 오른손에 공, 반대손에 글러브가 보인다. 7번은 release, 8~9번은 follow-through, 10~11번은 ready 복귀다. 7번 셀의 화면 오른쪽 detached ball은 제거 마스크가 필요하다.
- 다른 시트 손잡이/프레임 순서/이벤트는 최종 감사 전 NeedsReview다. 파일명만으로 Production 등록하지 않는다.

## 구현 경계

기존 Simulation 결과를 변경하지 않는다. 구단주 MatchPlayVisualizer의 Begin/Render→TryRevealPlaybackEvent 순서와 선수 커리어 PlayResolutionSequenceController의 공개 시점을 보존한다. 공통 sprite stage, 이벤트 기반 공 표시, 고정 actor 재사용을 Presentation에 둔다. 승인 Catalog가 없으면 기존 중계를 유지한다.

Importer는 검수용 Catalog와 Production Catalog를 구분하고 NeedsReview를 Production에서 거부한다. 미확정 자산은 검수와 처리 도구 개발을 막지 않으나 실제 승인과 혼동하지 않는다.