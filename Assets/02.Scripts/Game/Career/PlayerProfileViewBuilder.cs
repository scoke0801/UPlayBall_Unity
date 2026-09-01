using System;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>세이브 원본과 성장 읽기 모델을 선수 상세 화면용 값으로 투영한다.</summary>
    public sealed class PlayerProfileViewBuilder
    {
        private static readonly PlayerAbility[] BatterAbilities =
        {
            PlayerAbility.Contact,
            PlayerAbility.Power,
            PlayerAbility.Speed,
            PlayerAbility.Arm,
            PlayerAbility.Defense,
            PlayerAbility.BatterMental
        };

        private static readonly PlayerAbility[] PitcherAbilities =
        {
            PlayerAbility.Stamina,
            PlayerAbility.Velocity,
            PlayerAbility.Stuff,
            PlayerAbility.Breaking,
            PlayerAbility.Control,
            PlayerAbility.PitcherMental
        };

        /// <summary>현재 적용 능력치와 계약·기록·성장 상태를 한 화면 모델로 만든다.</summary>
        public PlayerProfileView Build(
            CareerState career,
            int overall,
            PlayerGameRole plannedRole,
            CareerGrowthView growth = null)
        {
            if (career == null)
                throw new ArgumentNullException(nameof(career));
            if (career.CurrentLeague?.CurrentSeason == null)
                throw new InvalidOperationException("진행 중인 시즌이 없습니다.");

            PlayerState player = career.MyPlayer;
            PlayerGrowthState growthState = player.GrowthState;
            SeasonState season = career.CurrentLeague.CurrentSeason;
            PlayerType playerType = IsPitcher(player.PrimaryPosition)
                ? PlayerType.Pitcher
                : PlayerType.Batter;
            TeamState team = FindTeam(career, player.CurrentTeamId);
            PlayerContractState contract = career.CurrentContract;
            CareerRecordCategory recordCategory = playerType == PlayerType.Pitcher
                ? CareerRecordCategory.Pitching
                : CareerRecordCategory.Batting;
            CareerRecordsView records = new CareerRecordsService().Build(career, recordCategory);

            return new PlayerProfileView
            {
                PlayerId = player.PlayerId,
                PlayerName = player.Name,
                Nationality = player.Nationality,
                Age = player.Age,
                PlayerType = playerType,
                Position = player.PrimaryPosition,
                BattingHand = player.BattingHand,
                ThrowingHand = player.ThrowingHand,
                TeamName = team.Name,
                TeamColor = team.PrimaryColor,
                TeamEmblemId = team.EmblemId,
                SeasonYear = season.Year,
                LeagueLevel = season.LeagueLevel,
                Overall = Clamp(overall, 0, 100),
                Condition = player.Condition,
                Fatigue = growthState?.Fatigue ?? 0,
                ManagerEvaluation = player.ManagerEvaluation,
                Durability = growthState?.Durability ?? 0,
                WorkEthic = growthState?.WorkEthic ?? WorkEthicGrade.Normal,
                CareerPhase = growthState?.CareerPhase ?? PlayerGrowthState.GetCareerPhase(player.Age),
                InjuryHistoryCount = growthState?.InjuryHistory.Count ?? 0,
                JoinedYear = contract.SignedYear,
                ProfessionalYears = Math.Max(1, season.Year - contract.SignedYear + 1),
                ContractEndYear = contract.EndYear,
                AnnualSalary = contract.AnnualSalary,
                ExpectedRole = career.CurrentExpectedRole,
                PlannedRole = plannedRole,
                Abilities = BuildAbilities(player, playerType, growth),
                BoardCells = growth?.BoardCells ?? Array.Empty<GrowthBoardCellView>(),
                OwnedBlocks = growth?.OwnedBlocks ?? Array.Empty<GrowthSkillBlockView>(),
                PlacedBlocks = growth?.PlacedBlocks ?? Array.Empty<GrowthSkillBlockView>(),
                AppliedLayout = growth?.AppliedLayout ?? Array.Empty<GrowthBoardLayoutPlacement>(),
                SeasonStatistics = new PlayerProfileStatisticsView(season.PlayerStatistics),
                CareerTotals = records.CareerTotals ?? Array.Empty<CareerRecordMetricValue>(),
                RecentGames = CopyRecentGames(season.PlayerStatistics)
            };
        }

        private static PlayerProfileAbilityView[] BuildAbilities(
            PlayerState player,
            PlayerType playerType,
            CareerGrowthView growth)
        {
            PlayerAbility[] abilities = playerType == PlayerType.Pitcher
                ? PitcherAbilities
                : BatterAbilities;
            int[] baseValues = growth?.BaseAbilities ?? GetBaseAbilities(player);
            int[] stableValues = growth?.StableAbilities ?? baseValues;
            int[] boardBonuses = growth?.BoardBonuses ?? Array.Empty<int>();
            AbilityRatings potential = player.GrowthState?.PotentialByAbility;
            var result = new PlayerProfileAbilityView[abilities.Length];
            for (int index = 0; index < abilities.Length; index++)
            {
                PlayerAbility ability = abilities[index];
                int abilityIndex = (int)ability;
                int baseValue = GetValue(baseValues, abilityIndex);
                int stableValue = GetValue(stableValues, abilityIndex, baseValue);
                int boardBonus = GetValue(boardBonuses, abilityIndex, stableValue - baseValue);
                int potentialValue = potential?.Get(ability) ?? baseValue;
                result[index] = new PlayerProfileAbilityView(
                    ability,
                    baseValue,
                    stableValue,
                    boardBonus,
                    potentialValue);
            }
            return result;
        }

        private static int[] GetBaseAbilities(PlayerState player)
        {
            if (player.GrowthState != null)
                return player.GrowthState.BaseAbilities.ToArray();

            var values = new int[PlayerAbilityCatalog.AbilityCount];
            BatterAttributes batter = player.BatterAttributes;
            PitcherAttributes pitcher = player.PitcherAttributes;
            values[(int)PlayerAbility.Contact] = batter.Contact;
            values[(int)PlayerAbility.Power] = batter.Power;
            values[(int)PlayerAbility.Speed] = batter.Speed;
            values[(int)PlayerAbility.Arm] = batter.Arm;
            values[(int)PlayerAbility.Defense] = batter.Defense;
            values[(int)PlayerAbility.BatterMental] = batter.Mental;
            values[(int)PlayerAbility.Stamina] = pitcher.Stamina;
            values[(int)PlayerAbility.Velocity] = pitcher.Velocity;
            values[(int)PlayerAbility.Stuff] = pitcher.Stuff;
            values[(int)PlayerAbility.Breaking] = pitcher.Breaking;
            values[(int)PlayerAbility.Control] = pitcher.Control;
            values[(int)PlayerAbility.PitcherMental] = pitcher.Mental;
            return values;
        }

        private static PlayerGameLogState[] CopyRecentGames(PlayerSeasonStatisticsState statistics)
        {
            if (statistics == null || statistics.RecentGames.Count == 0)
                return Array.Empty<PlayerGameLogState>();

            var result = new PlayerGameLogState[statistics.RecentGames.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = statistics.RecentGames[result.Length - 1 - index];
            return result;
        }

        private static TeamState FindTeam(CareerState career, int teamId)
        {
            for (int index = 0; index < career.CurrentLeague.Teams.Count; index++)
            {
                TeamState team = career.CurrentLeague.Teams[index];
                if (team.TeamId == teamId)
                    return team;
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }

        private static int GetValue(int[] values, int index, int fallback = 0)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : fallback;
        }

        private static bool IsPitcher(PlayerPosition position)
        {
            return position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
