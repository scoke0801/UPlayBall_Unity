using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>
    /// 대량 경기에서 기본 야구 지표와 능력치 격차가 유효한지 검증한다.
    /// </summary>
    public sealed class MatchSimulationStatisticsTests
    {
        private const int EqualTeamGames = 5000;
        private const int StrengthComparisonGames = 5000;

        [Test]
        public void Simulate_만경기에서기준통계와전력차가나타난다()
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            Team averageA = SimulationTestFactory.CreateTeam(1, 50, 50);
            Team averageB = SimulationTestFactory.CreateTeam(2, 50, 50);
            MatchRosterSnapshot averageRosterA = SimulationTestFactory.CreateDetailedRoster(averageA);
            MatchRosterSnapshot averageRosterB = SimulationTestFactory.CreateDetailedRoster(averageB);
            var totals = new LeagueBattingTotals();

            for (int game = 0; game < EqualTeamGames; game++)
            {
                MatchRosterSnapshot away = game % 2 == 0 ? averageRosterA : averageRosterB;
                MatchRosterSnapshot home = game % 2 == 0 ? averageRosterB : averageRosterA;
                ulong seed = (ulong)(100000 + game);
                var input = new MatchInput(
                    1, game + 1, seed, away, home,
                    Baseball.Core.Rules.MatchRules.CreateDefault(requiresWinner: false));
                MatchResult result = new MatchSimulator(balance, MatchRandomStreams.Create(seed))
                    .Simulate(input, NullMatchEventSink.Instance);
                totals.Add(result.AwayBoxScore);
                totals.Add(result.HomeBoxScore);
            }

            Team strongTeam = SimulationTestFactory.CreateTeam(3, 56, 56, 56);
            Team weakTeam = SimulationTestFactory.CreateTeam(4, 48, 48, 48);
            MatchRosterSnapshot strongRoster = SimulationTestFactory.CreateDetailedRoster(strongTeam);
            MatchRosterSnapshot weakRoster = SimulationTestFactory.CreateDetailedRoster(weakTeam);
            int strongWins = 0;
            int ties = 0;

            for (int game = 0; game < StrengthComparisonGames; game++)
            {
                bool strongIsAway = game % 2 == 0;
                MatchRosterSnapshot away = strongIsAway ? strongRoster : weakRoster;
                MatchRosterSnapshot home = strongIsAway ? weakRoster : strongRoster;
                ulong seed = (ulong)(200000 + game);
                var input = new MatchInput(
                    1, EqualTeamGames + game + 1, seed, away, home,
                    Baseball.Core.Rules.MatchRules.CreateDefault(requiresWinner: false));
                MatchResult result = new MatchSimulator(balance, MatchRandomStreams.Create(seed))
                    .Simulate(input, NullMatchEventSink.Instance);

                if (result.IsTie)
                    ties++;
                else if (result.WinnerTeamId == strongTeam.TeamId)
                    strongWins++;
            }

            double battingAverage = totals.AtBats == 0 ? 0d : (double)totals.Hits / totals.AtBats;
            double onBasePercentage = totals.OnBaseDenominator == 0
                ? 0d
                : (double)(totals.Hits + totals.Walks + totals.HitByPitches) / totals.OnBaseDenominator;
            double sluggingPercentage = totals.AtBats == 0
                ? 0d
                : (double)totals.TotalBases / totals.AtBats;
            double runsPerTeamGame = (double)totals.Runs / (EqualTeamGames * 2);
            double walkRate = totals.PlateAppearances == 0
                ? 0d
                : (double)totals.Walks / totals.PlateAppearances;
            double strikeoutRate = totals.PlateAppearances == 0
                ? 0d
                : (double)totals.Strikeouts / totals.PlateAppearances;
            double hitByPitchRate = totals.PlateAppearances == 0
                ? 0d
                : (double)totals.HitByPitches / totals.PlateAppearances;
            double strongWinRate = (double)strongWins / StrengthComparisonGames;

            System.Console.WriteLine(
                $"AVG {battingAverage:F3} / OBP {onBasePercentage:F3} / SLG {sluggingPercentage:F3} / " +
                $"R/G {runsPerTeamGame:F2} / BB% {walkRate:P1} / SO% {strikeoutRate:P1} / " +
                $"HBP% {hitByPitchRate:P2} / Strong W% {strongWinRate:P1} / Ties {ties}");

            Assert.That(battingAverage, Is.InRange(0.220d, 0.300d));
            Assert.That(onBasePercentage, Is.InRange(0.290d, 0.380d));
            Assert.That(sluggingPercentage, Is.InRange(0.330d, 0.470d));
            Assert.That(runsPerTeamGame, Is.InRange(3.2d, 5.8d));
            Assert.That(walkRate, Is.InRange(0.065d, 0.120d));
            Assert.That(strikeoutRate, Is.InRange(0.170d, 0.270d));
            // 최근 MLB의 HBP/PA는 약 1.1%다. 사구가 아예 없거나 볼넷 수준으로 흔해지면 실패한다.
            Assert.That(hitByPitchRate, Is.InRange(0.007d, 0.016d));
            Assert.That(strongWinRate, Is.GreaterThan(0.58d));
        }

        [Test]
        [Timeout(120000)]
        public void SkillBoard_만경기에서Epic교타블록의방향성이실제타격결과에나타난다()
        {
            const int PairedScenarios = 5_000;
            BalanceTable balance = BalanceTable.CreateDefault();
            SkillBlockDefinition contactEpic = FindBlock(
                balance.Growth.SkillBlocks,
                SkillBlockCategory.Contact,
                SkillBlockRarity.Unique);
            var growth = new PlayerGrowthState(
                1,
                22,
                PlayerType.Batter,
                new AbilityRatings(50),
                new AbilityRatings(70),
                WorkEthicGrade.Normal,
                90,
                0,
                70);
            var board = new SkillBoardState(balance.Growth.SkillBoard.BoardDefinitionId);
            SkillBlockInstance block = board.AddOwnedBlock(contactEpic.BlockId);
            var boardService = new SkillBoardService(
                balance.Growth.SkillBoard,
                balance.Growth.SkillBlocks);
            boardService.PlaceBlock(board, block.InstanceId, 0, 0, 0);
            int stableContact = boardService.GetStableAbility(board, growth, PlayerAbility.Contact);

            Team enhanced = CreateTeamWithLeadoffContact(11, stableContact);
            Team baseline = CreateTeamWithLeadoffContact(11, 50);
            Team opponent = CreateTeamWithLeadoffContact(12, 50);
            MatchRosterSnapshot enhancedRoster = SimulationTestFactory.CreateDetailedRoster(enhanced);
            MatchRosterSnapshot baselineRoster = SimulationTestFactory.CreateDetailedRoster(baseline);
            MatchRosterSnapshot opponentRoster = SimulationTestFactory.CreateDetailedRoster(opponent);
            int enhancedAtBats = 0;
            int enhancedHits = 0;
            int baselineAtBats = 0;
            int baselineHits = 0;
            int enhancedWins = 0;
            int baselineWins = 0;

            for (int scenario = 0; scenario < PairedScenarios; scenario++)
            {
                bool playerTeamIsAway = scenario % 2 == 0;
                ulong seed = (ulong)(700000 + scenario);
                MatchRosterSnapshot baselineAway = playerTeamIsAway ? baselineRoster : opponentRoster;
                MatchRosterSnapshot baselineHome = playerTeamIsAway ? opponentRoster : baselineRoster;
                MatchResult baselineResult = new MatchSimulator(balance, MatchRandomStreams.Create(seed)).Simulate(
                    new MatchInput(
                        1, scenario * 2 + 1, seed, baselineAway, baselineHome,
                        Baseball.Core.Rules.MatchRules.CreateDefault(requiresWinner: false)),
                    NullMatchEventSink.Instance);
                MatchRosterSnapshot enhancedAway = playerTeamIsAway ? enhancedRoster : opponentRoster;
                MatchRosterSnapshot enhancedHome = playerTeamIsAway ? opponentRoster : enhancedRoster;
                MatchResult enhancedResult = new MatchSimulator(balance, MatchRandomStreams.Create(seed)).Simulate(
                    new MatchInput(
                        1, scenario * 2 + 2, seed, enhancedAway, enhancedHome,
                        Baseball.Core.Rules.MatchRules.CreateDefault(requiresWinner: false)),
                    NullMatchEventSink.Instance);
                TeamBoxScore enhancedBox = playerTeamIsAway
                    ? enhancedResult.AwayBoxScore
                    : enhancedResult.HomeBoxScore;
                TeamBoxScore baselineBox = playerTeamIsAway
                    ? baselineResult.AwayBoxScore
                    : baselineResult.HomeBoxScore;
                PlayerBattingLine enhancedLine = FindBattingLine(enhancedBox, 1101);
                PlayerBattingLine baselineLine = FindBattingLine(baselineBox, 1101);
                enhancedAtBats += enhancedLine.AtBats;
                enhancedHits += enhancedLine.Hits;
                baselineAtBats += baselineLine.AtBats;
                baselineHits += baselineLine.Hits;
                if (enhancedResult.WinnerTeamId == enhanced.TeamId) enhancedWins++;
                if (baselineResult.WinnerTeamId == baseline.TeamId) baselineWins++;
            }

            double enhancedAverage = enhancedHits / (double)enhancedAtBats;
            double baselineAverage = baselineHits / (double)baselineAtBats;
            System.Console.WriteLine(
                $"Unique Contact +5 / Enhanced AVG {enhancedAverage:F3} / " +
                $"Baseline AVG {baselineAverage:F3} / W-L {enhancedWins}-{baselineWins}");

            Assert.That(stableContact, Is.EqualTo(55));
            Assert.That(enhancedAverage, Is.GreaterThan(baselineAverage));
            Assert.That(growth.BaseAbilities.Get(PlayerAbility.Contact), Is.EqualTo(50));
        }

        private static SkillBlockDefinition FindBlock(
            SkillBlockDefinition[] blocks,
            SkillBlockCategory category,
            SkillBlockRarity rarity)
        {
            for (int index = 0; index < blocks.Length; index++)
            {
                if (blocks[index].Category == category && blocks[index].Rarity == rarity)
                    return blocks[index];
            }
            throw new System.InvalidOperationException("기본 스킬 블록 풀에 필요한 블록이 없습니다.");
        }

        private static Team CreateTeamWithLeadoffContact(int teamId, int leadoffContact)
        {
            var slots = new LineupSlot[9];
            for (int index = 0; index < slots.Length; index++)
            {
                PlayerPosition position = (PlayerPosition)(index + 1);
                int contact = index == 0 ? leadoffContact : 50;
                var player = new Player(
                    teamId * 100 + index + 1,
                    $"{teamId}팀 타자 {index + 1}",
                    position,
                    Handedness.Right,
                    Handedness.Right,
                    new BatterAttributes(contact, 50, 50, 50, 50, 50),
                    new PitcherAttributes(20, 20, 20, 20, 20, 20));
                slots[index] = new LineupSlot(player, position);
            }
            var pitcher = new Player(
                teamId * 100 + 99,
                $"{teamId}팀 투수",
                PlayerPosition.StartingPitcher,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(20, 20, 20, 20, 30, 20),
                new PitcherAttributes(50, 50, 50, 50, 50, 50));
            return new Team(teamId, $"테스트 {teamId}팀", new Lineup(slots), pitcher);
        }

        private static PlayerBattingLine FindBattingLine(TeamBoxScore boxScore, int playerId)
        {
            for (int index = 0; index < boxScore.BattingLines.Count; index++)
            {
                if (boxScore.BattingLines[index].PlayerId == playerId)
                    return boxScore.BattingLines[index];
            }
            throw new System.InvalidOperationException("타격 기록을 찾지 못했습니다.");
        }

        private sealed class LeagueBattingTotals
        {
            public int PlateAppearances { get; private set; }
            public int AtBats { get; private set; }
            public int Runs { get; private set; }
            public int Hits { get; private set; }
            public int Walks { get; private set; }
            public int HitByPitches { get; private set; }
            public int Strikeouts { get; private set; }
            public int SacrificeFlies { get; private set; }
            public int TotalBases { get; private set; }
            public int OnBaseDenominator => AtBats + Walks + HitByPitches + SacrificeFlies;

            public void Add(TeamBoxScore boxScore)
            {
                Runs += boxScore.Runs;
                for (int index = 0; index < boxScore.BattingLines.Count; index++)
                {
                    PlayerBattingLine line = boxScore.BattingLines[index];
                    PlateAppearances += line.PlateAppearances;
                    AtBats += line.AtBats;
                    Hits += line.Hits;
                    Walks += line.Walks;
                    HitByPitches += line.HitByPitches;
                    Strikeouts += line.Strikeouts;
                    SacrificeFlies += line.SacrificeFlies;
                    int singles = line.Hits - line.Doubles - line.Triples - line.HomeRuns;
                    TotalBases += singles + line.Doubles * 2 + line.Triples * 3 + line.HomeRuns * 4;
                }
            }
        }
    }
}
