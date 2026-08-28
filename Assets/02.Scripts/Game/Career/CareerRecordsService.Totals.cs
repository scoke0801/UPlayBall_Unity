using Baseball.Core.Players;

namespace Baseball.Game.Career
{
    public sealed partial class CareerRecordsService
    {
        private sealed class PlayerStatisticsTotals
        {
            private int _games;
            private int _gamesStarted;
            private int _atBats;
            private int _plateAppearances;
            private int _runs;
            private int _hits;
            private int _doubles;
            private int _triples;
            private int _homeRuns;
            private int _runsBattedIn;
            private int _walks;
            private int _hitByPitches;
            private int _battingStrikeouts;
            private int _sacrificeFlies;
            private int _groundedIntoDoublePlays;
            private int _stolenBases;
            private int _caughtStealing;
            private int _pitchingAppearances;
            private int _pitchingStarts;
            private int _outsRecorded;
            private int _wins;
            private int _losses;
            private int _saves;
            private int _holds;
            private int _blownSaves;
            private int _hitsAllowed;
            private int _homeRunsAllowed;
            private int _runsAllowed;
            private int _earnedRuns;
            private int _walksAllowed;
            private int _hitBatters;
            private int _pitchingStrikeouts;
            private int _battersFaced;
            private int _qualityStarts;
            private FieldingTotals _fielding;

            public void Add(PlayerSeasonStatisticsState statistics)
            {
                if (statistics == null)
                    return;
                _games += statistics.GamesPlayed;
                _gamesStarted += statistics.GamesStarted;
                _plateAppearances += statistics.PlateAppearances;
                _atBats += statistics.AtBats;
                _runs += statistics.Runs;
                _hits += statistics.Hits;
                _doubles += statistics.Doubles;
                _triples += statistics.Triples;
                _homeRuns += statistics.HomeRuns;
                _runsBattedIn += statistics.RunsBattedIn;
                _walks += statistics.Walks;
                _hitByPitches += statistics.HitByPitches;
                _battingStrikeouts += statistics.BattingStrikeouts;
                _sacrificeFlies += statistics.SacrificeFlies;
                _groundedIntoDoublePlays += statistics.GroundedIntoDoublePlays;
                _stolenBases += statistics.StolenBases;
                _caughtStealing += statistics.CaughtStealing;
                _pitchingAppearances += statistics.PitchingAppearances;
                _pitchingStarts += statistics.PitchingStarts;
                _outsRecorded += statistics.OutsRecorded;
                _wins += statistics.Wins;
                _losses += statistics.Losses;
                _saves += statistics.Saves;
                _holds += statistics.Holds;
                _blownSaves += statistics.BlownSaves;
                _hitsAllowed += statistics.HitsAllowed;
                _homeRunsAllowed += statistics.HomeRunsAllowed;
                _runsAllowed += statistics.RunsAllowed;
                _earnedRuns += statistics.EarnedRuns;
                _walksAllowed += statistics.WalksAllowed;
                _hitBatters += statistics.HitBatters;
                _pitchingStrikeouts += statistics.PitchingStrikeouts;
                _battersFaced += statistics.BattersFaced;
                _qualityStarts += statistics.QualityStarts;
                for (int positionIndex = (int)PlayerPosition.Catcher;
                     positionIndex <= (int)PlayerPosition.ReliefPitcher;
                     positionIndex++)
                {
                    FieldingStatisticsState fielding = statistics.GetFielding((PlayerPosition)positionIndex);
                    if (fielding != null)
                        _fielding.Add(fielding);
                }
            }

