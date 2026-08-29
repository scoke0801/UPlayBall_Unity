using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    public sealed partial class RetirementRecapService
    {
        /// <summary>확정된 한 경기의 실제 기용·선택·기록에서 최초 기록과 대표 경기 후보를 누적한다.</summary>
        public void RecordCompletedGame(CareerState career, CareerMatchSession session)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!session.IsCommitted || !session.CareerResult.HasValue)
                throw new InvalidOperationException("커리어에 반영된 경기만 기억으로 기록할 수 있습니다.");

            RecordCompletedGame(career, session.CareerResult.Value, session);
        }

        /// <summary>즉시 진행 경로의 확정 경기에서도 선택 로그를 제외한 동일한 사실을 누적한다.</summary>
        public void RecordCompletedGame(CareerState career, CareerGameAdvanceResult result)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            RecordCompletedGame(career, result, session: null);
        }

        private void RecordCompletedGame(
            CareerState career,
            CareerGameAdvanceResult result,
            CareerMatchSession session)
        {
            SeasonState season = career.CurrentLeague.CurrentSeason;
            CareerMemoryLog log = career.Retirement.MemoryLog;
            CareerSeasonExperienceState experience = log.GetOrCreateSeason(season.SeasonId, season.Year);
            experience.RecordRole(result.Role);
            for (int index = 0; index < 6; index++)
            {
                int battingCount = session?.GetBattingApproachCount((BattingApproach)index) ?? 0;
                int pitchingCount = session?.GetPitchingApproachCount((PitchingApproach)index) ?? 0;
                if (battingCount > 0)
                    experience.RecordBattingApproach((BattingApproach)index, battingCount);
                if (pitchingCount > 0)
                    experience.RecordPitchingApproach((PitchingApproach)index, pitchingCount);
            }

            int teamId = career.MyPlayer.CurrentTeamId;
            int year = season.Year;
            if (HasOfficialAppearance(result))
                career.Retirement.RecordOfficialGame(result.GameId, year, result.Round, teamId);
            if (HasOfficialAppearance(result) && !log.ContainsType(CareerMemoryType.CareerDebut))
            {
                log.Append(CreateGameMemory(
                    career, result, year, teamId, CareerMemoryType.CareerDebut,
                    "career.memory.debut.title", "career.memory.debut.narrative",
                    90, 90, 20, 95, 85, "debut"));
            }
            if (result.Hits > 0 && !log.ContainsType(CareerMemoryType.FirstHit))
            {
                log.Append(CreateGameMemory(
                    career, result, year, teamId, CareerMemoryType.FirstHit,
                    "career.memory.first_hit.title", "career.memory.first_hit.narrative",
                    82, 75, 20, 80, 75, "first_record"));
            }
            if (result.HomeRuns > 0 && !log.ContainsType(CareerMemoryType.FirstHomeRun))
            {
                log.Append(CreateGameMemory(
                    career, result, year, teamId, CareerMemoryType.FirstHomeRun,
                    "career.memory.first_home_run.title", "career.memory.first_home_run.narrative",
                    86, 78, 20, 82, 82, "first_record"));
            }
            if (result.Role is PlayerGameRole.StartingBatter or PlayerGameRole.StartingPitcher &&
                !log.ContainsType(CareerMemoryType.RoleBreakthrough))
            {
                log.Append(CreateGameMemory(
                    career, result, year, teamId, CareerMemoryType.RoleBreakthrough,
                    "career.memory.first_start.title", "career.memory.first_start.narrative",
                    88, 92, 25, 72, 84, "role_change"));
            }
            if (IsExceptionalGame(career.MyPlayer.PrimaryPosition, result))
            {
                log.Append(CreateGameMemory(
                    career, result, year, teamId, CareerMemoryType.ExceptionalGame,
                    "career.memory.exceptional_game.title", "career.memory.exceptional_game.narrative",
                    92, 70, 35, 88, 90, "career_high_game"));
            }
        }

        /// <summary>완료된 훈련 결과를 선택 횟수와 성장 비용에 정확히 한 번 누적한다.</summary>
        public void RecordGrowthResult(CareerState career, GrowthResultRecord result)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.SourceType is not (GrowthSourceType.PersonalTraining or
                GrowthSourceType.TrainingPartner or GrowthSourceType.Study))
            {
                return;
            }

            SeasonState season = career.CurrentLeague.CurrentSeason;
            CareerMemoryLog log = career.Retirement.MemoryLog;
            CareerSeasonExperienceState experience = log.GetOrCreateSeason(season.SeasonId, season.Year);
            bool isStudy = result.SourceType == GrowthSourceType.Study;
            experience.RecordTraining(result.SourceId, result.MoneySpent, isStudy);
            int activityDateIndex = OffseasonDateIndex + CountTraining(experience);

            if (isStudy)
            {
                log.Append(new CareerMemoryRecord(
                    $"study:{result.SeasonYear}:{result.SourceId}:{experience.StudyCount}",
                    career.MyPlayerId,
                    result.SeasonYear,
                    activityDateIndex,
                    career.Retirement.IsRetired ? career.Retirement.LastTeamId : career.MyPlayer.CurrentTeamId,
                    CareerMemoryType.Study,
                    "career.memory.study.title",
                    "career.memory.study.narrative",
                    0,
                    string.Empty,
                    0,
                    68,
                    72,
                    100,
                    65,
                    60,
                    BuildGrowthStats(result),
                    new[] { "player_choice", "growth" },
                    "career_study"));
            }

            if (result.InjuryResult != GrowthInjuryResult.None)
            {
                int injurySequence = CountType(log, CareerMemoryType.Injury) + 1;
                log.Append(new CareerMemoryRecord(
                    $"injury:{result.SeasonYear}:{injurySequence}",
                    career.MyPlayerId,
                    result.SeasonYear,
                    activityDateIndex,
                    career.MyPlayer.CurrentTeamId,
                    CareerMemoryType.Injury,
                    "career.memory.injury.title",
                    "career.memory.injury.narrative",
                    0,
                    string.Empty,
                    0,
                    76,
                    70,
                    35,
                    48,
                    82,
                    new[] { new MemoryStatValue("weeks", result.WeeksSpent) },
                    new[] { "adversity", "growth" },
                    "career_injury"));
            }
        }

        /// <summary>계약 수락·거절처럼 플레이어가 직접 내린 결정을 회고 후보로 저장한다.</summary>
        public void RecordContractChoice(
            CareerState career,
            ContractOffer offer,
            bool isAccepted,
            bool isCurrentTeamOffer)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            SeasonState season = career.CurrentLeague.CurrentSeason;
            CareerMemoryType type = isAccepted
                ? CareerMemoryType.ContractAccepted
                : CareerMemoryType.ContractDeclined;
            string result = isAccepted ? "accepted" : "declined";
            int sequence = CountType(career.Retirement.MemoryLog, type) + 1;
            int dateIndex = season.Phase == SeasonPhase.RegularSeason
                ? GetCurrentDateIndex(season)
                : OffseasonDateIndex + 100 + sequence;
            career.Retirement.MemoryLog.Append(new CareerMemoryRecord(
                $"contract:{season.Year}:{offer.Team.TeamId}:{result}:{sequence}",
                career.MyPlayerId,
                season.Year,
                dateIndex,
                career.MyPlayer.CurrentTeamId,
                type,
                $"career.memory.contract_{result}.title",
                $"career.memory.contract_{result}.narrative",
                0,
                string.Empty,
                0,
                isAccepted ? 82 : 72,
                isAccepted ? 88 : 65,
                100,
                55,
                70,
                new[]
                {
                    new MemoryStatValue("annual_salary", offer.AnnualSalary, "money"),
                    new MemoryStatValue("contract_years", offer.ContractYears),
                    new MemoryStatValue("offer_score", offer.OfferScore, "decimal")
                },
                isCurrentTeamOffer
                    ? new[] { "player_choice", "contract", "current_team" }
                    : new[] { "player_choice", "contract", "movement" },
                "career_contract"));

            if (isAccepted && offer.Team.TeamId != career.MyPlayer.CurrentTeamId)
            {
                career.Retirement.MemoryLog.Append(new CareerMemoryRecord(
                    $"transfer:{season.Year}:{offer.Team.TeamId}:{sequence}",
                    career.MyPlayerId,
                    season.Year,
                    dateIndex,
                    career.MyPlayer.CurrentTeamId,
                    CareerMemoryType.Transfer,
                    "career.memory.transfer.title",
                    "career.memory.transfer.narrative",
                    0,
                    string.Empty,
                    0,
                    82,
                    90,
                    100,
                    65,
                    78,
                    new[]
                    {
                        new MemoryStatValue("from_team_id", career.MyPlayer.CurrentTeamId),
                        new MemoryStatValue("to_team_id", offer.Team.TeamId)
                    },
                    new[] { "player_choice", "movement" },
                    "career_transfer"));
            }
        }

        /// <summary>정규 시즌 중 플레이어가 구단에 전달한 트레이드 의사를 선택 기록으로 남긴다.</summary>
        public void RecordTradePreference(CareerState career, TradePreference preference)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (preference == TradePreference.Neutral)
                return;

            SeasonState season = career.CurrentLeague.CurrentSeason;
            int sequence = CountType(career.Retirement.MemoryLog, CareerMemoryType.TradePreference) + 1;
            career.Retirement.MemoryLog.Append(new CareerMemoryRecord(
                $"trade_preference:{season.Year}:{GetCurrentDateIndex(season)}:{sequence}",
                career.MyPlayerId,
                season.Year,
                GetCurrentDateIndex(season),
                career.MyPlayer.CurrentTeamId,
                CareerMemoryType.TradePreference,
                "career.memory.trade_preference.title",
                "career.memory.trade_preference.narrative",
                0,
                string.Empty,
                0,
                55,
                65,
                100,
                35,
                58,
                new[] { new MemoryStatValue("preference", (int)preference) },
                new[] { "player_choice", "movement" },
                "career_contract"));
        }

        /// <summary>시즌이 완전히 끝난 시점의 카드·성장판·계약·부상을 Archive에 고정한다.</summary>
        public CareerSeasonArchive ArchiveCompletedSeason(CareerState career, TeamState team)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (team == null) throw new ArgumentNullException(nameof(team));
            SeasonState season = career.CurrentLeague.CurrentSeason;
            for (int index = 0; index < career.Retirement.Seasons.Count; index++)
            {
                if (career.Retirement.Seasons[index].SeasonId == season.SeasonId)
                    return career.Retirement.Seasons[index];
            }

            RecordSeasonOutcomeMemories(career, season, team.TeamId);
            CareerSeasonExperienceState experience = career.Retirement.MemoryLog.FindSeason(season.SeasonId);
            int[] endAbilities = GetCurrentAbilities(career.MyPlayer);
            int[] startAbilities = CalculateSeasonStartAbilities(career.MyPlayer, season.Year, endAbilities);
            SeasonAbilitySnapshot[] abilitySnapshots = BuildAbilitySnapshots(startAbilities, endAbilities);
            CareerNamedCount[] trainingCounts = CopyTrainingCounts(experience);
            var growth = new GrowthSeasonSnapshot(
                abilitySnapshots,
                trainingCounts,
                experience?.GrowthMoneySpent ?? 0L,
                experience?.StudyCount ?? 0);
            var archive = new CareerSeasonArchive(
                season.SeasonId,
                season.Year,
                career.MyPlayer.Age,
                season.LeagueLevel,
                team.TeamId,
                team.Name,
                SelectPrimaryRole(career.MyPlayer.PrimaryPosition, experience, season.PlayerStatistics),
                CalculateOverall(career.MyPlayer, startAbilities),
                CalculateOverall(career.MyPlayer, endAbilities),
                new SeasonStatSnapshot(season.PlayerStatistics),
                new SeasonStatSnapshot(season.PostseasonPlayerStatistics),
                BuildAwardKeys(career, season.Awards),
                new ContractSeasonSnapshot(FindSeasonContract(career, season.Year)),
                growth,
                BuildInjurySnapshot(career.MyPlayer, season.Year),
                new PlayStyleSeasonSnapshot(experience),
                BuildSkillBoardSnapshot(career.MyPlayer.SkillBoardState),
                BuildSeasonMemoryIds(career.Retirement.MemoryLog, season.Year));
            career.Retirement.AddSeason(archive);
            return archive;
        }

        /// <summary>모든 대표 순간·통산 기록·칭호·유산을 은퇴 순간의 값으로 한 번만 확정한다.</summary>
    }
}

