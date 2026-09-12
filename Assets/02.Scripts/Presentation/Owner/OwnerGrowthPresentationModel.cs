using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Growth;

namespace Baseball.Presentation.Owner
{
    /// <summary>유학 비용·기간·상한을 반영한 성장 결과와 차단 사유다.</summary>
    public sealed class OwnerStudyOption
    {
        public CardStudyProgramDefinition Program { get; }
        public string RewardText { get; }
        public string UnlockText { get; }
        public string BlockedReason { get; }
        public bool IsUnlocked { get; }
        public bool CanStart => IsUnlocked && BlockedReason.Length == 0;
        public string CostText => Program.MoneyCost > 0 ? Program.MoneyCost.ToString("N0") + " PT" : "육성 포인트 " + Program.DevelopmentPointCost.ToString("N0");

        public OwnerStudyOption(
            CardStudyProgramDefinition program,
            string rewardText,
            string blockedReason,
            bool isUnlocked = true,
            string unlockText = "기본 해금")
        {
            Program = program;
            RewardText = rewardText;
            BlockedReason = blockedReason ?? string.Empty;
            IsUnlocked = isUnlocked;
            UnlockText = unlockText ?? string.Empty;
        }
    }

    /// <summary>실제 보유 카드의 장착 목록과 카드별 유학 선택지를 복사한다.</summary>
    public sealed class OwnerGrowthCardSnapshot
    {
        public OwnerCollectionCardSnapshot Card { get; }
        private readonly Lazy<OwnerCollectionCardSnapshot> _detailCard;
        private readonly Lazy<OwnerStudyOption[]> _studies;
        public OwnerCollectionCardSnapshot DetailCard => _detailCard.Value;
        public PlacedSkillBlock[] Placements { get; }
        public OwnerStudyOption[] Studies => _studies.Value;

        public OwnerGrowthCardSnapshot(OwnerCollectionCardSnapshot card,
            IReadOnlyList<PlacedSkillBlock> placements, IReadOnlyList<OwnerStudyOption> studies)
        {
            Card = card;
            Placements = OwnerPowerUpSnapshotCopy.Copy(placements);
            var copiedStudies = OwnerPowerUpSnapshotCopy.Copy(studies);
            _studies = new Lazy<OwnerStudyOption[]>(() => copiedStudies);
            _detailCard = new Lazy<OwnerCollectionCardSnapshot>(() => card);
        }

        /// <summary>선수 목록은 요약만 읽고 선택한 선수의 상세·유학 과정만 한 번 조회한다.</summary>
        public OwnerGrowthCardSnapshot(OwnerCollectionCardSnapshot card,
            IReadOnlyList<PlacedSkillBlock> placements, Func<OwnerStudyOption[]> studies,
            Func<OwnerCollectionCardSnapshot> detailCard)
        {
            Card = card ?? throw new ArgumentNullException(nameof(card));
            Placements = OwnerPowerUpSnapshotCopy.Copy(placements);
            _studies = new Lazy<OwnerStudyOption[]>(studies);
            _detailCard = new Lazy<OwnerCollectionCardSnapshot>(detailCard);
        }
    }

    /// <summary>스킬 배치와 유학 화면이 공유하는 런타임 조회 결과다.</summary>
    public sealed class OwnerGrowthSnapshot
    {
        public OwnerGrowthCardSnapshot[] Cards { get; }
        public SkillBlockInstance[] Inventory { get; }
        public SkillBlockDefinition[] Definitions { get; }
        public SkillBoardDefinition Board { get; }
        public int DevelopmentPoints { get; }
        public int StudyCount { get; }
        public int StudyCapacity { get; }
        public OwnerSchedulePermission SkillPermission { get; }
        public OwnerSeasonPhase SeasonPhase { get; }
        public int OffseasonCompletedWeeks { get; }
        public OwnerOffseasonPresentationModel Offseason { get; }

