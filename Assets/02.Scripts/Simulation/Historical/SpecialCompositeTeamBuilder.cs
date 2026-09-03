using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Historical
{
    /// <summary>Award 확정 뒤 동일 OriginYear 선수풀에서 중복 없는 특수 합성팀 세 종류를 만든다.</summary>
    public sealed class SpecialCompositeTeamBuilder
    {
        private readonly AwardScoringPolicy _scoringPolicy;
        private readonly ActiveRosterValidator _rosterValidator;

        public SpecialCompositeTeamBuilder(
            AwardScoringPolicy scoringPolicy,
            ActiveRosterValidator rosterValidator = null)
        {
            _scoringPolicy = scoringPolicy ?? throw new ArgumentNullException(nameof(scoringPolicy));
            _rosterValidator = rosterValidator ?? new ActiveRosterValidator();
        }

        public SpecialCompositeTeamSet Build(
            int originYear,
            IReadOnlyList<PlayerSeasonDefinition> allPlayerSeasons,
            WorldHistorySnapshot history,
            WorldCardCatalog cardCatalog,
            IRandomSource random)
        {
            if (originYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(originYear));
            if (allPlayerSeasons == null)
                throw new ArgumentNullException(nameof(allPlayerSeasons));
            if (history == null)
                throw new ArgumentNullException(nameof(history));
            if (cardCatalog == null)
                throw new ArgumentNullException(nameof(cardCatalog));
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            List<CompositeCandidate> pool = CreatePool(originYear, allPlayerSeasons, history, random);
            var allStarAwardIds = GetAwardIds(history.Awards, originYear, WorldAwardType.AllStar);
            var goldenGloveAwardIds = GetAwardIds(history.Awards, originYear, WorldAwardType.GoldenGlove);
            if (allStarAwardIds.Count == 0 || goldenGloveAwardIds.Count == 0)
                throw new InvalidOperationException("All-Star와 Golden Glove Award 확정 후에만 특수 합성팀을 만들 수 있습니다.");
            ValidateAwardPool(pool, allStarAwardIds, WorldAwardType.AllStar);
            ValidateAwardPool(pool, goldenGloveAwardIds, WorldAwardType.GoldenGlove);

            var globallyAssigned = new HashSet<string>(StringComparer.Ordinal);
            var teams = new SpecialCompositeTeamDefinition[3];
            teams[0] = BuildTeam(
                originYear,
                SpecialCompositeTeamType.AllStarComposite,
                pool,
                allStarAwardIds,
                globallyAssigned,
                cardCatalog,
                useRandomAsPrimaryRank: false);
            teams[1] = BuildTeam(
                originYear,
                SpecialCompositeTeamType.GoldenGloveComposite,
                pool,
                goldenGloveAwardIds,
                globallyAssigned,
                cardCatalog,
                useRandomAsPrimaryRank: false);
            teams[2] = BuildTeam(
                originYear,
                SpecialCompositeTeamType.YearSelectComposite,
                pool,
                EmptyAwardIds.Instance,
                globallyAssigned,
                cardCatalog,
                useRandomAsPrimaryRank: true);
            return new SpecialCompositeTeamSet(teams);
        }

        private SpecialCompositeTeamDefinition BuildTeam(
            int originYear,
            SpecialCompositeTeamType teamType,
            List<CompositeCandidate> pool,
            HashSet<string> awardIds,
            HashSet<string> globallyAssigned,
            WorldCardCatalog cardCatalog,
            bool useRandomAsPrimaryRank)
        {
            var selectedPlayerIds = new HashSet<string>(StringComparer.Ordinal);
            var selectedPersonIds = new HashSet<string>(StringComparer.Ordinal);
            var selected = new List<SelectedCompositePlayer>(ActiveRosterCompositionRule.ActiveRosterSize);
            int foreignCount = 0;

            ActiveRosterRole[] constrainedRoles =
            {
                ActiveRosterRole.StartingCatcher,
                ActiveRosterRole.StartingFirstBase,
                ActiveRosterRole.StartingSecondBase,
                ActiveRosterRole.StartingThirdBase,
                ActiveRosterRole.StartingShortstop,
                ActiveRosterRole.StartingLeftField,
                ActiveRosterRole.StartingCenterField,
                ActiveRosterRole.StartingRightField,
                ActiveRosterRole.StartingPitcher1,
                ActiveRosterRole.StartingPitcher2,
                ActiveRosterRole.StartingPitcher3,
                ActiveRosterRole.StartingPitcher4,
                ActiveRosterRole.StartingPitcher5,
                ActiveRosterRole.Bullpen1,
                ActiveRosterRole.Bullpen2,
                ActiveRosterRole.Bullpen3,
                ActiveRosterRole.Bullpen4,
                ActiveRosterRole.Setup,
                ActiveRosterRole.Closer,
                ActiveRosterRole.StartingDesignatedHitter
            };

            for (int index = 0; index < constrainedRoles.Length; index++)
            {
                SelectForRole(
                    constrainedRoles[index],
                    pool,
                    awardIds,
                    globallyAssigned,
                    selectedPlayerIds,
                    selectedPersonIds,
                    selected,
                    ref foreignCount,
                    useRandomAsPrimaryRank);
            }

            for (int index = 0; index < ActiveRosterCompositionRule.BenchHitterCount; index++)
            {
                SelectForRole(
                    ActiveRosterRole.BenchHitter,
                    pool,
                    awardIds,
                    globallyAssigned,
                    selectedPlayerIds,
                    selectedPersonIds,
                    selected,
                    ref foreignCount,
                    useRandomAsPrimaryRank);
            }

            selected.Sort(CompareSelectedRosterOrder);
            var outputEntries = new SpecialCompositeRosterEntry[selected.Count];
            var validationEntries = new ActiveRosterEntry[selected.Count];
            for (int index = 0; index < selected.Count; index++)
            {
                SelectedCompositePlayer item = selected[index];
                PlayerSeasonDefinition player = item.Candidate.Player;
                string cardId = ResolveCardId(teamType, player.PlayerSeasonId, cardCatalog);
                outputEntries[index] = new SpecialCompositeRosterEntry(player.PlayerSeasonId, cardId, item.Role);
                validationEntries[index] = new ActiveRosterEntry(
                    cardId,
                    player.PlayerSeasonId,
                    player.PlayerPersonId,
                    player.RegistrationType,
                    item.Role);
            }

            string teamSeasonKey = SpecialCompositeTeamDefinition.CreateStableTeamSeasonKey(originYear, teamType);
            RosterValidationResult validation = _rosterValidator.Validate(
                new CurrentRosterState(teamSeasonKey, validationEntries));
            if (!validation.IsValid)
                throw new InvalidOperationException("특수 합성팀이 공통 ActiveRoster 규칙을 만족하지 않습니다.");

            for (int index = 0; index < selected.Count; index++)
                globallyAssigned.Add(selected[index].Candidate.Player.PlayerSeasonId);
            return new SpecialCompositeTeamDefinition(teamType, originYear, outputEntries);
        }

        private static string ResolveCardId(
            SpecialCompositeTeamType teamType,
            string playerSeasonId,
            WorldCardCatalog cardCatalog)
        {
            PlayerCardEdition preferredEdition = teamType switch
            {
                SpecialCompositeTeamType.AllStarComposite => PlayerCardEdition.AllStar,
                SpecialCompositeTeamType.GoldenGloveComposite => PlayerCardEdition.GoldenGlove,
                SpecialCompositeTeamType.YearSelectComposite => PlayerCardEdition.Normal,
                _ => throw new ArgumentOutOfRangeException(nameof(teamType))
            };
            string preferredCardId = PlayerCardDefinition.CreateStableCardId(playerSeasonId, preferredEdition);
            if (cardCatalog.TryGetCard(preferredCardId, out PlayerCardDefinition preferredCard) &&
                string.Equals(preferredCard.PlayerSeasonId, playerSeasonId, StringComparison.Ordinal))
                return preferredCard.CardId;

            string normalCardId = PlayerCardDefinition.CreateStableCardId(
                playerSeasonId,
                PlayerCardEdition.Normal);
            if (cardCatalog.TryGetCard(normalCardId, out PlayerCardDefinition normalCard) &&
                string.Equals(normalCard.PlayerSeasonId, playerSeasonId, StringComparison.Ordinal))
                return normalCard.CardId;
            throw new InvalidOperationException($"WorldCardCatalog에 {playerSeasonId} Normal 카드가 없습니다.");
        }

        private void SelectForRole(
            ActiveRosterRole role,
            List<CompositeCandidate> pool,
            HashSet<string> awardIds,
            HashSet<string> globallyAssigned,
            HashSet<string> selectedPlayerIds,
            HashSet<string> selectedPersonIds,
            List<SelectedCompositePlayer> selected,
            ref int foreignCount,
            bool useRandomAsPrimaryRank)
        {
            CompositeCandidate best = null;
            for (int index = 0; index < pool.Count; index++)
            {
                CompositeCandidate candidate = pool[index];
                PlayerSeasonDefinition player = candidate.Player;
                if (globallyAssigned.Contains(player.PlayerSeasonId) ||
                    selectedPlayerIds.Contains(player.PlayerSeasonId) ||
                    selectedPersonIds.Contains(player.PlayerPersonId) ||
                    !CanFillRole(player, role) ||
                    (player.RegistrationType == RegistrationType.Foreign &&
                     foreignCount >= ActiveRosterCompositionRule.MaxForeignPlayers))
                    continue;

                if (best == null || CompareCandidate(
                        candidate,
                        best,
                        role,
                        awardIds,
                        useRandomAsPrimaryRank) < 0)
                    best = candidate;
            }

            if (best == null)
                throw new InvalidOperationException("세 특수 합성팀의 공통 25인 역할 구성을 채울 적격 선수가 부족합니다.");

            selectedPlayerIds.Add(best.Player.PlayerSeasonId);
            selectedPersonIds.Add(best.Player.PlayerPersonId);
            if (best.Player.RegistrationType == RegistrationType.Foreign)
                foreignCount++;
            selected.Add(new SelectedCompositePlayer(best, role));
        }

        private static int CompareCandidate(
            CompositeCandidate left,
            CompositeCandidate right,
            ActiveRosterRole role,
            HashSet<string> awardIds,
            bool useRandomAsPrimaryRank)
        {
            if (!useRandomAsPrimaryRank)
            {
                int awardOrder = awardIds.Contains(right.Player.PlayerSeasonId)
                    .CompareTo(awardIds.Contains(left.Player.PlayerSeasonId));
                if (awardOrder != 0) return awardOrder;
            }

            int fitOrder = GetRoleFit(right.Player, role).CompareTo(GetRoleFit(left.Player, role));
            if (fitOrder != 0) return fitOrder;

            if (useRandomAsPrimaryRank)
            {
                int randomOrder = left.RandomOrder.CompareTo(right.RandomOrder);
                if (randomOrder != 0) return randomOrder;
            }
            else
            {
                int performanceOrder = right.PerformanceScore.CompareTo(left.PerformanceScore);
                if (performanceOrder != 0) return performanceOrder;
                int randomOrder = left.RandomOrder.CompareTo(right.RandomOrder);
                if (randomOrder != 0) return randomOrder;
            }

            return string.CompareOrdinal(left.Player.PlayerSeasonId, right.Player.PlayerSeasonId);
        }

        private List<CompositeCandidate> CreatePool(
            int originYear,
            IReadOnlyList<PlayerSeasonDefinition> allPlayerSeasons,
            WorldHistorySnapshot history,
            IRandomSource random)
        {
            var players = new List<PlayerSeasonDefinition>();
            var playerSeasonIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < allPlayerSeasons.Count; index++)
            {
                PlayerSeasonDefinition player = allPlayerSeasons[index]
                    ?? throw new ArgumentException("null PlayerSeason이 있습니다.", nameof(allPlayerSeasons));
                if (player.OriginYear != originYear)
                    continue;
                if (!playerSeasonIds.Add(player.PlayerSeasonId))
                    throw new ArgumentException("같은 PlayerSeasonId가 선수풀에 중복되어 있습니다.", nameof(allPlayerSeasons));
                players.Add(player);
            }
            players.Sort((left, right) => string.CompareOrdinal(left.PlayerSeasonId, right.PlayerSeasonId));

            List<AwardCandidate> statistics = AwardCandidateAggregator.Aggregate(
                history.Statistics,
                AwardStatisticsScope.RegularSeason);
            var performanceByPlayer = new Dictionary<string, double>(StringComparer.Ordinal);
            for (int index = 0; index < statistics.Count; index++)
            {
                AwardCandidate candidate = statistics[index];
                if (candidate.SeasonYear != originYear)
                    continue;
                double score = AwardRanking.GetOverallScore(candidate, _scoringPolicy);
                if (!performanceByPlayer.TryGetValue(candidate.PlayerSeasonId, out double existing) || score > existing)
                    performanceByPlayer[candidate.PlayerSeasonId] = score;
            }

            var result = new List<CompositeCandidate>(players.Count);
            for (int index = 0; index < players.Count; index++)
            {
                PlayerSeasonDefinition player = players[index];
                performanceByPlayer.TryGetValue(player.PlayerSeasonId, out double performanceScore);
                result.Add(new CompositeCandidate(player, performanceScore, random.NextDouble()));
            }
            return result;
        }

        private static HashSet<string> GetAwardIds(
            WorldAwardRecord awards,
            int seasonYear,
            WorldAwardType awardType)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < awards.Entries.Count; index++)
            {
                WorldAwardEntry award = awards.Entries[index];
                if (award.SeasonYear == seasonYear && award.AwardType == awardType)
                    result.Add(award.PlayerSeasonId);
            }
            return result;
        }

        private static void ValidateAwardPool(
            List<CompositeCandidate> pool,
            HashSet<string> awardIds,
            WorldAwardType awardType)
        {
            var poolIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < pool.Count; index++)
                poolIds.Add(pool[index].Player.PlayerSeasonId);
            foreach (string awardId in awardIds)
            {
                if (!poolIds.Contains(awardId))
                    throw new InvalidOperationException(awardType + " 수상자가 같은 OriginYear 선수풀에 없습니다.");
            }
        }

        private static bool CanFillRole(PlayerSeasonDefinition player, ActiveRosterRole role)
        {
            return ActiveRosterCompositionRule.Standard.IsHitterRole(role)
                ? player.PlayerType == PlayerType.Batter
                : player.PlayerType == PlayerType.Pitcher;
        }

        private static int GetRoleFit(PlayerSeasonDefinition player, ActiveRosterRole role)
        {
            if (role == ActiveRosterRole.BenchHitter)
                return 1;
            if (ActiveRosterCompositionRule.Standard.IsStartingHitterRole(role))
            {
                PlayerPosition assigned = ActiveRosterCompositionRule.Standard.GetAssignedPosition(role);
                if (assigned == PlayerPosition.DesignatedHitter)
                    return player.Position == PlayerPosition.DesignatedHitter ? 2 : 1;
                return player.Position == assigned ? 2 : 0;
            }

            PitcherRole assignedRole = ActiveRosterCompositionRule.Standard.GetAssignedPitcherRole(role);
            if (player.PitcherRole == assignedRole)
                return 2;
            if (ActiveRosterCompositionRule.Standard.IsBullpenRole(role) &&
                player.Position == PlayerPosition.ReliefPitcher)
                return 1;
            return 0;
        }

        private static int CompareSelectedRosterOrder(
            SelectedCompositePlayer left,
            SelectedCompositePlayer right)
        {
            int roleOrder = left.Role.CompareTo(right.Role);
            return roleOrder != 0
                ? roleOrder
                : string.CompareOrdinal(left.Candidate.Player.PlayerSeasonId, right.Candidate.Player.PlayerSeasonId);
        }

        private sealed class CompositeCandidate
        {
            public CompositeCandidate(PlayerSeasonDefinition player, double performanceScore, double randomOrder)
            {
                Player = player;
                PerformanceScore = performanceScore;
                RandomOrder = randomOrder;
            }

            public PlayerSeasonDefinition Player { get; }
            public double PerformanceScore { get; }
            public double RandomOrder { get; }
        }

        private readonly struct SelectedCompositePlayer
        {
            public SelectedCompositePlayer(CompositeCandidate candidate, ActiveRosterRole role)
            {
                Candidate = candidate;
                Role = role;
            }

            public CompositeCandidate Candidate { get; }
            public ActiveRosterRole Role { get; }
        }

        private static class EmptyAwardIds
        {
            public static readonly HashSet<string> Instance = new HashSet<string>(StringComparer.Ordinal);
        }
    }
}