            public double GetValue(CareerRecordMetric metric)
            {
                int totalBases = _hits + _doubles + _triples * 2 + _homeRuns * 3;
                return metric switch
                {
                    CareerRecordMetric.Games => _games,
                    CareerRecordMetric.GamesStarted => _gamesStarted,
                    CareerRecordMetric.PlateAppearances => _plateAppearances,
                    CareerRecordMetric.AtBats => _atBats,
                    CareerRecordMetric.Runs => _runs,
                    CareerRecordMetric.Hits => _hits,
                    CareerRecordMetric.Singles => _hits - _doubles - _triples - _homeRuns,
                    CareerRecordMetric.Doubles => _doubles,
                    CareerRecordMetric.Triples => _triples,
                    CareerRecordMetric.HomeRuns => _homeRuns,
                    CareerRecordMetric.RunsBattedIn => _runsBattedIn,
                    CareerRecordMetric.Walks => _walks,
                    CareerRecordMetric.HitByPitches => _hitByPitches,
                    CareerRecordMetric.BattingStrikeouts => _battingStrikeouts,
                    CareerRecordMetric.SacrificeFlies => _sacrificeFlies,
                    CareerRecordMetric.GroundedIntoDoublePlays => _groundedIntoDoublePlays,
                    CareerRecordMetric.TotalBases => totalBases,
                    CareerRecordMetric.BattingAverage => _atBats == 0 ? 0d : _hits / (double)_atBats,
                    CareerRecordMetric.OnBasePercentage => _plateAppearances == 0
                        ? 0d
                        : (_hits + _walks + _hitByPitches) / (double)_plateAppearances,
                    CareerRecordMetric.SluggingPercentage => _atBats == 0 ? 0d : totalBases / (double)_atBats,
                    CareerRecordMetric.OnBasePlusSlugging =>
                        (_plateAppearances == 0 ? 0d : (_hits + _walks + _hitByPitches) / (double)_plateAppearances) +
                        (_atBats == 0 ? 0d : totalBases / (double)_atBats),
                    CareerRecordMetric.WalkStrikeoutRatio => _battingStrikeouts == 0
                        ? _walks
                        : _walks / (double)_battingStrikeouts,
                    CareerRecordMetric.PitchingAppearances => _pitchingAppearances,
                    CareerRecordMetric.PitchingStarts => _pitchingStarts,
                    CareerRecordMetric.OutsRecorded => _outsRecorded,
                    CareerRecordMetric.Wins => _wins,
                    CareerRecordMetric.Losses => _losses,
                    CareerRecordMetric.Saves => _saves,
                    CareerRecordMetric.Holds => _holds,
                    CareerRecordMetric.BlownSaves => _blownSaves,
                    CareerRecordMetric.HitsAllowed => _hitsAllowed,
                    CareerRecordMetric.HomeRunsAllowed => _homeRunsAllowed,
                    CareerRecordMetric.RunsAllowed => _runsAllowed,
                    CareerRecordMetric.EarnedRuns => _earnedRuns,
                    CareerRecordMetric.WalksAllowed => _walksAllowed,
                    CareerRecordMetric.HitBatters => _hitBatters,
                    CareerRecordMetric.PitchingStrikeouts => _pitchingStrikeouts,
                    CareerRecordMetric.BattersFaced => _battersFaced,
                    CareerRecordMetric.QualityStarts => _qualityStarts,
                    CareerRecordMetric.EarnedRunAverage => _outsRecorded == 0
                        ? 0d
                        : _earnedRuns * 27d / _outsRecorded,
                    CareerRecordMetric.WalksHitsPerInningPitched => _outsRecorded == 0
                        ? 0d
                        : (_walksAllowed + _hitsAllowed) * 3d / _outsRecorded,
                    CareerRecordMetric.StrikeoutWalkRatio => _walksAllowed == 0
                        ? _pitchingStrikeouts
                        : _pitchingStrikeouts / (double)_walksAllowed,
                    CareerRecordMetric.HomeRunsPerNineInnings => _outsRecorded == 0
                        ? 0d
                        : _homeRunsAllowed * 27d / _outsRecorded,
                    CareerRecordMetric.DefensiveOuts => _fielding.DefensiveOuts,
                    CareerRecordMetric.FieldingOpportunities => _fielding.Opportunities,
                    CareerRecordMetric.SuccessfulFieldingPlays => _fielding.SuccessfulPlays,
                    CareerRecordMetric.Putouts => _fielding.Putouts,
                    CareerRecordMetric.Assists => _fielding.Assists,
                    CareerRecordMetric.Errors => _fielding.Errors,
                    CareerRecordMetric.DoublePlays => _fielding.DoublePlays,
                    CareerRecordMetric.DifficultPlayAttempts => _fielding.DifficultPlayAttempts,
                    CareerRecordMetric.DifficultPlaysMade => _fielding.DifficultPlaysMade,
                    CareerRecordMetric.ExpectedOuts => _fielding.ExpectedOuts,
                    CareerRecordMetric.EstimatedRunsSaved => _fielding.EstimatedRunsSaved,
                    CareerRecordMetric.FieldingSuccessRate => _fielding.SuccessRate,
                    CareerRecordMetric.StolenBases => _stolenBases,
                    CareerRecordMetric.CaughtStealing => _caughtStealing,
                    CareerRecordMetric.StolenBasePercentage => _stolenBases + _caughtStealing == 0
                        ? 0d
                        : _stolenBases / (double)(_stolenBases + _caughtStealing),
                    _ => 0d
                };
            }
        }

        private struct FieldingTotals
        {
            public int DefensiveOuts;
            public int Opportunities;
            public int SuccessfulPlays;
            public int Putouts;
            public int Assists;
            public int Errors;
            public int DoublePlays;
            public int DifficultPlayAttempts;
            public int DifficultPlaysMade;
            public double ExpectedOuts;
            public double EstimatedRunsSaved;
            public double SuccessRate => Opportunities == 0 ? 0d : SuccessfulPlays / (double)Opportunities;

            public void Add(FieldingStatisticsState fielding)
            {
                DefensiveOuts += fielding.DefensiveOuts;
                Opportunities += fielding.Opportunities;
                SuccessfulPlays += fielding.SuccessfulPlays;
                Putouts += fielding.Putouts;
                Assists += fielding.Assists;
                Errors += fielding.Errors;
                DoublePlays += fielding.DoublePlays;
                DifficultPlayAttempts += fielding.DifficultPlayAttempts;
                DifficultPlaysMade += fielding.DifficultPlaysMade;
                ExpectedOuts += fielding.ExpectedOuts;
                EstimatedRunsSaved += fielding.EstimatedRunsSaved;
            }
        }
    }
}
