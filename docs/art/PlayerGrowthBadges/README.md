# 선수 카드 성장판 등급 아이콘

## 배지 툴팁 문구 간소화

- 유학 완료 문구와 뒤의 빈 줄을 제거했다. 유학지·등급·과정·적용 효과는 유지한다.
- 스킬블록 본문은 배치된 블록의 등급·효과 타입만 `N 등급 · 교타력` 형식으로 표시한다. 동일 등급·효과는 한 번만 표시하며 개수·칸 수·합계·상한은 제거했다.
- 기존 `PlayerCardGrowthBadgesView` 툴팁 Skin/Layout·여백·자동 높이·호버·포커스를 재사용한다. 신규 자산이나 Placeholder는 없다.
- 외부 컴파일은 Windows SDK 폴더 접근 권한 오류(MSB4184)로 중단됐다. 사용자 요청으로 에디터 테스트는 미실행이며 실제 화면·해상도별 Bounds 검수도 미실행이다.

## 스킬블록 N/R/E/U/L 제작 기록

- 기존 C/B/A/S 연결을 Normal=N, Rare=R, Elite=E, Unique=U, Legendary=L로 교체했다. 특성훈련 등급은 기존 C/B/A/S 계약을 유지한다.
- 배지는 최고 장착 희귀도다. 투자량을 같은 의미로 취급하지 않도록 공용 툴팁에 장착 블록 개수, 사용 칸/전체 칸, 실제 적용 능력치 합계/상한을 함께 표시한다. 합계는 경기와 동일한 `SkillBoardService.GetAbilityBonus`를 사용한다. 밸런스 수치는 변경하지 않았다.
- ImageGen 단일 원본 `SkillBlockRanks_Source.png`에서 `BoardRank_{N,R,E,U,L}.png` 256px Sprite를 분리했다. 초록 크로마키를 요청했으나 생성 결과에 실제 알파가 있어 추가 배경 제거 없이 보존했다. 원본 크기는 2172×724다.
- `SkillBlockRanks_AlphaReview.png`와 JSON에서 다섯 Sprite 모서리 투명, 밝고 어두운 배경 합성의 글자·프레임 보존을 확인했다.
- Skin/Layout Reference: 기존 `PlayerCardGrowthBadgesView`, 성장판 육각 메달, 공용 툴팁. 기존 우측 세로 슬롯·크기·호버·키보드 포커스 경로를 재사용한다. 신규 화면·내비게이션·플레이스홀더는 없다.
- 외부 `dotnet build Baseball.Presentation.csproj --no-restore -v quiet "-p:TargetPlatformRootPath=C:/Program Files (x86)/Windows Kits"` 오류·경고 0개. SDK 기본 탐색 폴더 권한 문제를 경로 지정으로 해결했다.
- 사용자 지시로 에디터 테스트는 실행하지 않았다. 실제 Unity 카드 화면, 해상도별 Bounds·툴팁 높이·입력 검수는 미실행이다.

아래 C/B/A/S 내용은 이전 제작 기록이며 현재 카드 연결에는 사용하지 않는다.

- 공용 `PlayerCardGrowthBadgesView`에서 상세·미니 카드 모두 오른쪽에 유학 → 특성 → 성장판을 정렬한다. 서포트 적용 시 같은 안전 영역을 네 슬롯으로 나눈다.
- 성장판 C/B/A/S는 ImageGen으로 한 장에 생성한 육각 메달을 개별 256px Sprite로 분리했다. 기존 특성 방패·유학·서포트 원화와 툴팁은 재사용한다.
- `BoardRanks_Source.png`는 생성 원본이다. 크로마키 초록을 요청했으나 실제 결과는 알파가 있는 RGBA였으므로 불필요한 추가 배경 제거는 하지 않았다. 원본은 별도로 보존한다.
- `Assets/10.Datas/Resources/UI/PlayerGrowthBadges/BoardRank_{C,B,A,S}.png`가 실제 연결 자산이다. 기존 타일 위 별도 글자 표시는 제거했다.
- `BoardRanks_AlphaReview.png`에서 밝고 어두운 배경, 112px·24px 합성을 검수했다. 각 Sprite 모서리 알파는 0이다.
- 외부 `dotnet build Baseball.Presentation.csproj --no-restore -v quiet` 오류·경고 0개. 사용자 요청으로 에디터 테스트를 실행하지 않았다. 실제 해상도별 카드 배치·포커스·툴팁 검수는 미실행이다.

