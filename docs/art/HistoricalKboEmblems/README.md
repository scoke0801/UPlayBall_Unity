# 역사 KBO 엠블렘 원본 기록

구단주 모드의 개발용 실제 Identity 표시가 `TeamSeasonKey`의 원본 연도 브랜드를 재현하도록 사용하는
역사 구단 엠블렘 원본이다. 이 자산은 Editor와 Development Build의 표시 오버레이에만 포함되며
`WorldIdentityRegistry`, Save, 시뮬레이션 입력에는 관여하지 않는다.

## 원본과 출처

| 파일 | 대상 | 출처 |
|---|---|---|
| `SammiSuperstarsSource.png` | 삼미 슈퍼스타즈 | [머니투데이 역사 기사](https://www.mt.co.kr/sports/2016/06/21/2016062014541743335) |
| `ChungboPintosSource.jpg` | 청보 핀토스 | [오마이뉴스 엠블렘 자료](https://www.ohmynews.com/NWS_Web/View/img_pg.aspx?CNTN_CD=IE002959503) |
| `PacificDolphinsSource.jpg` | 태평양 돌핀스 | [Redbubble 보관 이미지](https://www.redbubble.com/i/poster/Pacific-Dolphins-Logo-by-SeoulSights/52932335.LVTDI) |
| `HyundaiUnicornsSource.jpg` | 현대 유니콘스 | [역사 로고 보관 글](https://flytoazuresky.tistory.com/485457) |
| `MbcChungyongSource.jpg` | MBC 청룡 | [오마이뉴스 역사 사진](https://www.ohmynews.com/NWS_Web/View/img_pg.aspx?CNTN_CD=IE001903528) |
| `ObBearsSource.png` | OB 베어스 | [Seeklogo 역사 BI](https://seeklogo.com/vector-logo/531748/ob-bears-1982-1998) |
| `BinggraeEaglesSource.png` | 빙그레 이글스 | [한화 이글스 역사 기사](https://v.daum.net/v/9xiTpbMUdZ) |
| `HaitaiTigersSource.png` | 해태 타이거즈 1982/1996 BI | [Seeklogo 역사 BI](https://seeklogo.com/free-vector-logos/haitai) |
| `SsangbangwoolRaidersSource.jpg` | 쌍방울 레이더스 | [역사 로고 보관 글](https://www.francisco.kr/570) |
| `SkWyvernsSource.png` | SK 와이번스 | [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:SK_Wyverns_insignia.svg) |
| `NexenHeroesSource.png` | 넥센 히어로즈 | [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Nexen_Heroes_insignia.svg) |
| `HeroesEmblemCollectionSource.png` | 우리·서울 히어로즈 공통 wordmark | [Seeklogo 엠블렘 컬렉션](https://seeklogo.com/vector-logo/664643/nexen-heroes-emblem-collection) |

구단 BI의 권리는 각 구단·권리자에게 있다. 배포 범위와 라이선스는 Production Build 전 별도로
검토하며, 현재 파일은 실제 데이터 검증을 위한 개발 표시 자산이다.

## 가공 원칙

- 다운로드 원본은 이 폴더에 보존하고, Unity용 결과만
  `Assets/10.Datas/Resources/DevelopmentKboIdentities/Emblems/`에 둔다.
- 원본의 흰색·무채색 외곽 배경만 경계 연결 방식으로 제거한다. 로고 내부의 흰색 도형은 보존한다.
- 여러 시대 BI가 한 이미지에 있는 해태는 원본 합본을 보존하고, 같은 표시 이름의 엠블렘 조회가
  모호해지지 않도록 1982 BI 하나를 역사 브랜드 대표 Sprite로 사용한다.
- RGBA, 투명 모서리, 밝고 어두운 배경 합성을 확인한 뒤 등록한다. 태평양의 청·적 사각 배경처럼
  원래 BI 구성 요소인 배경은 제거하지 않는다.
