# 은퇴 회고 기반 시스템 연결 계획

## 1. 문서 목적

이 문서는 은퇴 회고 `한 선수의 기록`이 현재 숨기거나 축약해서 보여 주는 항목을 실제 사실로
채우기 위해, 먼저 구현해야 할 기반 시스템과 연결 계약을 정의한다.

은퇴 회고 화면을 더 화려하게 만드는 계획이 아니다. 경기·감독 AI·뉴스·월드 기록·세이브가
각자 소유해야 할 사실을 정확히 생산하고, 은퇴 시점에는 그 사실을 변경 불가능한
`RetirementRecapSnapshot`으로 고정하는 것이 목적이다.

판단 기준은 다음 두 가지다.

- 플레이어가 어떤 선택을 했고 그 결과가 무엇이었는지 저장된 사실로 설명할 수 있어야 한다.
- 밸런스나 문구 계산식이 바뀌어도 이미 은퇴한 커리어의 기록과 대표 순간은 달라지지 않아야 한다.

`BaseballManager_PROJECT.md`가 전체 프로젝트의 기준 문서이며, 이 문서는 41.10절 은퇴 회고의
세부 구현 계약이다.

---

## 2. 현재 구현과 미연결 범위

현재 구현은 다음을 이미 지원한다.

- 경기·훈련·유학·계약 수락/거절·트레이드 의사·부상을 `CareerMemoryLog`에 누적
- 시즌별 소속팀·역할·성적·Overall·성장·부상·플레이 방침·Skill Board 보관
- 대표 순간 점수화와 최대 7개 고정, 통산 기록·칭호·유산 스냅샷 생성
- 마지막 시즌 선언과 즉시 은퇴, 로스터·계약 정리
- 5막 회고, 최종 카드, 8개 탭 기록관, 현재 플레이 세션 내 다시 보기

아래 항목은 저장 원본이 없거나, 원본은 있지만 은퇴 회고까지 이어지는 참조 계약이 없다.

| 구분 | 현재 상태 | 은퇴 회고에 나타나는 제한 | 우선순위 |
| --- | --- | --- | ---: |
| 구종 선택·효율 | `PitchType`과 생성 시 repertoire만 존재 | 실제 사용 구종과 결정구를 숨김 | P0 |
| 경기 원본 보관 | 확정 직후 `CareerGameResult`만 존재 | `MatchId`는 표시하지만 과거 결과 화면을 열 수 없음 | P0 |
| 특수 기록·연속 기록 | 기본 시즌 누계만 존재 | 끝내기, 만루 홈런, 연속 안타, 완투·완봉 등을 계산 불가 | P0 |
| 주전·보직 변화 | 경기별 실제 역할만 누적 | 일회성 선발과 감독이 확정한 주전 승격을 구분하지 못함 | P0 |
| 뉴스 연결 | 중요 기사 원문과 `ArticleId`는 보관 | Memory와 기사 발행 결과가 연결되지 않음 | P0 |
| 부상 에피소드 | 부상 이력과 일부 치료 선택은 존재 | 회복 완료일과 실제 복귀 경기의 연결이 약함 | P1 |
| 리그·구단 통산 순위 | 현재 시즌 순위만 계산 가능 | 은퇴 순간의 리그/구단 통산 순위를 표시할 수 없음 | P0 |
| 디스크 Save/Load | 런타임 상태와 마이그레이션 계약만 존재 | 앱 종료 후 기록관 누적 불가 | P0 |
| 원본 화면 Deep Link | View에 ID 전달 슬롯만 존재 | 링크가 텍스트이며 실제 버튼 이동이 없음 | P1 |
| 은퇴 전용 오디오 | Lobby/Match BGM만 존재 | 회고 음악·카운트업·라커 종료음을 재생하지 못함 | P1 |
| 커리어 카드 이미지 | 런타임 UI만 존재 | 기록관 썸네일 PNG를 영구 보존하지 못함 | P2 |
| 동료·감독·팬 회고 | 관계 지표만 존재 | 발화자와 당시 맥락을 가진 사실 기반 인용문이 없음 | P2 |

---

## 3. 전체 데이터 흐름

```text
Simulation
  MatchResult · MatchEvent · MatchFactReport
        ↓
Game commit services
  CareerGameResult · 경기 원본 · 특수 기록 · 역할 변화 · 부상 · 뉴스 · 월드 통산 기록
        ↓
CareerMemoryLog + source IDs
        ↓ 은퇴 확정 시 한 번만 해석
RetirementRecapSnapshot
        ↓
Presentation
  5막 회고 · 기록관 · 원본 경기/뉴스 이동 · 커리어 카드

CareerSaveRepository ── 활성 커리어 전체 상태 저장
CareerArchiveRepository ── 은퇴 스냅샷과 카드만 별도 영구 보관
```

핵심 규칙은 다음과 같다.

