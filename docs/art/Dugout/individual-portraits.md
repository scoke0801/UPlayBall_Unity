# 덕아웃 인물별 초상화

감독 6명·수석코치 7명이 서로 다른 프로필을 사용한다. 기존 피규어 재질·정면 상반신·모자와
유니폼을 유지하고 얼굴형·눈매·머리·연령감과 안경으로 구분한다. 스카우터 초상화는 참조하거나 수정하지 않았다.

## 자산과 연결

`Assets/10.Datas/Resources/UI/OwnerDugout/Individuals/{인물 ID}.png`를 사용한다.
`OwnerDugoutPresentationBuilder`가 인물 ID로 경로를 구성하며 덕아웃 현재 인선과 선택창이 같은 경로를 소비한다.
외형을 변경할 때 같은 파일을 교체하면 되므로 인물별 코드 분기를 추가하지 않는다. 시뮬레이션·밸런스·저장 구조 변경은 없다.

| ID | 인물 | 제작 |
| --- | --- | --- |
| MGR-BALANCED | 윤도현 | 기존 manager-male 고정 |
| MGR-ATTACK | 강태욱 | 신규, 넓은 턱·짧은 검은 머리 |
| MGR-SMALLBALL | 서민재 | 신규, 긴 타원형 얼굴·가느다란 눈썹 |
| MGR-PITCHING | 문재혁 | 신규, 둥근 볼·백발 |
| MGR-ANALYTIC | 한지성 | 신규, 타원형 안경·갸름한 얼굴 |
| MGR-FLEXIBLE | 이서윤 | 기존 manager-female 고정 |
| HC-CONTACT | 오세진 | 기존 coach-male 고정 |
| HC-RUNNING | 배준호 | 신규, 짧은 머리·탄탄한 눈매 |
| HC-SMALLBALL | 노경민 | 신규, 둥근 볼·부드러운 눈썹 |
| HC-BENCH | 임수현 | 신규, 둥근 안경·짧은 앞머리 |
| HC-STARTER | 최도윤 | 신규, 넓은 턱·백발·작은 눈 |
| HC-BULLPEN | 정해원 | 신규, 긴 얼굴·물결 머리 |
| HC-DEFENSE | 김하린 | 기존 coach-female 고정 |

## 생성과 검수

내장 ImageGen으로 신규 9종을 각각 생성했다. [실제 프롬프트](individual-prompts.md)는
기존 덕아웃 제작과 같은 `figurine-v1 / single-chroma-v2` 마스터를 사용한다.
성인 여성 프런트 매니저 재질 변환 프롬프트는 대상·비례가 달라 사용하지 않았다.
생성 원본은 `output/imagegen/dugout-individual/{ID}-source.png`에 보존했다.

`Tools/ImageBackground/Remove-ImageBackground.ps1 -Mode ChromaKey -KeyColor '#00FF00'`의
기본 경계 설정으로 투명화했다. 13종 모두 1254×1254 RGBA, 투명 모서리와 불투명 전경을 확인했다.
`Review-ImageBackground.ps1`로 만든 `managers-review.png`, `coaches-review.png`를 눈으로 검사해
얼굴·머리카락·안경·흰 유니폼 보존 및 밝고 어두운 배경의 경계를 확인했다.
두 합성 검수 PNG와 알파 통계 JSON은 생성 원본과 같은 폴더에 있다.

카탈로그 13개 ID에 대응하는 PNG와 서로 다른 파일 해시를 확인했다.

## 중단 작업 재개 검증

이전 작업은 이미지 생성·배경 제거·자산 연결까지 끝났고, 마지막 안내 이후 Unity 검증도 정상 종료했다.
재개 시 생성 누락이 없음을 확인해 기존 결과를 유지하고 남은 캡처 검수와 기록을 마무리했다.

- 현재 카탈로그 감독 6명·수석코치 7명과 PNG 13개의 ID가 정확히 일치한다. 해시는 모두 다르며
  `.meta` 파일도 모두 존재한다. 현재 PNG 13개와 초상화 경로를 만드는 PresentationModel은
  Unity 검증용 프로젝트에 사용한 파일과 SHA-256이 일치한다.
- 현재 자산에 `Review-ImageBackground.ps1`을 다시 실행했다. 모두 1254×1254 RGBA이며
  네 모서리가 투명하고 반투명 경계·불투명 전경이 존재한다. 결과는 원본 폴더의
  `managers-resume-review.png`, `coaches-resume-review.png`와 각각의 JSON에 보존한다.
  밝고 어두운 배경 합성에서 얼굴·안경·머리·흰 의상의 보존을 확인했다.
- 기존 `output/dugout-validation/results.xml`은 Unity 6000.3.21f1의
  `OwnerDugoutRedesignTests` **13개 통과·0개 실패**를 기록한다.
  `unity.log`도 종료 코드 0을 확인한다. 재개 작업에서 테스트를 새로 실행한 결과는 아니다.
- `output/dugout-validation/screenshots/`에는 1280×720·1920×1080·2560×1440·3440×1440의
  기본 인선·여성 인선·수석코치 선택창 캡처 12장이 있다. 네 해상도에 걸친 대표 캡처 5장을
  눈으로 확인해 초상화 배경·비율·잘림을 검수했다. 개별 후보 13명 전체를 클릭하는
  Play Mode 검증이나 최종 플레이어 빌드는 수행하지 않았다.
