using System;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>Condition/Chemistry가 원본 능력치와 기존 경기 경로를 침범하지 않는지 검증한다.</summary>
    public sealed class ConditionChemistryContractTests
    {
        private const string TeamSeasonKey = "condition-team-2026";

        [Test]
        public void PresentationLevel_0부터100까지열단계경계를정확히매핑한다()
        {
            ConditionPresentationTable presentation = ConditionChemistryBalanceTable.CreateDefault().Presentation;

            for (int condition = 0; condition <= 100; condition++)
            {
                int expected = Math.Min(10, condition / 10 + 1);
                Assert.That(presentation.GetLevel(condition), Is.EqualTo(expected), $"Condition={condition}");
            }
            Assert.That(() => presentation.GetLevel(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => presentation.GetLevel(101), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void StoredCondition_원본과경기합성값을0부터100사이로제한한다()
        {
            var status = new TeamSeasonPlayerStatus("person-1", 50);
            status.ChangeCondition(int.MaxValue);
            Assert.That(status.StoredBaseCondition, Is.EqualTo(100));
            status.ChangeCondition(int.MinValue);
            Assert.That(status.StoredBaseCondition, Is.Zero);

            var maximum = new EffectiveMatchCondition(100, 10, 10, 10, 10);
            var minimum = new EffectiveMatchCondition(0, -10, -10, -10, -10);
            Assert.That(maximum.Value, Is.EqualTo(100));
            Assert.That(minimum.Value, Is.Zero);
            Assert.That(() => new TeamSeasonPlayerStatus("person-2", -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new EffectiveMatchCondition(101, 0, 0, 0, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void FamiliarityPair_순서를정규화하고TeamSeason별로격리하며Cap을지킨다()
        {
            var forward = new PlayerPersonPairKey("person-a", "person-b");
            var reverse = new PlayerPersonPairKey("person-b", "person-a");
            Assert.That(reverse, Is.EqualTo(forward));
            Assert.That(forward.FirstPlayerPersonId, Is.EqualTo("person-a"));
            Assert.That(forward.SecondPlayerPersonId, Is.EqualTo("person-b"));

            var firstTeam = new TeamChemistryFamiliarityState("team-a");
            var secondTeam = new TeamChemistryFamiliarityState("team-b");
            firstTeam.RecordLineupPair(forward, 60, 100);
            firstTeam.RecordLineupPair(reverse, 60, 100);
            firstTeam.RecordBatteryPair(reverse, 150, 100);

            Assert.That(firstTeam.GetLineupFamiliarity(forward), Is.EqualTo(100));
            Assert.That(firstTeam.GetBatteryFamiliarity(forward), Is.EqualTo(100));
            Assert.That(firstTeam.Entries.Count, Is.EqualTo(1));
            Assert.That(secondTeam.GetLineupFamiliarity(forward), Is.Zero);
            Assert.That(secondTeam.GetBatteryFamiliarity(forward), Is.Zero);
        }

        [Test]
        public void LineupResolver_정확히9명과TeamSeason격리를강제한다()
        {
            var resolver = new LineupChemistryResolver(ConditionChemistryBalanceTable.CreateDefault());
            LineupChemistryPlayer[] lineup = CreateChemistryLineup(CreateBalancedAttributes);
            var familiarity = new TeamChemistryFamiliarityState(TeamSeasonKey);

            Assert.That(() => resolver.Resolve(TeamSeasonKey, new LineupChemistryPlayer[8], familiarity),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => resolver.Resolve(TeamSeasonKey, new LineupChemistryPlayer[10], familiarity),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => resolver.Resolve("another-team", lineup, familiarity),
                Throws.TypeOf<ArgumentException>());

            lineup[8] = new LineupChemistryPlayer(lineup[0].PlayerPersonId, CreateBalancedAttributes(8));
            Assert.That(() => resolver.Resolve(TeamSeasonKey, lineup, familiarity),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void LineupChemistry_인접8개만평가하고9번과1번을연결하지않는다()
        {
            ConditionChemistryBalanceTable balance = ConditionChemistryBalanceTable.CreateDefault();
            var familiarity = new TeamChemistryFamiliarityState(TeamSeasonKey);
            familiarity.RecordLineupPair(new PlayerPersonPairKey("person-1", "person-9"), balance.FamiliarityCap, balance.FamiliarityCap);
            LineupChemistryResult result = new LineupChemistryResolver(balance).Resolve(
                TeamSeasonKey,
                CreateChemistryLineup(CreateBalancedAttributes),
                familiarity);

            Assert.That(result.Edges.Count, Is.EqualTo(8));
            for (int index = 0; index < result.Edges.Count; index++)
            {
                Assert.That(result.Edges[index].Pair,
                    Is.Not.EqualTo(new PlayerPersonPairKey("person-1", "person-9")));
                Assert.That(result.Edges[index].Familiarity, Is.Zero);
            }
            Assert.That(result.GetConditionModifier("person-1"), Is.Zero);
            Assert.That(result.GetConditionModifier("person-9"), Is.Zero);
        }

        [Test]
        public void LineupChemistry_최대최소를한단계로제한하고동일입력은동일하다()
        {
            ConditionChemistryBalanceTable balance = ConditionChemistryBalanceTable.CreateDefault();
            var positiveFamiliarity = new TeamChemistryFamiliarityState(TeamSeasonKey);
            for (int index = 1; index < 9; index++)
            {
                positiveFamiliarity.RecordLineupPair(
                    new PlayerPersonPairKey($"person-{index}", $"person-{index + 1}"),
                    balance.FamiliarityCap,
                    balance.FamiliarityCap);
            }

            LineupChemistryPlayer[] complementary = CreateChemistryLineup(
                index => index % 2 == 0 ? CreateTableSetterAttributes(index) : CreatePowerAttributes(index));
            var resolver = new LineupChemistryResolver(balance);
            LineupChemistryResult first = resolver.Resolve(TeamSeasonKey, complementary, positiveFamiliarity);
            LineupChemistryResult second = resolver.Resolve(TeamSeasonKey, complementary, positiveFamiliarity);
            for (int index = 0; index < first.Players.Count; index++)
            {
                Assert.That(first.Players[index].ConditionModifier, Is.EqualTo(balance.ConditionLevelStep));
                Assert.That(second.Players[index].ConditionModifier, Is.EqualTo(first.Players[index].ConditionModifier));
                Assert.That(second.Players[index].Score, Is.EqualTo(first.Players[index].Score));
            }

            LineupChemistryResult negative = resolver.Resolve(
                TeamSeasonKey,
                CreateChemistryLineup(CreatePowerAttributes),
                new TeamChemistryFamiliarityState(TeamSeasonKey));
            for (int index = 0; index < negative.Players.Count; index++)
                Assert.That(negative.Players[index].ConditionModifier, Is.EqualTo(-balance.ConditionLevelStep));
        }

        [Test]
        public void ChemistryResolvers_기존Batter와Pitcher능력치를변경하지않는다()
        {
            var batterAttributes = new BatterAttributes(88, 41, 76, 63, 72, 69);
            var pitcherAttributes = new PitcherAttributes(81, 79, 77, 75, 73, 71);
            var catcherAttributes = new BatterAttributes(52, 48, 44, 80, 91, 89);
            ConditionChemistryBalanceTable balance = ConditionChemistryBalanceTable.CreateDefault();

            new LineupChemistryResolver(balance).Resolve(
                TeamSeasonKey,
                CreateChemistryLineup(index => index == 0 ? batterAttributes : CreateBalancedAttributes(index)),
                new TeamChemistryFamiliarityState(TeamSeasonKey));
            new BatteryChemistryResolver(balance).Resolve(
                TeamSeasonKey,
                "pitcher",
                pitcherAttributes,
                "catcher",
                catcherAttributes,
                new TeamChemistryFamiliarityState(TeamSeasonKey));

            Assert.That(batterAttributes.Contact, Is.EqualTo(88));
            Assert.That(batterAttributes.Power, Is.EqualTo(41));
            Assert.That(batterAttributes.Speed, Is.EqualTo(76));
            Assert.That(pitcherAttributes.Stamina, Is.EqualTo(81));
            Assert.That(pitcherAttributes.Velocity, Is.EqualTo(79));
            Assert.That(pitcherAttributes.Mental, Is.EqualTo(71));
            Assert.That(catcherAttributes.Defense, Is.EqualTo(91));
            Assert.That(catcherAttributes.Mental, Is.EqualTo(89));
        }

        [Test]
        public void BatteryChemistry_최대최소와동일입력결정론을지킨다()
        {
            ConditionChemistryBalanceTable balance = ConditionChemistryBalanceTable.CreateDefault();
            var familiarity = new TeamChemistryFamiliarityState(TeamSeasonKey);
            familiarity.RecordBatteryPair(
                new PlayerPersonPairKey("pitcher", "catcher"),
                balance.FamiliarityCap,
                balance.FamiliarityCap);
            var resolver = new BatteryChemistryResolver(balance);
            var strongPitcher = new PitcherAttributes(80, 80, 80, 80, 80, 100);
            var strongCatcher = new BatterAttributes(50, 50, 50, 50, 100, 100);

            BatteryChemistryResult first = resolver.Resolve(
                TeamSeasonKey, "pitcher", strongPitcher, "catcher", strongCatcher, familiarity);
            BatteryChemistryResult second = resolver.Resolve(
                TeamSeasonKey, "pitcher", strongPitcher, "catcher", strongCatcher, familiarity);
            BatteryChemistryResult weak = resolver.Resolve(
                TeamSeasonKey,
                "weak-pitcher",
                new PitcherAttributes(50, 50, 50, 50, 50, 0),
                "weak-catcher",
                new BatterAttributes(50, 50, 50, 50, 0, 0),
                new TeamChemistryFamiliarityState(TeamSeasonKey));

            Assert.That(first.PitcherConditionModifier, Is.EqualTo(balance.ConditionLevelStep));
            Assert.That(weak.PitcherConditionModifier, Is.EqualTo(-balance.ConditionLevelStep));
            Assert.That(second.TotalScore, Is.EqualTo(first.TotalScore));
            Assert.That(second.PitcherConditionModifier, Is.EqualTo(first.PitcherConditionModifier));
            Assert.That(() => resolver.Resolve(
                    "another-team", "pitcher", strongPitcher, "catcher", strongCatcher, familiarity),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void MatchBatteryMatrix_투수와포수교체마다현재Pair로교체하며누적하지않는다()
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            MatchRosterSnapshot roster = CreateConditionRoster(includeConditions: true);
            var state = new DetailedTeamGameState(
                roster,
                new PitcherFatigueResolver(balance.Match),
                historicalConfiguration: null,
                conditionRatingResolver: new MatchConditionRatingResolver(balance.ConditionChemistry));
            Player starter = roster.StartingPitcher.Player;
            Player startingCatcher = roster.StartingLineup[0].Player;

            Assert.That(state.GetConditionRatingModifier(starter), Is.EqualTo(1));
            Assert.That(state.GetConditionRatingModifier(starter), Is.EqualTo(1), "반복 조회가 Battery 효과를 누적하면 안 된다.");
            Assert.That(state.GetConditionRatingModifier(startingCatcher), Is.Zero, "Battery 효과는 포수에게 적용하지 않는다.");

            state.SubstitutePositionPlayer(
                0,
                0,
                5,
                InningHalf.Top,
                SubstitutionType.DefensiveReplacement,
                DecisionReasonCode.DefensiveStrategy);
            Assert.That(state.GetConditionRatingModifier(starter), Is.EqualTo(-1),
                "포수 교체 뒤 기존 +10이 남지 않고 새 Pair -10으로 교체되어야 한다.");
            Assert.That(state.GetConditionRatingModifier(roster.Bench[0]), Is.Zero);

            state.ChangePitcher(1, 6, InningHalf.Top, PitcherChangeReason.Matchup, 0, 0);
            Assert.That(state.GetConditionRatingModifier(state.ActivePitcher), Is.EqualTo(1));
            Assert.That(state.GetConditionRatingModifier(state.ActivePitcher), Is.EqualTo(1),
                "투수 교체 뒤에도 Battery 효과는 조회마다 누적되지 않아야 한다.");
        }

        [Test]
        public void BatteryUsage_실제투수포수Pair의공동수비아웃만기록한다()
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            MatchRosterSnapshot roster = CreateConditionRoster(includeConditions: true);
            var state = new DetailedTeamGameState(
                roster,
                new PitcherFatigueResolver(balance.Match),
                historicalConfiguration: null,
                conditionRatingResolver: new MatchConditionRatingResolver(balance.ConditionChemistry));

            state.RecordDefensiveOut();
            state.RecordDefensiveOut();
            state.RecordDefensiveOut();
            state.SubstitutePositionPlayer(
                0,
                0,
                5,
                InningHalf.Top,
                SubstitutionType.DefensiveReplacement,
                DecisionReasonCode.DefensiveStrategy);
            state.RecordDefensiveOut();
            state.RecordDefensiveOut();
            state.ChangePitcher(1, 6, InningHalf.Top, PitcherChangeReason.Matchup, 0, 0);
            state.RecordDefensiveOut();

            BatteryUsageReport[] reports = state.BuildBatteryUsageReports();

            Assert.That(reports, Has.Length.EqualTo(3));
            Assert.That(reports[0].PitcherPlayerId, Is.EqualTo(roster.StartingPitcher.Player.PlayerId));
            Assert.That(reports[0].CatcherPlayerId, Is.EqualTo(roster.StartingLineup[0].Player.PlayerId));
            Assert.That(reports[0].DefensiveOuts, Is.EqualTo(3));
            Assert.That(reports[1].PitcherPlayerId, Is.EqualTo(roster.StartingPitcher.Player.PlayerId));
            Assert.That(reports[1].CatcherPlayerId, Is.EqualTo(roster.Bench[0].PlayerId));
            Assert.That(reports[1].DefensiveOuts, Is.EqualTo(2));
            Assert.That(reports[2].PitcherPlayerId, Is.EqualTo(roster.Bullpen[0].Player.PlayerId));
            Assert.That(reports[2].CatcherPlayerId, Is.EqualTo(roster.Bench[0].PlayerId));
            Assert.That(reports[2].DefensiveOuts, Is.EqualTo(1));

            var familiarity = new TeamChemistryFamiliarityState(TeamSeasonKey);
            var recorder = new ChemistryFamiliarityRecorder(balance.ConditionChemistry);
            recorder.RecordBatteryOuts(familiarity, "pitcher", "catcher", reports[1].DefensiveOuts);
            Assert.That(
                familiarity.GetBatteryFamiliarity(new PlayerPersonPairKey("pitcher", "catcher")),
                Is.EqualTo(1));
        }

        [Test]
        public void Recovery_시설과스태프를하나의Context에서곱해한번만적용한다()
        {
            var first = new TeamSeasonPlayerStatus("person-1", 40);
            var second = new TeamSeasonPlayerStatus("person-2", 90);
            var state = new TeamSeasonPlayerStatusState(TeamSeasonKey, new[] { first, second });
            var context = new ConditionRecoveryContext(
                baseRecovery: 7,
                facilityEfficiencyMultiplier: 1.5d,
                staffEfficiencyMultiplier: 2d);
            var resolver = new ConditionRecoveryResolver();

            int recovery = resolver.ApplyRecovery(state, context);

            Assert.That(recovery, Is.EqualTo(21));
            Assert.That(first.StoredBaseCondition, Is.EqualTo(61));
            Assert.That(second.StoredBaseCondition, Is.EqualTo(100));
        }

        [Test]
        public void MatchSnapshot_Condition이없으면기존경기능력치보정은0이다()
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            MatchRosterSnapshot roster = CreateConditionRoster(includeConditions: false);
            var state = new DetailedTeamGameState(
                roster,
                new PitcherFatigueResolver(balance.Match),
                historicalConfiguration: null,
                conditionRatingResolver: new MatchConditionRatingResolver(balance.ConditionChemistry));

            Assert.That(state.GetConditionRatingModifier(roster.StartingPitcher.Player), Is.Zero);
            for (int index = 0; index < roster.StartingLineup.Count; index++)
                Assert.That(state.GetConditionRatingModifier(roster.StartingLineup[index].Player), Is.Zero);
            for (int index = 0; index < roster.Bullpen.Count; index++)
                Assert.That(state.GetConditionRatingModifier(roster.Bullpen[index].Player), Is.Zero);
            Assert.That(balance.ConditionChemistry.Presentation.GetLevel(100), Is.EqualTo(10),
                "공통 BalanceTable에 ConditionChemistry가 연결되어야 한다.");
        }

        [Test]
        public void FamiliarityRecorder_실제인접8개만기록하고Cap에서멈춘다()
        {
            ConditionChemistryBalanceTable balance = ConditionChemistryBalanceTable.CreateDefault();
            var state = new TeamChemistryFamiliarityState(TeamSeasonKey);
            var recorder = new ChemistryFamiliarityRecorder(balance);
            string[] battingOrder =
            {
                "person-1", "person-2", "person-3", "person-4", "person-5",
                "person-6", "person-7", "person-8", "person-9"
            };

            for (int index = 0; index < 100; index++)
                recorder.RecordStartingLineup(state, battingOrder);
            recorder.RecordBatteryInnings(state, "pitcher", "catcher", 1000);

            Assert.That(state.Entries.Count, Is.EqualTo(9));
            Assert.That(state.GetLineupFamiliarity(new PlayerPersonPairKey("person-1", "person-2")),
                Is.EqualTo(balance.FamiliarityCap));
            Assert.That(state.GetLineupFamiliarity(new PlayerPersonPairKey("person-1", "person-9")), Is.Zero);
            Assert.That(state.GetBatteryFamiliarity(new PlayerPersonPairKey("pitcher", "catcher")),
                Is.EqualTo(balance.FamiliarityCap));
        }

        private static LineupChemistryPlayer[] CreateChemistryLineup(Func<int, BatterAttributes> createAttributes)
        {
            var result = new LineupChemistryPlayer[9];
            for (int index = 0; index < result.Length; index++)
                result[index] = new LineupChemistryPlayer($"person-{index + 1}", createAttributes(index));
            return result;
        }

        private static BatterAttributes CreateBalancedAttributes(int index)
        {
            return new BatterAttributes(50, 50, 50, 50, 50, 50);
        }

        private static BatterAttributes CreateTableSetterAttributes(int index)
        {
            return new BatterAttributes(90, 40, 90, 50, 50, 50);
        }

        private static BatterAttributes CreatePowerAttributes(int index)
        {
            return new BatterAttributes(40, 90, 40, 50, 50, 50);
        }

        private static MatchRosterSnapshot CreateConditionRoster(bool includeConditions)
        {
            var slots = new LineupSlot[9];
            for (int index = 0; index < slots.Length; index++)
            {
                PlayerPosition position = (PlayerPosition)(index + 1);
                Player player = CreatePlayer(100 + index, position, 50);
                slots[index] = new LineupSlot(player, position);
            }
            Player starter = CreatePlayer(199, PlayerPosition.StartingPitcher, 70);
            Player reliever = CreatePlayer(200, PlayerPosition.ReliefPitcher, 70);
            Player benchCatcher = CreatePlayer(210, PlayerPosition.Catcher, 55);

            MatchPlayerConditionEntry[] playerConditions = includeConditions
                ? new[]
                {
                    new MatchPlayerConditionEntry(starter.PlayerId, new EffectiveMatchCondition(80, 0, 0, 10, 0)),
                    new MatchPlayerConditionEntry(reliever.PlayerId, new EffectiveMatchCondition(80, 0, 0, 10, 0)),
                    new MatchPlayerConditionEntry(slots[0].Player.PlayerId, new EffectiveMatchCondition(80, 0, 0, 0, 0)),
                    new MatchPlayerConditionEntry(benchCatcher.PlayerId, new EffectiveMatchCondition(80, 0, 0, 0, 0))
                }
                : null;
            MatchBatteryConditionEntry[] batteryConditions = includeConditions
                ? new[]
                {
                    new MatchBatteryConditionEntry(starter.PlayerId, slots[0].Player.PlayerId, 10),
                    new MatchBatteryConditionEntry(starter.PlayerId, benchCatcher.PlayerId, -10),
                    new MatchBatteryConditionEntry(reliever.PlayerId, slots[0].Player.PlayerId, -10),
                    new MatchBatteryConditionEntry(reliever.PlayerId, benchCatcher.PlayerId, 10)
                }
                : null;

            return new MatchRosterSnapshot(
                1,
                "Condition 테스트 구단",
                new Lineup(slots),
                new PitcherRosterEntry(starter, PitcherRole.Starter),
                new[] { new PitcherRosterEntry(reliever, PitcherRole.MiddleRelief) },
                new[] { benchCatcher },
                ManagerTacticalProfile.Balanced,
                RunningApproach.Balanced,
                playerConditions: playerConditions,
                batteryConditions: batteryConditions);
        }

        private static Player CreatePlayer(int playerId, PlayerPosition position, int rating)
        {
            return new Player(
                playerId,
                $"Condition 선수 {playerId}",
                position,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(rating, rating, rating, rating, rating, rating),
                new PitcherAttributes(rating, rating, rating, rating, rating, rating));
        }
    }
}
