using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>한 시즌 타격·투구 핵심 원본을 은퇴 후에도 재계산 없이 표시한다.</summary>
    public readonly struct SeasonStatSnapshot
    {
        public SeasonStatSnapshot(
            int games,
            int gamesStarted,
            int plateAppearances,
            int atBats,
            int runs,
            int hits,
            int doubles,
            int triples,
            int homeRuns,
            int runsBattedIn,
            int walks,
            int hitByPitches,
            int battingStrikeouts,
            int stolenBases,
            int caughtStealing,
            int pitchingAppearances,
            int pitchingStarts,
            int outsRecorded,
            int wins,
            int losses,
            int saves,
            int holds,
            int earnedRuns,
            int hitsAllowed,
            int walksAllowed,
            int pitchingStrikeouts,
            int qualityStarts,
            int fieldingErrors)
        {
            Games = games;
            GamesStarted = gamesStarted;
            PlateAppearances = plateAppearances;
            AtBats = atBats;
            Runs = runs;
            Hits = hits;
            Doubles = doubles;
            Triples = triples;
            HomeRuns = homeRuns;
            RunsBattedIn = runsBattedIn;
            Walks = walks;
            HitByPitches = hitByPitches;
            BattingStrikeouts = battingStrikeouts;
            StolenBases = stolenBases;
            CaughtStealing = caughtStealing;
            PitchingAppearances = pitchingAppearances;
            PitchingStarts = pitchingStarts;
            OutsRecorded = outsRecorded;
            Wins = wins;
            Losses = losses;
            Saves = saves;
            Holds = holds;
            EarnedRuns = earnedRuns;
            HitsAllowed = hitsAllowed;
            WalksAllowed = walksAllowed;
            PitchingStrikeouts = pitchingStrikeouts;
            QualityStarts = qualityStarts;
            FieldingErrors = fieldingErrors;
        }

        public SeasonStatSnapshot(PlayerSeasonStatisticsState statistics)
        {
            Games = statistics?.GamesPlayed ?? 0;
            GamesStarted = statistics?.GamesStarted ?? 0;
            PlateAppearances = statistics?.PlateAppearances ?? 0;
            AtBats = statistics?.AtBats ?? 0;
            Runs = statistics?.Runs ?? 0;
            Hits = statistics?.Hits ?? 0;
            Doubles = statistics?.Doubles ?? 0;
            Triples = statistics?.Triples ?? 0;
            HomeRuns = statistics?.HomeRuns ?? 0;
            RunsBattedIn = statistics?.RunsBattedIn ?? 0;
            Walks = statistics?.Walks ?? 0;
            HitByPitches = statistics?.HitByPitches ?? 0;
            BattingStrikeouts = statistics?.BattingStrikeouts ?? 0;
            StolenBases = statistics?.StolenBases ?? 0;
            CaughtStealing = statistics?.CaughtStealing ?? 0;
            PitchingAppearances = statistics?.PitchingAppearances ?? 0;
            PitchingStarts = statistics?.PitchingStarts ?? 0;
            OutsRecorded = statistics?.OutsRecorded ?? 0;
            Wins = statistics?.Wins ?? 0;
            Losses = statistics?.Losses ?? 0;
            Saves = statistics?.Saves ?? 0;
            Holds = statistics?.Holds ?? 0;
            EarnedRuns = statistics?.EarnedRuns ?? 0;
            HitsAllowed = statistics?.HitsAllowed ?? 0;
            WalksAllowed = statistics?.WalksAllowed ?? 0;
            PitchingStrikeouts = statistics?.PitchingStrikeouts ?? 0;
            QualityStarts = statistics?.QualityStarts ?? 0;
            FieldingErrors = statistics?.FieldingErrors ?? 0;
        }

        public int Games { get; }
        public int GamesStarted { get; }
        public int PlateAppearances { get; }
        public int AtBats { get; }
        public int Runs { get; }
        public int Hits { get; }
        public int Doubles { get; }
        public int Triples { get; }
        public int HomeRuns { get; }
        public int RunsBattedIn { get; }
        public int Walks { get; }
        public int HitByPitches { get; }
        public int BattingStrikeouts { get; }
        public int StolenBases { get; }
        public int CaughtStealing { get; }
        public int PitchingAppearances { get; }
        public int PitchingStarts { get; }
        public int OutsRecorded { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int Saves { get; }
        public int Holds { get; }
        public int EarnedRuns { get; }
        public int HitsAllowed { get; }
        public int WalksAllowed { get; }
        public int PitchingStrikeouts { get; }
        public int QualityStarts { get; }
        public int FieldingErrors { get; }
        public int TotalBases => Hits + Doubles + Triples * 2 + HomeRuns * 3;
        public double BattingAverage => AtBats == 0 ? 0d : Hits / (double)AtBats;
        public double OnBasePercentage => PlateAppearances == 0
            ? 0d
            : (Hits + Walks + HitByPitches) / (double)PlateAppearances;
        public double SluggingPercentage => AtBats == 0 ? 0d : TotalBases / (double)AtBats;
        public double EarnedRunAverage => OutsRecorded == 0 ? 0d : EarnedRuns * 27d / OutsRecorded;
        public double WalksHitsPerInningPitched => OutsRecorded == 0
            ? 0d
            : (WalksAllowed + HitsAllowed) * 3d / OutsRecorded;
    }

    /// <summary>시즌 중 적용된 계약 조건을 당시 값으로 고정한다.</summary>
    public readonly struct ContractSeasonSnapshot
    {
        public ContractSeasonSnapshot(PlayerContractState contract)
        {
            ContractId = contract?.ContractId ?? 0;
            SignedYear = contract?.SignedYear ?? 0;
            EndYear = contract?.EndYear ?? 0;
            AnnualSalary = contract?.AnnualSalary ?? 0L;
            SigningBonus = contract?.SigningBonus ?? 0L;
            PromisedRole = contract?.PromisedRole ?? ExpectedRole.BenchCompetition;
        }

        public int ContractId { get; }
        public int SignedYear { get; }
        public int EndYear { get; }
        public long AnnualSalary { get; }
        public long SigningBonus { get; }
        public ExpectedRole PromisedRole { get; }
    }

    /// <summary>한 능력치의 시즌 시작·종료 값을 고정한다.</summary>
    public readonly struct SeasonAbilitySnapshot
    {
        public SeasonAbilitySnapshot(PlayerAbility ability, int start, int end)
        {
            Ability = ability;
            Start = start;
            End = end;
        }

        public PlayerAbility Ability { get; }
        public int Start { get; }
        public int End { get; }
        public int Change => End - Start;
    }

    /// <summary>훈련 횟수와 비용, 능력치 변화를 한 시즌 성장 결과로 고정한다.</summary>
    public sealed class GrowthSeasonSnapshot
    {
        private readonly SeasonAbilitySnapshot[] _abilities;
        private readonly CareerNamedCount[] _trainingCounts;

        public GrowthSeasonSnapshot(
            SeasonAbilitySnapshot[] abilities,
            CareerNamedCount[] trainingCounts,
            long moneySpent,
            int studyCount)
        {
            _abilities = abilities == null
                ? Array.Empty<SeasonAbilitySnapshot>()
                : (SeasonAbilitySnapshot[])abilities.Clone();
            _trainingCounts = trainingCounts == null
                ? Array.Empty<CareerNamedCount>()
                : (CareerNamedCount[])trainingCounts.Clone();
            MoneySpent = moneySpent;
            StudyCount = studyCount;
        }

        public IReadOnlyList<SeasonAbilitySnapshot> Abilities => _abilities;
        public IReadOnlyList<CareerNamedCount> TrainingCounts => _trainingCounts;
        public long MoneySpent { get; }
        public int StudyCount { get; }
    }

    /// <summary>시즌 부상과 플레이어 치료 선택을 사실 그대로 고정한다.</summary>
    public sealed class InjurySeasonSnapshot
    {
        private readonly InjuryRecordSnapshot[] _injuries;

        public InjurySeasonSnapshot(InjuryRecordSnapshot[] injuries)
        {
            _injuries = injuries == null
                ? Array.Empty<InjuryRecordSnapshot>()
                : (InjuryRecordSnapshot[])injuries.Clone();
        }

        public IReadOnlyList<InjuryRecordSnapshot> Injuries => _injuries;
    }

    public readonly struct InjuryRecordSnapshot
    {
        public InjuryRecordSnapshot(InjuryRecord injury)
        {
            SourceId = injury?.SourceId ?? string.Empty;
            Severity = injury?.Severity ?? InjurySeverity.Discomfort;
            MinimumAbsenceDays = injury?.MinimumAbsenceDays ?? 0;
            MaximumAbsenceDays = injury?.MaximumAbsenceDays ?? 0;
            TreatmentChoice = injury?.TreatmentChoice;
        }

        public string SourceId { get; }
        public InjurySeverity Severity { get; }
        public int MinimumAbsenceDays { get; }
        public int MaximumAbsenceDays { get; }
        public InjuryTreatmentChoice? TreatmentChoice { get; }
    }

    /// <summary>플레이어가 실제로 고른 타격·투구 방침과 구종 사용 횟수를 고정한다.</summary>
    public sealed class PlayStyleSeasonSnapshot
    {
        private readonly int[] _battingApproaches;
        private readonly int[] _pitchingApproaches;
        private readonly int[] _pitchTypes;

        public PlayStyleSeasonSnapshot(
            CareerSeasonExperienceState experience)
        {
            _battingApproaches = new int[6];
            _pitchingApproaches = new int[6];
            _pitchTypes = new int[8];
            if (experience == null)
                return;
            for (int index = 0; index < _battingApproaches.Length; index++)
                _battingApproaches[index] = experience.GetBattingApproachCount((BattingApproach)index);
            for (int index = 0; index < _pitchingApproaches.Length; index++)
                _pitchingApproaches[index] = experience.GetPitchingApproachCount((PitchingApproach)index);
            for (int index = 0; index < _pitchTypes.Length; index++)
                _pitchTypes[index] = experience.GetPitchTypeCount((PitchType)index);
        }

        public int GetBattingApproachCount(BattingApproach approach) => _battingApproaches[(int)approach];
        public int GetPitchingApproachCount(PitchingApproach approach) => _pitchingApproaches[(int)approach];
        public int GetPitchTypeCount(PitchType pitchType) => _pitchTypes[(int)pitchType];
    }

    /// <summary>한 시즌 종료 시점에 장착된 스킬 블록 한 개를 고정한다.</summary>
    public readonly struct SkillBlockArchiveSnapshot
    {
        public SkillBlockArchiveSnapshot(
            int instanceId,
            string definitionId,
            SkillBlockRarity rarity,
            SkillBlockCategory category,
            int originX,
            int originY,
            int rotationQuarterTurns)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId ?? string.Empty;
            Rarity = rarity;
            Category = category;
            OriginX = originX;
            OriginY = originY;
            RotationQuarterTurns = rotationQuarterTurns;
        }

        public int InstanceId { get; }
        public string DefinitionId { get; }
        public SkillBlockRarity Rarity { get; }
        public SkillBlockCategory Category { get; }
        public int OriginX { get; }
        public int OriginY { get; }
        public int RotationQuarterTurns { get; }
    }

    /// <summary>전성기 빌드와 마지막 빌드를 재현할 수 있는 시즌 성장판 배치다.</summary>
    public sealed class SkillBoardSeasonSnapshot
    {
        private readonly SkillBlockArchiveSnapshot[] _blocks;

        public SkillBoardSeasonSnapshot(string boardDefinitionId, SkillBlockArchiveSnapshot[] blocks)
        {
            BoardDefinitionId = boardDefinitionId ?? string.Empty;
            _blocks = blocks == null
                ? Array.Empty<SkillBlockArchiveSnapshot>()
                : (SkillBlockArchiveSnapshot[])blocks.Clone();
        }

        public string BoardDefinitionId { get; }
        public IReadOnlyList<SkillBlockArchiveSnapshot> Blocks => _blocks;
    }

    /// <summary>한 시즌 카드와 성장·계약·부상·선택 원본을 함께 보관한다.</summary>
    public sealed class CareerSeasonArchive
    {
        private readonly string[] _awards;
        private readonly string[] _memoryIds;

        public CareerSeasonArchive(
            int seasonId,
            int season,
            int age,
            LeagueLevel leagueLevel,
            int teamId,
            string teamName,
            PlayerGameRole primaryRole,
            int startOverall,
            int endOverall,
            SeasonStatSnapshot stats,
            SeasonStatSnapshot postseasonStats,
            string[] awards,
            ContractSeasonSnapshot contract,
            GrowthSeasonSnapshot growth,
            InjurySeasonSnapshot injuries,
            PlayStyleSeasonSnapshot playStyle,
            SkillBoardSeasonSnapshot skillBoard,
            string[] memoryIds)
        {
            SeasonId = seasonId;
            Season = season;
            Age = age;
            LeagueLevel = leagueLevel;
            TeamId = teamId;
            TeamName = teamName ?? string.Empty;
            PrimaryRole = primaryRole;
            StartOverall = startOverall;
            EndOverall = endOverall;
            Stats = stats;
            PostseasonStats = postseasonStats;
            _awards = awards == null ? Array.Empty<string>() : (string[])awards.Clone();
            Contract = contract;
            Growth = growth;
            Injuries = injuries;
            PlayStyle = playStyle;
            SkillBoard = skillBoard;
            _memoryIds = memoryIds == null ? Array.Empty<string>() : (string[])memoryIds.Clone();
        }

        public int SeasonId { get; }
        public int Season { get; }
        public int Age { get; }
        public LeagueLevel LeagueLevel { get; }
        public int TeamId { get; }
        public string TeamName { get; }
        public PlayerGameRole PrimaryRole { get; }
        public int StartOverall { get; }
        public int EndOverall { get; }
        public SeasonStatSnapshot Stats { get; }
        public SeasonStatSnapshot PostseasonStats { get; }
        public IReadOnlyList<string> Awards => _awards;
        public ContractSeasonSnapshot Contract { get; }
        public GrowthSeasonSnapshot Growth { get; }
        public InjurySeasonSnapshot Injuries { get; }
        public PlayStyleSeasonSnapshot PlayStyle { get; }
        public SkillBoardSeasonSnapshot SkillBoard { get; }
        public IReadOnlyList<string> MemoryIds => _memoryIds;
    }

    /// <summary>공식 통산 표시에 쓰는 정규시즌 누적과 활동 시즌 수다.</summary>
    public readonly struct CareerTotalStatSnapshot
    {
        public CareerTotalStatSnapshot(SeasonStatSnapshot totals, int seasons)
        {
            Totals = totals;
            Seasons = seasons;
        }

        public SeasonStatSnapshot Totals { get; }
        public int Seasons { get; }
    }

    /// <summary>숫자로 본 커리어와 가장 큰 선택을 한 화면에 전달한다.</summary>
    public sealed class CareerChoiceSnapshot
    {
        public CareerChoiceSnapshot(
            int teamCount,
            int contractCount,
            int renewalCount,
            int transferCount,
            int trainingCount,
            int studyCount,
            int injuryReturnCount,
            int postseasonCount,
            int championshipCount,
            CareerNamedCount mostUsedTraining,
            string longestRoleKey,
            int longestRoleSeasons,
            string longestSkillBlockId,
            int longestSkillSeasons,
            string mostUsedApproachKey,
            int mostUsedApproachCount,
            int totalApproachCount,
            string biggestChoiceMemoryId,
            long highestDeclinedAnnualSalary,
            string highestDeclinedMemoryId,
            int longestAcceptedContractYears)
        {
            TeamCount = teamCount;
            ContractCount = contractCount;
            RenewalCount = renewalCount;
            TransferCount = transferCount;
            TrainingCount = trainingCount;
            StudyCount = studyCount;
            InjuryReturnCount = injuryReturnCount;
            PostseasonCount = postseasonCount;
            ChampionshipCount = championshipCount;
            MostUsedTraining = mostUsedTraining;
            LongestRoleKey = longestRoleKey ?? string.Empty;
            LongestRoleSeasons = longestRoleSeasons;
            LongestSkillBlockId = longestSkillBlockId ?? string.Empty;
            LongestSkillSeasons = longestSkillSeasons;
            MostUsedApproachKey = mostUsedApproachKey ?? string.Empty;
            MostUsedApproachCount = mostUsedApproachCount;
            TotalApproachCount = totalApproachCount;
            BiggestChoiceMemoryId = biggestChoiceMemoryId ?? string.Empty;
            HighestDeclinedAnnualSalary = highestDeclinedAnnualSalary;
            HighestDeclinedMemoryId = highestDeclinedMemoryId ?? string.Empty;
            LongestAcceptedContractYears = longestAcceptedContractYears;
        }

        public int TeamCount { get; }
        public int ContractCount { get; }
        public int RenewalCount { get; }
        public int TransferCount { get; }
        public int TrainingCount { get; }
        public int StudyCount { get; }
        public int InjuryReturnCount { get; }
        public int PostseasonCount { get; }
        public int ChampionshipCount { get; }
        public CareerNamedCount MostUsedTraining { get; }
        public string LongestRoleKey { get; }
        public int LongestRoleSeasons { get; }
        public string LongestSkillBlockId { get; }
        public int LongestSkillSeasons { get; }
        public string MostUsedApproachKey { get; }
        public int MostUsedApproachCount { get; }
        public int TotalApproachCount { get; }
        public string BiggestChoiceMemoryId { get; }
        public long HighestDeclinedAnnualSalary { get; }
        public string HighestDeclinedMemoryId { get; }
        public int LongestAcceptedContractYears { get; }
    }

    /// <summary>실제로 계산 가능한 리그 유산만 노출한다.</summary>
    public sealed class LeagueLegacySnapshot
    {
        public LeagueLegacySnapshot(int awardCount, int championshipCount, string[] meaningfulRecords)
        {
            AwardCount = awardCount;
            ChampionshipCount = championshipCount;
            MeaningfulRecords = meaningfulRecords == null
                ? Array.Empty<string>()
                : (string[])meaningfulRecords.Clone();
        }

        public int AwardCount { get; }
        public int ChampionshipCount { get; }
        public IReadOnlyList<string> MeaningfulRecords { get; }
    }

    /// <summary>구단별 활동 기간과 계산 가능한 유산을 보관한다.</summary>
    public sealed class FranchiseLegacySnapshot
    {
        public FranchiseLegacySnapshot(
            int primaryTeamId,
            string primaryTeamName,
            int seasons,
            int games,
            string[] meaningfulRecords)
        {
            PrimaryTeamId = primaryTeamId;
            PrimaryTeamName = primaryTeamName ?? string.Empty;
            Seasons = seasons;
            Games = games;
            MeaningfulRecords = meaningfulRecords == null
                ? Array.Empty<string>()
                : (string[])meaningfulRecords.Clone();
        }

        public int PrimaryTeamId { get; }
        public string PrimaryTeamName { get; }
        public int Seasons { get; }
        public int Games { get; }
        public IReadOnlyList<string> MeaningfulRecords { get; }
    }

    /// <summary>최종 카드가 통산 기록 중 크게 남길 대표 숫자를 고정한다.</summary>
    public readonly struct CareerSignatureRecordSnapshot
    {
        public CareerSignatureRecordSnapshot(string statKey, double value, string formatKey)
        {
            StatKey = statKey ?? string.Empty;
            Value = value;
            FormatKey = formatKey ?? string.Empty;
        }

        public string StatKey { get; }
        public double Value { get; }
        public string FormatKey { get; }
    }

    /// <summary>은퇴 순간에 확정되어 회고와 기록관이 유일하게 읽는 불변 결과다.</summary>
    public sealed class RetirementRecapSnapshot
    {
        private readonly CareerSeasonArchive[] _seasons;
        private readonly CareerMemoryRecord[] _featuredMemories;

        public RetirementRecapSnapshot(
            int snapshotVersion,
            int playerId,
            string playerName,
            PlayerPosition position,
            Handedness battingHand,
            Handedness throwingHand,
            int debutSeason,
            int retirementSeason,
            RetirementReason retirementReason,
            string careerTitlePrimary,
            string careerTitleSecondary,
            CareerSeasonArchive[] seasons,
            CareerMemoryRecord[] featuredMemories,
            CareerTotalStatSnapshot careerStats,
            CareerChoiceSnapshot careerChoices,
            LeagueLegacySnapshot leagueLegacy,
            FranchiseLegacySnapshot franchiseLegacy,
            int careerBestSeason,
            CareerSignatureRecordSnapshot signatureRecord,
            string finalNarrativeKey,
            string finalPresentationAssetKey)
        {
            SnapshotVersion = snapshotVersion;
            PlayerId = playerId;
            PlayerName = playerName ?? string.Empty;
            Position = position;
            BattingHand = battingHand;
            ThrowingHand = throwingHand;
            DebutSeason = debutSeason;
            RetirementSeason = retirementSeason;
            RetirementReason = retirementReason;
            CareerTitlePrimary = careerTitlePrimary ?? string.Empty;
            CareerTitleSecondary = careerTitleSecondary ?? string.Empty;
            _seasons = seasons == null
                ? Array.Empty<CareerSeasonArchive>()
                : (CareerSeasonArchive[])seasons.Clone();
            _featuredMemories = featuredMemories == null
                ? Array.Empty<CareerMemoryRecord>()
                : (CareerMemoryRecord[])featuredMemories.Clone();
            CareerStats = careerStats;
            CareerChoices = careerChoices;
            LeagueLegacy = leagueLegacy;
            FranchiseLegacy = franchiseLegacy;
            CareerBestSeason = careerBestSeason;
            SignatureRecord = signatureRecord;
            FinalNarrativeKey = finalNarrativeKey ?? string.Empty;
            FinalPresentationAssetKey = finalPresentationAssetKey ?? string.Empty;
        }

        public int SnapshotVersion { get; }
        public int PlayerId { get; }
        public string PlayerName { get; }
        public PlayerPosition Position { get; }
        public Handedness BattingHand { get; }
        public Handedness ThrowingHand { get; }
        public int DebutSeason { get; }
        public int RetirementSeason { get; }
        public RetirementReason RetirementReason { get; }
        public string CareerTitlePrimary { get; }
        public string CareerTitleSecondary { get; }
        public IReadOnlyList<CareerSeasonArchive> Seasons => _seasons;
        public IReadOnlyList<CareerMemoryRecord> FeaturedMemories => _featuredMemories;
        public CareerTotalStatSnapshot CareerStats { get; }
        public CareerChoiceSnapshot CareerChoices { get; }
        public LeagueLegacySnapshot LeagueLegacy { get; }
        public FranchiseLegacySnapshot FranchiseLegacy { get; }
        public int CareerBestSeason { get; }
        public CareerSignatureRecordSnapshot SignatureRecord { get; }
        public string FinalNarrativeKey { get; }
        public string FinalPresentationAssetKey { get; }
    }

    /// <summary>은퇴 선언과 확정 스냅샷, 시즌 Archive를 커리어 세이브에서 소유한다.</summary>
    public sealed class CareerRetirementState
    {
        private readonly List<CareerSeasonArchive> _seasons = new();

        public CareerRetirementState(int saveVersion)
        {
            SaveVersion = saveVersion;
            MemoryLog = new CareerMemoryLog();
        }

        public int SaveVersion { get; private set; }
        public CareerMemoryLog MemoryLog { get; }
        public int DeclaredFinalSeasonId { get; private set; }
        public RetirementRecapSnapshot Snapshot { get; private set; }
        public LeagueId LastLeagueId { get; private set; }
        public int LastTeamId { get; private set; }
        public int LastOfficialGameId { get; private set; }
        public int LastOfficialGameYear { get; private set; }
        public int LastOfficialGameRound { get; private set; }
        public int LastOfficialGameTeamId { get; private set; }
        public IReadOnlyList<CareerSeasonArchive> Seasons => _seasons;
        public bool IsFinalSeasonDeclared => DeclaredFinalSeasonId > 0;
        public bool IsRetired => Snapshot != null;

        public void DeclareFinalSeason(int seasonId)
        {
            if (seasonId <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonId));
            if (IsRetired)
                throw new InvalidOperationException("이미 은퇴가 확정되었습니다.");
            if (IsFinalSeasonDeclared)
                throw new InvalidOperationException("마지막 시즌이 이미 선언되었습니다.");
            DeclaredFinalSeasonId = seasonId;
        }

        public void AddSeason(CareerSeasonArchive season)
        {
            if (season == null)
                throw new ArgumentNullException(nameof(season));
            for (int index = 0; index < _seasons.Count; index++)
            {
                if (_seasons[index].SeasonId == season.SeasonId)
                    return;
            }
            if (_seasons.Count > 0 && _seasons[^1].Season >= season.Season)
                throw new InvalidOperationException("시즌 Archive는 시간 순서대로 추가해야 합니다.");
            _seasons.Add(season);
        }

        public void RecordOfficialGame(int gameId, int year, int round, int teamId)
        {
            if (gameId <= 0 || year <= 0 || round <= 0 || teamId <= 0)
                throw new ArgumentOutOfRangeException(nameof(gameId));
            if (LastOfficialGameYear > year ||
                LastOfficialGameYear == year && LastOfficialGameRound > round)
            {
                throw new InvalidOperationException("마지막 공식 출전은 시간 순서대로 기록해야 합니다.");
            }
            LastOfficialGameId = gameId;
            LastOfficialGameYear = year;
            LastOfficialGameRound = round;
            LastOfficialGameTeamId = teamId;
        }

        public void Complete(RetirementRecapSnapshot snapshot, LeagueId lastLeagueId, int lastTeamId)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (Snapshot != null)
                throw new InvalidOperationException("은퇴 스냅샷은 한 번만 확정할 수 있습니다.");
            if (!lastLeagueId.IsAssigned || lastTeamId <= 0)
                throw new ArgumentException("마지막 소속 정보가 필요합니다.");
            Snapshot = snapshot;
            LastLeagueId = lastLeagueId;
            LastTeamId = lastTeamId;
        }

        public void UpgradeSaveVersion(int saveVersion)
        {
            if (saveVersion <= SaveVersion)
                throw new ArgumentOutOfRangeException(nameof(saveVersion));
            SaveVersion = saveVersion;
        }
    }
}