1. 사실을 만든 시스템이 원본을 소유한다. `RetirementRecapService`가 과거를 추측하지 않는다.
2. 동일 경기·사건은 커밋 경계에서 정확히 한 번만 누적한다.
3. Memory는 원본을 복제하기보다 안정적인 `MatchId`, `ArticleId`, `SourceEventId`로 연결한다.
4. 은퇴 스냅샷을 만든 뒤에는 라이브 월드 통계나 뉴스 템플릿을 다시 읽어 결과를 바꾸지 않는다.
5. 과거 버전에 없던 데이터는 `기록 없음`으로 남긴다. 소급 생성은 저장된 사실만으로 가능한 경우에만 한다.

---

## 4. P0-1 — 실제 구종 선택·사용·효율

### 4.1 현재 문제

`PitchType`과 `PitchRepertoireEntry`는 있지만 repertoire는 생성 프로필에만 있고,
`MatchEventType.Pitch`에는 구종이 없다. `PlateAppearanceSimulator`도 구종을 결과 확률에 사용하지 않는다.

따라서 현재 상태에서 단순히 구종 이름을 무작위로 붙이면 실제 경기에 영향을 주지 않은 장식 데이터가 된다.
은퇴 회고의 `가장 많이 던진 변화구`, `결정구로 가장 효과가 좋았던 구종`에는 사용할 수 없다.

### 4.2 필요한 기반

#### 선수 구종 상태

모든 투수에게 시즌을 넘어 유지되는 구종 상태가 필요하다.

```csharp
public sealed class PlayerPitchRepertoireState
{
    public int PlayerId;
    public IReadOnlyList<PitchRepertoireEntry> Entries;
}
```

- 내 선수와 AI 투수 모두 `PlayerState`에서 소유한다.
- 생성 시 선택한 repertoire는 `CareerCreationProfile`에만 남기지 않고 `PlayerState`로 복사한다.
- AI 선수는 archetype과 능력치에 따라 결정론적으로 3~5개 구종을 생성한다.
- 신구종 습득과 숙련도 변화가 구현되면 이 상태만 수정한다.

#### 구종 선택

`Baseball.Simulation`에 `PitchSelectionSimulator`를 둔다.

입력은 repertoire, 숙련도, count, `PitchingApproach`, batter handedness, 이전 구종이다.
출력은 `PitchType` 하나다. 경기 결과 RNG 순서를 흔들지 않도록 경기 Seed에서 분리한
전용 RNG stream을 주입한다.

`MatchEvent`의 `Pitch` 사건에 `PitchType`을 추가하고 결정론 비교·해시에도 포함한다.
즉시 경기와 상세 경기 모두 같은 선택기를 사용해야 한다. 다만 즉시 경기는
`NullMatchEventSink`를 사용하므로 영구 통계의 원본을 이벤트 버퍼에만 두어서는 안 된다.

#### 결과 모델 연결

구종은 최소한 다음 결과에 실제 영향을 줘야 한다.

- 숙련도와 batter handedness에 따른 Contact 조정
- 구종 계열에 따른 SwingingStrike, GroundBall, HardHit 성향
- 같은 구종 반복 사용 시 예측 페널티
- 주무기와 `PitchingApproach` 조합 효과

계수는 `BalanceTable`에 두고 코드에 흩뿌리지 않는다. 이 변경은 밸런스 변경이므로
10,000경기 이상을 실행해 리그 평균 득점, ERA, BB%, K%, HR%, 투구 수와 구종 분포를 검증한다.

### 4.3 누적 데이터

```csharp
public sealed class PitchUsageLine
{
    public PitchType PitchType;
    public int Pitches;
    public int CalledStrikes;
    public int SwingingStrikes;
    public int BallsInPlay;
    public int Outs;
    public int HitsAllowed;
    public int HomeRunsAllowed;
    public int StrikeoutsFinished;
}
```

Simulation은 event sink 사용 여부와 무관하게 allocation이 제한된 `MatchFactReport`를
`MatchResult`에 포함한다. 이 report에는 선수별 `PitchUsageReport`가 들어가며,
`LeagueStatisticsService.RecordMatch`의 확정 커밋에서 이를 `CareerSeasonExperienceState`에 더한다.
Presentation에서 이벤트를 소비할 때 누적하면 즉시 결과 모드와 전체 중계 모드의 기록이
달라지므로 금지한다.

가장 많이 던진 구종은 `Pitches`로 정한다. 가장 효과적인 결정구는 최소 표본을 만족한 구종 중
`StrikeoutsFinished`와 SwingingStrike 비율로 정하며, 표본이 부족하면 표시하지 않는다.

### 4.4 완료 조건

- 같은 Seed의 전체 `MatchEvent` 스트림과 구종 누계가 완전히 일치한다.
- `NullMatchEventSink`에서도 모든 구종의 `Pitches` 합계가 선수의 `PitchesThrown`과 일치한다.
- 보유하지 않은 구종이 선택되는 경우가 0건이다.
- 즉시 결과와 상세 중계의 구종 누계가 같다.
- 10,000경기 통계가 목표 범위에 들고, 구종 효과가 능력치보다 결과를 압도하지 않는다.

