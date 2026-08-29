using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    public enum RetirementRecapAct
    {
        Prologue,
        SeasonTimeline,
        PlayerBuilt,
        FeaturedMemories,
        CareerLegacy,
        Farewell,
        CareerCard
    }

    public enum RetirementArchiveTab
    {
        Summary,
        SeasonTimeline,
        FeaturedMemories,
        FullRecords,
        ContractsAndMoves,
        Growth,
        News,
        FinalGame
    }

    /// <summary>회고 한 장면이 표시할 고정 텍스트·강조 숫자·권장 재생 시간을 묶는다.</summary>
    public sealed class RetirementRecapBeat
    {
        public RetirementRecapBeat(
            RetirementRecapAct act,
            string eyebrow,
            string title,
            string body,
            string[] statLines,
            float duration,
            string assetKey,
            bool isHighlight = false)
        {
            Act = act;
            Eyebrow = eyebrow ?? string.Empty;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            StatLines = statLines ?? Array.Empty<string>();
            Duration = duration < 1f ? 1f : duration;
            AssetKey = assetKey ?? string.Empty;
            IsHighlight = isHighlight;
        }

        public RetirementRecapAct Act { get; }
        public string Eyebrow { get; }
        public string Title { get; }
        public string Body { get; }
        public IReadOnlyList<string> StatLines { get; }
        public float Duration { get; }
        public string AssetKey { get; }
        public bool IsHighlight { get; }
    }

    /// <summary>기록관 탭 한 장과 원본 경기·뉴스 연결 ID를 전달한다.</summary>
    public sealed class RetirementArchivePage
    {
        public RetirementArchivePage(
            RetirementArchiveTab tab,
            string title,
            string body,
            int linkedMatchId = 0,
            string linkedNewsId = "")
        {
            Tab = tab;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            LinkedMatchId = linkedMatchId;
            LinkedNewsId = linkedNewsId ?? string.Empty;
        }

        public RetirementArchiveTab Tab { get; }
        public string Title { get; }
        public string Body { get; }
        public int LinkedMatchId { get; }
        public string LinkedNewsId { get; }
    }

    /// <summary>은퇴 스냅샷만 읽어 5막 회고와 재탐색 가능한 기록관 문장을 만든다.</summary>
    public sealed class RetirementRecapViewBuilder
    {
        public RetirementRecapBeat[] BuildRecap(RetirementRecapSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var beats = new List<RetirementRecapBeat>();
            beats.Add(new RetirementRecapBeat(
                RetirementRecapAct.Prologue,
                "한 선수의 기록",
                snapshot.PlayerName,
                $"{GetPositionLabel(snapshot.Position)} · 프로 {snapshot.CareerStats.Seasons}년\n" +
                $"{snapshot.DebutSeason} – {snapshot.RetirementSeason}\n\n" +
                $"{snapshot.DebutSeason}년 봄, 한 선수의 기록이 시작됐다.",
                Array.Empty<string>(),
                8f,
                snapshot.FinalPresentationAssetKey,
                isHighlight: true));

            for (int index = 0; index < snapshot.Seasons.Count; index++)
                beats.Add(BuildSeasonBeat(snapshot, snapshot.Seasons[index], index));

            AddPlayerBuiltBeats(snapshot, beats);
            for (int index = 0; index < snapshot.FeaturedMemories.Count; index++)
                beats.Add(BuildMemoryBeat(snapshot.FeaturedMemories[index]));
            AddLegacyBeats(snapshot, beats);
            return beats.ToArray();
        }

        public RetirementArchivePage BuildArchivePage(
            RetirementRecapSnapshot snapshot,
            RetirementArchiveTab tab)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return tab switch
            {
                RetirementArchiveTab.Summary => BuildSummaryPage(snapshot),
                RetirementArchiveTab.SeasonTimeline => BuildTimelinePage(snapshot),
                RetirementArchiveTab.FeaturedMemories => BuildMemoriesPage(snapshot),
                RetirementArchiveTab.FullRecords => BuildRecordsPage(snapshot),
                RetirementArchiveTab.ContractsAndMoves => BuildContractsPage(snapshot),
                RetirementArchiveTab.Growth => BuildGrowthPage(snapshot),
                RetirementArchiveTab.News => BuildNewsPage(snapshot),
                RetirementArchiveTab.FinalGame => BuildFinalGamePage(snapshot),
                _ => throw new ArgumentOutOfRangeException(nameof(tab))
            };
        }

        private static RetirementRecapBeat BuildSeasonBeat(
            RetirementRecapSnapshot snapshot,
            CareerSeasonArchive season,
            int index)
        {
            bool isHighlight = index == 0 || index == snapshot.Seasons.Count - 1 ||
                               season.Season == snapshot.CareerBestSeason;
            string eyebrow = season.Season == snapshot.CareerBestSeason
                ? "CAREER BEST"
                : index == 0
                    ? "PRO DEBUT"
                    : index == snapshot.Seasons.Count - 1 ? "FINAL SEASON" : "SEASON TIMELINE";
            string summary = BuildSeasonStatLine(snapshot.Position, season.Stats);
            string body = $"{season.TeamName}\n{GetRoleLabel(season.PrimaryRole)}";
            if (season.StartOverall > 0 && season.EndOverall > 0)
                body += $"\n\nOverall {season.StartOverall} → {season.EndOverall}";
            if (season.Awards.Count > 0)
                body += $"\n수상·우승 {season.Awards.Count}개";
            if (season.Injuries.Injuries.Count > 0)
                body += $"\n부상 {season.Injuries.Injuries.Count}회";
            return new RetirementRecapBeat(
                RetirementRecapAct.SeasonTimeline,
                eyebrow,
                $"{season.Season} SEASON · {season.Age}세",
                body,
                new[] { summary },
                isHighlight ? 4f : 2f,
                isHighlight ? "career_timeline_highlight" : "career_timeline",
                isHighlight);
        }

        private static void AddPlayerBuiltBeats(
            RetirementRecapSnapshot snapshot,
            List<RetirementRecapBeat> beats)
        {
            if (snapshot.Seasons.Count == 0)
                return;
            CareerSeasonArchive first = snapshot.Seasons[0];
            CareerSeasonArchive last = snapshot.Seasons[^1];
            int peakOverall = first.EndOverall;
            for (int index = 1; index < snapshot.Seasons.Count; index++)
                peakOverall = Math.Max(peakOverall, snapshot.Seasons[index].EndOverall);
            beats.Add(new RetirementRecapBeat(
                RetirementRecapAct.PlayerBuilt,
                "내가 만든 선수",
                "성장의 방향",
                "기록은 경기장에서 남았고, 선수의 방향은 매 순간의 선택으로 만들어졌다.",
                new[]
                {
                    first.StartOverall > 0 ? $"데뷔 Overall  {first.StartOverall}" : "데뷔 Overall  기록 없음",
                    peakOverall > 0 ? $"커리어 최고 Overall  {peakOverall}" : "커리어 최고 Overall  기록 없음",
                    last.EndOverall > 0 ? $"은퇴 Overall  {last.EndOverall}" : "은퇴 Overall  기록 없음",
                    $"훈련 {snapshot.CareerChoices.TrainingCount}회 · 유학 {snapshot.CareerChoices.StudyCount}회"
                },
                6f,
                "career_growth",
                true));

            CareerChoiceSnapshot choices = snapshot.CareerChoices;
            if (choices.TotalApproachCount > 0)
            {
                double ratio = choices.MostUsedApproachCount * 100d / choices.TotalApproachCount;
                beats.Add(new RetirementRecapBeat(
                    RetirementRecapAct.PlayerBuilt,
                    "플레이어의 선택",
                    "가장 자주 선택한 경기 방침",
                    GetApproachLabel(choices.MostUsedApproachKey),
                    new[] { $"{ratio:0.#}% · {choices.MostUsedApproachCount:N0}회" },
                    5f,
                    "career_play_style"));
            }
            if (!string.IsNullOrWhiteSpace(choices.LongestSkillBlockId))
            {
                beats.Add(new RetirementRecapBeat(
                    RetirementRecapAct.PlayerBuilt,
                    "스킬 블록의 기록",
                    choices.LongestSkillBlockId,
                    "가장 많은 시즌을 함께한 스킬 블록",
                    new[] { $"장착 {choices.LongestSkillSeasons}시즌" },
                    5f,
                    "career_skill_board"));
            }
        }

        private static RetirementRecapBeat BuildMemoryBeat(CareerMemoryRecord memory)
        {
            var stats = new string[memory.Stats.Count];
            for (int index = 0; index < stats.Length; index++)
                stats[index] = FormatMemoryStat(memory.Stats[index]);
            return new RetirementRecapBeat(
                RetirementRecapAct.FeaturedMemories,
                $"{memory.Season} · 대표 순간",
                GetMemoryTitle(memory.Type),
                GetMemoryNarrative(memory.Type),
                stats,
                memory.Type is CareerMemoryType.FinalAppearance or CareerMemoryType.Championship ? 7f : 5f,
                memory.PresentationAssetKey,
                true);
        }

        private static void AddLegacyBeats(
            RetirementRecapSnapshot snapshot,
            List<RetirementRecapBeat> beats)
        {
            SeasonStatSnapshot totals = snapshot.CareerStats.Totals;
            bool isPitcher = IsPitcher(snapshot.Position);
            beats.Add(new RetirementRecapBeat(
                RetirementRecapAct.CareerLegacy,
                "커리어 기록이 완성되었습니다",
                snapshot.PlayerName,
                $"{snapshot.CareerStats.Seasons}년에 걸쳐 남긴 기록",
                isPitcher
                    ? new[]
                    {
                        $"통산 경기  {totals.PitchingAppearances:N0}",
                        $"통산 이닝  {FormatInnings(totals.OutsRecorded)}",
                        $"통산 승리  {totals.Wins:N0}",
                        $"통산 탈삼진  {totals.PitchingStrikeouts:N0}",
                        $"ERA  {totals.EarnedRunAverage:0.00} · WHIP  {totals.WalksHitsPerInningPitched:0.00}"
                    }
                    : new[]
                    {
                        $"통산 경기  {totals.Games:N0}",
                        $"통산 안타  {totals.Hits:N0}",
                        $"통산 홈런  {totals.HomeRuns:N0}",
                        $"통산 타점  {totals.RunsBattedIn:N0}",
                        $"AVG  {totals.BattingAverage:.000} · OBP  {totals.OnBasePercentage:.000} · SLG  {totals.SluggingPercentage:.000}"
                    },
                8f,
                "career_scoreboard",
                true));

            CareerSeasonArchive best = FindSeason(snapshot, snapshot.CareerBestSeason);
            if (best != null)
            {
                beats.Add(new RetirementRecapBeat(
                    RetirementRecapAct.CareerLegacy,
                    "가장 빛났던 시즌",
                    $"CAREER BEST · {best.Season}",
                    best.TeamName,
                    new[] { BuildSeasonStatLine(snapshot.Position, best.Stats) },
                    6f,
                    "career_high",
                    true));
            }

            beats.Add(new RetirementRecapBeat(
                RetirementRecapAct.CareerLegacy,
                $"{snapshot.PlayerName}가 남긴 것",
                GetTitleLabel(snapshot.CareerTitlePrimary),
                string.IsNullOrWhiteSpace(snapshot.CareerTitleSecondary)
                    ? $"{snapshot.FranchiseLegacy.PrimaryTeamName} · {snapshot.FranchiseLegacy.Seasons}시즌"
                    : $"그리고, {GetTitleLabel(snapshot.CareerTitleSecondary)}",
                new[]
                {
                    $"소속 구단 {snapshot.CareerChoices.TeamCount}개",
                    $"포스트시즌 {snapshot.CareerChoices.PostseasonCount}시즌",
                    $"우승 {snapshot.CareerChoices.ChampionshipCount}회 · 수상 {snapshot.LeagueLegacy.AwardCount}회"
                },
                7f,
                "career_legacy",
                true));

            beats.Add(new RetirementRecapBeat(
                RetirementRecapAct.Farewell,
                "마지막 인사",
                snapshot.PlayerName,
                GetFinalNarrative(snapshot.FinalNarrativeKey),
                new[] { $"{snapshot.DebutSeason} – {snapshot.RetirementSeason}" },
                9f,
                snapshot.FinalPresentationAssetKey,
                true));

            beats.Add(new RetirementRecapBeat(
                RetirementRecapAct.CareerCard,
                "CAREER ARCHIVE",
                snapshot.PlayerName,
                $"{GetPositionLabel(snapshot.Position)} · {snapshot.DebutSeason} – {snapshot.RetirementSeason}\n\n" +
                $"“{GetTitleLabel(snapshot.CareerTitlePrimary)}”",
                new[]
                {
                    $"{GetStatLabel(snapshot.SignatureRecord.StatKey)}  {FormatValue(snapshot.SignatureRecord.Value, snapshot.SignatureRecord.FormatKey)}",
                    isPitcher
                        ? $"{totals.PitchingAppearances:N0}경기 · {totals.Wins:N0}승 · {totals.PitchingStrikeouts:N0}탈삼진"
                        : $"{totals.Games:N0}경기 · {totals.Hits:N0}안타 · {totals.HomeRuns:N0}홈런"
                },
                10f,
                "career_archive_card",
                true));
        }

        private static RetirementArchivePage BuildSummaryPage(RetirementRecapSnapshot snapshot)
        {
            return new RetirementArchivePage(
                RetirementArchiveTab.Summary,
                "커리어 요약",
                $"{snapshot.PlayerName}\n{GetPositionLabel(snapshot.Position)} · " +
                $"{snapshot.DebutSeason} – {snapshot.RetirementSeason}\n\n" +
                $"{GetTitleLabel(snapshot.CareerTitlePrimary)}\n" +
                (string.IsNullOrWhiteSpace(snapshot.CareerTitleSecondary)
                    ? string.Empty
                    : $"그리고, {GetTitleLabel(snapshot.CareerTitleSecondary)}\n") +
                $"\n대표 기록\n{GetStatLabel(snapshot.SignatureRecord.StatKey)}  " +
                FormatValue(snapshot.SignatureRecord.Value, snapshot.SignatureRecord.FormatKey));
        }

        private static RetirementArchivePage BuildTimelinePage(RetirementRecapSnapshot snapshot)
        {
            var body = new StringBuilder();
            for (int index = 0; index < snapshot.Seasons.Count; index++)
            {
                CareerSeasonArchive season = snapshot.Seasons[index];
                body.Append(season.Season).Append(" · ").Append(season.TeamName)
                    .Append(" · ").Append(GetRoleLabel(season.PrimaryRole)).Append('\n');
                if (season.StartOverall > 0 && season.EndOverall > 0)
                    body.Append("Overall ").Append(season.StartOverall).Append(" → ").Append(season.EndOverall).Append(" · ");
                body.Append(BuildSeasonStatLine(snapshot.Position, season.Stats)).Append("\n\n");
            }
            return new RetirementArchivePage(RetirementArchiveTab.SeasonTimeline, "시즌 타임라인", body.ToString());
        }

        private static RetirementArchivePage BuildMemoriesPage(RetirementRecapSnapshot snapshot)
        {
            var body = new StringBuilder();
            for (int index = 0; index < snapshot.FeaturedMemories.Count; index++)
            {
                CareerMemoryRecord memory = snapshot.FeaturedMemories[index];
                body.Append(memory.Season).Append(" · ").Append(GetMemoryTitle(memory.Type)).Append('\n')
                    .Append(GetMemoryNarrative(memory.Type)).Append("\n\n");
            }
            return new RetirementArchivePage(
                RetirementArchiveTab.FeaturedMemories,
                "대표 순간",
                body.ToString());
        }

        private static RetirementArchivePage BuildRecordsPage(RetirementRecapSnapshot snapshot)
        {
            SeasonStatSnapshot stats = snapshot.CareerStats.Totals;
            string body = IsPitcher(snapshot.Position)
                ? $"경기 {stats.PitchingAppearances:N0} · 선발 {stats.PitchingStarts:N0}\n" +
                  $"이닝 {FormatInnings(stats.OutsRecorded)} · {stats.Wins}승 {stats.Losses}패\n" +
                  $"세이브 {stats.Saves} · 홀드 {stats.Holds} · 탈삼진 {stats.PitchingStrikeouts:N0}\n" +
                  $"ERA {stats.EarnedRunAverage:0.00} · WHIP {stats.WalksHitsPerInningPitched:0.00}"
                : $"경기 {stats.Games:N0} · 타석 {stats.PlateAppearances:N0}\n" +
                  $"안타 {stats.Hits:N0} · 홈런 {stats.HomeRuns:N0} · 타점 {stats.RunsBattedIn:N0}\n" +
                  $"득점 {stats.Runs:N0} · 도루 {stats.StolenBases:N0}\n" +
                  $"AVG {stats.BattingAverage:.000} · OBP {stats.OnBasePercentage:.000} · SLG {stats.SluggingPercentage:.000}";
            return new RetirementArchivePage(RetirementArchiveTab.FullRecords, "전체 기록", body);
        }

        private static RetirementArchivePage BuildContractsPage(RetirementRecapSnapshot snapshot)
        {
            var body = new StringBuilder();
            CareerChoiceSnapshot choices = snapshot.CareerChoices;
            body.Append("체결한 계약 ").Append(choices.ContractCount)
                .Append(" · 재계약 ").Append(choices.RenewalCount)
                .Append(" · 이적 ").Append(choices.TransferCount).Append('\n');
            if (choices.LongestAcceptedContractYears > 0)
                body.Append("선택한 가장 긴 계약 ").Append(choices.LongestAcceptedContractYears).Append("년\n");
            if (choices.HighestDeclinedAnnualSalary > 0L)
                body.Append("거절한 최고 연봉 ").Append(choices.HighestDeclinedAnnualSalary.ToString("N0", CultureInfo.InvariantCulture)).Append("\n");
            body.Append('\n');
            int previousContractId = 0;
            for (int index = 0; index < snapshot.Seasons.Count; index++)
            {
                CareerSeasonArchive season = snapshot.Seasons[index];
                if (season.Contract.ContractId <= 0 || season.Contract.ContractId == previousContractId)
                    continue;
                previousContractId = season.Contract.ContractId;
                body.Append(season.Contract.SignedYear).Append(" · ").Append(season.TeamName).Append('\n')
                    .Append(season.Contract.EndYear - season.Contract.SignedYear + 1).Append("년 · 연봉 ")
                    .Append(season.Contract.AnnualSalary.ToString("N0", CultureInfo.InvariantCulture))
                    .Append(" · ").Append(GetExpectedRoleLabel(season.Contract.PromisedRole)).Append("\n\n");
            }
            return new RetirementArchivePage(RetirementArchiveTab.ContractsAndMoves, "계약과 이동", body.ToString());
        }

        private static RetirementArchivePage BuildGrowthPage(RetirementRecapSnapshot snapshot)
        {
            var body = new StringBuilder();
            for (int index = 0; index < snapshot.Seasons.Count; index++)
            {
                CareerSeasonArchive season = snapshot.Seasons[index];
                body.Append(season.Season);
                if (season.StartOverall <= 0 || season.EndOverall <= 0)
                    body.Append(" · 이전 버전에서 능력치 변화가 저장되지 않음");
                else
                    body.Append(" · Overall ").Append(season.StartOverall).Append(" → ").Append(season.EndOverall);
                body.Append('\n');
                for (int abilityIndex = 0; abilityIndex < season.Growth.Abilities.Count; abilityIndex++)
                {
                    SeasonAbilitySnapshot ability = season.Growth.Abilities[abilityIndex];
                    if (ability.Change != 0)
                        body.Append("  ").Append(ability.Ability).Append(' ')
                            .Append(ability.Change > 0 ? "+" : string.Empty).Append(ability.Change).Append('\n');
                }
                body.Append('\n');
            }
            return new RetirementArchivePage(RetirementArchiveTab.Growth, "성장 기록", body.ToString());
        }

        private static RetirementArchivePage BuildNewsPage(RetirementRecapSnapshot snapshot)
        {
            var body = new StringBuilder();
            string linkedNewsId = string.Empty;
            for (int index = 0; index < snapshot.FeaturedMemories.Count; index++)
            {
                CareerMemoryRecord memory = snapshot.FeaturedMemories[index];
                if (string.IsNullOrWhiteSpace(memory.NewsId)) continue;
                if (linkedNewsId.Length == 0) linkedNewsId = memory.NewsId;
                body.Append(memory.Season).Append(" · ").Append(GetMemoryTitle(memory.Type))
                    .Append(" · ").Append(memory.NewsId).Append('\n');
            }
            if (body.Length == 0)
                body.Append("사실로 저장된 뉴스 연결이 없습니다. 존재하지 않는 기사는 생성하지 않습니다.");
            return new RetirementArchivePage(RetirementArchiveTab.News, "뉴스 보관함", body.ToString(), linkedNewsId: linkedNewsId);
        }

        private static RetirementArchivePage BuildFinalGamePage(RetirementRecapSnapshot snapshot)
        {
            CareerMemoryRecord final = null;
            for (int index = snapshot.FeaturedMemories.Count - 1; index >= 0; index--)
            {
                if (snapshot.FeaturedMemories[index].Type == CareerMemoryType.FinalAppearance)
                {
                    final = snapshot.FeaturedMemories[index];
                    break;
                }
            }
            string body = final == null
                ? "마지막 경기의 원본 ID가 저장되지 않았습니다. 시즌 최종 기록만 보존합니다."
                : $"{final.Season} · {GetMemoryTitle(final.Type)}\n{GetMemoryNarrative(final.Type)}\n" +
                  (final.MatchId > 0 ? $"\n경기 ID {final.MatchId}" : "\n원본 경기 연결 없음");
            return new RetirementArchivePage(
                RetirementArchiveTab.FinalGame,
                "마지막 경기",
                body,
                final?.MatchId ?? 0,
                final?.NewsId ?? string.Empty);
        }

        private static CareerSeasonArchive FindSeason(RetirementRecapSnapshot snapshot, int year)
        {
            for (int index = 0; index < snapshot.Seasons.Count; index++)
            {
                if (snapshot.Seasons[index].Season == year)
                    return snapshot.Seasons[index];
            }
            return null;
        }

        private static string BuildSeasonStatLine(PlayerPosition position, SeasonStatSnapshot stats)
        {
            return IsPitcher(position)
                ? $"{stats.PitchingAppearances}경기 · {FormatInnings(stats.OutsRecorded)}이닝 · " +
                  $"{stats.Wins}승 · ERA {stats.EarnedRunAverage:0.00} · {stats.PitchingStrikeouts}탈삼진"
                : $"{stats.Games}경기 · AVG {stats.BattingAverage:.000} · " +
                  $"{stats.HomeRuns}홈런 · {stats.RunsBattedIn}타점";
        }

        private static bool IsPitcher(PlayerPosition position) =>
            position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;

        private static string FormatInnings(int outs) => $"{outs / 3}.{outs % 3}";

        private static string FormatMemoryStat(MemoryStatValue stat) =>
            $"{GetStatLabel(stat.StatKey)}  {FormatValue(stat.Value, stat.FormatKey)}";

        private static string FormatValue(double value, string formatKey)
        {
            return formatKey switch
            {
                "average" => value.ToString(".000", CultureInfo.InvariantCulture),
                "decimal" => value.ToString("0.00", CultureInfo.InvariantCulture),
                "innings" => FormatInnings((int)value),
                _ => value.ToString("N0", CultureInfo.InvariantCulture)
            };
        }

        private static string GetMemoryTitle(CareerMemoryType type)
        {
            return type switch
            {
                CareerMemoryType.CareerDebut => "프로 데뷔",
                CareerMemoryType.FirstHit => "첫 번째 안타",
                CareerMemoryType.FirstHomeRun => "첫 번째 홈런",
                CareerMemoryType.FirstPitchingWin => "첫 선발승",
                CareerMemoryType.FirstSave => "첫 세이브",
                CareerMemoryType.RoleBreakthrough => "처음 차지한 자리",
                CareerMemoryType.ExceptionalGame => "커리어 최고의 순간",
                CareerMemoryType.Postseason => "가을의 경기",
                CareerMemoryType.Championship => "우승",
                CareerMemoryType.Award => "개인 수상",
                CareerMemoryType.Injury => "멈춰야 했던 시간",
                CareerMemoryType.InjuryReturn => "다시 경기장으로",
                CareerMemoryType.Study => "새로운 환경에서의 훈련",
                CareerMemoryType.ContractAccepted => "선택한 계약",
                CareerMemoryType.ContractDeclined => "거절한 제안",
                CareerMemoryType.Transfer => "새 유니폼",
                CareerMemoryType.TradePreference => "새 기회를 향한 의사",
                CareerMemoryType.FinalSeasonDeclared => "마지막 시즌 선언",
                CareerMemoryType.FinalAppearance => "마지막 출전",
                _ => "커리어의 한 장면"
            };
        }

        private static string GetMemoryNarrative(CareerMemoryType type)
        {
            return type switch
            {
                CareerMemoryType.CareerDebut => "프로의 무대에 처음 자신의 이름을 남겼다.",
                CareerMemoryType.FirstHit => "선수 생활의 모든 안타는 이 기록에서 시작됐다.",
                CareerMemoryType.FirstHomeRun => "처음으로 담장을 넘긴 공은 오래 남았다.",
                CareerMemoryType.FirstPitchingWin => "마운드를 내려오던 순간, 자신의 자리가 보이기 시작했다.",
                CareerMemoryType.FirstSave => "마지막 아웃을 지켜내며 처음 경기를 끝냈다.",
                CareerMemoryType.RoleBreakthrough => "기회를 기다리던 선수에서, 이름이 불리는 선수로.",
                CareerMemoryType.ExceptionalGame => "그날만큼은 모든 승부가 선명했다.",
                CareerMemoryType.Championship => "긴 시즌의 끝에서 팀과 함께 마지막 자리에 섰다.",
                CareerMemoryType.Award => "한 시즌의 시간이 공식적인 기록으로 인정받았다.",
                CareerMemoryType.Injury => "경기에 나서지 못한 시간도 커리어의 일부였다.",
                CareerMemoryType.InjuryReturn => "멈췄던 시간을 지나 다시 출전 기록을 남겼다.",
                CareerMemoryType.ContractAccepted => "어떤 유니폼을 입을지 직접 결정한 순간이었다.",
                CareerMemoryType.ContractDeclined => "더 큰 금액보다 자신이 원하는 방향을 선택했다.",
                CareerMemoryType.Transfer => "더 많은 기회를 찾아 새로운 팀으로 향했다.",
                CareerMemoryType.FinalSeasonDeclared => "마지막이라는 것을 알고 한 시즌을 준비했다.",
                CareerMemoryType.FinalAppearance => "긴 시간 동안 이어온 마지막 공식 기록이었다.",
                _ => "그 선택과 결과가 한 선수의 커리어를 만들었다."
            };
        }

        private static string GetFinalNarrative(string key)
        {
            return key switch
            {
                "career.retirement.final.medical" => "몸은 더 이상 다음 경기를 허락하지 않았다.\n하지만 여기까지 버텨온 시간까지 사라지는 것은 아니다.",
                "career.retirement.final.unsigned" => "더 이상 전화는 오지 않았다.\n그는 기다림을 끝내고, 자신의 방식으로 마지막을 정했다.",
                "career.retirement.final.short" => "긴 시간은 아니었다.\n그러나 프로의 무대에 자신의 이름을 남긴 순간은 분명히 존재했다.",
                "career.retirement.final.franchise" => "오랜 시간 같은 유니폼을 입었고,\n결국 그 이름은 구단의 역사 일부가 되었다.",
                "career.retirement.final.journeyman" => "여러 도시와 여러 유니폼을 지나왔지만,\n모든 기록의 주인은 언제나 한 선수였다.",
                "career.retirement.final.declared" => "마지막이라는 것을 알고 시작한 한 시즌.\n그는 약속한 자리에서 자신의 커리어를 끝냈다.",
                _ => "이제는 다음 경기를 준비하지 않아도 된다.\n그러나 그가 남긴 기록은 계속해서 이곳에 머문다."
            };
        }

        private static string GetTitleLabel(string key)
        {
            return key switch
            {
                "career.title.short_but_clear" => "짧지만 분명했던 도전",
                "career.title.era_defining" => "시대를 대표한 선수",
                "career.title.franchise_face" => "프랜차이즈의 얼굴",
                "career.title.late_bloomer" => "늦게 피어난 주전",
                "career.title.rose_again" => "다시 일어선 선수",
                "career.title.always_ready" => "매일 준비되어 있던 투수",
                "career.title.many_cities" => "여러 도시에서 자신의 자리를 만든 선수",
                "career.title.built_with_consistency" => "꾸준함으로 시간을 쌓은 선수",
                "career.title.strong_in_autumn" => "가을에 강했던 선수",
                "career.title.became_team_name" => "한 팀의 이름이 된 선수",
                "career.title.found_a_place_everywhere" => "자리를 가리지 않은 선수",
                _ => key
            };
        }

        private static string GetPositionLabel(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "포수",
                PlayerPosition.FirstBase => "1루수",
                PlayerPosition.SecondBase => "2루수",
                PlayerPosition.ThirdBase => "3루수",
                PlayerPosition.Shortstop => "유격수",
                PlayerPosition.LeftField => "좌익수",
                PlayerPosition.CenterField => "중견수",
                PlayerPosition.RightField => "우익수",
                PlayerPosition.DesignatedHitter => "지명타자",
                PlayerPosition.StartingPitcher => "선발 투수",
                PlayerPosition.ReliefPitcher => "구원 투수",
                _ => position.ToString()
            };
        }

        private static string GetRoleLabel(PlayerGameRole role)
        {
            return role switch
            {
                PlayerGameRole.StartingBatter => "주전 야수",
                PlayerGameRole.Bench => "후보 야수",
                PlayerGameRole.StartingPitcher => "선발 로테이션",
                PlayerGameRole.ReliefPitcher => "계투",
                PlayerGameRole.PitcherRest => "투수 휴식",
                _ => "로스터 경쟁"
            };
        }

        private static string GetExpectedRoleLabel(ExpectedRole role)
        {
            return role switch
            {
                ExpectedRole.StartingCompetition => "주전 경쟁",
                ExpectedRole.RosterCompetition => "1군 경쟁",
                _ => "후보 경쟁"
            };
        }

        private static string GetApproachLabel(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "기록 없음";
            return key.Replace("batting.", string.Empty).Replace("pitching.", string.Empty) switch
            {
                "Power" => "강하게 타격",
                "Contact" => "컨택 중심",
                "Patient" => "신중한 타격",
                "Aggressive" => "적극적인 타격",
                "Challenge" => "정면 승부",
                "Nibble" => "유인구 중심",
                "Control" => "제구 우선",
                "Stuff" => "구위 우선",
                _ => "균형"
            };
        }

        private static string GetStatLabel(string key)
        {
            return key switch
            {
                "games" => "경기",
                "plate_appearances" => "타석",
                "hits" => "안타",
                "home_runs" => "홈런",
                "runs_batted_in" => "타점",
                "pitching_appearances" => "등판",
                "outs_recorded" => "이닝",
                "wins" => "승",
                "saves" => "세이브",
                "strikeouts" => "탈삼진",
                "weeks" => "소요 주",
                "injury_count" => "부상",
                _ => key.Replace('_', ' ')
            };
        }
    }
}
