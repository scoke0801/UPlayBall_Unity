using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>세 리그 AI 선수의 실제 출장량을 영구 성장·노쇠·커리어 기록에 일괄 반영한다.</summary>
    public sealed class WorldAiPlayerDevelopmentService
    {
        private const ulong NaturalDevelopmentStream = 0x41494E4154555241UL;
        private const ulong AgingStream = 0x41494147494E4721UL;

        private readonly CareerState _career;
        private readonly CareerSeasonUsageSummaryBuilder _usageBuilder;
        private readonly NaturalDevelopmentResolver _naturalDevelopmentResolver;
        private readonly AgingResolver _agingResolver;

        public WorldAiPlayerDevelopmentService(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            _usageBuilder = new CareerSeasonUsageSummaryBuilder(
                balance.PlayerEvaluation,
                balance.CareerSeason.StartingRotationSize);
            _naturalDevelopmentResolver = new NaturalDevelopmentResolver(balance.Growth);
            _agingResolver = new AgingResolver(balance.Growth);
        }

        /// <summary>확정된 각 리그 정규시즌 기록을 같은 순서와 사건별 Seed로 한 번만 결산한다.</summary>
        public void SettleCompletedSeasonPlayers()
        {
            IReadOnlyList<LeagueState> leagues = _career.World.Leagues;
            for (int leagueIndex = 0; leagueIndex < leagues.Count; leagueIndex++)
            {
                LeagueState league = leagues[leagueIndex];
                SeasonState season = league.CurrentSeason ??
                                     throw new InvalidOperationException($"{league.LeagueId}의 현재 시즌이 없습니다.");
                SettleLeague(league, season);
            }
        }

        private void SettleLeague(LeagueState league, SeasonState season)
        {
            for (int teamIndex = 0; teamIndex < league.Teams.Count; teamIndex++)
            {
                TeamState team = league.Teams[teamIndex];
                for (int rosterIndex = 0; rosterIndex < team.RosterCompetitors.Count; rosterIndex++)
                {
                    RosterCompetitorState competitor = team.RosterCompetitors[rosterIndex];
                    if (competitor.PlayerId == _career.MyPlayer.PlayerId)
                        continue;

                    PlayerState player = _career.World.GetPlayer(competitor.PlayerId);
                    PlayerGrowthState growth = player.GrowthState ??
                                               throw new InvalidOperationException(
                                                   $"AI PlayerId {player.PlayerId}의 성장 상태가 없습니다.");
                    PlayerCompetitionStatisticsState statistics =
                        season.LeagueStatistics.RegularSeason.GetPlayer(player.PlayerId) ??
                        new PlayerCompetitionStatisticsState(
                            player.PlayerId,
                            player.Name,
                            player.CurrentTeamId,
                            player.PrimaryPosition);
                    SeasonUsageSummary usage = _usageBuilder.Build(player.PrimaryPosition, statistics);
                    ulong playerStream = ((ulong)(uint)season.SeasonId << 32) | (uint)player.PlayerId;
                    ulong naturalSeed = DeterministicSeed.Derive(
                        league.RandomSeed,
                        playerStream ^ NaturalDevelopmentStream);
                    ulong agingSeed = DeterministicSeed.Derive(
                        league.RandomSeed,
                        playerStream ^ AgingStream);

                    _naturalDevelopmentResolver.Resolve(
                        growth,
                        usage,
                        season.Year,
                        naturalSeed,
                        new Pcg32Random(naturalSeed));
                    _agingResolver.Resolve(
                        growth,
                        season.Year,
                        agingSeed,
                        new Pcg32Random(agingSeed));
                    player.RecordAiSeasonStatistics(
                        season.Year,
                        statistics.Batting.PlateAppearances,
                        statistics.Pitching.OutsRecorded);
                    player.SynchronizeFromGrowthState();
                }
            }
        }
    }
}