---

## 5. P0-2 — 경기 원본과 결정적 기록 보관

### 5.1 현재 문제

`CareerGameResult`는 통계를 누적한 직후 장기 보관되지 않는다. 은퇴 Memory에는 `MatchId`와
내 선수 기록 일부만 남기 때문에 당시 점수, 상대, 라인스코어, 주요 사건을 다시 열 수 없다.

모든 경기의 전체 이벤트 스트림을 그대로 저장하면 장기 커리어 세이브가 지나치게 커진다.
따라서 전체 경기 요약과 대표 경기 상세 보관을 분리한다.

### 5.2 데이터 계약

```csharp
public sealed class CareerMatchArchiveEntry
{
    public int MatchId;
    public int SeasonId;
    public int SeasonYear;
    public int Round;
    public DateTime GameDate;
    public CompetitionScope Scope;

    public int HomeTeamId;
    public int AwayTeamId;
    public int HomeScore;
    public int AwayScore;

    public PlayerGameLineSnapshot MyPlayerLine;
    public IReadOnlyList<int> InningRunsHome;
    public IReadOnlyList<int> InningRunsAway;
    public IReadOnlyList<MatchHighlightSnapshot> Highlights;

    public bool IsPinned;
    public string SourceEventId;
}
```

보관 정책은 다음과 같다.

- 모든 내 선수 커리어 경기는 compact summary를 저장한다.
- 데뷔, 최초 기록, 포스트시즌, 우승 결정전, 마지막 경기, 기록 경신 후보는 `IsPinned`로 보호한다.
- 전체 `MatchEvent` 복제는 대표 후보 상위 경기와 마지막 경기 등 제한된 수에만 허용한다.
- 결과 화면 재열람에는 compact summary를 사용하고, 경기 전체 재생은 별도 기능으로 취급한다.
- 보관 크기 제한으로 삭제할 때 Memory나 News가 참조하는 경기는 삭제하지 않는다.

Simulation의 `MatchFactReport`에는 event sink와 독립적인 이닝별 득점, 특수 기록 사실,
제한된 Highlight 후보도 포함한다. `CareerGameResult`가 확정되고 통계 누적이 성공한 같은
커밋 경계에서 이 report를 archive로 옮긴다. 화면 진입 여부나 관전 모드에 의존하면 안 된다.

### 5.3 특수 기록·연속 기록

기본 시즌 합계와 별도로 `CareerAchievementState`를 둔다.

추가로 누적할 사실은 다음과 같다.

- 타자: MultiHit 경기, WalkOffHit, GrandSlam, 현재/최장 HittingStreak, 한 경기 최다 안타·홈런·타점
- 투수: CompleteGame, Shutout, ScorelessAppearance, 현재/최장 ScorelessInnings,
  한 경기 최다 탈삼진
- 공통: 포스트시즌 출전, 우승 확정 경기 기여, 역할별·포지션별 실제 출전 경기

WalkOffHit와 GrandSlam은 최종 BoxScore만으로 알 수 없으므로 Simulation이 득점·주자 상태와
타석 종료 시점에서 `MatchAchievementFact`로 확정한다. 완투·완봉도 팀 경기 종료 조건과 실제
투수 교체 이력을 함께 보고 같은 report에 남긴다. Game은 이 사실을 다시 추론하지 않는다.

연속 기록은 시즌 경계에서 초기화할 것과 커리어 전체로 이어질 것을 야구 규칙에 맞게 구분한다.
정규시즌과 포스트시즌 연속 기록은 섞지 않는다.

### 5.4 은퇴 회고 연결

- `CareerMemoryRecord.MatchId`가 `CareerMatchArchiveEntry.MatchId`를 참조한다.
- 은퇴 확정 시 Featured Memory가 참조하는 경기 요약을 `RetirementRecapSnapshot`에도 복사한다.
- 활성 세이브가 없어도 은퇴 기록관의 `마지막 경기`와 대표 경기 결과는 열 수 있어야 한다.
- `경기 결과 보기`는 `CareerDeepLinkRouter.OpenArchivedMatch(matchId)`를 호출한다.

### 5.5 완료 조건

- archive의 점수·내 선수 기록이 BoxScore 및 시즌 누계와 일치한다.
- 같은 `MatchId`를 두 번 커밋하면 거부하거나 동일 결과로 무시한다.
- 모든 Featured Memory의 `MatchId`가 은퇴 스냅샷 안에서 해석 가능하다.
- 20시즌 커리어에서 archive 용량 상한과 pinned 보존 규칙을 만족한다.

---

## 6. P0-3 — 감독이 확정한 역할·주전 경쟁 이력

### 6.1 현재 문제

