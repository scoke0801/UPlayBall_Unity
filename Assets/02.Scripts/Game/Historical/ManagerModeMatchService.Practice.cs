using System;
using Baseball.Core.Historical;
using Baseball.Core.Rules;
using Baseball.Game.Career;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Game.Historical
{
    public sealed partial class ManagerModeMatchService
    {
        /// <summary>연습경기 전력 비교도 실제 선발·성장·팀컬러가 반영된 경기 입력을 읽는다.</summary>
        public MatchRosterSnapshot CreatePracticePlayerRoster(ManagerHistoricalRuntimeState runtime)
        {
            var mode = runtime.ManagerMode; var preset = mode.GetSelectedLineupPreset();
            var validation = _presetValidator.Validate(preset, CreateValidationContext(runtime, runtime.PlayerTeamSeasonKey));
            if (!validation.CanStartGame) throw new InvalidOperationException("선수 오더에서 출전 편성을 확인해 주세요.");
            var plan = new PreGamePlanSnapshot(1, runtime.PlayerTeamSeasonKey, preset, validation);
            return BuildTeam(runtime, runtime.PlayerTeamSeasonKey, plan, mode.LiveSeason.NextPlayerGame?.Round ?? 1,
                PlayerIdMap.Create(runtime)).Roster;
        }
        /// <summary>현재 편성을 상세 엔진에 전달하되 정규 일정·피로·재화·전술 수량은 변경하지 않는다.</summary>
        public ManagerModeMatchResult PlayPractice(ManagerHistoricalRuntimeState runtime,
            MatchRosterSnapshot opponent, int attempt, ulong seed, IMatchEventSink sink)
        {
            var mode = runtime.ManagerMode;
            var preset = mode.GetSelectedLineupPreset();
            var validation = _presetValidator.Validate(preset, CreateValidationContext(runtime, runtime.PlayerTeamSeasonKey));
            if (!validation.CanStartGame) throw new InvalidOperationException("선수 오더에서 출전 편성을 확인해 주세요.");
            var plan = new PreGamePlanSnapshot(attempt, runtime.PlayerTeamSeasonKey, preset, validation);
            int rotation = mode.LiveSeason.NextPlayerGame?.Round ?? 1;
            var player = BuildTeam(runtime, runtime.PlayerTeamSeasonKey, plan, rotation, PlayerIdMap.Create(runtime));
            var configuration = new HistoricalMatchConfiguration(_balance.HistoricalAssignment.CreateRule(),
                homeTacticLoadout: CreateConfirmedLoadout(plan.TacticCardIds));
            var input = new MatchInput(mode.LiveSeason.OriginYear, attempt, seed, opponent, player.Roster,
                new MatchRules(9, 0, ExtraInningPolicy.DrawAtLimit, 10, true, 0),
                SimulationRulesVersion.DetailedV2,
                SimulationVersionStamp.CreateCurrent(_balance.Version, _content.Manifest.ContentHash,
                    (int)SimulationRulesVersion.DetailedV2), configuration);
            var profile = new MatchExecutionProfile(SimulationEngineKind.Detailed, MatchDecisionMode.InternalAiOnly,
                MatchEventMode.Full, MatchDecisionTraceMode.Full, MatchStatisticsMode.FullBoxScore);
            var match = new MatchSimulator(_balance, MatchRandomStreams.Create(seed)).Simulate(input, sink, profile);
            return new ManagerModeMatchResult(match, plan, player.LineupChemistry,
                HomeGameFinanceResult.CreateNotHomeGame("practice:" + attempt), default,
                mode.Dugout.ManagerId, mode.Dugout.HeadCoachId, player.Roster.ManagerProfile,
                _dugoutCatalog.GetManager(mode.Dugout.ManagerId).DisplayName,
                _dugoutCatalog.GetHeadCoach(mode.Dugout.HeadCoachId).DisplayName);
        }
    }
}
