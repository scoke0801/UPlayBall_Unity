namespace Baseball.Game.Career
{
    public sealed partial class CareerRecordsService
    {
        private sealed class PlayerStatisticsTotals
        {
            private int _games;
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
            private int _stolenBases;
            private int _caughtStealing;
            private int _pitchingAppearances;
            private int _pitchingStarts;
            private int _outsRecorded;
            private int _wins;
            private int _losses;
            private int _saves;
            private int _holds;
            private int _hitsAllowed;
            private int _earnedRuns;
            private int _walksAllowed;
            private int _pitchingStrikeouts;

            public void Add(PlayerSeasonStatisticsState statistics)
            {
                if (statistics == null)
                    return;
                _games += statistics.GamesPlayed;
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
                _stolenBases += statistics.StolenBases;
                _caughtStealing += statistics.CaughtStealing;
                _pitchingAppearances += statistics.PitchingAppearances;
                _pitchingStarts += statistics.PitchingStarts;
                _outsRecorded += statistics.OutsRecorded;
                _wins += statistics.Wins;
                _losses += statistics.Losses;
                _saves += statistics.Saves;
                _holds += statistics.Holds;
                _hitsAllowed += statistics.HitsAllowed;
                _earnedRuns += statistics.EarnedRuns;
                _walksAllowed += statistics.WalksAllowed;
                _pitchingStrikeouts += statistics.PitchingStrikeouts;
            }

            public double GetValue(CareerRecordMetric metric)
            {
                int totalBases = _hits + _doubles + _triples * 2 + _homeRuns * 3;
                return metric switch
                {
                    CareerRecordMetric.Games => _games,
                    CareerRecordMetric.AtBats => _atBats,
                    CareerRecordMetric.Runs => _runs,
                    CareerRecordMetric.Hits => _hits,
                    CareerRecordMetric.Doubles => _doubles,
                    CareerRecordMetric.Triples => _triples,
                    CareerRecordMetric.HomeRuns => _homeRuns,
                    CareerRecordMetric.RunsBattedIn => _runsBattedIn,
                    CareerRecordMetric.Walks => _walks,
                    CareerRecordMetric.BattingStrikeouts => _battingStrikeouts,
                    CareerRecordMetric.BattingAverage => _atBats == 0 ? 0d : _hits / (double)_atBats,
                    CareerRecordMetric.OnBasePercentage => _plateAppearances == 0
                        ? 0d
                        : (_hits + _walks + _hitByPitches) / (double)_plateAppearances,
                    CareerRecordMetric.SluggingPercentage => _atBats == 0 ? 0d : totalBases / (double)_atBats,
                    CareerRecordMetric.OnBasePlusSlugging =>
                        (_plateAppearances == 0 ? 0d : (_hits + _walks + _hitByPitches) / (double)_plateAppearances) +
                        (_atBats == 0 ? 0d : totalBases / (double)_atBats),
                    CareerRecordMetric.PitchingAppearances => _pitchingAppearances,
                    CareerRecordMetric.PitchingStarts => _pitchingStarts,
                    CareerRecordMetric.OutsRecorded => _outsRecorded,
                    CareerRecordMetric.Wins => _wins,
                    CareerRecordMetric.Losses => _losses,
                    CareerRecordMetric.Saves => _saves,
                    CareerRecordMetric.Holds => _holds,
                    CareerRecordMetric.HitsAllowed => _hitsAllowed,
                    CareerRecordMetric.EarnedRuns => _earnedRuns,
                    CareerRecordMetric.WalksAllowed => _walksAllowed,
                    CareerRecordMetric.PitchingStrikeouts => _pitchingStrikeouts,
                    CareerRecordMetric.EarnedRunAverage => _outsRecorded == 0
                        ? 0d
                        : _earnedRuns * 27d / _outsRecorded,
                    CareerRecordMetric.WalksHitsPerInningPitched => _outsRecorded == 0
                        ? 0d
                        : (_walksAllowed + _hitsAllowed) * 3d / _outsRecorded,
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
            public int Opportunities;
            public int SuccessfulPlays;
            public int Putouts;
            public int Assists;
            public int Errors;
            public int DoublePlays;
            public double EstimatedRunsSaved;
            public double SuccessRate => Opportunities == 0 ? 0d : SuccessfulPlays / (double)Opportunities;

            public void Add(FieldingStatisticsState fielding)
            {
                Opportunities += fielding.Opportunities;
                SuccessfulPlays += fielding.SuccessfulPlays;
                Putouts += fielding.Putouts;
                Assists += fielding.Assists;
                Errors += fielding.Errors;
                DoublePlays += fielding.DoublePlays;
                EstimatedRunsSaved += fielding.EstimatedRunsSaved;
            }
        }
    }
}