현재는 실제 경기 역할을 세어 `PrimaryRole`을 만들고 첫 선발 출전을 `RoleBreakthrough`로 기록한다.
하지만 대체 선발 한 경기와 감독이 시즌 보직을 주전으로 올린 사건은 의미가 다르다.

`주전 확정`, `선발 로테이션 진입`, `마무리 전환` 같은 문장을 사용하려면 감독 AI가 내린
역할 배정 자체가 저장되어야 한다.

### 6.2 데이터 계약

```csharp
public sealed class CareerRoleAssignmentRecord
{
    public string AssignmentId;
    public int SeasonId;
    public int DateIndex;
    public int TeamId;
    public ExpectedRole PreviousRole;
    public ExpectedRole NewRole;
    public ManagerDecisionReason Reason;
    public int CompetitionRank;
    public int ManagerEvaluation;
}
```

- `ManagerUsageAi`가 역할을 다시 평가한 뒤 역할이 실제로 바뀔 때만 기록한다.
- 계약서의 기대 역할, 감독의 현재 배정 역할, 한 경기 실제 출장 역할을 서로 다른 필드로 유지한다.
- 트레이드·부상 대체·스프링캠프 경쟁·부진·복귀 등 이유를 enum으로 저장한다.
- 팀 이름이나 선수 이름으로 역할을 분기하지 않는다.

### 6.3 은퇴 회고 연결

- `RoleBreakthrough`는 첫 선발 출전이 아니라 첫 의미 있는 역할 승격 기록에서 만든다.
- 첫 선발 출전은 별도의 `FirstStart` 사실로 남긴다.
- 역할 승격 뒤 실제 출전이 한 번도 없으면 `주전으로 활약했다`고 서술하지 않는다.
- 가장 오래 유지한 보직은 Assignment 기간과 실제 출전 경기 수를 함께 보고 선정한다.

### 6.4 완료 조건

- 같은 역할 재평가가 중복 이력을 만들지 않는다.
- 역할 변경 날짜와 경기 역할이 시간 역행하지 않는다.
- 계약 기대 역할과 감독 실제 역할이 다를 때 두 값이 모두 보존된다.
- 후보→주전, 선발→계투, 계투→마무리 전환 테스트가 통과한다.

---

## 7. P0-4 — 뉴스 원문과 Memory 연결

### 7.1 재사용할 기존 기반

`CareerNewsState`는 이미 다음을 보유한다.

- 생성 당시 제목·Lead·본문을 고정한 `NewsArticleState`
- 안정적인 `ArticleId`
- 중요 기사를 시즌 이후에도 남기는 `CareerNewsArchiveEntry`
- 원인이 된 `SourceEventIds`

새 뉴스 저장소를 만들 필요는 없다. 빠진 것은 Memory의 원인 사건과 발행된 기사 사이를 묶는 과정이다.

### 7.2 연결 방식

Memory 생성 시 아직 기사가 발행되지 않았을 수 있으므로 `ArticleId`를 즉시 추측하지 않는다.
`CareerMemoryRecord`에는 `SourceEventId`를 추가하고 기존 `NewsId`는 연결 완료 뒤의 결과 ID로 사용한다.

1. 경기·계약·부상 시스템이 공통 `SourceEventId`를 만든다.
2. `CareerMemoryRecord`와 `NewsEvent`가 같은 `SourceEventId`를 보관한다.
3. `CareerNewsService.PublishCycle`이 기사 발행을 끝낸 뒤
   `CareerMemoryNewsLinkService`가 SourceEventId로 `ArticleId`를 연결한다.
4. 은퇴 확정 시 연결된 중요 기사의 원문을 `RetirementRecapSnapshot`에 복사한다.

`CareerNewsState`에는 다음 읽기 API가 필요하다.

```csharp
public NewsArticleState FindArticle(string articleId);
public NewsArticleState FindLatestArticleBySourceEvent(string sourceEventId);
```

현재 private인 `FindArticle`을 읽기 전용 public API로 승격하되 상태 수정 권한은 주지 않는다.

### 7.3 보관 정책

- Featured Memory가 참조한 기사는 반드시 `IsCareerArchive`로 승격한다.
- 일반 기사를 시즌 종료 시 정리해도 참조된 기사 원문은 남는다.
- 기사 템플릿이 바뀌어도 이미 발행된 Headline/Lead/Body를 다시 생성하지 않는다.
- 기사가 실제로 발행되지 않은 사건에는 뉴스 버튼을 만들지 않는다.

### 7.4 완료 조건

- 기사 발행 전 생성된 Memory도 발행 후 정확한 `ArticleId`와 연결된다.
- 시즌 compact 이후에도 Featured Memory의 기사 원문을 열 수 있다.
- 하나의 사건이 병합 기사에 포함된 경우 병합된 최종 기사로 연결된다.
- 없는 기사를 만들어내거나 다른 선수의 기사로 연결하는 경우가 0건이다.