        public OwnerGrowthSnapshot(IReadOnlyList<OwnerGrowthCardSnapshot> cards,
            IReadOnlyList<SkillBlockInstance> inventory, SkillBlockDefinition[] definitions,
            SkillBoardDefinition board, int developmentPoints, int studyCount, int studyCapacity,
            OwnerSchedulePermission? skillPermission = null,
            OwnerSeasonPhase seasonPhase = OwnerSeasonPhase.RegularSeason, int offseasonCompletedWeeks = 0,
            OwnerOffseasonPresentationModel offseason = null)
        {
            Cards = OwnerPowerUpSnapshotCopy.Copy(cards);
            Inventory = OwnerPowerUpSnapshotCopy.Copy(inventory);
            Definitions = OwnerPowerUpSnapshotCopy.Copy(definitions);
            Board = board;
            DevelopmentPoints = developmentPoints;
            StudyCount = studyCount;
            StudyCapacity = studyCapacity;
            SkillPermission = skillPermission ?? new OwnerSchedulePermission(false, "시즌 일정을 확인해 주세요.");
            SeasonPhase = seasonPhase;
            OffseasonCompletedWeeks = offseasonCompletedWeeks;
            Offseason = offseason ?? new OwnerOffseasonPresentationModel(seasonPhase, offseasonCompletedWeeks, null);
        }

        /// <summary>다른 카드의 장착 상태까지 포함해 공유 블록의 소유자를 조회한다.</summary>
        public string GetEquippedCardId(int instanceId)
        {
            foreach (OwnerGrowthCardSnapshot card in Cards)
                foreach (PlacedSkillBlock placement in card.Placements)
                    if (placement.Instance.InstanceId == instanceId) return card.Card.CardId;
            return string.Empty;
        }

        /// <summary>실제 Simulation 서비스로 복사본을 구성해 경계·회전·겹침을 검증한다.</summary>
        public SkillBoardState CreateBoardState(OwnerGrowthCardSnapshot card)
        {
            var state = new SkillBoardState(Board.BoardDefinitionId);
            foreach (SkillBlockInstance block in Inventory) state.AddOwnedBlock(block);
            var service = new SkillBoardService(Board, Definitions);
            foreach (PlacedSkillBlock placement in card.Placements)
                service.PlaceBlock(state, placement.Instance.InstanceId,
                    placement.OriginX, placement.OriginY, placement.RotationQuarterTurns);
            return state;
        }
    }

    /// <summary>성장 화면의 보유 선수 목록을 선수단 장착 상태로 좁히는 필터다.</summary>
    public enum OwnerGrowthRosterFilter
    {
        All,
        ActiveRoster,
        Reserve
    }

    /// <summary>선수 유형·검색어·선수단 상태를 적용하고 등록 카드를 우선 정렬한다.</summary>
    public static class OwnerGrowthRosterPresentationBuilder
    {
        public static List<OwnerGrowthCardSnapshot> Build(
            OwnerGrowthSnapshot snapshot,
            bool isPitcher,
            OwnerGrowthRosterFilter filter,
            string query = null,
            bool sortByCost = false)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            string normalizedQuery = query?.Trim() ?? string.Empty;
            var activeRosterCards = new List<OwnerGrowthCardSnapshot>();
            var reserveCards = new List<OwnerGrowthCardSnapshot>();
            foreach (OwnerGrowthCardSnapshot candidate in snapshot.Cards)
            {
                bool candidateIsPitcher = candidate.Card.Position == PlayerPosition.StartingPitcher ||
                                          candidate.Card.Position == PlayerPosition.ReliefPitcher;
                if (candidateIsPitcher != isPitcher || !MatchesFilter(candidate, filter) ||
                    !MatchesQuery(candidate, normalizedQuery))
                    continue;
                if (candidate.Card.IsActiveRoster) activeRosterCards.Add(candidate);
                else reserveCards.Add(candidate);
            }

            var cards = new List<OwnerGrowthCardSnapshot>(activeRosterCards.Count + reserveCards.Count);
            cards.AddRange(activeRosterCards);
            cards.AddRange(reserveCards);
            if (sortByCost)
            {
                // 같은 코스트는 기존 등록 우선·보유 순서를 유지한다.
                var order = new Dictionary<OwnerGrowthCardSnapshot, int>();
                for (int index = 0; index < cards.Count; index++) order[cards[index]] = index;
                cards.Sort((left, right) =>
                {
                    int cost = right.Card.Cost.CompareTo(left.Card.Cost);
                    return cost != 0 ? cost : order[left].CompareTo(order[right]);
                });
            }
            return cards;
        }

