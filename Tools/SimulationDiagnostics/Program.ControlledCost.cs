using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    internal static partial class Program
    {
        private static int RunControlledCost(string[] args)
        {
            int count = ParseCount(args, 1, 10000);
            string path = args.Length > 2 ? args[2] :
                "Assets/Editor Default Resources/HistoricalSimulation/1982-2025/Runtime/Years/2025.json";
            int lowCost = args.Length > 3 ? int.Parse(args[3]) : 1;
            if (lowCost < 1 || lowCost >= 10) throw new ArgumentOutOfRangeException(nameof(lowCost));
            BalanceTable balance = args.Length > 4
                ? Baseball.Tools.CommonMatchBalanceInput.Load(args[4]) : BalanceTable.CreateDefault();
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            Console.WriteLine($"ControlledCost GamesPerScenario={count} Source={path}");
            Console.WriteLine($"BalanceHash={balance.ContentHash}");
            Console.WriteLine("동일 상대·Seed·중립 구단의 한 슬롯만 교체. 현재 코드 비교이며 변경 전 엔진과 비교하지 않음.");
            foreach (bool pitcher in new[] { false, true })
            {
                foreach (int cost in new[] { lowCost, 10 })
                {
                    JsonElement season = SelectControlledSeason(document.RootElement.GetProperty("playerSeasons"), pitcher, cost);
                    Player selected = CreateControlledPlayer(season, pitcher);
                    Console.WriteLine($"Role={(pitcher ? "StarterSlot" : "FirstBase")} NativeRole={season.GetProperty("pitcherRole").GetString()} Cost={cost} Season={season.GetProperty("playerSeasonId").GetString()} ArsenalCount={selected.PitchRepertoire.Count} Attributes={season.GetProperty("baseAttributes").GetRawText()}");
                    MatchRosterSnapshot baseline = CreateRoster(1, 50, 50, 50);
                    var slots = new LineupSlot[9];
                    for (int i = 0; i < slots.Length; i++) slots[i] = baseline.StartingLineup[i];
                    if (!pitcher) slots[1] = new LineupSlot(selected, PlayerPosition.FirstBase);
                    var team = new MatchRosterSnapshot(1, "가상 검증팀", new Lineup(slots),
                        pitcher ? new PitcherRosterEntry(selected, PitcherRole.Starter) : baseline.StartingPitcher,
                        baseline.Bullpen, Array.Empty<Player>(), ManagerTacticalProfile.Balanced, RunningApproach.Balanced);
                    MatchRosterSnapshot opponent = CreateRoster(2, 50, 50, 50);
                    var aggregate = new ControlledStatistics();
                    for (int i = 0; i < count; i++)
                    {
                        ulong seed = DeterministicSeed.Derive(0xC051UL, (ulong)i);
                        var input = new MatchInput(1, i + 1, seed, team, opponent, MatchRules.CreateDefault(false));
                        MatchResult result = new MatchSimulator(balance, MatchRandomStreams.Create(seed)).Simulate(input, NullMatchEventSink.Instance);
                        aggregate.Add(result, selected.PlayerId, pitcher);
                    }
                    Console.WriteLine(aggregate.Format(count, pitcher));
                }
            }
            WritePitchGrowthMeasurements();
            return 0;
        }

        private static JsonElement SelectControlledSeason(JsonElement seasons, bool pitcher, int cost)
        {
            var candidates = new List<JsonElement>();
            foreach (JsonElement season in seasons.EnumerateArray())
            {
                if (season.GetProperty("cost").GetInt32() != cost) continue;
                bool matches = pitcher ? season.GetProperty("playerType").GetString() == "Pitcher" :
                    season.GetProperty("position").GetString() == "1B";
                if (matches) candidates.Add(season);
            }
            if (candidates.Count == 0) throw new InvalidOperationException("비교할 Cost/역할 표본이 없습니다.");
            candidates.Sort((a, b) =>
            {
                int score = CompositeProxy(a, pitcher).CompareTo(CompositeProxy(b, pitcher));
                return score != 0 ? score : string.CompareOrdinal(a.GetProperty("playerSeasonId").GetString(), b.GetProperty("playerSeasonId").GetString());
            });
            return candidates[candidates.Count / 2];
        }

        private static double CompositeProxy(JsonElement season, bool pitcher)
        {
            JsonElement ratings = season.GetProperty("baseAttributes");
            int sum = 0;
            for (int i = pitcher ? 6 : 0; i < (pitcher ? 12 : 6); i++) sum += ratings[i].GetInt32();
            return sum / 6d;
        }

        private static Player CreateControlledPlayer(JsonElement season, bool pitcher)
        {
            JsonElement a = season.GetProperty("baseAttributes");
            var repertoire = new List<PitchRepertoireEntry>();
            if (season.TryGetProperty("pitchRepertoire", out JsonElement pitches))
                foreach (JsonElement pitch in pitches.EnumerateArray())
                    repertoire.Add(new PitchRepertoireEntry(Enum.Parse<PitchType>(pitch.GetProperty("pitchType").GetString()),
                        pitch.GetProperty("baseMastery").GetInt32(), pitch.GetProperty("isPrimary").GetBoolean(),
                        pitch.GetProperty("developmentAffinity").GetDouble(), pitch.GetProperty("usagePreference").GetDouble(),
                        pitch.GetProperty("velocityOffset").GetDouble()));
            return new Player(pitcher ? 1900 : 1002, "가상 비교 선수", pitcher ? PlayerPosition.StartingPitcher : PlayerPosition.FirstBase,
                Handedness.Right, Handedness.Right, new BatterAttributes(a[0].GetInt32(), a[1].GetInt32(), a[2].GetInt32(),
                    a[3].GetInt32(), a[4].GetInt32(), a[5].GetInt32()),
                new PitcherAttributes(a[6].GetInt32(), a[7].GetInt32(), a[8].GetInt32(), a[9].GetInt32(), a[10].GetInt32(), a[11].GetInt32()),
                pitchRepertoire: repertoire);
        }

        private static void WritePitchGrowthMeasurements()
        {
            PitchArsenalBalance balance = PitchArsenalBalance.CreateDefault();
            Console.WriteLine("PitchType,Velocity+1,+5,+10(kph),Breaking+1,+5,+10(quality),AuxGrowth3,5,6");
            foreach (PitchType type in Enum.GetValues(typeof(PitchType)))
            {
                var entry = new PitchRepertoireEntry(type, 60, false);
                var values = new List<string>();
                foreach (int gain in new[] { 1, 5, 10 })
                    values.Add((PitchEffectivenessResolver.ResolveVelocityKph(entry, 50 + gain, balance) -
                        PitchEffectivenessResolver.ResolveVelocityKph(entry, 50, balance)).ToString("F3"));
                foreach (int gain in new[] { 1, 5, 10 })
                    values.Add((PitchEffectivenessResolver.ResolveQuality(entry, 50, 50 + gain, 50, balance) -
                        PitchEffectivenessResolver.ResolveQuality(entry, 50, 50, 50, balance)).ToString("F3"));
                foreach (int size in new[] { 3, 5, 6 }) values.Add(PitchGrowthResolver.ResolveEfficiency(entry, size, size - 1, balance).ToString("F3"));
                Console.WriteLine(type + "," + string.Join(",", values));
            }
        }

        private sealed class ControlledStatistics
        {
            private long _ab, _pa, _hits, _tb, _bb, _hbp, _sf, _hr, _k, _rbi, _runs;
            private long _outs, _allowed, _wins, _draws, _difference;
            private long _earned, _stolenBases, _caughtStealing;
            public void Add(MatchResult result, int playerId, bool pitcher)
            {
                int margin = result.AwayBoxScore.Runs - result.HomeBoxScore.Runs;
                _difference += margin;
                if (margin > 0) _wins++; else if (margin == 0) _draws++;
                if (pitcher)
                {
                    foreach (PlayerPitchingLine line in result.AwayBoxScore.PitchingLines)
                        if (line.PlayerId == playerId)
                        {
                            _outs += line.OutsRecorded; _hits += line.HitsAllowed; _bb += line.WalksAllowed;
                            _k += line.Strikeouts; _hr += line.HomeRunsAllowed; _allowed += line.RunsAllowed;
                            _earned += line.EarnedRuns;
                        }
                    return;
                }
                foreach (PlayerBattingLine line in result.AwayBoxScore.BattingLines)
                    if (line.PlayerId == playerId)
                    {
                        _ab += line.AtBats; _pa += line.PlateAppearances; _hits += line.Hits;
                        _tb += line.Hits + line.Doubles + 2 * line.Triples + 3 * line.HomeRuns;
                        _bb += line.Walks; _hbp += line.HitByPitches; _sf += line.SacrificeFlies;
                        _hr += line.HomeRuns; _k += line.Strikeouts; _rbi += line.RunsBattedIn; _runs += line.Runs;
                        _stolenBases += line.StolenBases; _caughtStealing += line.CaughtStealing;
                    }
            }
            public string Format(int count, bool pitcher)
            {
                string team = $" RD/G={_difference / (double)count:F3} Win%={100d * _wins / count:F2} Draw%={100d * _draws / count:F2}";
                return pitcher ? $"K/9={27d * _k / _outs:F3} BB/9={27d * _bb / _outs:F3} HR/9={27d * _hr / _outs:F3} WHIP={3d * (_hits + _bb) / _outs:F3} ERA={27d * _earned / _outs:F3} IP/G={_outs / (3d * count):F3} RA/G={_allowed / (double)count:F3}" + team :
                    $"AVG={_hits / (double)_ab:F3} OBP={(_hits + _bb + _hbp) / (double)(_ab + _bb + _hbp + _sf):F3} SLG={_tb / (double)_ab:F3} HR={_hr} BB%={100d * _bb / _pa:F2} HBP%={100d * _hbp / _pa:F2} K%={100d * _k / _pa:F2} SB/G={_stolenBases / (double)count:F3} CS/G={_caughtStealing / (double)count:F3} RBI/G={_rbi / (double)count:F3} R/G={_runs / (double)count:F3}" + team;
            }
        }
    }
}