---

## 8. P1 — 부상 에피소드와 실제 복귀

부상 이력에는 발생 원인·심각도·예상 결장 기간·일부 치료 선택이 존재하지만,
`부상 → 치료 선택 → 회복 단계 → 출전 가능 판정 → 실제 복귀 경기`를 하나로 묶는 ID가 없다.

```csharp
public sealed class CareerInjuryEpisode
{
    public string EpisodeId;
    public int SeasonId;
    public int TeamId;
    public DateTime InjuredAt;
    public InjurySeverity Severity;
    public InjuryTreatmentChoice? TreatmentChoice;
    public DateTime? ClearedAt;
    public int ReturnMatchId;
}
```

연결 규칙은 다음과 같다.

- 부상 확정 시 Episode를 생성하고 이후 뉴스도 같은 `EpisodeId`를 SourceEventId로 사용한다.
- 치료·재활 선택은 선택이 확정된 커밋에서 Episode에 기록한다.
- 회복 완료는 출전 가능 판정일이며, 복귀는 그 이후 첫 공식 출전 경기다.
- 회복만 하고 출전하지 못한 경우 `복귀전`을 생성하지 않는다.
- 의료 은퇴는 마지막 활성 Episode를 은퇴 사유와 연결하되 부상을 반복 노출하지 않는다.

테스트는 치료 선택 보존, 회복 후 무출장, 실제 복귀 경기 연결, 시즌을 넘긴 부상을 포함한다.

---

## 9. P0-5 — 월드·리그·구단 통산 기록과 순위

### 9.1 현재 문제

`CareerRecordsService`는 현재 시즌 선수 순위와 내 선수의 시즌 이력을 계산한다.
`LeagueSeasonSummaryState`는 팀 순위·우승·수상만 보존한다.
`WorldRecordState`는 시작 연도 외에 통산 통계를 가지고 있지 않으며, AI 선수의 `PlayerState`도
통산 PlateAppearance·PitchingOut·등록 시즌 수만 가진다.

시즌이 교체되면 다른 선수들의 상세 시즌 통계가 사라지므로 은퇴 시점에 리그 통산 순위를
소급 계산할 수 없다.

### 9.2 데이터 소유권

`WorldState.Records` 아래에 전 선수의 확정 통산 기록을 둔다.

```csharp
public sealed class PlayerCareerStatisticsState
{
    public int PlayerId;
    public CareerStatLine RegularSeason;
    public CareerStatLine Postseason;
    public IReadOnlyList<PlayerTeamCareerSplit> TeamSplits;
    public IReadOnlyList<PlayerPositionCareerSplit> PositionSplits;
}
```

- `LeagueStatisticsService.RecordMatch`가 시즌 기록과 월드 통산 기록을 같은 커밋에서 갱신한다.
- 정규시즌과 포스트시즌을 분리한다.
- 이적 전후 구단 split을 보존한다.
- 은퇴 선수도 월드 레지스트리와 통산 기록에서 삭제하지 않는다.
- 중복 `GameId` 커밋을 검출한다.

### 9.3 순위 스냅샷

```csharp
public readonly struct CareerRankSnapshot
{
    public LegacyScope Scope;
    public CareerStatMetric Metric;
    public double Value;
    public int Rank;
    public int QualifiedPopulation;
    public int TeamId;
    public LeagueId LeagueId;
}
```

은퇴 확정 직전에 다음 세 범위를 계산한다.

- `World`: `HistoryStartYear` 이후 전체 월드 선수
- `League`: 해당 `LeagueId`에서 기록한 split
- `Franchise`: 해당 구단에서 기록한 split

Rate stat은 최소 Career PA/IP 기준을 만족한 선수만 순위를 계산한다. 동률은 같은 순위로 처리하고,
비교 순서를 `PlayerId`로 고정해 결정론을 보장한다.

화면에는 다음 조건 중 하나를 만족한 순위만 노출한다.

- 상위 50위
- 상위 10%
- 구단 기록 상위 10위

순위 밖에서는 억지 등수 대신 장기 출전, 연속 등록 시즌, 여러 구단 주전 경험 같은 사실 기반
`MeaningfulRecordSnapshot`을 보여 준다.

### 9.4 과거 기록 범위

게임 월드가 시작되기 전 실제 역사나 가상의 선대 기록을 만들지 않는다.
`HistoryStartYear` 이후 기록만 계산하고 화면에 `이 세계가 시작된 이후`라는 범위를 명시한다.

### 9.5 완료 조건

- 동일한 경기 집합을 시즌 합계로 더한 값과 월드 통산 합계가 일치한다.
- 이적 선수의 팀 split 합이 전체 통산과 일치한다.
- 은퇴 선수를 포함한 순위를 독립적인 brute-force 계산과 비교해 모두 일치한다.
- 동률, 최소 타석/이닝, 정규/포스트시즌 분리가 정확하다.
- 20시즌 월드에서 기록 누적 시간과 메모리 사용량이 허용 범위에 든다.

