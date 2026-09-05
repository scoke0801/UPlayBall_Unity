using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>공통 Roster·Position·Bullpen·League·Club DNA Resolver를 검증한다.</summary>
    public sealed class HistoricalRosterLeagueResolverTests
    {
        [Test]
        public void ActiveRosterValidator_정확한25인구성을허용한다()
        {
            CurrentRosterState roster = CreateValidRoster(3);

            RosterValidationResult result = new ActiveRosterValidator().Validate(roster);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Issues, Is.Empty);
        }

        [Test]
        public void ActiveRosterValidator_외국인4명과PlayerPerson중복을거부한다()
        {
            var entries = new List<ActiveRosterEntry>(CreateValidRoster(4).Entries);
            ActiveRosterEntry original = entries[1];
            entries[1] = new ActiveRosterEntry(
                original.CardId,
                original.PlayerSeasonId,
                entries[0].PlayerPersonId,
                original.RegistrationType,
                original.Role);

            RosterValidationResult result = new ActiveRosterValidator().Validate(
                new CurrentRosterState("COMETS_2011", entries));

            Assert.That(result.IsValid, Is.False);
            Assert.That(HasIssue(result, RosterValidationIssueCode.ForeignPlayerCount), Is.True);
            Assert.That(HasIssue(result, RosterValidationIssueCode.DuplicatePlayerPersonId), Is.True);
        }

        [Test]
        public void ActiveRosterValidator_같은범주수라도고정슬롯중복을거부한다()
        {
            var entries = new List<ActiveRosterEntry>(CreateValidRoster(0).Entries);
            ActiveRosterEntry rightFielder = entries[7];
            entries[7] = new ActiveRosterEntry(
                rightFielder.CardId,
                rightFielder.PlayerSeasonId,
                rightFielder.PlayerPersonId,
                rightFielder.RegistrationType,
                ActiveRosterRole.StartingCenterField);

            RosterValidationResult result = new ActiveRosterValidator().Validate(
                new CurrentRosterState("COMETS_2011", entries));

            Assert.That(result.IsValid, Is.False);
            Assert.That(HasIssue(result, RosterValidationIssueCode.FixedRoleCount), Is.True);
        }

        [Test]
        public void RosterCostResolver_벤치는제외하고주전교체와투수내역할교체를구분한다()
        {
            CurrentRosterState baseline = CreateValidRoster(0);
            int[] costs = new int[baseline.Entries.Count];
            for (int index = 0; index < costs.Length; index++)
                costs[index] = 5;
            costs[0] = 2;
            costs[9] = 8;
            WorldCardCatalog catalog = CreateCatalog(baseline, costs);
            var resolver = new RosterCostResolver();

            RosterCostBreakdown original = resolver.Resolve(baseline, catalog);
            RosterCostBreakdown starterBenchSwap = resolver.Resolve(SwapRoles(baseline, 0, 9), catalog);
            RosterCostBreakdown benchBenchSwap = resolver.Resolve(SwapRoles(baseline, 9, 10), catalog);
            RosterCostBreakdown pitcherRoleSwap = resolver.Resolve(SwapRoles(baseline, 14, 24), catalog);

            Assert.That(original.StartingHitterCost, Is.EqualTo(42));
            Assert.That(original.PitcherCost, Is.EqualTo(55));
            Assert.That(starterBenchSwap.TotalCost, Is.EqualTo(original.TotalCost + 6));
            Assert.That(benchBenchSwap.TotalCost, Is.EqualTo(original.TotalCost));
            Assert.That(pitcherRoleSwap.TotalCost, Is.EqualTo(original.TotalCost));
        }

        [Test]
        public void PositionAssignmentPenaltyResolver_비주포지션은허용하고경기비용을반환한다()
        {
            PositionAssignmentRule rule = CreatePositionRule();

            PositionAssignmentPenalty result = new PositionAssignmentPenaltyResolver().EvaluateHitter(
                PlayerPosition.FirstBase,
                PlayerPosition.Catcher,
                rule);

            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.IsOffPosition, Is.True);
            Assert.That(result.ConditionPenalty, Is.EqualTo(7));
            Assert.That(result.FieldingErrorProbabilityMultiplier, Is.EqualTo(1.8d));
        }

        [Test]
        public void PositionAssignmentPenaltyResolver_DH는모든야수에게무패널티다()
        {
            PositionAssignmentPenalty result = new PositionAssignmentPenaltyResolver().EvaluateHitter(
                PlayerPosition.Catcher,
                PlayerPosition.DesignatedHitter,
                CreatePositionRule());

            Assert.That(result.IsOffPosition, Is.False);
            Assert.That(result.ConditionPenalty, Is.Zero);
            Assert.That(result.FieldingErrorProbabilityMultiplier, Is.EqualTo(1d));
        }

        [Test]
        public void PositionAssignmentPenaltyResolver_투수역할불일치는허용하고컨디션비용을반환한다()
        {
            PositionAssignmentPenalty result = new PositionAssignmentPenaltyResolver().EvaluatePitcher(
                PitcherRole.Starter,
                PitcherRole.Closer,
                CreatePositionRule());

            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.IsOffPosition, Is.True);
            Assert.That(result.ConditionPenalty, Is.EqualTo(9));
            Assert.That(result.FieldingErrorProbabilityMultiplier, Is.EqualTo(1d));
        }

        [Test]
        public void PositionAssignmentPenaltyResolver_낮은역할신뢰도는불일치비용을완화한다()
        {
            PositionAssignmentRule rule = CreatePositionRule();

            PositionAssignmentPenalty result = new PositionAssignmentPenaltyResolver().EvaluatePitcher(
                PitcherRole.Starter,
                PitcherRole.MiddleRelief,
                PitcherRoleConfidence.Low,
                rule);

            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.IsOffPosition, Is.True);
            Assert.That(result.ConditionPenalty, Is.EqualTo(2));
        }

        [TestCase(PitcherRoleConfidence.Low, 2)]
        [TestCase(PitcherRoleConfidence.Medium, 6)]
        [TestCase(PitcherRoleConfidence.High, 9)]
        public void PitcherRoleMismatchPenaltyDefinition_신뢰도배율을가장가까운정수로환산한다(
            PitcherRoleConfidence confidence,
            int expectedPenalty)
        {
            var definition = new PitcherRoleMismatchPenaltyDefinition(9);

            Assert.That(definition.GetConditionPenalty(confidence), Is.EqualTo(expectedPenalty));
        }

        [Test]
        public void BullpenUsageResolver_접전에서는상위불펜을우선하고소진시Bullpen4를사용한다()
        {
            BullpenUsagePolicy policy = CreateBullpenPolicy();
            var candidates = new[]
            {
                new BullpenCandidateState("BP4", ActiveRosterRole.Bullpen4, 80, true),
                new BullpenCandidateState("BP3", ActiveRosterRole.Bullpen3, 30, true),
                new BullpenCandidateState("BP2", ActiveRosterRole.Bullpen2, 90, false),
                new BullpenCandidateState("BP1", ActiveRosterRole.Bullpen1, 40, true)
            };

            BullpenCandidateState? selected = new BullpenUsageResolver().SelectCandidate(
                policy,
                new BullpenSelectionContext(7, 1, 1.5d),
                candidates);

            Assert.That(selected.HasValue, Is.True);
            Assert.That(selected.Value.BullpenRole, Is.EqualTo(ActiveRosterRole.Bullpen4));
        }

        [Test]
        public void BullpenUsageResolver_큰열세에서는Policy에따라Bullpen4를먼저선택한다()
        {
            BullpenUsagePolicy policy = CreateBullpenPolicy();
            BullpenCandidateState[] candidates = CreateHealthyBullpenCandidates();

            BullpenCandidateState? selected = new BullpenUsageResolver().SelectCandidate(
                policy,
                new BullpenSelectionContext(6, -6, 0.4d),
                candidates);

            Assert.That(selected.HasValue, Is.True);
            Assert.That(selected.Value.BullpenRole, Is.EqualTo(ActiveRosterRole.Bullpen4));
        }

        [Test]
        public void BullpenUsageResolver_후보열거순서와무관하게같은선수를고른다()
        {
            BullpenUsagePolicy policy = CreateBullpenPolicy();
            var forward = new[]
            {
                new BullpenCandidateState("BP1_B", ActiveRosterRole.Bullpen1, 90, true),
                new BullpenCandidateState("BP1_A", ActiveRosterRole.Bullpen1, 90, true),
                new BullpenCandidateState("BP2", ActiveRosterRole.Bullpen2, 90, true),
                new BullpenCandidateState("BP3", ActiveRosterRole.Bullpen3, 90, true),
                new BullpenCandidateState("BP4", ActiveRosterRole.Bullpen4, 90, true)
            };
            var reverse = new[] { forward[4], forward[3], forward[2], forward[1], forward[0] };
            var context = new BullpenSelectionContext(8, 0, 2d);
            var resolver = new BullpenUsageResolver();

            BullpenCandidateState? first = resolver.SelectCandidate(policy, context, forward);
            BullpenCandidateState? second = resolver.SelectCandidate(policy, context, reverse);

            Assert.That(first.Value.PlayerSeasonId, Is.EqualTo("BP1_A"));
            Assert.That(second.Value.PlayerSeasonId, Is.EqualTo(first.Value.PlayerSeasonId));
        }

        [Test]
        public void LeaguePromotionResolver_승률기준으로인접등급만이동한다()
        {
            LeagueDefinition definition = CreateLeagueDefinition();
            var resolver = new LeaguePromotionResolver();

            Assert.That(
                resolver.ResolveNextGrade(LeagueGrade.Rookie, 60, 40, definition),
                Is.EqualTo(LeagueGrade.Minor));
            Assert.That(
                resolver.ResolveNextGrade(LeagueGrade.Major, 35, 65, definition),
                Is.EqualTo(LeagueGrade.Minor));
            Assert.That(
                resolver.ResolveNextGrade(LeagueGrade.Galaxy, 80, 20, definition),
                Is.EqualTo(LeagueGrade.Galaxy));
        }

        [Test]
        public void TeamSeasonClubStateResolver_모든축의시즌변화를최대5로제한한다()
        {
            var current = new TeamSeasonClubState("COMETS_2011", CreateDna(50d));
            var policy = new ClubDnaUpdatePolicy(0.6d, 0.25d, 0.15d, 5d);

            TeamSeasonClubState updated = new TeamSeasonClubStateResolver().ResolveNextSeason(
                current,
                CreateDna(100d),
                CreateDna(100d),
                policy);

            Assert.That(updated.Ratings.Contact, Is.EqualTo(55d));
            Assert.That(updated.Ratings.Experience, Is.EqualTo(55d));
            Assert.That(current.Ratings.Contact, Is.EqualTo(50d));
        }

        [Test]
        public void TeamSeasonClubStateResolver_같은Franchise의다른TeamSeason과상태를공유하지않는다()
        {
            var first = new TeamSeasonClubState("COMETS_2011", CreateDna(40d));
            var second = new TeamSeasonClubState("COMETS_2012", CreateDna(70d));
            var resolver = new TeamSeasonClubStateResolver();
            var policy = new ClubDnaUpdatePolicy(0.6d, 0.25d, 0.15d, 5d);

            TeamSeasonClubState updatedFirst = resolver.ResolveNextSeason(
                first,
                CreateDna(100d),
                CreateDna(100d),
                policy);

            Assert.That(updatedFirst.TeamSeasonKey, Is.EqualTo("COMETS_2011"));
            Assert.That(updatedFirst.Ratings.Contact, Is.EqualTo(45d));
            Assert.That(second.Ratings.Contact, Is.EqualTo(70d));
        }

        [Test]
        public void FranchiseIdentityResolver_입력순서와무관하게같은장기평균을만든다()
        {
            var older = new TeamSeasonClubState("COMETS_2011", CreateDna(40d));
            var newer = new TeamSeasonClubState("COMETS_2012", CreateDna(80d));
            var resolver = new FranchiseIdentityResolver();

            FranchiseIdentityProfile forward = resolver.Resolve("COMETS", new[] { older, newer });
            FranchiseIdentityProfile reverse = resolver.Resolve("COMETS", new[] { newer, older });

            Assert.That(forward.Ratings.Contact, Is.EqualTo(60d));
            Assert.That(reverse.Ratings.Contact, Is.EqualTo(forward.Ratings.Contact));
        }

        private static bool HasIssue(RosterValidationResult result, RosterValidationIssueCode code)
        {
            for (int index = 0; index < result.Issues.Count; index++)
                if (result.Issues[index].Code == code) return true;
            return false;
        }

        private static CurrentRosterState CreateValidRoster(int foreignCount)
        {
            ActiveRosterRole[] roles =
            {
                ActiveRosterRole.StartingCatcher,
                ActiveRosterRole.StartingFirstBase,
                ActiveRosterRole.StartingSecondBase,
                ActiveRosterRole.StartingThirdBase,
                ActiveRosterRole.StartingShortstop,
                ActiveRosterRole.StartingLeftField,
                ActiveRosterRole.StartingCenterField,
                ActiveRosterRole.StartingRightField,
                ActiveRosterRole.StartingDesignatedHitter,
                ActiveRosterRole.BenchHitter,
                ActiveRosterRole.BenchHitter,
                ActiveRosterRole.BenchHitter,
                ActiveRosterRole.BenchHitter,
                ActiveRosterRole.BenchHitter,
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
                ActiveRosterRole.Closer
            };
            var entries = new ActiveRosterEntry[roles.Length];
            for (int index = 0; index < roles.Length; index++)
            {
                entries[index] = new ActiveRosterEntry(
                    $"SEASON_{index:D2}:Normal",
                    $"SEASON_{index:D2}",
                    $"PERSON_{index:D2}",
                    index < foreignCount ? RegistrationType.Foreign : RegistrationType.Domestic,
                    roles[index]);
            }
            return new CurrentRosterState("COMETS_2011", entries);
        }

        private static CurrentRosterState SwapRoles(CurrentRosterState roster, int firstIndex, int secondIndex)
        {
            var entries = new ActiveRosterEntry[roster.Entries.Count];
            for (int index = 0; index < entries.Length; index++)
            {
                ActiveRosterEntry source = roster.Entries[index];
                ActiveRosterRole role = index == firstIndex
                    ? roster.Entries[secondIndex].Role
                    : index == secondIndex
                        ? roster.Entries[firstIndex].Role
                        : source.Role;
                entries[index] = new ActiveRosterEntry(
                    source.CardId,
                    source.PlayerSeasonId,
                    source.PlayerPersonId,
                    source.RegistrationType,
                    role);
            }
            return new CurrentRosterState(roster.TeamSeasonKey, entries);
        }

        private static WorldCardCatalog CreateCatalog(CurrentRosterState roster, IReadOnlyList<int> costs)
        {
            var seasons = new PlayerSeasonDefinition[roster.Entries.Count];
            var cards = new PlayerCardDefinition[roster.Entries.Count];
            var ratings = new AbilityRatings(50);
            var modifiers = new int[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                bool isPitcher = ActiveRosterCompositionRule.Standard.IsPitcherRole(entry.Role);
                seasons[index] = new PlayerSeasonDefinition(
                    entry.PlayerSeasonId,
                    entry.PlayerPersonId,
                    2011,
                    "COMETS",
                    roster.TeamSeasonKey,
                    isPitcher ? PlayerPosition.StartingPitcher : PlayerPosition.Catcher,
                    isPitcher ? PitcherRole.Starter : PitcherRole.MiddleRelief,
                    isPitcher ? PlayerType.Pitcher : PlayerType.Batter,
                    RegistrationType.Domestic,
                    ratings,
                    costs[index],
                    ratings);
                cards[index] = new PlayerCardDefinition(
                    entry.CardId,
                    entry.PlayerSeasonId,
                    PlayerCardEdition.Normal,
                    modifiers);
            }
            return new WorldCardCatalog(seasons, cards);
        }

        private static PositionAssignmentRule CreatePositionRule()
        {
            return new PositionAssignmentRule(
                new OffPositionPenaltyDefinition(7, 1.8d),
                new PitcherRoleMismatchPenaltyDefinition(9));
        }

        private static BullpenUsagePolicy CreateBullpenPolicy()
        {
            return new BullpenUsagePolicy(new[]
            {
                new BullpenUsageBand(
                    1, 20, -2, 2, 0.8d, 10d, 50,
                    new[]
                    {
                        ActiveRosterRole.Bullpen1,
                        ActiveRosterRole.Bullpen2,
                        ActiveRosterRole.Bullpen3,
                        ActiveRosterRole.Bullpen4
                    }),
                new BullpenUsageBand(
                    1, 20, -20, -3, 0d, 10d, 40,
                    new[]
                    {
                        ActiveRosterRole.Bullpen4,
                        ActiveRosterRole.Bullpen3,
                        ActiveRosterRole.Bullpen2,
                        ActiveRosterRole.Bullpen1
                    })
            });
        }

        private static BullpenCandidateState[] CreateHealthyBullpenCandidates()
        {
            return new[]
            {
                new BullpenCandidateState("BP1", ActiveRosterRole.Bullpen1, 90, true),
                new BullpenCandidateState("BP2", ActiveRosterRole.Bullpen2, 90, true),
                new BullpenCandidateState("BP3", ActiveRosterRole.Bullpen3, 90, true),
                new BullpenCandidateState("BP4", ActiveRosterRole.Bullpen4, 90, true)
            };
        }

        private static LeagueDefinition CreateLeagueDefinition()
        {
            LeagueGrade[] grades = (LeagueGrade[])Enum.GetValues(typeof(LeagueGrade));
            var rules = new LeagueGradeRule[grades.Length];
            for (int index = 0; index < grades.Length; index++)
            {
                double? promotion = grades[index] == LeagueGrade.Galaxy ? (double?)null : 0.6d;
                double? relegation = grades[index] == LeagueGrade.Rookie ? (double?)null : 0.4d;
                rules[index] = new LeagueGradeRule(grades[index], 100, promotion, relegation);
            }
            return new LeagueDefinition(rules);
        }

        private static ClubDnaRatings CreateDna(double value)
        {
            return new ClubDnaRatings(value, value, value, value, value, value, value, value);
        }
    }
}
