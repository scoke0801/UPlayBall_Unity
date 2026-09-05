using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Simulation.Career;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Game.Historical
{
    /// <summary>정본 TeamSeasonKey와 경기 일정용 정수 TeamId를 연결한다.</summary>
    public sealed class ManagerTeamReference
    {
        public ManagerTeamReference(int teamId, string teamSeasonKey)
        {
            if (teamId <= 0) throw new ArgumentOutOfRangeException(nameof(teamId));
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(teamSeasonKey));
            TeamId = teamId;
            TeamSeasonKey = teamSeasonKey.Trim();
        }

        public int TeamId { get; }
        public string TeamSeasonKey { get; }
    }

    /// <summary>구단주 모드의 저장 가능한 현재 시즌·주차·일정을 소유한다.</summary>
    public sealed class ManagerLiveSeasonState
    {
        private readonly ManagerTeamReference[] _teams;

        public ManagerLiveSeasonState(
            string seasonId,
            int seasonNumber,
            int originYear,
            int currentWeekIndex,
            int playerTeamId,
            IReadOnlyList<ManagerTeamReference> teams,
            SeasonScheduleState schedule)
        {
            if (string.IsNullOrWhiteSpace(seasonId))
                throw new ArgumentException("SeasonId는 비어 있을 수 없습니다.", nameof(seasonId));
            if (seasonNumber <= 0 || originYear <= 0 || currentWeekIndex < 0 || playerTeamId <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonNumber));
            if (teams == null || teams.Count < 2)
                throw new ArgumentException("두 구단 이상의 Team reference가 필요합니다.", nameof(teams));

            SeasonId = seasonId.Trim();
            SeasonNumber = seasonNumber;
            OriginYear = originYear;
            CurrentWeekIndex = currentWeekIndex;
            PlayerTeamId = playerTeamId;
            Schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _teams = new ManagerTeamReference[teams.Count];
            var ids = new HashSet<int>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            bool foundPlayer = false;
            for (int index = 0; index < teams.Count; index++)
            {
                ManagerTeamReference team = teams[index]
                    ?? throw new ArgumentException("null Team reference가 있습니다.", nameof(teams));
                if (!ids.Add(team.TeamId) || !keys.Add(team.TeamSeasonKey))
                    throw new ArgumentException("TeamId와 TeamSeasonKey는 중복될 수 없습니다.", nameof(teams));
                if (team.TeamId == playerTeamId) foundPlayer = true;
                _teams[index] = team;
            }
            if (!foundPlayer)
                throw new ArgumentException("PlayerTeamId가 Team reference에 없습니다.", nameof(playerTeamId));
            Array.Sort(_teams, (left, right) => left.TeamId.CompareTo(right.TeamId));
        }

        public string SeasonId { get; }
        public int SeasonNumber { get; }
        public int OriginYear { get; }
        public int CurrentWeekIndex { get; private set; }
        public int PlayerTeamId { get; }
        public IReadOnlyList<ManagerTeamReference> Teams => _teams;
        public SeasonScheduleState Schedule { get; }
        public ScheduledGameState NextPlayerGame => Schedule.GetNextGameForTeam(PlayerTeamId);

        public void AdvanceWeek()
        {
            CurrentWeekIndex = checked(CurrentWeekIndex + 1);
        }

        public string GetTeamSeasonKey(int teamId)
        {
            for (int index = 0; index < _teams.Length; index++)
                if (_teams[index].TeamId == teamId) return _teams[index].TeamSeasonKey;
            throw new KeyNotFoundException($"TeamId {teamId}의 TeamSeasonKey가 없습니다.");
        }
    }

    /// <summary>네 확장 시스템의 저장 원본을 구단주 모드 한 Aggregate로 묶는다.</summary>
    public sealed class ManagerModeRuntimeState
    {
        private readonly List<StaffContractState> _staffContracts;
        private readonly List<LineupPresetState> _lineupPresets;
        private readonly TeamSeasonPlayerStatusState[] _playerStatuses;
        private readonly TeamChemistryFamiliarityState[] _familiarities;

        public ManagerModeRuntimeState(
            ClubOperationState clubOperation,
            StaffCatalog staffCatalog,
            IReadOnlyList<StaffContractState> staffContracts,
            TeamStaffAssignmentState staffAssignment,
            IReadOnlyList<LineupPresetState> lineupPresets,
            string selectedLineupPresetId,
            IReadOnlyList<TeamSeasonPlayerStatusState> playerStatuses,
            IReadOnlyList<TeamChemistryFamiliarityState> familiarities,
            ManagerLiveSeasonState liveSeason)
        {
            ClubOperation = clubOperation ?? throw new ArgumentNullException(nameof(clubOperation));
            StaffCatalog = staffCatalog ?? throw new ArgumentNullException(nameof(staffCatalog));
            StaffAssignment = staffAssignment ?? throw new ArgumentNullException(nameof(staffAssignment));
            LiveSeason = liveSeason ?? throw new ArgumentNullException(nameof(liveSeason));
            if (!string.Equals(ClubOperation.TeamSeasonKey, StaffAssignment.TeamSeasonKey, StringComparison.Ordinal))
                throw new ArgumentException("구단 운영 상태와 Staff Assignment의 TeamSeasonKey가 다릅니다.");
            if (!string.Equals(
                    ClubOperation.TeamSeasonKey,
                    LiveSeason.GetTeamSeasonKey(LiveSeason.PlayerTeamId),
                    StringComparison.Ordinal))
                throw new ArgumentException("Live Season의 플레이어 구단과 운영 상태가 다릅니다.");

            _staffContracts = CopyContracts(staffContracts, StaffCatalog);
            _lineupPresets = CopyPresets(lineupPresets);
            SelectedLineupPresetId = RequireSelectedPreset(selectedLineupPresetId, _lineupPresets);
            _playerStatuses = CopyPlayerStatuses(playerStatuses, LiveSeason.Teams);
            _familiarities = CopyFamiliarities(familiarities, LiveSeason.Teams);
        }

        public ClubOperationState ClubOperation { get; private set; }
        public StaffCatalog StaffCatalog { get; }
        public IReadOnlyList<StaffContractState> StaffContracts => _staffContracts;
        public TeamStaffAssignmentState StaffAssignment { get; private set; }
        public IReadOnlyList<LineupPresetState> LineupPresets => _lineupPresets;
        public string SelectedLineupPresetId { get; private set; }
        public IReadOnlyList<TeamSeasonPlayerStatusState> PlayerStatuses => _playerStatuses;
        public IReadOnlyList<TeamChemistryFamiliarityState> Familiarities => _familiarities;
        public ManagerLiveSeasonState LiveSeason { get; private set; }

        public LineupPresetState GetSelectedLineupPreset()
        {
            for (int index = 0; index < _lineupPresets.Count; index++)
                if (string.Equals(_lineupPresets[index].PresetId, SelectedLineupPresetId, StringComparison.Ordinal))
                    return _lineupPresets[index];
            throw new InvalidOperationException("선택된 LineupPreset이 없습니다.");
        }

        public void SelectLineupPreset(string presetId)
        {
            SelectedLineupPresetId = RequireSelectedPreset(presetId, _lineupPresets);
        }

        public void UpsertLineupPreset(LineupPresetState preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            for (int index = 0; index < _lineupPresets.Count; index++)
            {
                if (!string.Equals(_lineupPresets[index].PresetId, preset.PresetId, StringComparison.Ordinal))
                    continue;
                _lineupPresets[index] = preset;
                return;
            }
            _lineupPresets.Add(preset);
            _lineupPresets.Sort((left, right) => string.CompareOrdinal(left.PresetId, right.PresetId));
        }

        public void ReplaceStaffState(
            IReadOnlyList<StaffContractState> contracts,
            TeamStaffAssignmentState assignment)
        {
            if (assignment == null) throw new ArgumentNullException(nameof(assignment));
            if (!string.Equals(assignment.TeamSeasonKey, ClubOperation.TeamSeasonKey, StringComparison.Ordinal))
                throw new ArgumentException("다른 구단의 Staff Assignment를 적용할 수 없습니다.", nameof(assignment));
            List<StaffContractState> validated = CopyContracts(contracts, StaffCatalog);
            _staffContracts.Clear();
            _staffContracts.AddRange(validated);
            StaffAssignment = assignment;
        }

        /// <summary>검증을 마친 다음 시즌 운영·일정·Staff 상태를 한 번에 교체한다.</summary>
        internal void AdvanceSeason(
            ClubOperationState clubOperation,
            ManagerLiveSeasonState liveSeason,
            IReadOnlyList<StaffContractState> staffContracts,
            TeamStaffAssignmentState staffAssignment)
        {
            if (clubOperation == null) throw new ArgumentNullException(nameof(clubOperation));
            if (liveSeason == null) throw new ArgumentNullException(nameof(liveSeason));
            if (staffAssignment == null) throw new ArgumentNullException(nameof(staffAssignment));
            if (liveSeason.SeasonNumber != LiveSeason.SeasonNumber + 1 ||
                liveSeason.OriginYear != LiveSeason.OriginYear ||
                liveSeason.CurrentWeekIndex != 0)
            {
                throw new ArgumentException("다음 시즌 번호·기준 연도·주차가 올바르지 않습니다.", nameof(liveSeason));
            }
            if (!string.Equals(clubOperation.TeamSeasonKey, ClubOperation.TeamSeasonKey, StringComparison.Ordinal) ||
                !string.Equals(clubOperation.CurrentSeason.SeasonId, liveSeason.SeasonId, StringComparison.Ordinal) ||
                !string.Equals(staffAssignment.TeamSeasonKey, ClubOperation.TeamSeasonKey, StringComparison.Ordinal))
            {
                throw new ArgumentException("다음 시즌 운영·일정·Staff의 구단/시즌 계약이 일치하지 않습니다.");
            }
            ValidateSameTeamReferences(LiveSeason, liveSeason);

            List<StaffContractState> validatedContracts = CopyContracts(staffContracts, StaffCatalog);
            _staffContracts.Clear();
            _staffContracts.AddRange(validatedContracts);
            StaffAssignment = staffAssignment;
            ClubOperation = clubOperation;
            LiveSeason = liveSeason;
        }

        public TeamSeasonPlayerStatusState GetPlayerStatus(string teamSeasonKey)
        {
            for (int index = 0; index < _playerStatuses.Length; index++)
                if (string.Equals(_playerStatuses[index].TeamSeasonKey, teamSeasonKey, StringComparison.Ordinal))
                    return _playerStatuses[index];
            throw new KeyNotFoundException($"TeamSeasonKey {teamSeasonKey}의 Player Status가 없습니다.");
        }

        public TeamChemistryFamiliarityState GetFamiliarity(string teamSeasonKey)
        {
            for (int index = 0; index < _familiarities.Length; index++)
                if (string.Equals(_familiarities[index].TeamSeasonKey, teamSeasonKey, StringComparison.Ordinal))
                    return _familiarities[index];
            throw new KeyNotFoundException($"TeamSeasonKey {teamSeasonKey}의 Familiarity가 없습니다.");
        }

        private static List<StaffContractState> CopyContracts(
            IReadOnlyList<StaffContractState> source,
            StaffCatalog catalog)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var result = new List<StaffContractState>(source.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                StaffContractState contract = source[index]
                    ?? throw new ArgumentException("null Staff Contract가 있습니다.", nameof(source));
                catalog.Get(contract.StaffId);
                if (!ids.Add(contract.ContractId))
                    throw new ArgumentException("ContractId는 중복될 수 없습니다.", nameof(source));
                result.Add(contract);
            }
            result.Sort((left, right) => string.CompareOrdinal(left.ContractId, right.ContractId));
            return result;
        }

        private static List<LineupPresetState> CopyPresets(IReadOnlyList<LineupPresetState> source)
        {
            if (source == null || source.Count == 0)
                throw new ArgumentException("하나 이상의 LineupPreset이 필요합니다.", nameof(source));
            var result = new List<LineupPresetState>(source.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                LineupPresetState preset = source[index]
                    ?? throw new ArgumentException("null LineupPreset이 있습니다.", nameof(source));
                if (!ids.Add(preset.PresetId))
                    throw new ArgumentException("PresetId는 중복될 수 없습니다.", nameof(source));
                result.Add(preset);
            }
            result.Sort((left, right) => string.CompareOrdinal(left.PresetId, right.PresetId));
            return result;
        }

        private static string RequireSelectedPreset(string value, IReadOnlyList<LineupPresetState> presets)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("SelectedLineupPresetId는 비어 있을 수 없습니다.", nameof(value));
            string id = value.Trim();
            for (int index = 0; index < presets.Count; index++)
                if (string.Equals(presets[index].PresetId, id, StringComparison.Ordinal)) return id;
            throw new ArgumentException("SelectedLineupPresetId가 LineupPreset 목록에 없습니다.", nameof(value));
        }

        private static TeamSeasonPlayerStatusState[] CopyPlayerStatuses(
            IReadOnlyList<TeamSeasonPlayerStatusState> source,
            IReadOnlyList<ManagerTeamReference> teams)
        {
            if (source == null || source.Count != teams.Count)
                throw new ArgumentException("모든 참가 구단의 Player Status가 필요합니다.", nameof(source));
            var result = new TeamSeasonPlayerStatusState[source.Count];
            for (int index = 0; index < result.Length; index++)
            {
                string key = teams[index].TeamSeasonKey;
                for (int candidate = 0; candidate < source.Count; candidate++)
                {
                    if (string.Equals(source[candidate]?.TeamSeasonKey, key, StringComparison.Ordinal))
                    {
                        result[index] = source[candidate];
                        break;
                    }
                }
                if (result[index] == null)
                    throw new ArgumentException($"{key} Player Status가 없습니다.", nameof(source));
            }
            return result;
        }

        private static TeamChemistryFamiliarityState[] CopyFamiliarities(
            IReadOnlyList<TeamChemistryFamiliarityState> source,
            IReadOnlyList<ManagerTeamReference> teams)
        {
            if (source == null || source.Count != teams.Count)
                throw new ArgumentException("모든 참가 구단의 Familiarity가 필요합니다.", nameof(source));
            var result = new TeamChemistryFamiliarityState[source.Count];
            for (int index = 0; index < result.Length; index++)
            {
                string key = teams[index].TeamSeasonKey;
                for (int candidate = 0; candidate < source.Count; candidate++)
                {
                    if (string.Equals(source[candidate]?.TeamSeasonKey, key, StringComparison.Ordinal))
                    {
                        result[index] = source[candidate];
                        break;
                    }
                }
                if (result[index] == null)
                    throw new ArgumentException($"{key} Familiarity가 없습니다.", nameof(source));
            }
            return result;
        }

        private static void ValidateSameTeamReferences(
            ManagerLiveSeasonState current,
            ManagerLiveSeasonState next)
        {
            if (current.PlayerTeamId != next.PlayerTeamId || current.Teams.Count != next.Teams.Count)
                throw new ArgumentException("다음 시즌 참가 구단 구성이 달라졌습니다.", nameof(next));
            for (int index = 0; index < current.Teams.Count; index++)
            {
                ManagerTeamReference left = current.Teams[index];
                ManagerTeamReference right = next.Teams[index];
                if (left.TeamId != right.TeamId ||
                    !string.Equals(left.TeamSeasonKey, right.TeamSeasonKey, StringComparison.Ordinal))
                {
                    throw new ArgumentException("다음 시즌 Team reference가 현재 Historical snapshot과 다릅니다.", nameof(next));
                }
            }
        }
    }

    /// <summary>새 구단주 Save에 라이브 시즌과 네 확장 시스템의 초기 상태를 결정론적으로 만든다.</summary>
    public static class ManagerModeRuntimeFactory
    {
        private const ulong ScheduleStream = 0x4D4752534348444CUL;

        public static ManagerModeRuntimeState CreateInitial(
            string playerTeamSeasonKey,
            int originYear,
            ulong worldSeed,
            LeagueInstance league,
            IReadOnlyList<CurrentRosterState> rosters,
            StaffCatalog staffCatalog,
            BalanceTable balance)
        {
            if (league == null) throw new ArgumentNullException(nameof(league));
            if (rosters == null) throw new ArgumentNullException(nameof(rosters));
            if (staffCatalog == null) throw new ArgumentNullException(nameof(staffCatalog));
            if (balance == null) throw new ArgumentNullException(nameof(balance));

            ManagerTeamReference[] teams = CreateTeamReferences(league);
            int playerTeamId = FindTeamId(teams, playerTeamSeasonKey);
            SeasonScheduleState schedule = CreateSchedule(
                teams,
                worldSeed,
                1,
                balance.CareerSeason.RegularSeasonGamesPerTeam);
            string seasonId = $"manager:{originYear}:1";
            var liveSeason = new ManagerLiveSeasonState(
                seasonId,
                1,
                originYear,
                0,
                playerTeamId,
                teams,
                schedule);
            ClubOperationState operation = CreateClubOperation(playerTeamSeasonKey, seasonId, balance.ClubOperation);
            LineupPresetState defaultPreset = CreateDefaultPreset(FindRoster(rosters, playerTeamSeasonKey));
            CreateTeamStates(rosters, balance.ConditionChemistry.NeutralMatchCondition,
                out TeamSeasonPlayerStatusState[] statuses,
                out TeamChemistryFamiliarityState[] familiarities);
            return new ManagerModeRuntimeState(
                operation,
                staffCatalog,
                Array.Empty<StaffContractState>(),
                new TeamStaffAssignmentState(playerTeamSeasonKey),
                new[] { defaultPreset },
                defaultPreset.PresetId,
                statuses,
                familiarities,
                liveSeason);
        }

        private static ManagerTeamReference[] CreateTeamReferences(LeagueInstance league)
        {
            var keys = new string[league.ParticipantTeamCount];
            int index = 0;
            for (int regular = 0; regular < league.RegularTeamSeasonKeys.Count; regular++)
                keys[index++] = league.RegularTeamSeasonKeys[regular];
            for (int special = 0; special < league.SpecialCompositeTeams.Count; special++)
                keys[index++] = league.SpecialCompositeTeams[special].TeamSeasonKey;
            Array.Sort(keys, StringComparer.Ordinal);
            var result = new ManagerTeamReference[keys.Length];
            for (int teamIndex = 0; teamIndex < keys.Length; teamIndex++)
                result[teamIndex] = new ManagerTeamReference(teamIndex + 1, keys[teamIndex]);
            return result;
        }

        private static int FindTeamId(IReadOnlyList<ManagerTeamReference> teams, string teamSeasonKey)
        {
            for (int index = 0; index < teams.Count; index++)
                if (string.Equals(teams[index].TeamSeasonKey, teamSeasonKey, StringComparison.Ordinal))
                    return teams[index].TeamId;
            throw new ArgumentException("플레이어 구단이 리그 참가팀에 없습니다.", nameof(teamSeasonKey));
        }

        /// <summary>현재 Historical roster snapshot을 유지한 채 다음 운영 시즌 일정을 결정론적으로 만든다.</summary>
        public static ManagerLiveSeasonState CreateNextSeason(
            ManagerLiveSeasonState current,
            ulong worldSeed,
            int configuredGamesPerTeam)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            int nextSeasonNumber = checked(current.SeasonNumber + 1);
            string seasonId = $"manager:{current.OriginYear}:{nextSeasonNumber}";
            SeasonScheduleState schedule = CreateSchedule(
                current.Teams,
                worldSeed,
                nextSeasonNumber,
                configuredGamesPerTeam);
            return new ManagerLiveSeasonState(
                seasonId,
                nextSeasonNumber,
                current.OriginYear,
                0,
                current.PlayerTeamId,
                current.Teams,
                schedule);
        }

        /// <summary>팬·구장·시설·티켓 정책을 유지하고 새 시즌 재무/영수증 경계를 연다.</summary>
        public static ClubOperationState CreateNextClubOperation(
            ClubOperationState current,
            string nextSeasonId)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (string.IsNullOrWhiteSpace(nextSeasonId))
                throw new ArgumentException("다음 SeasonId가 필요합니다.", nameof(nextSeasonId));
            var facilities = new FacilityState[current.Facilities.Count];
            for (int index = 0; index < facilities.Length; index++)
            {
                FacilityState source = current.Facilities[index];
                facilities[index] = new FacilityState(source.Type, source.Level);
            }
            return new ClubOperationState(
                current.TeamSeasonKey,
                current.FanBase,
                current.Popularity,
                current.AttendanceMomentum,
                new StadiumState(current.Stadium.Level, current.Stadium.Capacity),
                facilities,
                new TicketPolicy(current.TicketPolicy.PriceTier),
                new WeeklyOperationLedger(nextSeasonId, 0),
                new SeasonFinanceSummary(nextSeasonId));
        }

        private static SeasonScheduleState CreateSchedule(
            IReadOnlyList<ManagerTeamReference> teams,
            ulong worldSeed,
            int seasonNumber,
            int configuredGamesPerTeam)
        {
            if (seasonNumber <= 0) throw new ArgumentOutOfRangeException(nameof(seasonNumber));
            if (configuredGamesPerTeam <= 0)
                throw new ArgumentOutOfRangeException(nameof(configuredGamesPerTeam));
            var ids = new int[teams.Count];
            for (int index = 0; index < ids.Length; index++) ids[index] = teams[index].TeamId;
            int gamesPerTeam = ResolveCompatibleGamesPerTeam(teams.Count, configuredGamesPerTeam);
            ulong scheduleSeed = DeterministicSeed.Derive(
                DeterministicSeed.Derive(worldSeed, ScheduleStream),
                unchecked((ulong)seasonNumber));
            var generator = new SeasonScheduleGenerator(
                new Pcg32Random(scheduleSeed));
            ScheduledGameDefinition[] definitions = generator.Generate(ids, gamesPerTeam);
            var games = new ScheduledGameState[definitions.Length];
            for (int index = 0; index < games.Length; index++)
            {
                ScheduledGameDefinition definition = definitions[index];
                games[index] = new ScheduledGameState(
                    definition.GameId,
                    definition.Round,
                    DeterministicSeed.Derive(scheduleSeed, unchecked((ulong)definition.GameId)),
                    definition.AwayTeamId,
                    definition.HomeTeamId);
            }
            return new SeasonScheduleState(games);
        }

        private static int ResolveCompatibleGamesPerTeam(int teamCount, int configuredGamesPerTeam)
        {
            if (teamCount < 2) throw new ArgumentOutOfRangeException(nameof(teamCount));
            if ((teamCount & 1) == 0)
                return configuredGamesPerTeam;

            int opponentCount = teamCount - 1;
            int compatibleGames = configuredGamesPerTeam - configuredGamesPerTeam % opponentCount;
            if (compatibleGames <= 0)
                throw new InvalidOperationException("설정된 경기 수로 홀수 구단 균등 대진을 만들 수 없습니다.");
            // 기존 SeasonScheduleGenerator는 홀수 구단에서 전 구단 동률 경기 수를 보장하려면
            // 상대 구단 수 단위의 완전 cycle을 요구한다. 설정값을 넘기지 않는 가장 가까운 값만 사용한다.
            return compatibleGames;
        }

        private static ClubOperationState CreateClubOperation(
            string teamSeasonKey,
            string seasonId,
            ClubOperationBalanceTable balance)
        {
            StadiumLevelDefinition stadium = balance.GetStadiumLevel(1);
            int count = Enum.GetValues(typeof(FacilityType)).Length;
            var facilities = new FacilityState[count];
            for (int index = 0; index < count; index++)
                facilities[index] = new FacilityState((FacilityType)index, 0);
            return new ClubOperationState(
                teamSeasonKey,
                50d,
                50d,
                50d,
                new StadiumState(stadium.Level, stadium.Capacity),
                facilities,
                new TicketPolicy(TicketPriceTier.Standard),
                new WeeklyOperationLedger(seasonId, 0),
                new SeasonFinanceSummary(seasonId));
        }

        private static LineupPresetState CreateDefaultPreset(CurrentRosterState roster)
        {
            var starting = new LineupPresetSlot[ActiveRosterCompositionRule.StartingHitterCount];
            var batting = new string[starting.Length];
            for (int index = 0; index < starting.Length; index++)
            {
                ActiveRosterEntry entry = FindRole(roster, (ActiveRosterRole)index);
                starting[index] = new LineupPresetSlot(entry.CardId, (PlayerPosition)(index + 1));
                batting[index] = entry.CardId;
            }
            var bench = new string[ActiveRosterCompositionRule.BenchHitterCount];
            int benchIndex = 0;
            var starters = new string[ActiveRosterCompositionRule.StartingPitcherCount];
            var bullpen = new string[ActiveRosterCompositionRule.BullpenPitcherCount];
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                if (entry.Role == ActiveRosterRole.BenchHitter) bench[benchIndex++] = entry.CardId;
                else if (entry.Role >= ActiveRosterRole.StartingPitcher1 && entry.Role <= ActiveRosterRole.StartingPitcher5)
                    starters[(int)entry.Role - (int)ActiveRosterRole.StartingPitcher1] = entry.CardId;
                else if (entry.Role >= ActiveRosterRole.Bullpen1 && entry.Role <= ActiveRosterRole.Bullpen4)
                    bullpen[(int)entry.Role - (int)ActiveRosterRole.Bullpen1] = entry.CardId;
            }
            return new LineupPresetState(
                "preset:default",
                "기본 라인업",
                starting,
                batting,
                bench,
                starters,
                bullpen,
                FindRole(roster, ActiveRosterRole.Setup).CardId,
                FindRole(roster, ActiveRosterRole.Closer).CardId,
                new string[LineupPresetState.TeamColorSlotCount],
                Array.Empty<string>());
        }

        private static ActiveRosterEntry FindRole(CurrentRosterState roster, ActiveRosterRole role)
        {
            for (int index = 0; index < roster.Entries.Count; index++)
                if (roster.Entries[index].Role == role) return roster.Entries[index];
            throw new InvalidOperationException($"{roster.TeamSeasonKey} 로스터에 {role} 슬롯이 없습니다.");
        }

        private static CurrentRosterState FindRoster(
            IReadOnlyList<CurrentRosterState> rosters,
            string teamSeasonKey)
        {
            for (int index = 0; index < rosters.Count; index++)
                if (string.Equals(rosters[index].TeamSeasonKey, teamSeasonKey, StringComparison.Ordinal))
                    return rosters[index];
            throw new ArgumentException("플레이어 구단 로스터가 없습니다.", nameof(teamSeasonKey));
        }

        private static void CreateTeamStates(
            IReadOnlyList<CurrentRosterState> rosters,
            int initialCondition,
            out TeamSeasonPlayerStatusState[] statuses,
            out TeamChemistryFamiliarityState[] familiarities)
        {
            statuses = new TeamSeasonPlayerStatusState[rosters.Count];
            familiarities = new TeamChemistryFamiliarityState[rosters.Count];
            for (int teamIndex = 0; teamIndex < rosters.Count; teamIndex++)
            {
                CurrentRosterState roster = rosters[teamIndex];
                var players = new TeamSeasonPlayerStatus[roster.Entries.Count];
                for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
                    players[playerIndex] = new TeamSeasonPlayerStatus(
                        roster.Entries[playerIndex].PlayerPersonId,
                        initialCondition);
                statuses[teamIndex] = new TeamSeasonPlayerStatusState(roster.TeamSeasonKey, players);
                familiarities[teamIndex] = new TeamChemistryFamiliarityState(roster.TeamSeasonKey);
            }
        }
    }
}