---

## 10. P0-6 — 실제 디스크 Save/Load와 커리어 기록관

### 10.1 저장 경계

활성 커리어 저장과 은퇴 기록관 저장을 분리한다.

```text
Application.persistentDataPath/
  Saves/
    slot_001.json
    slot_001.bak
  CareerArchive/
    index.json
    {ArchiveId}.json
    {ArchiveId}.png
```

- `CareerSaveRepository`: 진행 중인 월드·커리어 상태 전체 저장/로드
- `CareerArchiveRepository`: 은퇴 스냅샷과 최종 카드만 독립 저장
- 활성 세이브를 삭제해도 기록관 보존 여부는 삭제 확인 화면에서 별도로 선택

### 10.2 직렬화 계약

현재 도메인 클래스의 private collection과 get-only property를 `JsonUtility`에 직접 넘기지 않는다.
명시적인 public field 기반 Save DTO와 mapper를 둔다.

```csharp
[Serializable]
public sealed class CareerSaveEnvelope
{
    public int SaveVersion;
    public string GameVersion;
    public string SavedAtUtc;
    public string PayloadChecksum;
    public CareerSaveData Data;
}
```

- SO 참조 대신 ID만 저장한다.
- dictionary와 hash set은 정렬된 배열 DTO로 변환한다.
- 쓰기는 `.tmp`에 완료한 뒤 기존 파일을 `.bak`으로 옮기고 원자적으로 교체한다.
- 로드 시 checksum과 불변 조건을 검사하고 실패하면 `.bak` 복구를 시도한다.
- `SaveVersion`과 은퇴 기록관의 `ArchiveVersion`을 분리한다.

### 10.3 은퇴 커밋 순서

```text
1. 마지막 시즌 Archive 확정
2. RetirementRecapSnapshot 생성
3. CareerArchiveRepository에 snapshot 저장
4. 저장 성공 확인
5. 월드에서 선수 은퇴 커밋
6. 활성 세이브 저장
7. 회고 Presentation 진입
```

3단계가 실패하면 은퇴를 완료 처리하지 않고 재시도 가능한 상태를 유지한다.
월드 은퇴만 성공하고 기록관 저장이 사라지는 반쪽 커밋을 만들지 않는다.
`CareerRetirementState`에는 고정 `RetirementTransactionId`와 진행 단계를 저장하고,
`CareerArchiveRepository`는 같은 TransactionId의 저장을 idempotent upsert로 처리한다.
각 단계 사이에서 프로세스가 종료돼도 재로드 후 같은 트랜잭션을 이어갈 수 있어야 한다.

### 10.4 마이그레이션

- 다음 SaveVersion에서 새 상태를 추가하되 구현 시점의 실제 버전 번호를 사용한다.
- v12 이전 세이브는 저장된 시즌 기록만 archive로 변환한다.
- 구종 사용량, 역할 변경 날짜, 거절 계약, 과거 경기 원문은 소급 추정하지 않는다.
- `ArchiveVersion` 마이그레이션은 이미 고정된 문장과 순위를 재계산하지 않고 필드 형태만 변환한다.

### 10.5 완료 조건

- 새 게임→10시즌 진행→저장→프로세스 재시작→로드 결과가 저장 전 상태와 일치한다.
- 은퇴 직전 저장과 은퇴 후 기록관 저장을 각각 재로드할 수 있다.
- v11/v12 fixture를 최신 버전으로 마이그레이션한다.
- 임의로 중단된 `.tmp`, 손상된 본 파일, 정상 `.bak` 복구 테스트가 통과한다.
- 여러 은퇴 커리어가 `ArchiveId` 충돌 없이 기록관에 누적된다.

---

## 11. P1 — 기록관 Deep Link

현재 `RetirementArchivePage`는 `LinkedMatchId`와 `LinkedNewsId`를 전달하지만 UI는 텍스트만 그린다.

Presentation에 `CareerDeepLinkRouter`를 두고 다음 명령을 지원한다.

```csharp
OpenArchivedMatch(int matchId)
OpenArchivedNews(string articleId)
OpenArchivedSeason(int seasonId)
```

- 버튼은 대상이 실제로 존재할 때만 생성한다.
- 은퇴 뒤 `CareerManager.HasActiveCareer == false`여도 snapshot 내부 archive를 열 수 있어야 한다.
- 화면을 닫으면 은퇴 기록관의 이전 탭과 스크롤 위치로 돌아온다.
- 원본이 없는 마이그레이션 기록에는 링크 텍스트 자체를 표시하지 않는다.

완료 조건은 마지막 경기·대표 경기·뉴스·시즌 카드 왕복 이동과 잘못된 ID의 안전한 실패다.

---

## 12. P1 — 은퇴 전용 오디오

### 12.1 기존 기반 확장