        public static int Count(OwnerGrowthSnapshot snapshot, bool isPitcher, OwnerGrowthRosterFilter filter)
        {
            if (snapshot == null) return 0;
            int count = 0;
            foreach (OwnerGrowthCardSnapshot candidate in snapshot.Cards)
            {
                bool candidateIsPitcher = candidate.Card.Position == PlayerPosition.StartingPitcher ||
                                          candidate.Card.Position == PlayerPosition.ReliefPitcher;
                if (candidateIsPitcher == isPitcher && MatchesFilter(candidate, filter))
                    count++;
            }
            return count;
        }

        private static bool MatchesFilter(OwnerGrowthCardSnapshot card, OwnerGrowthRosterFilter filter)
        {
            bool isActiveRoster = card.Card.IsActiveRoster;
            return filter == OwnerGrowthRosterFilter.All ||
                   (filter == OwnerGrowthRosterFilter.ActiveRoster && isActiveRoster) ||
                   (filter == OwnerGrowthRosterFilter.Reserve && !isActiveRoster);
        }

        private static bool MatchesQuery(OwnerGrowthCardSnapshot card, string query)
        {
            if (query.Length == 0) return true;
            string role = OwnerCollectionPresentationBuilder.FormatPlayerRole(
                card.Card.Position,
                card.Card.PitcherRole,
                card.Card.IsPositionEvidenceMissing);
            return Contains(card.Card.DisplayName, query) ||
                   Contains(role, query) ||
                   card.Card.OriginYear.ToString().Contains(query);
        }