생성 지시: 동일한 육각 실루엣의 2×2 시트, 좌상 C(은색), 우상 B(파랑), 좌하 A(금색), 우하 S(보라·금색). 중앙에 큰 등급 문자, 성장판을 뜻하는 격자 음각, 정면 새틴 금속, 동일 조명, 셀별 충분한 여백.

## 상세 카드 좌우 전환 배지 누락 수정

- `ClearChildren`이 앞면 아이콘을 분리하고 지연 파괴한 뒤에도 공용 배지 컴포넌트가 이전 참조를 재사용하던 원인을 수정했다. `SetIcon`은 현재 카드에서 분리된 아이콘을 재생성하고, 서포트 잔여 경기 텍스트도 새 아이콘에 다시 연결한다.
- 기존 `UI_Popup_OwnerPlayerCard`와 `PlayerCardGrowthBadgesView`, 배지 Sprite·스킨·배치·입력·포커스 경로를 재사용한다. 레이아웃과 Content Safe Bounds 계산은 변경하지 않았다.
- 변경 파일의 `git diff --check` 통과. 외부 Presentation 컴파일은 작업 범위 밖 `OwnerModeShellCoordinator.cs:1491`의 `UI_Popup_OwnerCardSynthesis` 참조 오류 1건으로 실패했다. 사용자 지시로 에디터 테스트는 실행하지 않았으며 실제 좌우 연속 전환·해상도별 화면 검수는 미실행이다.

## 호버 트윈 누락 수정

- 미니·상세 카드에서 성장판 C/B/A/S와 서포트 배지도 기존 유학·특성과 동일한 0.12초·1.08배 호버/포커스 트윈을 공유한다. 공통 배율을 사용해 등급 배지만 있는 카드의 연속 진입·이탈도 현재 크기부터 이어진다.
- 카드 재바인딩 시 현재 배율을 적용하고 비활성화 시 모든 배지를 원래 크기로 복원한다. 기존 Sprite·배치·툴팁·입력 경로를 재사용한다.
- 수정 후 외부 컴파일은 Game 레이어의 `AdvanceNewsClock`, `RecordManagerNews` 등 참조 오류 8건으로 실패했다. 사용자 요청으로 에디터 테스트는 실행하지 않았으며 실제 화면의 트윈·해상도별 검수는 미실행이다.

## 툴팁 설명 보완

- 유학 설명은 상태 → `유학지 · n 등급` → 과정명 → 적용 효과 순서로 줄을 나눈다. 등급은 `ApplyStudyTiers`가 사용하는 해금 단계의 1~3 등급이며 카드·특성 등급과 구분한다.
- 모든 카드 생성 경로에서 실제 `OwnerCardGrowth` 정의를 전달한다. 완료 원장에 저장된 과정명과 일치하는 정의로 목적지·등급을 찾고, 같은 과정·결과의 활성 효과를 합산한다. 일치하는 기록이 없으면 유학지·등급 기록 없음을 표시한다.
- 진행 중에는 목적지·등급·남은 주수와 기본 수료 효과, 성장 상한 안내를 표시한다. 사전에 결정된 대성공 결과는 노출하지 않는다.
- 유학지·등급 연결 후 외부 Presentation 컴파일 오류·경고 0개. 에디터 테스트와 실제 화면 검수는 실행하지 않았다.

- 특성명과 효과 설명을 줄바꿈으로 분리하고 `C 등급`처럼 등급 문자 뒤를 띄운다. 기존 공용 툴팁 스킨·여백·자동 높이 계산을 재사용한다.
- 유학 완료 배지는 카드에 저장된 현재 유학 보너스를 능력치별 `교타력 +3` 형식으로 표시한다. 보너스가 없으면 없음을 명시한다. 진행 중에는 남은 주수·완료 시 적용 안내와 기존 유학 효과를 구분하며 미확정 보상은 노출하지 않는다.
- 외부 컴파일은 작업 범위 밖 `OwnerGuidePresentationData.cs`의 `OwnerManagerNewsCopy` 참조 오류 1건으로 실패했다. 에디터 테스트와 실제 화면 검수는 미실행이다.