`SoundManager`는 BGM 두 소스 크로스페이드만 지원하고 `BgmSituation`은 Lobby와 MatchPlay뿐이다.

다음 항목을 추가한다.

- `BgmSituation.RetirementRecap`
- 은퇴 전용 `BgmPlaylistDefinition`
- UI 효과음용 별도 mixer group과 짧은 AudioSource pool
- `RetirementRecapAudioDefinition` SO

효과음 cue는 다음처럼 의미 단위로 정의한다.

```text
LockerOpen
TimelineAdvance
MilestoneReveal
CareerStatCount
SignatureDraw
LockerClose
ScoreboardPowerOff
```

`UI_Popup_RetirementRecap`은 cue ID만 요청하고 AudioClip을 직접 소유하지 않는다.
회고가 끝나거나 새 커리어로 이동하면 이전 상황의 BGM을 복원한다.

### 12.2 연출 규칙

- Count-up은 숫자 한 자리마다 재생하지 않고 일정 간격으로 제한한다.
- 전체 건너뛰기 시 진행 중 cue를 중지하고 종료 cue만 한 번 재생한다.
- BGM이 없어도 회고 진행 시간과 상태 전환은 같아야 한다.
- 오디오는 시뮬레이션 Seed나 결과에 영향을 주지 않는다.

PlayMode 테스트는 상황 전환, 중복 BGM 시작 방지, skip 시 AudioSource 정리, Popup 파괴 후 cue 잔존 0건을 확인한다.

---

## 13. P2 — 최종 커리어 카드 이미지

`CareerCardExporter`가 전용 Canvas를 `RenderTexture`로 렌더링해 PNG를 만든다.

- 원본 데이터는 `RetirementRecapSnapshot`이며 화면을 캡처해 사실을 재해석하지 않는다.
- 16:9 회고 화면 캡처와 별개로 고정 비율 카드 레이아웃을 사용한다.
- 파일명은 선수 이름이 아니라 `ArchiveId`를 사용한다.
- 이미지 생성 실패가 은퇴 스냅샷 저장을 막지는 않는다. 기본 카드로 재생성 가능해야 한다.
- 외부 공유 기능은 MVP 범위가 아니며 로컬 기록관 썸네일에만 사용한다.

완료 조건은 서로 다른 해상도와 한국어 이름 길이, 투수/타자 카드, 재생성 후 동일 내용 검증이다.

---

## 14. P2 — 동료·감독·팬 회고 문구

관계 지표 숫자만으로 특정 인물이 말했다고 만들면 허위 서술이 된다.
발화자를 표시하려면 당시 인물과 관계 사건을 먼저 저장해야 한다.

```csharp
public sealed class CareerRelationshipMemory
{
    public string MemoryId;
    public int SeasonId;
    public int SpeakerId;
    public RelationshipSpeakerType SpeakerType;
    public string SourceEventId;
    public string QuoteTemplateKey;
    public IReadOnlyList<MemoryStatValue> Facts;
}
```

- 감독은 해당 시즌 실제 `ManagerId`가 있을 때만 등장한다.
- 동료는 같은 시점 로스터에 있었고 SourceEvent에 함께 참여한 선수만 고른다.
- 팬 문구는 특정 인물을 가장하지 않고 집계된 FanResponse 구간을 사용한다.
- 템플릿은 저장된 사실 범위를 넘는 감정·약속·갈등을 만들어내지 않는다.
- 한 회고에서 인용문은 최대 2개로 제한해 선수 본인의 기록을 가리지 않는다.

감독·동료 identity 시스템이 생기기 전에는 이 범위를 구현하지 않는다.

---

## 15. 은퇴 스냅샷 확장 계약

위 기반이 구현되면 `RetirementRecapSnapshot`에 라이브 상태 참조가 아니라 다음 고정 값을 추가한다.

```csharp
public IReadOnlyList<PitchUsageSnapshot> PitchUsage;
public CareerAchievementSnapshot Achievements;
public IReadOnlyList<CareerRoleAssignmentSnapshot> RoleHistory;
public IReadOnlyList<CareerRankSnapshot> Rankings;
public IReadOnlyList<ArchivedMatchSnapshot> ReferencedMatches;
public IReadOnlyList<ArchivedNewsSnapshot> ReferencedNews;
```

- 경기와 뉴스는 Featured Memory, 마지막 경기, 최종 카드가 실제로 참조하는 항목만 복사한다.
- 월드 전체 통계나 전체 뉴스 저장소를 은퇴 스냅샷 안에 중복 저장하지 않는다.
- `SnapshotVersion`은 `SaveVersion`, `ArchiveVersion`과 독립적으로 올린다.
- Snapshot v1 기록관에는 새 통산 순위나 구종 효율을 소급 생성하지 않고 해당 블록을 숨긴다.
- Snapshot 생성 뒤에는 라이브 `WorldState`, `CareerNewsState`, `CareerMatchArchiveState`를 다시 읽지 않는다.

