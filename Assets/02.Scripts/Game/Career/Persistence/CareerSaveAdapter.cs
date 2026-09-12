using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Game.Career.Diagnostics;

namespace Baseball.Game.Career.Persistence
{
    /// <summary>CareerState Aggregate와 디스크 포맷 v1 사이의 Capture/Restore 경계다.</summary>
    public sealed class CareerSaveAdapter
    {
        public const int CurrentSaveVersion = 1;

        private readonly CareerSaveGraphSerializer _graphSerializer = new();

        public CareerSaveData CreateSaveData(
            CareerState career,
            CareerSeasonTransitionService seasonTransition,
            CareerSaveContentReference content,
            string buildVersion,
            long savedAtUtcTicks)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (content == null) throw new ArgumentNullException(nameof(content));

            string checksum = CareerStateChecksum.Calculate(career);
            return new CareerSaveData
            {
                saveVersion = CurrentSaveVersion,
                buildVersion = buildVersion ?? string.Empty,
                content = content,
                summary = CreateSummary(career, savedAtUtcTicks),
                careerChecksum = checksum,
                payloadSha256 = string.Empty,
                graph = _graphSerializer.Capture(new CareerSaveRuntimeRoot
                {
                    career = career,
                    seasonTransition = seasonTransition
                })
            };
        }

        public CareerSaveRestoreResult Restore(CareerSaveData saveData, BalanceTable balance)
        {
            if (saveData == null) throw new ArgumentNullException(nameof(saveData));
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            if (saveData.saveVersion != CurrentSaveVersion)
                throw new CareerSaveCompatibilityException(
                    $"세이브 버전 {saveData.saveVersion}은 현재 버전 {CurrentSaveVersion}과 호환되지 않습니다.");

            CareerSaveRuntimeRoot root = _graphSerializer.Restore<CareerSaveRuntimeRoot>(saveData.graph);
            CareerState career = root.career ??
                                  throw new InvalidOperationException("세이브에 CareerState가 없습니다.");
            if (career.SaveVersion != NewGameFlow.CurrentSaveVersion)
                throw new CareerSaveCompatibilityException(
                    $"커리어 상태 버전 {career.SaveVersion}은 현재 버전과 호환되지 않습니다.");
            career.World.ValidateInvariants();
            string restoredChecksum = CareerStateChecksum.Calculate(career);
            if (!string.Equals(restoredChecksum, saveData.careerChecksum, StringComparison.Ordinal))
                throw new InvalidOperationException("저장된 커리어 상태가 손상되어 checksum이 일치하지 않습니다.");

            root.seasonTransition?.RestoreRuntimeDependencies(career, balance);
            return new CareerSaveRestoreResult(career, root.seasonTransition);
        }

        private static CareerSaveSummaryData CreateSummary(CareerState career, long savedAtUtcTicks)
        {
            int teamId = career.MyPlayer.CurrentTeamId > 0
                ? career.MyPlayer.CurrentTeamId
                : career.Retirement.LastTeamId;
            TeamState team = teamId > 0 ? career.World.GetTeam(teamId) : null;
            string teamName = team?.Name ?? "무소속";
            SeasonState season = career.CurrentLeague.CurrentSeason;
            return new CareerSaveSummaryData
            {
                playerName = career.MyPlayer.Name,
                playerId = career.MyPlayer.PlayerId,
                playerPersonId = career.MyPlayer.HistoricalPlayerPersonId,
                position = GetPositionLabel(career.MyPlayer.PrimaryPosition),
                teamName = teamName,
                teamId = teamId,
                teamSeasonKey = team?.OriginTeamSeasonKey ?? string.Empty,
                franchiseId = team?.OriginFranchiseId ?? string.Empty,
                originYear = team?.OriginYear ?? 0,
                leagueName = GetLeagueLabel(career.CurrentLeague.LeagueLevel),
                seasonId = season.SeasonId,
                year = season.Year,
                seasonPhase = GetPhaseLabel(season.Phase),
                savedAtUtcTicks = savedAtUtcTicks
            };
        }

        private static string GetPositionLabel(PlayerPosition position) => position switch
        {
            PlayerPosition.StartingPitcher => "선발투수",
            PlayerPosition.ReliefPitcher => "구원투수",
            PlayerPosition.Catcher => "포수",
            PlayerPosition.FirstBase => "1루수",
            PlayerPosition.SecondBase => "2루수",
            PlayerPosition.ThirdBase => "3루수",
            PlayerPosition.Shortstop => "유격수",
            PlayerPosition.LeftField => "좌익수",
            PlayerPosition.CenterField => "중견수",
            PlayerPosition.RightField => "우익수",
            PlayerPosition.DesignatedHitter => "지명타자",
            _ => "미정"
        };

        private static string GetLeagueLabel(LeagueLevel level) =>
            WorldGenerationConfiguration.GetDefaultDefinition(level).DisplayName;

        private static string GetPhaseLabel(SeasonPhase phase) => phase switch
        {
            SeasonPhase.Preseason => "시즌 준비",
            SeasonPhase.RegularSeason => "정규 시즌",
            SeasonPhase.Postseason => "포스트시즌",
            SeasonPhase.SeasonReview => "시즌 결산",
            SeasonPhase.Offseason => "오프시즌",
            SeasonPhase.Completed => "시즌 완료",
            _ => "시즌 정보 없음"
        };
    }

    /// <summary>세이브 헤더의 Bake·Balance 정체성이 현재 실행 데이터와 같은지 검사한다.</summary>
    public static class CareerSaveCompatibilityValidator
    {
        public static void Validate(
            CareerSaveData saveData,
            CareerSaveContentReference currentContent)
        {
            if (saveData == null) throw new ArgumentNullException(nameof(saveData));
            if (currentContent == null) throw new ArgumentNullException(nameof(currentContent));
            if (saveData.saveVersion != CareerSaveAdapter.CurrentSaveVersion)
                throw new CareerSaveCompatibilityException(
                    $"세이브 버전 {saveData.saveVersion}은 현재 버전과 호환되지 않습니다.");
            CareerSaveContentReference saved = saveData.content ??
                throw new CareerSaveCompatibilityException("세이브에 Bake 식별 정보가 없습니다.");

            if (saved.assetFormatVersion != currentContent.assetFormatVersion ||
                saved.contentSchemaVersion != currentContent.contentSchemaVersion ||
                saved.normalizedSchemaVersion != currentContent.normalizedSchemaVersion ||
                saved.balanceVersion != currentContent.balanceVersion ||
                saved.worldRecordMode != currentContent.worldRecordMode ||
                !Same(saved.assetArchiveHash, currentContent.assetArchiveHash) ||
                !Same(saved.normalizedContentHash, currentContent.normalizedContentHash) ||
                !Same(saved.referenceDataVersion, currentContent.referenceDataVersion) ||
                !Same(saved.generatorVersion, currentContent.generatorVersion) ||
                !Same(saved.historicalBalanceVersion, currentContent.historicalBalanceVersion) ||
                !Same(saved.contentHash, currentContent.contentHash) ||
                !Same(saved.balanceContentHash, currentContent.balanceContentHash))
            {
                throw new CareerSaveCompatibilityException(
                    "현재 선수 Bake 또는 Balance가 저장 시점과 달라 이 세이브를 불러올 수 없습니다.");
            }
        }

        private static bool Same(string left, string right) =>
            string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.Ordinal);
    }
}
