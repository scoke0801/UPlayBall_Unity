# Sprite Sheet 실물 감사

Gate A 감사 기록. 폴더의 경기장/합성 시안 2개와 동작 시트 9개에 더해 상위 `docs/design`의
`…09_27_54.png`(HitToRun), `…09_27_51.png`(HighlightReaction)를 확보해 총 11개 모션 시트를 처리한다.

- 우투수_피치: 1448×1086, 4×3. 정면에서 화면 왼쪽 손 투구, 오른쪽 글러브로 R 확인. cell 7 분리 공 mask 필요.
- 좌투수_피치: 1448×1086, 4×3. 화면 오른쪽 투구손과 왼쪽 글러브로 L 확인. cell 7 분리 공 mask 필요.
- 우투수_준비: 1536×1024, 4×2. 이름과 달리 R FollowReady. 0→1→2→3→4→5→6→7 전환.
- 좌투수_준비: 1536×1024, 4×2. Set 반복. 손 겹침으로 실제 handedness NeedsReview.
- 투수_수비: 1536×1024, 4×2. L FollowReady. 화면 왼쪽 글러브 유지.
- 우타자_히트/좌타자_히트: 1448×1086, 4×3. 후면 자세의 회전/앞발 진행은 대칭이나 사진만으로 손 그립 확정이 어렵다. NeedsReview. 이전 구현의 A=R/B=L 판독은 배트가 홈 바깥으로 향하는 합성 결과와 충돌해 철회했다. 현재 검수 화면에서는 A를 오른쪽 타석(L 후보), B를 왼쪽 타석(R 후보)에 배치한다. 손잡이 확정은 아니다.
- 수비: 1448×1086, 4×3. R 송구 확인. cell 2,10 분리 공, cell 3~5 포구 공. 0→1→2→3→4→5→6→7→8→9→10→11.
- 플라이수비: 1774×887, 5×2. R 송구 준비 확인. cell 5 분리 공, 6 글러브 속 공. 0→1→2→3→4→5→6→7→8→9.

Pitch 기본 시간 순서는 Set→LegLift→Stride→ArmAcceleration→Release→Follow→Ready이며 cell 7 Release. Swing cell 5 SwingWindowOpen, 7 BatContact는 검수 후보다. Ground cell 3 GloveContact, 6 Transfer, 10 ThrowRelease. Fly cell 6 GloveContact, 9 ThrowReady.

좌우 손잡이와 모션 품질은 별개다. NeedsReview는 Production Catalog에 넣지 않는다. 키잉/분할/메타데이터 자동화는 검수용 출력을 생성하며, 최종 밝고 어두운 배경 QA 이후 승인 여부를 기록한다. 원본은 보존한다.