---

## 16. 구현 순서

### 단계 A — 사실 생산자 완성

1. `CareerMatchArchiveState`와 특수·연속 기록 누적
2. `CareerRoleAssignmentRecord`와 감독 AI 역할 변화 사건
3. News SourceEvent와 Memory의 연결
4. `CareerInjuryEpisode`와 실제 복귀 경기 연결

이 단계가 끝나면 대표 순간 카드가 원본 경기·뉴스·역할 변화로 설명 가능해진다.

### 단계 B — 구종과 통산 비교 기반

5. 모든 투수의 영속 repertoire와 `PitchSelectionSimulator`
6. 구종 효과를 PlateAppearance 모델에 연결하고 10,000경기 밸런스 검증
7. `WorldCareerStatisticsState`와 리그·구단·월드 통산 순위

이 단계가 끝나면 투수 스타일과 유산 화면의 핵심 빈칸이 해소된다.

### 단계 C — 영구 보관과 탐색

8. 명시적 Save DTO, 원자적 `CareerSaveRepository`, 마이그레이션 fixture
9. 별도 `CareerArchiveRepository`와 다중 커리어 기록관
10. 경기·뉴스·시즌 Deep Link
11. 커리어 카드 PNG 생성

이 단계가 끝나면 앱을 종료한 뒤에도 기록관을 탐색할 수 있다.

### 단계 D — 감정 연출 마감

12. 은퇴 BGM situation과 cue 기반 효과음
13. PlayMode 해상도·입력·Tween·오디오 검증
14. 감독·동료 identity 기반이 생긴 뒤 사실 기반 회고 문구 연결

---

## 17. 통합 테스트 매트릭스

| 테스트 | 핵심 검증 |
| --- | --- |
| 결정론 | 같은 Seed의 구종·이벤트·Memory·대표 순간·순위가 동일 |
| 경기 정합성 | Match archive, BoxScore, 시즌 기록, 월드 통산 기록 합계 일치 |
| 참조 무결성 | Featured Memory의 MatchId/ArticleId/SeasonId가 모두 해석 가능 |
| 역할 이력 | 일회성 선발과 감독의 주전 배정을 구분 |
| 부상 이력 | 부상·치료·회복·실제 복귀가 같은 Episode로 연결 |
| 순위 | 동률·최소 표본·이적 split·은퇴 선수 포함 결과가 brute-force와 일치 |
| 저장 왕복 | 활성 커리어와 은퇴 archive의 save/load 결과 동일 |
| 마이그레이션 | 없는 과거 데이터를 꾸며내지 않고 `기록 없음` 유지 |
| 용량 | 20시즌 경기 archive, 뉴스, 월드 통산 기록이 목표 크기 이내 |
| UI 이동 | 기록관→경기/뉴스/시즌→기록관 복귀 상태 유지 |
| PlayMode | 16:9/16:10, 키보드·패드 포커스, 1초 skip, Tween/Audio 잔존 0건 |

구종 효과나 확률 계수를 변경한 빌드는 반드시 대량 시뮬레이션 결과를 함께 남긴다.

---

## 18. 하지 않을 것

- 과거 경기 영상이나 모든 경기의 전체 이벤트 스트림을 무제한 저장하지 않는다.
- 저장되지 않은 구종, 타구 속도, 감독 발언, 동료 감정을 은퇴 시점에 생성하지 않는다.
- 낮은 통산 순위를 억지로 노출해 커리어를 실패 등급처럼 만들지 않는다.
- 은퇴 회고를 위해 Core/Simulation에서 `UnityEngine`, `AudioClip`, Sprite를 참조하지 않는다.
- 기록관 구현을 감독 모드나 구단 운영 기능으로 확장하지 않는다.
- 세이브 구현 전까지 현재 세션 기록관을 영구 저장이라고 부르지 않는다.

---

## 19. 최종 완료 정의

은퇴 회고 기반 시스템은 다음을 모두 만족해야 완료다.

1. 화면에 표시된 모든 경기·선택·순위·문장이 저장된 Source Fact로 추적된다.
2. 대표 순간에서 당시 경기 결과, 기사, 시즌 기록으로 실제 이동할 수 있다.
3. 투수의 구종 사용과 효율이 실제 시뮬레이션 결과에서 집계된다.
4. 리그·구단 통산 순위가 모든 현역·은퇴 선수의 누적 기록을 기준으로 계산된다.
5. 프로세스를 종료하고 다시 실행해도 활성 커리어와 은퇴 기록관이 동일하게 복원된다.
6. 같은 Seed와 같은 입력으로 회고 스냅샷까지 동일한 결과가 나온다.
7. Unity PlayMode 시각·입력·오디오 검증과 관련 EditMode 테스트가 모두 통과한다.

이 완료 정의를 통과하기 전에는 비활성 버튼이나 `기록 없음`을 임의의 값으로 채우지 않는다.
