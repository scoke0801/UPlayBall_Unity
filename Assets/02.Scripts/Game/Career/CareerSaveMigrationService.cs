using System;
using System.Collections.Generic;

namespace Baseball.Game.Career
{
    /// <summary>
    /// v7 단일 리그 커리어를 기존 상태 손실 없이 v8 다중 리그 월드로 승격한다.
    /// </summary>
    public sealed class CareerSaveMigrationService
    {
        private readonly NewGameConfiguration _configuration;

        public CareerSaveMigrationService(NewGameConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public void MigrateV7ToV8(CareerState career, ulong migrationSeed)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (career.SaveVersion != 7)
                throw new InvalidOperationException("SaveVersion 7 커리어만 v8로 마이그레이션할 수 있습니다.");

            LeagueState legacyLeague = career.CurrentLeague;
            NormalizeLegacyContracts(career, legacyLeague.LeagueId);
            WorldState world = new CareerWorldFactory(_configuration).CreateMigratedWorld(
                career.World.WorldSeed,
                migrationSeed,
                legacyLeague,
                career.MyPlayer,
                career.ContractHistory);
            career.UpgradeToWorld(8, world);
            ImportLegacyMovementHistory(career);
            career.World.ValidateInvariants();
        }

        /// <summary>v8 커리어의 월드·기록은 보존하고 현재 시즌에 v9 결산 스냅샷을 복원한다.</summary>
        public void MigrateV8ToV9(CareerState career)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (career.SaveVersion != 8)
                throw new InvalidOperationException("SaveVersion 8 커리어만 v9로 마이그레이션할 수 있습니다.");

            SeasonState season = career.CurrentLeague.CurrentSeason;
            if (CanCaptureSeasonReview(season))
            {
                SeasonReviewSnapshot snapshot = season.ReviewSnapshot ??
                                                SeasonReviewSnapshot.CaptureRegularSeason(career);
                if (season.Postseason?.IsCompleted == true)
                    snapshot.CompletePostseason(career);
                if (season.Settlement.IsApplied && !snapshot.IsSettlementApplied)
                {
                    // v8에는 성장 전 값이 없으므로 현재 값을 양쪽에 넣어 허위 성장량을 만들지 않는다.
                    int[] abilities = career.MyPlayer.GrowthState.BaseAbilities.ToArray();
                    snapshot.CompleteSettlement(season.Settlement, abilities, abilities);
                }
                season.MigrateSeasonReview(snapshot);
            }

            career.UpgradeSaveVersion(9);
        }

        /// <summary>v9 월드·시즌 상태를 보존하고 커리어 내러티브 상태가 포함된 v10으로 승격한다.</summary>
        public void MigrateV9ToV10(CareerState career)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (career.SaveVersion != 9)
                throw new InvalidOperationException("SaveVersion 9 커리어만 v10으로 마이그레이션할 수 있습니다.");
            if (career.Narrative == null)
                throw new InvalidOperationException("v10 승격에 필요한 커리어 내러티브 상태가 없습니다.");

            career.Narrative.UpgradeSaveVersion(10);
            career.UpgradeSaveVersion(10);
        }

        /// <summary>v10 커리어에 기본 생성 프로필과 경기 운영 설정을 부여해 v11로 승격한다.</summary>
        public void MigrateV10ToV11(CareerState career)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (career.SaveVersion != 10)
                throw new InvalidOperationException("SaveVersion 10 커리어만 v11로 마이그레이션할 수 있습니다.");
            if (career.CreationProfile == null || career.GameSettings == null)
                throw new InvalidOperationException("v11 승격에 필요한 기본 생성 프로필을 만들 수 없습니다.");

            career.UpgradeSaveVersion(11);
        }

        /// <summary>v11의 실제 기록은 보존하고 이후 선택만 누적하는 은퇴 회고 상태를 v12로 승격한다.</summary>
        public void MigrateV11ToV12(CareerState career)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (career.SaveVersion != 11)
                throw new InvalidOperationException("SaveVersion 11 커리어만 v12로 마이그레이션할 수 있습니다.");
            if (career.Retirement == null)
                throw new InvalidOperationException("v12 승격에 필요한 은퇴 회고 상태를 만들 수 없습니다.");

            career.Retirement.UpgradeSaveVersion(12);
            career.UpgradeSaveVersion(12);
        }

        private static bool CanCaptureSeasonReview(SeasonState season)
        {
            return season?.TeamRecords != null && season.Phase is
                SeasonPhase.Postseason or
                SeasonPhase.SeasonReview or
                SeasonPhase.Offseason or
                SeasonPhase.Completed;
        }

        private static void NormalizeLegacyContracts(CareerState career, LeagueId leagueId)
        {
            var usedContractIds = new HashSet<int>();
            int nextContractId = 1;
            for (int index = 0; index < career.ContractHistory.Count; index++)
            {
                int contractId = career.ContractHistory[index].ContractId;
                if (contractId <= 0)
                    continue;
                if (!usedContractIds.Add(contractId))
                    throw new InvalidOperationException($"기존 ContractId {contractId}가 중복되었습니다.");
                nextContractId = Math.Max(nextContractId, contractId + 1);
            }

            for (int index = 0; index < career.ContractHistory.Count; index++)
            {
                PlayerContractState contract = career.ContractHistory[index];
                int contractId = contract.ContractId > 0 ? contract.ContractId : nextContractId;
                if (contract.ContractId <= 0)
                {
                    usedContractIds.Add(contractId);
                    nextContractId++;
                }
                contract.MigrateLegacyIdentity(
                    contractId,
                    career.MyPlayerId,
                    leagueId,
                    ReferenceEquals(contract, career.CurrentContract));
            }

            career.MyPlayer.ReplaceActiveContract(
                career.CurrentContract.ContractId,
                leagueId);
        }

        private static void ImportLegacyMovementHistory(CareerState career)
        {
            for (int index = 0; index < career.TradeState.History.Count; index++)
            {
                TradeHistoryRecord history = career.TradeState.History[index];
                DateTime movementDate = new DateTime(history.Year, 1, 1).AddDays(history.GameIndex);
                career.World.MovementLedger.Record(new PlayerMovementRecord(
                    movementDate,
                    history.SeasonId,
                    career.MyPlayer.PlayerId,
                    PlayerMovementType.Trade,
                    career.CurrentLeague.LeagueId,
                    history.PreviousTeamId,
                    career.CurrentLeague.LeagueId,
                    history.NewTeamId,
                    history.PreviousRole,
                    career.CurrentContract.PromisedRole,
                    history.ProjectedRole,
                    career.CurrentContract.ContractId,
                    "LegacySingleLeague 트레이드 이력"));
                if (movementDate > career.World.Calendar.CurrentDate)
                    career.World.Calendar.AdvanceTo(movementDate);
            }
        }
    }
}
