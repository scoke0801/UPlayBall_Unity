# 개발용 실제 Identity와 KBO 엠블렘

## 범위

- 정규화된 1982~2025 KBO Source의 3,510명 실명을 Runtime-safe `PlayerPersonId`에 연결했다.
- 현재 KBO 10개 구단의 실제 이름과 공식 경기 BI를 `FranchiseId`에 연결했다.
- Editor와 Development Build는 기본 ON, 일반 배포 Build는 OFF이며 화면 설정 옵션도 숨긴다.
- 표시 오버레이만 바꾸므로 World Identity 원본, Stable ID, 경기·기록·세이브는 바뀌지 않는다.

## 공식 원본 확인

- KIA 타이거즈 CI: https://tigers.co.kr/tigers/bi/intro
- kt wiz BI: https://www.ktwiz.co.kr/ktwiz/bi/symbol
- 삼성 라이온즈 엠블렘: https://www.samsunglions.com/intro/intro04.asp
- SSG 랜더스 엠블렘: https://www.ssglanders.com/landers/emblem
- 현재 경기 BI 원본: `https://www.hanwhaeagles.co.kr/images/pages/game/bi_game_{team}.png`

게임 리소스에는 위 공식 사이트가 2026 경기 일정에서 사용하는 10개 구단 BI PNG를 그대로 복사했다.
작은 UI에서도 원본 형태와 색을 보존하는 것이 우선이라 재해석, 외곽선 추가, 색상 보정은 하지 않았다.

## ImageGen 검증

내장 ImageGen의 reference-image edit 모드로 KIA 원본을 두 차례 검증했다. 사용한 핵심 프롬프트는
`Preserve the exact silhouette, geometry, spacing, colors, outlines, and proportions. Clean pixelation and upscale edges only. Do not redesign or stylize.`였다.

첫 결과는 원본에 없는 그라데이션·입체 외곽선을 추가했고, 보정 결과는 형태를 바꾸고 투명 배경을
체크무늬 픽셀로 그렸다. 사용자의 `실제 엠블렘과 동일` 조건을 만족하지 못해 두 결과 모두 불합격
처리했다. 최종 게임 리소스는 이 실패 결과가 아니라 공식 원본 픽셀을 사용한다.
