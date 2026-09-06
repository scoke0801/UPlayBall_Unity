using System;
using System.IO;
using Baseball.Core.Balance;
using Baseball.Game.Career.Persistence;
using Baseball.Game.Data;
using Baseball.Game.Historical;
using Baseball.Game.Unity.Persistence;
using UnityEngine;

namespace Baseball.Game.Career
{
    public sealed partial class CareerManager
    {
        private CareerSaveJsonStore _careerSaveStore;
        private CareerSaveAdapter _careerSaveAdapter;
        private CareerSaveContentReference _careerSaveContent;
        private BalanceTable _persistenceBalance;

        /// <summary>설정 화면에 표시할 수동 슬롯과 자동 백업 상태를 읽는다.</summary>
        public CareerSaveSlotView InspectCareerSave()
        {
            try
            {
                EnsurePersistenceStore();
                if (!_careerSaveStore.Exists)
                {
                    return new CareerSaveSlotView(
                        CareerSaveSlotStatus.Empty,
                        null,
                        _careerSaveStore.BackupExists
                            ? "수동 저장은 없지만 복구 가능한 자동 백업이 있습니다."
                            : "저장된 선수 커리어가 없습니다.",
                        _careerSaveStore.BackupExists);
                }

                EnsurePersistenceContext();
                CareerSaveData saveData = _careerSaveStore.LoadPrimary();
                CareerSaveCompatibilityValidator.Validate(saveData, _careerSaveContent);
                return new CareerSaveSlotView(
                    CareerSaveSlotStatus.Ready,
                    saveData.summary,
                    "불러올 수 있는 선수 커리어입니다.",
                    _careerSaveStore.BackupExists);
            }
            catch (CareerSaveCompatibilityException exception)
            {
                return new CareerSaveSlotView(
                    CareerSaveSlotStatus.Incompatible,
                    null,
                    exception.Message,
                    _careerSaveStore?.BackupExists == true);
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                return new CareerSaveSlotView(
                    CareerSaveSlotStatus.Damaged,
                    null,
                    GetPersistenceFailureMessage(exception, "저장 데이터를 읽지 못했습니다."),
                    _careerSaveStore?.BackupExists == true);
            }
        }

        /// <summary>현재 커리어를 단일 수동 슬롯에 저장하고 기존 파일을 자동 백업으로 남긴다.</summary>
        public CareerSaveCommandResult SaveCareer()
        {
            if (CurrentCareer == null)
                return CareerSaveCommandResult.Failure("진행 중인 선수 커리어가 없습니다.");
            if (_activeMatch != null)
                return CareerSaveCommandResult.Failure("경기 준비 또는 진행 중에는 저장할 수 없습니다. 경기를 마친 뒤 저장해 주세요.");
            if (IsSeasonFastForwardRunning)
                return CareerSaveCommandResult.Failure("시즌 자동 진행이 끝난 뒤 저장할 수 있습니다.");

            try
            {
                EnsurePersistenceContext();
                CareerSaveData saveData = _careerSaveAdapter.CreateSaveData(
                    CurrentCareer,
                    _seasonTransitionService,
                    _careerSaveContent,
                    Application.version,
                    DateTime.UtcNow.Ticks);
                _careerSaveStore.SaveAtomic(saveData);
                return CareerSaveCommandResult.Success("선수 커리어를 저장했습니다.");
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                return CareerSaveCommandResult.Failure(
                    GetPersistenceFailureMessage(exception, "저장하지 못했습니다."));
            }
        }

        /// <summary>수동 슬롯을 검증·복원한 뒤 현재 런타임 커리어를 한 번에 교체한다.</summary>
        public CareerSaveCommandResult LoadCareer()
        {
            if (IsSeasonFastForwardRunning)
                return CareerSaveCommandResult.Failure("시즌 자동 진행이 끝난 뒤 불러올 수 있습니다.");
            try
            {
                EnsurePersistenceContext();
                CareerSaveData saveData = _careerSaveStore.LoadPrimary();
                CareerSaveRestoreResult restored = ValidateAndRestore(saveData);
                ApplyRestoredCareer(restored);
                return CareerSaveCommandResult.Success("선수 커리어를 불러왔습니다.");
            }
            catch (CareerSaveCompatibilityException exception)
            {
                return CareerSaveCommandResult.Failure(exception.Message);
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                return CareerSaveCommandResult.Failure(
                    GetPersistenceFailureMessage(exception, "불러오지 못했습니다."));
            }
        }

        /// <summary>자동 백업을 검증한 뒤 수동 슬롯으로 승격하고 현재 커리어에 적용한다.</summary>
        public CareerSaveCommandResult RecoverCareerSaveBackup()
        {
            if (IsSeasonFastForwardRunning)
                return CareerSaveCommandResult.Failure("시즌 자동 진행이 끝난 뒤 백업을 복구할 수 있습니다.");
            try
            {
                EnsurePersistenceContext();
                CareerSaveData saveData = _careerSaveStore.LoadBackup();
                CareerSaveRestoreResult restored = ValidateAndRestore(saveData);
                _careerSaveStore.PromoteBackupToPrimaryAtomic();
                ApplyRestoredCareer(restored);
                return CareerSaveCommandResult.Success("자동 백업을 복구했습니다.");
            }
            catch (CareerSaveCompatibilityException exception)
            {
                return CareerSaveCommandResult.Failure(exception.Message);
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                return CareerSaveCommandResult.Failure(
                    GetPersistenceFailureMessage(exception, "백업을 복구하지 못했습니다."));
            }
        }