        private static bool Contains(string value, string query) =>
            !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

    }

    /// <summary>구단주 성장 밸런스와 저장 상태에서 표시 데이터를 구성한다.</summary>
    public static class OwnerGrowthPresentationBuilder
    {
        /// <summary>실제 보유 카드와 밸런스 정의에서 두 성장 화면의 조회 결과를 만든다.</summary>
        public static OwnerGrowthSnapshot Build(OwnerModeManager manager, OwnerCollectionSnapshot collection,
            Func<string, OwnerCollectionCardSnapshot> detailResolver = null)
        {
            var runtime = manager.Runtime;
            int capacity = manager.Balance.OwnerCardGrowth.GetStudyCapacity(
                runtime.ManagerMode.ClubOperation.GetFacility(FacilityType.TrainingCenter).Level);
            var cards = new List<OwnerGrowthCardSnapshot>();
            var ownedCards = new Dictionary<string, OwnedPlayerCardState>(StringComparer.Ordinal);
            foreach (OwnedPlayerCardState owned in runtime.OwnedCards) ownedCards.Add(owned.CardId, owned);
            foreach (OwnerCollectionCardSnapshot card in collection.Cards)
            {
                if (!ownedCards.TryGetValue(card.CardId, out OwnedPlayerCardState owned) ||
                    !runtime.WorldCardCatalog.TryGetCard(card.CardId, out PlayerCardDefinition definition)) continue;
                PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(definition);
                cards.Add(new OwnerGrowthCardSnapshot(card, owned.SkillBoard.Placements,
                    () => CreateStudyOptions(manager, card, owned, season, capacity),
                    () => detailResolver == null ? card : detailResolver(card.CardId)));
            }
            return new OwnerGrowthSnapshot(cards, runtime.PlayerGrowth.Inventory.Blocks,
                manager.Balance.Growth.SkillBlocks, manager.Balance.Growth.SkillBoard,
                runtime.Economy.DevelopmentPoints, runtime.PlayerGrowth.StudyProjects.Count, capacity,
                OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.SkillBlock),
                OwnerScheduleGateService.GetPhase(runtime), runtime.PlayerGrowth.Offseason.CompletedWeeks,
                OwnerOffseasonPresentationModel.Build(manager));
        }

        private static OwnerStudyOption[] CreateStudyOptions(OwnerModeManager manager,
            OwnerCollectionCardSnapshot card, OwnedPlayerCardState owned, PlayerSeasonDefinition season, int capacity)
        {
            var runtime = manager.Runtime;
            OwnerCardStudyUnlockProgress unlockProgress = OwnerCardStudyUnlockEvaluator.Evaluate(runtime);
            AbilityRatings baseAttributes = season.CreateBaseAttributes();
            AbilityRatings ceiling = season.CreateTrainingCeiling();
            var studies = new List<OwnerStudyOption>();
            foreach (CardStudyProgramDefinition program in manager.Balance.OwnerCardGrowth.StudyPrograms)
            {
                if (program.PlayerType != season.PlayerType) continue;
                var rewards = new StringBuilder();
                int totalGain = 0;
                foreach (AbilityChange reward in program.Rewards)
                {
                    int current = baseAttributes.Get(reward.Ability) + owned.Training.GetBonus(reward.Ability);
                    int gain = Math.Min(reward.Amount, Math.Max(0, ceiling.Get(reward.Ability) - current));
                    totalGain += gain;
                    rewards.Append(CareerSharedSnapshotFormatters.FormatAbility(reward.Ability))
                        .Append("  +").Append(gain).AppendLine();
                }
                bool isUnlocked = unlockProgress.IsUnlocked(program.UnlockRequirement);
                string unlockText = FormatUnlockText(program.UnlockRequirement);
                OwnerSchedulePermission permission = OwnerScheduleGateService.Evaluate(
                    runtime, OwnerGrowthAction.OverseasTraining, program.DurationWeeks);
                string reason = !permission.IsAllowed ? permission.Reason
                    : !isUnlocked ? FormatUnlockBlockedReason(program.UnlockRequirement)
                    : card.IsActiveRoster && !OwnerScheduleGateService.CanStudyWhileRegistered(runtime) ? "1군 등록 선수입니다. 선수단에서 등록을 해제한 뒤 신청하세요."
                    : owned.LastStudySeason == runtime.ManagerMode.LiveSeason.SeasonNumber ? "이번 시즌 유학을 이미 사용했습니다."
                    : runtime.PlayerGrowth.StudyProjects.Count >= capacity ? "유학 정원이 가득 찼습니다. 훈련 시설과 복귀 일정을 확인하세요."
                    : totalGain == 0 ? "이 과정의 성장 상한에 도달했습니다."
                    : runtime.Economy.DevelopmentPoints < program.DevelopmentPointCost || runtime.Economy.Money < program.MoneyCost ? "유학에 필요한 자원이 부족합니다."
                    : string.Empty;
                studies.Add(new OwnerStudyOption(
                    program,
                    rewards.ToString().TrimEnd() + (program.GreatSuccessBonus > 0
                        ? "\n대성공 " + (program.GreatSuccessProbability * 100).ToString("0") + "% · 주 능력 추가 +" + program.GreatSuccessBonus : ""),
                    reason,
                    isUnlocked,
                    unlockText));
            }
            return studies.ToArray();
        }

        private static string FormatUnlockText(CardStudyUnlockRequirement requirement) => requirement.Kind switch
        {
            CardStudyUnlockKind.ReachLeagueGrade =>
                $"해금 조건  {OwnerLeagueDisplayNameFormatter.FormatFull(requirement.RequiredLeagueGrade)} 진출",
            CardStudyUnlockKind.WinPostseason =>
                $"해금 조건  포스트시즌 우승 {requirement.RequiredChampionships}회",
            _ => "해금 조건  기본 개방"
        };

        private static string FormatUnlockBlockedReason(CardStudyUnlockRequirement requirement) => requirement.Kind switch
        {
            CardStudyUnlockKind.ReachLeagueGrade =>
                $"잠김 · {OwnerLeagueDisplayNameFormatter.FormatFull(requirement.RequiredLeagueGrade)}에 진출하면 이용할 수 있습니다.",
            CardStudyUnlockKind.WinPostseason =>
                $"잠김 · 포스트시즌 우승 {requirement.RequiredChampionships}회를 달성하면 이용할 수 있습니다.",
            _ => string.Empty
        };
    }
}
