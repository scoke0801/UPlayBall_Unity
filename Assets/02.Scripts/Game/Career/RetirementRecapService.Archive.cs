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
        private int CalculateOverall(PlayerState player, int[] abilities)
        {
            var ratings = new AbilityRatings(abilities);
            var snapshot = new Player(
                player.PlayerId,
                player.Name,
                player.PrimaryPosition,
                player.BattingHand,
                player.ThrowingHand,
                ratings.ToBatterAttributes(),
                ratings.ToPitcherAttributes(),
                nationality: player.Nationality);
            return new PlayerValueEvaluator(_balance.PlayerEvaluation).CalculatePositionValue(snapshot);
        }

        private SkillBoardSeasonSnapshot BuildSkillBoardSnapshot(SkillBoardState board)
        {
            var blocks = new SkillBlockArchiveSnapshot[board.PlacedBlocks.Count];
            for (int index = 0; index < blocks.Length; index++)
            {
                PlacedSkillBlock placement = board.PlacedBlocks[index];
                SkillBlockDefinition definition = FindSkillDefinition(placement.Instance.DefinitionId);
                blocks[index] = new SkillBlockArchiveSnapshot(
                    placement.Instance.InstanceId,
                    placement.Instance.DefinitionId,
                    definition?.Rarity ?? SkillBlockRarity.Normal,
                    definition?.Category ?? SkillBlockCategory.Contact,
                    placement.OriginX,
                    placement.OriginY,
                    placement.RotationQuarterTurns);
            }
            return new SkillBoardSeasonSnapshot(board.BoardDefinitionId, blocks);
        }

        private SkillBlockDefinition FindSkillDefinition(string definitionId)
        {
            for (int index = 0; index < _balance.Growth.SkillBlocks.Length; index++)
            {
                if (string.Equals(_balance.Growth.SkillBlocks[index].BlockId, definitionId, StringComparison.Ordinal))
                    return _balance.Growth.SkillBlocks[index];
            }
            return null;
        }

        private static int[] GetCurrentAbilities(PlayerState player)
        {
            if (player.GrowthState != null)
                return player.GrowthState.BaseAbilities.ToArray();
            var result = new int[(int)PlayerAbility.Count];
            BatterAttributes batter = player.BatterAttributes;
            PitcherAttributes pitcher = player.PitcherAttributes;
            result[(int)PlayerAbility.Contact] = batter.Contact;
            result[(int)PlayerAbility.Power] = batter.Power;
            result[(int)PlayerAbility.Speed] = batter.Speed;
            result[(int)PlayerAbility.Arm] = batter.Arm;
            result[(int)PlayerAbility.Defense] = batter.Defense;
            result[(int)PlayerAbility.BatterMental] = batter.Mental;
            result[(int)PlayerAbility.Stamina] = pitcher.Stamina;
            result[(int)PlayerAbility.Velocity] = pitcher.Velocity;
            result[(int)PlayerAbility.Stuff] = pitcher.Stuff;
            result[(int)PlayerAbility.Breaking] = pitcher.Breaking;
            result[(int)PlayerAbility.Control] = pitcher.Control;
            result[(int)PlayerAbility.PitcherMental] = pitcher.Mental;
            for (int index = 0; index < result.Length; index++)
            {
                if (result[index] < AbilityRatings.Minimum) result[index] = AbilityRatings.Minimum;
                else if (result[index] > AbilityRatings.Maximum) result[index] = AbilityRatings.Maximum;
            }
            return result;
        }

        private static int[] CalculateSeasonStartAbilities(PlayerState player, int year, int[] endAbilities)
        {
            var start = (int[])endAbilities.Clone();
            if (player.GrowthState == null)
                return start;
            for (int recordIndex = 0; recordIndex < player.GrowthState.GrowthHistory.Count; recordIndex++)
            {
                GrowthResultRecord record = player.GrowthState.GrowthHistory[recordIndex];
                if (record.SeasonYear != year)
                    continue;
                for (int changeIndex = 0; changeIndex < record.AbilityChanges.Length; changeIndex++)
                {
                    AbilityChange change = record.AbilityChanges[changeIndex];
                    start[(int)change.Ability] -= change.Amount;
                }
            }
            for (int index = 0; index < start.Length; index++)
            {
                if (start[index] < AbilityRatings.Minimum) start[index] = AbilityRatings.Minimum;
                else if (start[index] > AbilityRatings.Maximum) start[index] = AbilityRatings.Maximum;
            }
            return start;
        }

        private static InjurySeasonSnapshot BuildInjurySnapshot(PlayerState player, int year)
        {
            if (player.GrowthState == null)
                return new InjurySeasonSnapshot(Array.Empty<InjuryRecordSnapshot>());
            int count = 0;
            for (int index = 0; index < player.GrowthState.InjuryHistory.Count; index++)
            {
                if (player.GrowthState.InjuryHistory[index].SeasonYear == year) count++;
            }
            var injuries = new InjuryRecordSnapshot[count];
            int target = 0;
            for (int index = 0; index < player.GrowthState.InjuryHistory.Count; index++)
            {
                InjuryRecord injury = player.GrowthState.InjuryHistory[index];
                if (injury.SeasonYear == year)
                    injuries[target++] = new InjuryRecordSnapshot(injury);
            }
            return new InjurySeasonSnapshot(injuries);
        }

        private static PlayerGameRole SelectPrimaryRole(
            PlayerPosition position,
            CareerSeasonExperienceState experience,
            PlayerSeasonStatisticsState statistics)
        {
            if (experience != null)
            {
                int best = 0;
                int bestCount = -1;
                for (int index = 0; index < 6; index++)
                {
                    int count = experience.GetRoleCount((PlayerGameRole)index);
                    if (count > bestCount)
                    {
                        best = index;
                        bestCount = count;
                    }
                }
                if (bestCount > 0)
                    return (PlayerGameRole)best;
            }
            if (position == PlayerPosition.StartingPitcher) return PlayerGameRole.StartingPitcher;
            if (position == PlayerPosition.ReliefPitcher) return PlayerGameRole.ReliefPitcher;
            return statistics?.GamesStarted > 0 ? PlayerGameRole.StartingBatter : PlayerGameRole.Bench;
        }

        private static PlayerContractState FindSeasonContract(CareerState career, int year)
        {
            for (int index = career.ContractHistory.Count - 1; index >= 0; index--)
            {
                PlayerContractState contract = career.ContractHistory[index];
                if (contract.SignedYear <= year && contract.EndYear >= year)
                    return contract;
            }
            return career.CurrentContract;
        }

        private static string[] BuildAwardKeys(CareerState career, SeasonAwardsState awards)
        {
            if (awards == null)
                return Array.Empty<string>();
            var result = new List<string>();
            for (int index = 0; index < awards.Results.Count; index++)
            {
                SeasonAwardResultState award = awards.Results[index];
                if (award.IncludesWinner(career.MyPlayerId))
                    result.Add(award.AwardId);
            }
            result.Sort(StringComparer.Ordinal);
            if (career.CurrentLeague.CurrentSeason.Postseason?.PlayerTeamResult ==
                PlayerTeamPostseasonResult.Champion)
            {
                result.Add("champion");
            }
            return result.ToArray();
        }

        private static CareerSeasonArchive[] CopySeasons(IReadOnlyList<CareerSeasonArchive> source)
        {
            var result = new CareerSeasonArchive[source.Count];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }

        private static SeasonAbilitySnapshot[] BuildAbilitySnapshots(int[] start, int[] end)
        {
            var result = new SeasonAbilitySnapshot[(int)PlayerAbility.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = new SeasonAbilitySnapshot((PlayerAbility)index, start[index], end[index]);
            return result;
        }

        private static CareerNamedCount[] CopyTrainingCounts(CareerSeasonExperienceState experience)
        {
            if (experience == null)
                return Array.Empty<CareerNamedCount>();
            var result = new CareerNamedCount[experience.TrainingCounts.Count];
            for (int index = 0; index < result.Length; index++) result[index] = experience.TrainingCounts[index];
            return result;
        }

        private static string[] BuildSeasonMemoryIds(CareerMemoryLog log, int year)
        {
            var result = new List<string>();
            for (int index = 0; index < log.Records.Count; index++)
            {
                if (log.Records[index].Season == year) result.Add(log.Records[index].MemoryId);
            }
            return result.ToArray();
        }
    }
}