        /// <summary>사용자 확인을 마친 단일 슬롯의 수동 저장·백업·임시 파일을 삭제한다.</summary>
        public CareerSaveCommandResult DeleteCareerSave()
        {
            try
            {
                EnsurePersistenceStore();
                _careerSaveStore.DeleteAll();
                return CareerSaveCommandResult.Success("선수 커리어 저장 데이터와 백업을 삭제했습니다.");
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                return CareerSaveCommandResult.Failure(
                    GetPersistenceFailureMessage(exception, "저장 데이터를 삭제하지 못했습니다."));
            }
        }

        /// <summary>복원 상태에서 플레이어가 이어서 처리해야 할 가장 앞선 화면을 결정한다.</summary>
        public CareerResumeDestination ResolveCareerResumeDestination()
        {
            if (HasRetirementRecap)
                return CareerResumeDestination.RetirementRecap;
            if (CurrentCareer?.Narrative.PendingReaction != null)
                return CareerResumeDestination.PendingReaction;
            if (_seasonTransitionService?.Step is SeasonTransitionStep.CurrentTeamNegotiation or
                SeasonTransitionStep.ContractOffers)
                return CareerResumeDestination.ContractDecision;
            SeasonPhase? phase = CurrentCareer?.CurrentLeague.CurrentSeason.Phase;
            if (phase == SeasonPhase.SeasonReview)
                return CareerResumeDestination.SeasonReview;
            if (phase == SeasonPhase.Offseason)
                return CareerResumeDestination.Offseason;
            return CareerResumeDestination.Home;
        }

        private CareerSaveRestoreResult ValidateAndRestore(CareerSaveData saveData)
        {
            CareerSaveCompatibilityValidator.Validate(saveData, _careerSaveContent);
            return _careerSaveAdapter.Restore(saveData, _persistenceBalance);
        }

        private void ApplyRestoredCareer(CareerSaveRestoreResult restored)
        {
            BeginCareer(restored.Career, _persistenceBalance);
            _seasonTransitionService = restored.SeasonTransition;
            LastError = string.Empty;
            CareerChanged?.Invoke();
        }

        private void EnsurePersistenceContext()
        {
            EnsurePersistenceStore();
            if (_careerSaveContent != null && _persistenceBalance != null)
                return;

            NewGameConfiguration configuration = NewGameDefinition.LoadConfiguration();
            HistoricalContentManifest manifest = NewGameDefinition
                .LoadHistoricalContentProvider()
                .Load()
                .Manifest;
            _persistenceBalance = _balance ?? configuration.Balance;
            _careerSaveContent = new CareerSaveContentReference
            {
                assetFormatVersion = manifest.AssetFormatVersion,
                contentSchemaVersion = manifest.ContentSchemaVersion,
                assetArchiveHash = manifest.AssetArchiveHash,
                normalizedSchemaVersion = manifest.NormalizedSchemaVersion,
                normalizedContentHash = manifest.NormalizedContentHash,
                referenceDataVersion = manifest.ReferenceDataVersion,
                generatorVersion = manifest.GeneratorVersion,
                historicalBalanceVersion = manifest.BalanceVersion,
                contentHash = manifest.ContentHash,
                balanceVersion = _persistenceBalance.Version,
                balanceContentHash = _persistenceBalance.ContentHash,
                worldRecordMode = (int)configuration.WorldRecordMode
            };
        }

        private void EnsurePersistenceStore()
        {
            _careerSaveStore ??= new CareerSaveJsonStore(CareerSavePath.GetDefaultFilePath());
            _careerSaveAdapter ??= new CareerSaveAdapter();
        }

        private static bool IsPersistenceFailure(Exception exception) =>
            exception is IOException or UnauthorizedAccessException or
                InvalidOperationException or ArgumentException or NotSupportedException or
                FormatException or OverflowException or MemberAccessException or
                System.Security.Cryptography.CryptographicException or
                System.Reflection.TargetInvocationException;

        private static string GetPersistenceFailureMessage(Exception exception, string prefix)
        {
            string reason = exception switch
            {
                FileNotFoundException => "파일을 찾을 수 없습니다.",
                InvalidDataException => "파일이 손상되었거나 형식이 올바르지 않습니다.",
                UnauthorizedAccessException => "저장 폴더에 접근할 권한이 없습니다.",
                IOException => "저장 장치에서 파일을 읽거나 쓰지 못했습니다.",
                NotSupportedException => "현재 플랫폼에서 지원하지 않는 파일 작업입니다.",
                _ => "커리어 상태의 형식이나 불변식이 올바르지 않습니다."
            };
            return prefix + " " + reason;
        }
    }
}
