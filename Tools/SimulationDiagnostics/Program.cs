using System;
using System.Collections.Generic;
using System.Diagnostics;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    internal static class Program
    {
        private static readonly string[] DiagnosticTeamNames =
        {
            "진단 서울", "진단 부산", "진단 인천", "진단 광주",
            "진단 수원", "진단 대전", "진단 대구", "진단 창원"
        };

        private static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.GetType().FullName);
                Console.Error.WriteLine(exception.Message);
                Console.Error.WriteLine(exception.StackTrace ?? string.Empty);
                return 1;
            }
        }

        private static int Run(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--growth-cohort", StringComparison.Ordinal))
            {
                int careerCount = args.Length > 1 && int.TryParse(args[1], out int parsedCareers)
                    ? parsedCareers
                    : 1000;
                int maximumSeasons = args.Length > 2 && int.TryParse(args[2], out int parsedSeasons)
                    ? parsedSeasons
                    : 20;
                GrowthRoleCohortReport report = GrowthRoleCohortDiagnostics.Run(careerCount, maximumSeasons);
                report.Validate();
                Console.WriteLine(report.Format());
                return 0;
            }

            if (args.Length > 0 && string.Equals(args[0], "benchmark-match", StringComparison.Ordinal))
            {
                int benchmarkCount = ParseCount(args, 1, 10000);
                bool usesBackgroundProfile = args.Length <= 2 ||
                                             !string.Equals(args[2], "full", StringComparison.OrdinalIgnoreCase);
                return RunMatchBenchmark(benchmarkCount, usesBackgroundProfile);
            }

            int countArgumentIndex = args.Length > 0 &&
                                     string.Equals(args[0], "balance-match", StringComparison.Ordinal)
                ? 1
                : 0;
            int gameCount = ParseCount(args, countArgumentIndex, 10000);
            if (gameCount <= 0)
                return 2;

            DiagnosticRunMetadata.Write("balance-match", gameCount, "FullResultWithNullEventSink");
            Console.WriteLine();
            BalanceTable balance = BalanceTable.CreateDefault();
            CareerCreationStatistics creationStatistics = MeasureCareerCreation(balance, gameCount);
            MatchRosterSnapshot away = CreateRoster(1, 50, 50, 50);
            MatchRosterSnapshot home = CreateRoster(2, 50, 50, 50);
            var statistics = new AggregateStatistics();
            for (int gameIndex = 0; gameIndex < gameCount; gameIndex++)
            {
                ulong seed = DeterministicSeed.Derive(0xD37A11EDUL, (ulong)gameIndex);
                var input = new MatchInput(
                    1,
                    gameIndex + 1,
                    seed,
                    away,
                    home,
                    MatchRules.CreateDefault(requiresWinner: false));
                MatchResult result = new MatchSimulator(balance, MatchRandomStreams.Create(seed))
                    .Simulate(input, NullMatchEventSink.Instance);
                statistics.Add(result);
            }

            VerifyDeterminism(balance, away, home);
            VerifyCoreRules(balance, away, home);
            creationStatistics.Validate(balance);
            statistics.Validate(gameCount);
            Console.WriteLine(creationStatistics.Format(gameCount));
            Console.WriteLine();
            Console.WriteLine(statistics.Format(gameCount));
            return 0;
        }

        private static int RunMatchBenchmark(int gameCount, bool usesBackgroundProfile)
        {
            if (gameCount <= 0)
                return 2;

            const int WarmupGameCount = 128;
            BalanceTable balance = BalanceTable.CreateDefault();
            MatchRosterSnapshot away = CreateRoster(1, 50, 50, 50);
            MatchRosterSnapshot home = CreateRoster(2, 50, 50, 50);
            for (int index = 0; index < WarmupGameCount; index++)
                SimulateBenchmarkMatch(balance, away, home, index, usesBackgroundProfile);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            int gen2Before = GC.CollectionCount(2);
            long scoreChecksum = 0;
            var stopwatch = Stopwatch.StartNew();
            for (int gameIndex = 0; gameIndex < gameCount; gameIndex++)
            {
                MatchResult result = SimulateBenchmarkMatch(
                    balance,
                    away,
                    home,
                    gameIndex,
                    usesBackgroundProfile);
                scoreChecksum += result.AwayBoxScore.Runs * 31L + result.HomeBoxScore.Runs;
            }
            stopwatch.Stop();
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            DiagnosticRunMetadata.Write(
                "benchmark-match",
                gameCount,
                usesBackgroundProfile ? "DetailedBackground" : "DetailedInteractiveWithNullSink");
            Console.WriteLine();
            Console.WriteLine($"ElapsedMilliseconds={stopwatch.Elapsed.TotalMilliseconds:F3}");
            Console.WriteLine($"MicrosecondsPerGame={stopwatch.Elapsed.TotalMilliseconds * 1000d / gameCount:F3}");
            Console.WriteLine($"AllocatedBytes={allocatedBytes:N0}");
            Console.WriteLine($"AllocatedBytesPerGame={allocatedBytes / (double)gameCount:F1}");
            Console.WriteLine($"Gen0Collections={GC.CollectionCount(0) - gen0Before}");
            Console.WriteLine($"Gen1Collections={GC.CollectionCount(1) - gen1Before}");
            Console.WriteLine($"Gen2Collections={GC.CollectionCount(2) - gen2Before}");
            Console.WriteLine($"ScoreChecksum={scoreChecksum}");
            return 0;
        }

        private static MatchResult SimulateBenchmarkMatch(
            BalanceTable balance,
            MatchRosterSnapshot away,
            MatchRosterSnapshot home,
            int gameIndex,
            bool usesBackgroundProfile)
        {
            ulong seed = DeterministicSeed.Derive(0xD37A11EDUL, (ulong)gameIndex);
            var input = new MatchInput(
                1,
                gameIndex + 1,
                seed,
                away,
                home,
                MatchRules.CreateDefault(requiresWinner: false));
            var simulator = new MatchSimulator(balance, MatchRandomStreams.Create(seed));
            return usesBackgroundProfile
                ? simulator.Simulate(
                    input,
                    NullMatchEventSink.Instance,
                    MatchExecutionProfile.DetailedBackground)
                : simulator.Simulate(input, NullMatchEventSink.Instance);
        }

        private static int ParseCount(string[] args, int index, int defaultValue)
        {
            return args.Length > index && int.TryParse(args[index], out int parsed)
                ? parsed
                : defaultValue;
        }

        private static CareerCreationStatistics MeasureCareerCreation(BalanceTable balance, int sampleCount)
        {
            CareerCreationRules rules = CareerCreationRules.CreateDefault();
            int[] batterValues = rules.Batter.CreateWeightedValues(1, 1, 1, 1, 1, 1);
            int[] pitcherValues = rules.Pitcher.CreateWeightedValues(1, 1, 1, 1);
            var batter = new Player(
                9000001,
                "균형형 타자",
                PlayerPosition.Shortstop,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(
                    batterValues[0], batterValues[1], batterValues[3],
                    batterValues[5], batterValues[4], batterValues[2]),
                default);
            var pitcher = new Player(
                9000002,
                "균형형 투수",
                PlayerPosition.StartingPitcher,
                Handedness.Right,
                Handedness.Right,
                default,
                new PitcherAttributes(
                    pitcherValues[3], pitcherValues[0], pitcherValues[0],
                    pitcherValues[2], pitcherValues[1], pitcherValues[1]));
            var evaluator = new PlayerValueEvaluator(balance.PlayerEvaluation);
            var result = new CareerCreationStatistics(
                evaluator.CalculatePositionValue(batter),
                evaluator.CalculatePositionValue(pitcher));

            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                ulong seed = DeterministicSeed.Derive(0x4352454154494F4EUL, (ulong)sampleIndex);
                GeneratedTeam[] teams = new TeamGenerator(
                        balance.TeamGeneration,
                        new Pcg32Random(seed))
                    .GenerateLeague(
                        8,
                        TeamArchetypeLibrary.CreateDefaultPool(),
                        DiagnosticTeamNames);
                for (int teamIndex = 0; teamIndex < teams.Length; teamIndex++)
                {
                    IReadOnlyList<RosterCompetitor> competitors = teams[teamIndex]
                        .GetPositionCompetitors(PlayerPosition.Shortstop);
                    int strongest = 0;
                    for (int competitorIndex = 0; competitorIndex < competitors.Count; competitorIndex++)
                    {
                        int overall = competitors[competitorIndex].Overall;
                        result.AddCompetitor(overall);
                        if (overall > strongest) strongest = overall;
                    }
                    result.AddStrongestCompetitor(strongest);
                }
            }
            return result;
        }

        private static void VerifyDeterminism(
            BalanceTable balance,
            MatchRosterSnapshot away,
            MatchRosterSnapshot home)
        {
            const ulong seed = 0x5EEDC0DEUL;
            var input = new MatchInput(
                1,
                999999,
                seed,
                away,
                home,
                MatchRules.CreateDefault(requiresWinner: false));
            MatchResult first = new MatchSimulator(balance, MatchRandomStreams.Create(seed)).Simulate(input);
            MatchResult second = new MatchSimulator(balance, MatchRandomStreams.Create(seed)).Simulate(input);
            if (first.Events.Count != second.Events.Count)
                throw new InvalidOperationException("결정론 검증 실패: 이벤트 수가 다릅니다.");
            for (int index = 0; index < first.Events.Count; index++)
            {
                if (!first.Events[index].Equals(second.Events[index]))
                    throw new InvalidOperationException($"결정론 검증 실패: 이벤트 {index}가 다릅니다.");
            }
        }

        private static void VerifyCoreRules(
            BalanceTable balance,
            MatchRosterSnapshot away,
            MatchRosterSnapshot home)
        {
            VerifyFatigue(balance);
            VerifyMatchSession(balance, away, home);
            VerifyExtraInnings(balance, away, home);
        }

        private static void VerifyFatigue(BalanceTable balance)
        {
            var resolver = new PitcherFatigueResolver(balance.Match);
            PitcherGameState low = resolver.CreateState(new PitcherRosterEntry(
                CreatePitcher(99001, PlayerPosition.StartingPitcher, 55, 30),
                PitcherRole.Starter));
            PitcherGameState high = resolver.CreateState(new PitcherRosterEntry(
                CreatePitcher(99002, PlayerPosition.StartingPitcher, 55, 90),
                PitcherRole.Starter));
            for (int pitch = 0; pitch < 80; pitch++)
            {
                low.RecordPitch();
                high.RecordPitch();
            }
            if (low.FatigueRatio <= high.FatigueRatio)
                throw new InvalidOperationException("피로 검증 실패: Stamina가 피로 비율을 낮추지 못했습니다.");

            EffectivePitcherRatings tired = resolver.Resolve(low, PitchingApproach.Balanced);
            double velocityLoss = low.Player.PitcherAttributes.Velocity - tired.Velocity;
            double controlLoss = low.Player.PitcherAttributes.Control - tired.Control;
            if (controlLoss <= velocityLoss)
                throw new InvalidOperationException("피로 검증 실패: 한계 구간의 Control 하락이 충분하지 않습니다.");
        }

        private static void VerifyMatchSession(
            BalanceTable balance,
            MatchRosterSnapshot away,
            MatchRosterSnapshot home)
        {
            const ulong seed = 0x51A5510FUL;
            var input = new MatchInput(
                1,
                888881,
                seed,
                away,
                home,
                MatchRules.CreateDefault(requiresWinner: false));
            int controlledId = away.StartingLineup[0].Player.PlayerId;
            var session = new MatchSession(
                input,
                balance,
                controlledId,
                controlsBatting: true,
                controlsPitching: false,
                InterventionLevel.FullControl);
            MatchSessionStep first = AdvanceToDecision(session);
            if (!first.BattingDecision.HasValue || first.BattingDecision.Value.DecisionIndex != 0)
                throw new InvalidOperationException("MatchSession 검증 실패: 첫 타석 결정을 요청하지 않았습니다.");
            session.SubmitBattingApproach(BattingApproach.Contact);
            MatchSessionStep second = AdvanceToDecision(session);
            if (!second.BattingDecision.HasValue || second.BattingDecision.Value.DecisionIndex != 1)
                throw new InvalidOperationException("MatchSession 검증 실패: 결정이 타석당 한 번 소비되지 않았습니다.");
        }

        private static MatchSessionStep AdvanceToDecision(MatchSession session)
        {
            for (int safety = 0; safety < 5000; safety++)
            {
                MatchSessionStep step = session.Advance();
                if (step.Kind is MatchSessionStepKind.DecisionRequired or MatchSessionStepKind.MatchEnded)
                    return step;
            }
            throw new InvalidOperationException("MatchSession 검증 실패: 안전 한도를 초과했습니다.");
        }

        private static void VerifyExtraInnings(
            BalanceTable balance,
            MatchRosterSnapshot away,
            MatchRosterSnapshot home)
        {
            ulong tiedSeed = 0;
            for (ulong seed = 1; seed <= 500; seed++)
            {
                MatchResult candidate = SimulateOneInning(
                    balance, away, home, seed, ExtraInningPolicy.DrawAtLimit);
                if (!candidate.IsTie) continue;
                tiedSeed = seed;
                if (!ContainsEvent(candidate, MatchEventType.MatchEndedAsDraw))
                    throw new InvalidOperationException("연장 검증 실패: 정규 시즌 무승부 이벤트가 없습니다.");
                break;
            }
            if (tiedSeed == 0)
                throw new InvalidOperationException("연장 검증 실패: 1이닝 동점 Seed를 찾지 못했습니다.");

            MatchResult winner = SimulateOneInning(
                balance, away, home, tiedSeed, ExtraInningPolicy.ContinueUntilWinner);
            if (winner.IsTie || ContainsEvent(winner, MatchEventType.MatchEndedAsDraw))
                throw new InvalidOperationException("연장 검증 실패: 승자 필요 경기에서 무승부가 발생했습니다.");
        }

        private static MatchResult SimulateOneInning(
            BalanceTable balance,
            MatchRosterSnapshot away,
            MatchRosterSnapshot home,
            ulong seed,
            ExtraInningPolicy policy)
        {
            var rules = new MatchRules(
                regulationInnings: 1,
                maximumRegulationExtraInnings: 0,
                extraInningPolicy: policy,
                automaticRunnerStartInning: 2,
                usesDesignatedHitter: true,
                intentionalWalkPitchCount: 0);
            var input = new MatchInput(1, (int)seed + 700000, seed, away, home, rules);
            return new MatchSimulator(balance, MatchRandomStreams.Create(seed)).Simulate(input);
        }

        private static bool ContainsEvent(MatchResult result, MatchEventType eventType)
        {
            for (int index = 0; index < result.Events.Count; index++)
            {
                if (result.Events[index].EventType == eventType) return true;
            }
            return false;
        }

        private static MatchRosterSnapshot CreateRoster(
            int teamId,
            int batting,
            int pitching,
            int defense)
        {
            var slots = new LineupSlot[9];
            var bench = new Player[9];
            for (int index = 0; index < 9; index++)
            {
                PlayerPosition position = (PlayerPosition)(index + 1);
                slots[index] = new LineupSlot(
                    CreateBatter(teamId * 1000 + index + 1, position, batting, defense),
                    position);
                bench[index] = CreateBatter(teamId * 1000 + 100 + index, position, batting - 4, defense + 6);
            }

            var starter = new PitcherRosterEntry(
                CreatePitcher(teamId * 1000 + 900, PlayerPosition.StartingPitcher, pitching, stamina: 58),
                PitcherRole.Starter);
            var bullpen = new[]
            {
                new PitcherRosterEntry(
                    CreatePitcher(teamId * 1000 + 901, PlayerPosition.StartingPitcher, pitching - 3, 62),
                    PitcherRole.Swingman),
                new PitcherRosterEntry(
                    CreatePitcher(teamId * 1000 + 902, PlayerPosition.ReliefPitcher, pitching - 2, 48),
                    PitcherRole.MiddleRelief),
                new PitcherRosterEntry(
                    CreatePitcher(teamId * 1000 + 903, PlayerPosition.ReliefPitcher, pitching + 3, 44),
                    PitcherRole.Setup),
                new PitcherRosterEntry(
                    CreatePitcher(teamId * 1000 + 904, PlayerPosition.ReliefPitcher, pitching + 5, 40),
                    PitcherRole.Closer)
            };
            return new MatchRosterSnapshot(
                teamId,
                $"진단 {teamId}팀",
                new Lineup(slots),
                starter,
                bullpen,
                bench,
                ManagerTacticalProfile.Balanced,
                RunningApproach.Balanced);
        }

        private static Player CreateBatter(
            int playerId,
            PlayerPosition position,
            int batting,
            int defense)
        {
            return new Player(
                playerId,
                $"타자 {playerId}",
                position,
                playerId % 2 == 0 ? Handedness.Left : Handedness.Right,
                Handedness.Right,
                new BatterAttributes(batting, batting, batting, batting, ClampRating(defense), batting),
                new PitcherAttributes(20, 20, 20, 20, 20, 20));
        }

        private static Player CreatePitcher(
            int playerId,
            PlayerPosition position,
            int pitching,
            int stamina)
        {
            return new Player(
                playerId,
                $"투수 {playerId}",
                position,
                Handedness.Right,
                playerId % 2 == 0 ? Handedness.Left : Handedness.Right,
                new BatterAttributes(20, 20, 35, 20, 48, 50),
                new PitcherAttributes(
                    ClampRating(stamina),
                    ClampRating(pitching),
                    ClampRating(pitching),
                    ClampRating(pitching),
                    ClampRating(pitching),
                    ClampRating(pitching)));
        }

        private static int ClampRating(int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }

        private sealed class AggregateStatistics
        {
            private long _plateAppearances;
            private long _atBats;
            private long _hits;
            private long _totalBases;
            private long _walks;
            private long _hitByPitches;
            private long _strikeouts;
            private long _runs;
            private long _homeRuns;
            private long _errors;
            private long _pitchersUsed;
            private long _starterPitches;
            private long _stolenBases;
            private long _caughtStealing;
            private long _sacrificeBunts;
            private long _intentionalWalks;
            private long _doublePlays;
            private long _draws;
            private int _maximumPitchersUsed;

            public void Add(MatchResult result)
            {
                Add(result.AwayBoxScore);
                Add(result.HomeBoxScore);
                _runs += result.AwayBoxScore.Runs + result.HomeBoxScore.Runs;
                _errors += result.AwayBoxScore.Errors + result.HomeBoxScore.Errors;
                if (result.IsTie) _draws++;
            }

            private void Add(TeamBoxScore box)
            {
                for (int index = 0; index < box.BattingLines.Count; index++)
                {
                    PlayerBattingLine line = box.BattingLines[index];
                    _plateAppearances += line.PlateAppearances;
                    _atBats += line.AtBats;
                    _hits += line.Hits;
                    _totalBases += line.Hits - line.Doubles - line.Triples - line.HomeRuns +
                                   line.Doubles * 2L + line.Triples * 3L + line.HomeRuns * 4L;
                    _walks += line.Walks;
                    _hitByPitches += line.HitByPitches;
                    _strikeouts += line.Strikeouts;
                    _homeRuns += line.HomeRuns;
                    _stolenBases += line.StolenBases;
                    _caughtStealing += line.CaughtStealing;
                    _sacrificeBunts += line.SacrificeBunts;
                    _intentionalWalks += line.IntentionalWalks;
                    _doublePlays += line.GroundedIntoDoublePlays;
                }
                int used = 0;
                for (int index = 0; index < box.PitchingLines.Count; index++)
                {
                    PlayerPitchingLine line = box.PitchingLines[index];
                    if (line.PitchesThrown > 0) used++;
                    if (index == 0) _starterPitches += line.PitchesThrown;
                }
                _pitchersUsed += used;
                if (used > _maximumPitchersUsed) _maximumPitchersUsed = used;
            }

            public void Validate(int games)
            {
                if (games < 1000) return;
                double average = Ratio(_hits, _atBats);
                double onBase = Ratio(_hits + _walks + _hitByPitches, _atBats + _walks + _hitByPitches);
                double slugging = Ratio(_totalBases, _atBats);
                double strikeoutRate = Ratio(_strikeouts, _plateAppearances);
                if (average < 0.220d || average > 0.300d ||
                    onBase < 0.290d || onBase > 0.380d ||
                    slugging < 0.330d || slugging > 0.470d ||
                    strikeoutRate < 0.165d || strikeoutRate > 0.270d)
                {
                    throw new InvalidOperationException("통계 안전 범위를 벗어났습니다.");
                }
                if (_maximumPitchersUsed < 3)
                    throw new InvalidOperationException("다인 불펜 검증 실패: 세 명 이상 등판한 팀이 없습니다.");
            }

            public string Format(int games)
            {
                double obpDenominator = _atBats + _walks + _hitByPitches;
                long stealAttempts = _stolenBases + _caughtStealing;
                return string.Join(Environment.NewLine, new[]
                {
                    $"Games={games:N0}",
                    $"AVG={Ratio(_hits, _atBats):F3}",
                    $"OBP={Ratio(_hits + _walks + _hitByPitches, obpDenominator):F3}",
                    $"SLG={Ratio(_totalBases, _atBats):F3}",
                    $"R/G(team)={Ratio(_runs, games * 2L):F3}",
                    $"HR/G(team)={Ratio(_homeRuns, games * 2L):F3}",
                    $"BB%={Ratio(_walks, _plateAppearances) * 100d:F2}",
                    $"SO%={Ratio(_strikeouts, _plateAppearances) * 100d:F2}",
                    $"HBP%={Ratio(_hitByPitches, _plateAppearances) * 100d:F2}",
                    $"Errors/Game={Ratio(_errors, games):F3}",
                    $"PitchersUsed/Team={Ratio(_pitchersUsed, games * 2L):F3}",
                    $"StarterPitches={Ratio(_starterPitches, games * 2L):F2}",
                    $"SB/Team={Ratio(_stolenBases, games * 2L):F3}",
                    $"SB%={Ratio(_stolenBases, stealAttempts) * 100d:F2}",
                    $"SAC/Team={Ratio(_sacrificeBunts, games * 2L):F3}",
                    $"IBB/Team={Ratio(_intentionalWalks, games * 2L):F3}",
                    $"GIDP/Team={Ratio(_doublePlays, games * 2L):F3}",
                    $"Draw%={Ratio(_draws, games) * 100d:F2}"
                });
            }

            private static double Ratio(double numerator, double denominator)
            {
                return denominator <= 0d ? 0d : numerator / denominator;
            }
        }

        private sealed class CareerCreationStatistics
        {
            private long _competitorOverallTotal;
            private long _strongestOverallTotal;
            private long _competitorCount;
            private long _strongestCount;

            public CareerCreationStatistics(int batterValue, int pitcherValue)
            {
                BatterValue = batterValue;
                PitcherValue = pitcherValue;
            }

            public int BatterValue { get; }
            public int PitcherValue { get; }

            public void AddCompetitor(int overall)
            {
                _competitorOverallTotal += overall;
                _competitorCount++;
            }

            public void AddStrongestCompetitor(int overall)
            {
                _strongestOverallTotal += overall;
                _strongestCount++;
            }

            public void Validate(BalanceTable balance)
            {
                double competitorAverage = Ratio(_competitorOverallTotal, _competitorCount);
                double strongestAverage = Ratio(_strongestOverallTotal, _strongestCount);
                if (BatterValue != 60 || PitcherValue != 60)
                    throw new InvalidOperationException("생성 밸런스 실패: 균형형의 포지션 평가가 60이 아닙니다.");
                if (Math.Abs(competitorAverage - BatterValue) > 2d)
                    throw new InvalidOperationException("생성 밸런스 실패: 플레이어와 Rookie 평균의 차이가 너무 큽니다.");
                if (BatterValue + balance.CareerSeason.StartingCompetitionBonus < strongestAverage)
                {
                    throw new InvalidOperationException(
                        "생성 밸런스 실패: 주전 경쟁 계약 보정으로도 평균 최고 경쟁자에게 도달하지 못합니다.");
                }
            }

            public string Format(int samples)
            {
                return string.Join(Environment.NewLine, new[]
                {
                    $"CareerCreationSamples={samples:N0}",
                    $"BalancedBatterValue={BatterValue}",
                    $"BalancedPitcherValue={PitcherValue}",
                    $"RookieCompetitorAverage={Ratio(_competitorOverallTotal, _competitorCount):F2}",
                    $"RookieStrongestAtPosition={Ratio(_strongestOverallTotal, _strongestCount):F2}"
                });
            }

            private static double Ratio(double numerator, double denominator)
            {
                return denominator <= 0d ? 0d : numerator / denominator;
            }
        }
    }
}
