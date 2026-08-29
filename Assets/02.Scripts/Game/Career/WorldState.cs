using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 동시 존재하는 리그와 전역 구단·선수·계약 레지스트리를 소유한다.
    /// </summary>
    public sealed class WorldState
    {
        private readonly List<LeagueState> _leagues;
        private readonly List<TeamState> _teams;
        private readonly List<PlayerState> _players;
        private readonly List<PlayerContractState> _contracts;

        public WorldState(
            ulong worldSeed,
            GlobalCalendarState calendar,
            IReadOnlyList<LeagueState> leagues,
            IReadOnlyList<PlayerState> players,
            IReadOnlyList<PlayerContractState> contracts,
            int historyStartYear,
            PlayerMovementLedger movementLedger = null,
            TeamLeagueMovementLedger teamMovementLedger = null,
            WorldRecordState records = null,
            DomainEventJournal domainEvents = null)
        {
            WorldSeed = worldSeed;
            Calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
            HistoryStartYear = historyStartYear;
            MovementLedger = movementLedger ?? new PlayerMovementLedger();
            TeamMovementLedger = teamMovementLedger ?? new TeamLeagueMovementLedger();
            Records = records ?? new WorldRecordState(historyStartYear);
            DomainEvents = domainEvents ?? new DomainEventJournal();
            _leagues = CopyAndSortLeagues(leagues);
            _players = CopyAndSortPlayers(players);
            _contracts = CopyAndSortContracts(contracts);
            _teams = BuildTeamRegistry(_leagues);
            ValidateInvariants();
        }

        public ulong WorldSeed { get; }
        public GlobalCalendarState Calendar { get; }
        public int HistoryStartYear { get; }
        public PlayerMovementLedger MovementLedger { get; }
        public TeamLeagueMovementLedger TeamMovementLedger { get; }
        public WorldRecordState Records { get; }
        public DomainEventJournal DomainEvents { get; }
        public IReadOnlyList<LeagueState> Leagues => _leagues;
        public IReadOnlyList<TeamState> Teams => _teams;
        public IReadOnlyList<PlayerState> Players => _players;
        public IReadOnlyList<PlayerContractState> Contracts => _contracts;

        public LeagueState GetLeague(LeagueId leagueId)
        {
            for (int index = 0; index < _leagues.Count; index++)
            {
                if (_leagues[index].LeagueId == leagueId)
                    return _leagues[index];
            }
            throw new InvalidOperationException($"LeagueId {leagueId}를 찾을 수 없습니다.");
        }

        public TeamState GetTeam(int teamId)
        {
            int index = FindTeamIndex(teamId);
            if (index < 0)
                throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
            return _teams[index];
        }

        public PlayerState GetPlayer(int playerId)
        {
            int index = FindPlayerIndex(playerId);
            if (index < 0)
                throw new InvalidOperationException($"PlayerId {playerId}를 찾을 수 없습니다.");
            return _players[index];
        }

        public LeagueState GetLeagueForTeam(int teamId) => GetLeague(GetTeam(teamId).LeagueId);

        /// <summary>
        /// 시즌 전환으로 생긴 같은 리그의 새 상태와 구단 스냅샷을 전역 레지스트리에 반영한다.
        /// </summary>
        public void ReplaceLeague(LeagueState league)
        {
            if (league == null)
                throw new ArgumentNullException(nameof(league));
            int leagueIndex = FindLeagueIndex(league.LeagueId);
            _leagues[leagueIndex] = league;
            RebuildTeamsForLeague(league);
            SynchronizeRosterPlayers(league);
            ValidateInvariants();
        }

        /// <summary>
        /// 동일 리그 트레이드로 교체된 두 구단을 리그와 전역 레지스트리에 함께 커밋한다.
        /// </summary>
        public void ReplaceTeams(TeamState first, TeamState second)
        {
            if (first == null || second == null || first.TeamId == second.TeamId)
                throw new ArgumentException("서로 다른 두 구단 상태가 필요합니다.");
            if (first.LeagueId != second.LeagueId)
                throw new InvalidOperationException("시즌 중 트레이드는 동일 리그 구단 사이에서만 가능합니다.");

            LeagueState league = GetLeague(first.LeagueId);
            league.ReplaceTeams(first, second);
            ReplaceTeamInRegistry(first);
            ReplaceTeamInRegistry(second);
            ValidateInvariants();
        }

        /// <summary>현역 선수의 마지막 소속·계약을 정리하고 월드 역사에 은퇴를 한 번 기록한다.</summary>
        public void RetirePlayer(
            int playerId,
            int seasonId,
            ExpectedRole previousRole,
            string reason)
        {
            PlayerState player = GetPlayer(playerId);
            if (player.CareerStatus != PlayerCareerStatus.ActiveRoster)
                throw new InvalidOperationException("현역 로스터 선수만 이 경로로 은퇴할 수 있습니다.");
            if (seasonId <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonId));

            int previousTeamId = player.CurrentTeamId;
            LeagueId previousLeagueId = player.CurrentLeagueId;
            TeamState team = GetTeam(previousTeamId).WithoutRosteredPlayer(playerId);
            LeagueState league = GetLeague(previousLeagueId);
            league.ReplaceTeam(team);
            ReplaceTeamInRegistry(team);
            DeactivateActiveContract(player);
            player.Retire();

            MovementLedger.Record(new PlayerMovementRecord(
                Calendar.CurrentDate,
                seasonId,
                playerId,
                PlayerMovementType.Retirement,
                previousLeagueId,
                previousTeamId,
                LeagueId.Unassigned,
                0,
                previousRole,
                previousRole,
                previousRole,
                0,
                reason));
            DomainEvents.Append(new WorldDomainEvent(
                $"retirement:{seasonId}:{playerId}",
                "player_retirement",
                Calendar.CurrentDate,
                playerId,
                previousTeamId));
            ValidateInvariants();
        }

        public void RegisterContract(
            PlayerContractState contract,
            int playerId,
            LeagueId targetLeagueId = default)
        {
            if (contract == null)
                throw new ArgumentNullException(nameof(contract));
            TeamState team = GetTeam(contract.TeamId);
            LeagueId contractLeagueId = targetLeagueId.IsAssigned ? targetLeagueId : team.LeagueId;
            if (contract.ContractId <= 0)
                contract.AttachIdentity(GetNextContractId(), playerId, contractLeagueId);
            if (FindContractIndex(contract.ContractId) >= 0)
                throw new InvalidOperationException($"ContractId {contract.ContractId}가 중복되었습니다.");
            for (int index = 0; index < _contracts.Count; index++)
            {
                if (_contracts[index].PlayerId == playerId && _contracts[index].IsActive)
                    _contracts[index].Deactivate();
            }
            _contracts.Add(contract);
            _contracts.Sort((left, right) => left.ContractId.CompareTo(right.ContractId));
            GetPlayer(playerId).ReplaceActiveContract(contract.ContractId, contractLeagueId);
        }

        /// <summary>트레이드에서 계약 조건은 유지하고 활성 계약의 승계 구단만 바꾼다.</summary>
        public PlayerContractState TransferActiveContract(int playerId, int targetTeamId, LeagueId leagueId)
        {
            TeamState targetTeam = GetTeam(targetTeamId);
            if (targetTeam.LeagueId != leagueId)
                throw new InvalidOperationException("계약 승계 구단과 리그가 다릅니다.");
            for (int index = 0; index < _contracts.Count; index++)
            {
                PlayerContractState contract = _contracts[index];
                if (contract.PlayerId != playerId || !contract.IsActive)
                    continue;
                contract.TransferTo(targetTeamId, leagueId);
                return contract;
            }
            return null;
        }

        /// <summary>모든 리그의 다음 시즌 로스터·구단 승강·AI 계약을 한 월드 경계에서 커밋한다.</summary>
        public void CommitOffseasonMarket(
            IReadOnlyList<LeagueState> nextLeagues,
            WorldOffseasonMarketPlan marketPlan,
            int myPlayerId,
            int nextSeasonId,
            int nextYear)
        {
            if (nextLeagues == null || nextLeagues.Count != _leagues.Count)
                throw new ArgumentException("모든 리그의 다음 시즌 상태가 필요합니다.", nameof(nextLeagues));
            if (marketPlan == null) throw new ArgumentNullException(nameof(marketPlan));

            int existingPlayerCount = _players.Count;
            for (int index = 0; index < existingPlayerCount; index++)
            {
                PlayerState player = _players[index];
                if (player.PlayerId != myPlayerId && player.CareerStatus == PlayerCareerStatus.ActiveRoster)
                    player.AdvanceAge();
            }

            for (int index = 0; index < marketPlan.NewPlayers.Count; index++)
            {
                PlayerState player = marketPlan.NewPlayers[index];
                if (FindPlayerIndex(player.PlayerId) >= 0)
                    throw new InvalidOperationException($"PlayerId {player.PlayerId}가 신규 선수와 중복되었습니다.");
                _players.Add(player);
            }
            _players.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));

            for (int index = 0; index < marketPlan.Decisions.Count; index++)
            {
                AiMarketDecision decision = marketPlan.Decisions[index];
                if (decision.PlayerId == myPlayerId)
                    continue;
                PlayerState player = GetPlayer(decision.PlayerId);
                if (!decision.PreservesContract)
                    DeactivateActiveContract(player);
                if (decision.IsRetirement)
                    player.Retire();
                else
                {
                    player.TransferTo(decision.TargetTeamId, decision.TargetLeagueId);
                    if (decision.PreservesContract)
                        RelocateActiveContract(player, decision.TargetTeamId, decision.TargetLeagueId);
                }
            }

            _leagues.Clear();
            for (int index = 0; index < nextLeagues.Count; index++)
                _leagues.Add(nextLeagues[index] ?? throw new ArgumentException("null 리그가 있습니다.", nameof(nextLeagues)));
            _leagues.Sort(CompareLeagues);
            _teams.Clear();
            _teams.AddRange(BuildTeamRegistry(_leagues));

            for (int index = 0; index < marketPlan.LeagueMovementPlan.Records.Count; index++)
            {
                TeamLeagueMovementRecord movement = marketPlan.LeagueMovementPlan.Records[index];
                TeamMovementLedger.Record(movement);
                DomainEvents.Append(new WorldDomainEvent(
                    $"team-league-result:{movement.Year}:{movement.TeamId}:{(int)movement.MovementType}",
                    movement.MovementType == TeamLeagueMovementType.Promotion
                        ? "PromotionClinched"
                        : "RelegationConfirmed",
                    Calendar.CurrentDate,
                    movement.TeamId,
                    (int)movement.TargetTier));
                DomainEvents.Append(new WorldDomainEvent(
                    $"team-league-move:{movement.Year}:{movement.TeamId}:{(int)movement.MovementType}",
                    "TeamLeagueChanged",
                    Calendar.CurrentDate,
                    movement.TeamId,
                    (int)movement.TargetTier));
            }
            for (int index = 0; index < marketPlan.LeagueMovementPlan.TiebreakGames.Count; index++)
            {
                LeagueTiebreakGameState game = marketPlan.LeagueMovementPlan.TiebreakGames[index];
                DomainEvents.Append(new WorldDomainEvent(
                    $"league-tiebreak:{game.SeasonId}:{game.LeagueId}:{game.BoundaryRank}",
                    "LeagueTiebreakerPlayed",
                    Calendar.CurrentDate,
                    game.WinnerTeamId,
                    game.LoserTeamId));
            }

            for (int index = 0; index < marketPlan.Decisions.Count; index++)
            {
                AiMarketDecision decision = marketPlan.Decisions[index];
                if (decision.PlayerId == myPlayerId)
                    continue;
                int contractId = 0;
                if (!decision.IsRetirement && !decision.PreservesContract)
                {
                    var contract = new PlayerContractState(
                        NewGameFlow.CurrentSaveVersion,
                        decision.TargetTeamId,
                        nextYear,
                        decision.ContractYears,
                        0L,
                        decision.AnnualSalary,
                        decision.ExpectedRole);
                    RegisterContract(contract, decision.PlayerId);
                    contractId = contract.ContractId;
                }
                else if (decision.PreservesContract)
                {
                    contractId = GetPlayer(decision.PlayerId).ActiveContractId;
                }
                MovementLedger.Record(new PlayerMovementRecord(
                    Calendar.CurrentDate,
                    nextSeasonId,
                    decision.PlayerId,
                    decision.MovementType,
                    decision.PreviousLeagueId,
                    decision.PreviousTeamId,
                    decision.TargetLeagueId,
                    decision.TargetTeamId,
                    decision.ExpectedRole,
                    decision.ExpectedRole,
                    decision.ExpectedRole,
                    contractId,
                    decision.Reason));
                if (decision.MovementType != PlayerMovementType.CurrentTeamRenewal)
                {
                    DomainEvents.Append(new WorldDomainEvent(
                        $"ai-market:{nextYear}:{decision.PlayerId}:{(int)decision.MovementType}",
                        GetMarketEventType(decision.MovementType),
                        Calendar.CurrentDate,
                        decision.PlayerId,
                        decision.IsRetirement ? decision.PreviousTeamId : decision.TargetTeamId));
                }
            }

            ValidateInvariants();
        }

        /// <summary>
        /// 전역 ID·소속·활성 계약 불변 조건을 검사하고 첫 위반에서 실패한다.
        /// </summary>
        public void ValidateInvariants()
        {
            EnsureUniqueLeagueIds();
            EnsureCompleteLeaguePyramid();
            EnsureUniqueTeamIds();
            EnsureUniquePlayerIds();
            EnsureUniqueContractIds();
            EnsureScheduleReferencesStayInLeague();
            EnsureLeagueHistoryReferences();

            for (int index = 0; index < _teams.Count; index++)
            {
                TeamState team = _teams[index];
                LeagueState teamLeague = GetLeague(team.LeagueId);
                for (int rosterIndex = 0; rosterIndex < team.RosterPlayerIds.Count; rosterIndex++)
                {
                    int playerId = team.RosterPlayerIds[rosterIndex];
                    PlayerState rosterPlayer = GetPlayer(playerId);
                    if (rosterPlayer.CurrentTeamId != team.TeamId)
                        throw new InvalidOperationException($"PlayerId {playerId}의 소속 구단 참조가 다릅니다.");
                    for (int previousIndex = 0; previousIndex < rosterIndex; previousIndex++)
                    {
                        if (team.RosterPlayerIds[previousIndex] == playerId)
                            throw new InvalidOperationException($"TeamId {team.TeamId}의 로스터에 PlayerId {playerId}가 중복되었습니다.");
                    }
                }
                for (int competitorIndex = 0;
                     competitorIndex < team.RosterCompetitors.Count;
                     competitorIndex++)
                {
                    RosterCompetitorState competitor = team.RosterCompetitors[competitorIndex];
                    PlayerState player = GetPlayer(competitor.PlayerId);
                    if (player.PrimaryPosition != competitor.Position ||
                        !string.Equals(player.Name, competitor.Name, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"PlayerId {player.PlayerId}의 글로벌 정체성과 구단 로스터 정체성이 다릅니다.");
                    }
                    if (player.GrowthState == null || player.GrowthState.Age != player.Age)
                        throw new InvalidOperationException($"AI PlayerId {player.PlayerId}의 성장 상태가 유효하지 않습니다.");
                    bool rosterSnapshotMustBeCurrent = teamLeague.CurrentSeason?.Phase is
                        SeasonPhase.Preseason or SeasonPhase.RegularSeason;
                    if (rosterSnapshotMustBeCurrent &&
                        (player.CareerPlateAppearances != competitor.CareerPlateAppearances ||
                         player.CareerPitchingOuts != competitor.CareerPitchingOuts ||
                         player.RegisteredSeasons != competitor.RegisteredSeasons))
                    {
                        throw new InvalidOperationException(
                            $"AI PlayerId {player.PlayerId}의 커리어 합계가 구단 로스터와 다릅니다.");
                    }
                }
                for (int rawPosition = (int)PlayerPosition.Catcher;
                     rawPosition <= (int)PlayerPosition.ReliefPitcher;
                     rawPosition++)
                {
                    bool hasCompetitor = false;
                    for (int competitorIndex = 0;
                         competitorIndex < team.RosterCompetitors.Count;
                         competitorIndex++)
                    {
                        if ((int)team.RosterCompetitors[competitorIndex].Position == rawPosition)
                        {
                            hasCompetitor = true;
                            break;
                        }
                    }
                    if (!hasCompetitor)
                    {
                        throw new InvalidOperationException(
                            $"TeamId {team.TeamId}의 {(PlayerPosition)rawPosition} 경기 가능 선수가 없습니다.");
                    }
                }
            }

            for (int index = 0; index < _players.Count; index++)
            {
                PlayerState player = _players[index];
                int membershipCount = CountRosterMemberships(player.PlayerId);
                if (player.CareerStatus == PlayerCareerStatus.ActiveRoster)
                {
                    TeamState team = GetTeam(player.CurrentTeamId);
                    if (player.CurrentLeagueId != team.LeagueId)
                        throw new InvalidOperationException($"PlayerId {player.PlayerId}의 리그와 구단 리그가 다릅니다.");
                    if (membershipCount != 1)
                        throw new InvalidOperationException($"PlayerId {player.PlayerId}의 로스터 소속 수가 {membershipCount}개입니다.");
                }
                else if (membershipCount != 0 || player.CurrentTeamId != 0 || player.CurrentLeagueId.IsAssigned)
                {
                    throw new InvalidOperationException($"PlayerId {player.PlayerId}의 비현역 소속 참조가 남아 있습니다.");
                }
            }

            for (int index = 0; index < _contracts.Count; index++)
            {
                PlayerContractState contract = _contracts[index];
                if (contract.ContractId <= 0)
                    throw new InvalidOperationException("계약 레지스트리에 할당되지 않은 ContractId가 있습니다.");
                TeamState team = GetTeam(contract.TeamId);
                if (contract.IsActive && contract.CurrentLeagueId != team.LeagueId)
                    throw new InvalidOperationException($"ContractId {contract.ContractId}의 리그 참조가 잘못되었습니다.");
                GetTeam(contract.SigningTeamId);
                PlayerState contractPlayer = GetPlayer(contract.PlayerId);
                if (contract.IsActive && contractPlayer.CurrentTeamId != contract.TeamId)
                    throw new InvalidOperationException($"ContractId {contract.ContractId}의 활성 계약 구단이 선수 소속과 다릅니다.");
            }

            for (int playerIndex = 0; playerIndex < _players.Count; playerIndex++)
            {
                PlayerState player = _players[playerIndex];
                int activeCount = 0;
                for (int contractIndex = 0; contractIndex < _contracts.Count; contractIndex++)
                {
                    PlayerContractState contract = _contracts[contractIndex];
                    if (contract.PlayerId == player.PlayerId && contract.IsActive)
                        activeCount++;
                }
                if (activeCount > 1)
                    throw new InvalidOperationException($"PlayerId {player.PlayerId}의 활성 계약이 중복되었습니다.");
                if (player.CareerStatus != PlayerCareerStatus.ActiveRoster && activeCount != 0)
                    throw new InvalidOperationException($"PlayerId {player.PlayerId}의 비현역 활성 계약이 남아 있습니다.");
                if (activeCount == 1 && !HasMatchingActiveContract(player))
                    throw new InvalidOperationException($"PlayerId {player.PlayerId}의 ActiveContractId가 계약 레지스트리와 다릅니다.");
            }
        }

        private int CountRosterMemberships(int playerId)
        {
            int count = 0;
            for (int teamIndex = 0; teamIndex < _teams.Count; teamIndex++)
            {
                IReadOnlyList<int> roster = _teams[teamIndex].RosterPlayerIds;
                for (int playerIndex = 0; playerIndex < roster.Count; playerIndex++)
                {
                    if (roster[playerIndex] == playerId)
                        count++;
                }
            }
            return count;
        }

        private bool HasMatchingActiveContract(PlayerState player)
        {
            for (int index = 0; index < _contracts.Count; index++)
            {
                PlayerContractState contract = _contracts[index];
                if (contract.PlayerId == player.PlayerId && contract.IsActive)
                    return contract.ContractId == player.ActiveContractId;
            }
            return false;
        }

        private int FindLeagueIndex(LeagueId leagueId)
        {
            for (int index = 0; index < _leagues.Count; index++)
            {
                if (_leagues[index].LeagueId == leagueId)
                    return index;
            }
            throw new InvalidOperationException($"LeagueId {leagueId}를 찾을 수 없습니다.");
        }

        private int FindTeamIndex(int teamId)
        {
            int low = 0;
            int high = _teams.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int comparison = _teams[middle].TeamId.CompareTo(teamId);
                if (comparison == 0) return middle;
                if (comparison < 0) low = middle + 1;
                else high = middle - 1;
            }
            return -1;
        }

        private int FindPlayerIndex(int playerId)
        {
            int low = 0;
            int high = _players.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int comparison = _players[middle].PlayerId.CompareTo(playerId);
                if (comparison == 0) return middle;
                if (comparison < 0) low = middle + 1;
                else high = middle - 1;
            }
            return -1;
        }

        private int FindContractIndex(int contractId)
        {
            int low = 0;
            int high = _contracts.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int comparison = _contracts[middle].ContractId.CompareTo(contractId);
                if (comparison == 0) return middle;
                if (comparison < 0) low = middle + 1;
                else high = middle - 1;
            }
            return -1;
        }

        private int GetNextContractId() => _contracts.Count == 0 ? 1 : _contracts[^1].ContractId + 1;

        private void DeactivateActiveContract(PlayerState player)
        {
            for (int index = 0; index < _contracts.Count; index++)
            {
                PlayerContractState contract = _contracts[index];
                if (contract.PlayerId == player.PlayerId && contract.IsActive)
                    contract.Deactivate();
            }
            player.ClearActiveContract();
        }

        private static string GetMarketEventType(PlayerMovementType movementType)
        {
            return movementType switch
            {
                PlayerMovementType.Retirement => "PlayerRetired",
                PlayerMovementType.Promotion => "PlayerPromoted",
                PlayerMovementType.TeamPromotion => "PlayerPromotedWithTeam",
                PlayerMovementType.TeamRelegation => "PlayerRelegatedWithTeam",
                PlayerMovementType.SameLeagueTransfer => "PlayerTransferred",
                PlayerMovementType.Rehabilitation => "PlayerRehabilitationSigned",
                PlayerMovementType.InitialSigning => "RookiePlayerSigned",
                _ => "PlayerContractSigned"
            };
        }

        private void RelocateActiveContract(PlayerState player, int teamId, LeagueId leagueId)
        {
            for (int index = 0; index < _contracts.Count; index++)
            {
                PlayerContractState contract = _contracts[index];
                if (contract.ContractId == player.ActiveContractId && contract.IsActive)
                {
                    contract.TransferTo(teamId, leagueId);
                    return;
                }
            }
            throw new InvalidOperationException($"PlayerId {player.PlayerId}의 승계할 활성 계약이 없습니다.");
        }

        private void RebuildTeamsForLeague(LeagueState league)
        {
            for (int index = _teams.Count - 1; index >= 0; index--)
            {
                if (_teams[index].LeagueId == league.LeagueId)
                    _teams.RemoveAt(index);
            }
            for (int index = 0; index < league.Teams.Count; index++)
                _teams.Add(league.Teams[index]);
            _teams.Sort((left, right) => left.TeamId.CompareTo(right.TeamId));
        }

        private void SynchronizeRosterPlayers(LeagueState league)
        {
            for (int teamIndex = 0; teamIndex < league.Teams.Count; teamIndex++)
            {
                TeamState team = league.Teams[teamIndex];
                for (int rosterIndex = 0; rosterIndex < team.RosterCompetitors.Count; rosterIndex++)
                {
                    RosterCompetitorState competitor = team.RosterCompetitors[rosterIndex];
                    int playerIndex = FindPlayerIndex(competitor.PlayerId);
                    if (playerIndex >= 0)
                    {
                        _players[playerIndex].TransferTo(team.TeamId, league.LeagueId);
                        continue;
                    }
                    _players.Add(CareerWorldFactory.CreateRosterPlayerState(
                        league.LeagueId,
                        league.LeagueLevel,
                        team.TeamId,
                        competitor,
                        WorldSeed));
                }
            }
            _players.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
        }

        private static bool IsRosteredInLeague(LeagueState league, int playerId)
        {
            for (int teamIndex = 0; teamIndex < league.Teams.Count; teamIndex++)
            {
                IReadOnlyList<int> roster = league.Teams[teamIndex].RosterPlayerIds;
                for (int rosterIndex = 0; rosterIndex < roster.Count; rosterIndex++)
                {
                    if (roster[rosterIndex] == playerId)
                        return true;
                }
            }
            return false;
        }

        private void ReplaceTeamInRegistry(TeamState team)
        {
            int index = FindTeamIndex(team.TeamId);
            if (index < 0)
                throw new InvalidOperationException($"TeamId {team.TeamId}를 찾을 수 없습니다.");
            _teams[index] = team;
        }

        private void EnsureUniqueLeagueIds()
        {
            for (int index = 1; index < _leagues.Count; index++)
            {
                if (_leagues[index - 1].LeagueId == _leagues[index].LeagueId)
                    throw new InvalidOperationException($"LeagueId {_leagues[index].LeagueId}가 중복되었습니다.");
            }
        }

        private void EnsureCompleteLeaguePyramid()
        {
            if (_leagues.Count != LeagueLevelRules.Count)
                return;
            var tierCounts = new int[LeagueLevelRules.Count];
            for (int index = 0; index < _leagues.Count; index++)
            {
                LeagueState league = _leagues[index];
                if (!LeagueLevelRules.IsValid(league.LeagueLevel))
                    throw new InvalidOperationException($"{league.LeagueId}의 LeagueTier가 유효하지 않습니다.");
                tierCounts[(int)league.LeagueLevel]++;
                if (league.Teams.Count != 8)
                    throw new InvalidOperationException($"{league.LeagueId}의 구단 수가 {league.Teams.Count}개입니다.");
            }
            for (int tier = 0; tier < tierCounts.Length; tier++)
            {
                if (tierCounts[tier] != 1)
                    throw new InvalidOperationException($"{(LeagueLevel)tier} 단계 리그 수가 {tierCounts[tier]}개입니다.");
            }
        }

        private void EnsureUniqueTeamIds()
        {
            for (int index = 1; index < _teams.Count; index++)
            {
                if (_teams[index - 1].TeamId == _teams[index].TeamId)
                    throw new InvalidOperationException($"TeamId {_teams[index].TeamId}가 중복되었습니다.");
            }
        }

        private void EnsureUniquePlayerIds()
        {
            for (int index = 1; index < _players.Count; index++)
            {
                if (_players[index - 1].PlayerId == _players[index].PlayerId)
                    throw new InvalidOperationException($"PlayerId {_players[index].PlayerId}가 중복되었습니다.");
            }
        }

        private void EnsureUniqueContractIds()
        {
            for (int index = 1; index < _contracts.Count; index++)
            {
                if (_contracts[index - 1].ContractId == _contracts[index].ContractId)
                    throw new InvalidOperationException($"ContractId {_contracts[index].ContractId}가 중복되었습니다.");
            }
        }

        private void EnsureScheduleReferencesStayInLeague()
        {
            for (int leagueIndex = 0; leagueIndex < _leagues.Count; leagueIndex++)
            {
                LeagueState league = _leagues[leagueIndex];
                SeasonScheduleState schedule = league.CurrentSeason?.Schedule;
                if (schedule == null)
                    continue;
                for (int gameIndex = 0; gameIndex < schedule.Games.Count; gameIndex++)
                {
                    ScheduledGameState game = schedule.Games[gameIndex];
                    if (game.AwayTeamId == game.HomeTeamId)
                        throw new InvalidOperationException($"GameId {game.GameId}에 같은 구단이 두 번 배정되었습니다.");
                    if (GetTeam(game.AwayTeamId).LeagueId != league.LeagueId ||
                        GetTeam(game.HomeTeamId).LeagueId != league.LeagueId)
                    {
                        throw new InvalidOperationException($"GameId {game.GameId}가 다른 리그 구단을 참조합니다.");
                    }
                }
            }
        }

        private void EnsureLeagueHistoryReferences()
        {
            for (int leagueIndex = 0; leagueIndex < _leagues.Count; leagueIndex++)
            {
                LeagueState league = _leagues[leagueIndex];
                if (league.CurrentSeason != null && league.LeagueYear != league.CurrentSeason.Year)
                    throw new InvalidOperationException($"{league.LeagueId}의 리그 연도와 현재 시즌 연도가 다릅니다.");
                int previousYear = int.MinValue;
                for (int summaryIndex = 0;
                     summaryIndex < league.CompletedSeasonSummaries.Count;
                     summaryIndex++)
                {
                    LeagueSeasonSummaryState summary = league.CompletedSeasonSummaries[summaryIndex];
                    if (summary.LeagueId != league.LeagueId)
                        throw new InvalidOperationException("시즌 요약의 LeagueId가 소유 리그와 다릅니다.");
                    if (summary.Year <= previousYear || summary.Year >= league.LeagueYear)
                        throw new InvalidOperationException($"{league.LeagueId}의 시즌 요약 연도 순서가 잘못되었습니다.");
                    previousYear = summary.Year;
                    for (int standingIndex = 0; standingIndex < summary.Standings.Count; standingIndex++)
                    {
                        TeamSeasonSummaryState standing = summary.Standings[standingIndex];
                        if (standing.Rank != standingIndex + 1 || FindTeamIndex(standing.TeamId) < 0)
                        {
                            throw new InvalidOperationException($"{league.LeagueId}의 완료 시즌 순위표가 잘못되었습니다.");
                        }
                    }
                    if (summary.ChampionTeamId > 0 && FindTeamIndex(summary.ChampionTeamId) < 0)
                    {
                        throw new InvalidOperationException("우승 구단이 시즌 요약 리그에 속하지 않습니다.");
                    }
                    if (summary.RunnerUpTeamId > 0 && FindTeamIndex(summary.RunnerUpTeamId) < 0)
                    {
                        throw new InvalidOperationException("준우승 구단이 시즌 요약 리그에 속하지 않습니다.");
                    }
                }
            }
        }

        private static List<LeagueState> CopyAndSortLeagues(IReadOnlyList<LeagueState> source)
        {
            if (source == null || source.Count == 0)
                throw new ArgumentException("월드에는 하나 이상의 리그가 필요합니다.", nameof(source));
            var result = new List<LeagueState>(source.Count);
            for (int index = 0; index < source.Count; index++)
                result.Add(source[index] ?? throw new ArgumentException("null 리그가 있습니다.", nameof(source)));
            result.Sort(CompareLeagues);
            return result;
        }

        private static int CompareLeagues(LeagueState left, LeagueState right)
        {
            int tier = left.LeagueLevel.CompareTo(right.LeagueLevel);
            return tier != 0 ? tier : left.LeagueId.CompareTo(right.LeagueId);
        }

        private static List<TeamState> BuildTeamRegistry(IReadOnlyList<LeagueState> leagues)
        {
            var result = new List<TeamState>();
            for (int leagueIndex = 0; leagueIndex < leagues.Count; leagueIndex++)
            {
                LeagueState league = leagues[leagueIndex];
                for (int teamIndex = 0; teamIndex < league.Teams.Count; teamIndex++)
                {
                    TeamState team = league.Teams[teamIndex];
                    if (team.LeagueId != league.LeagueId)
                        throw new InvalidOperationException($"TeamId {team.TeamId}의 LeagueId가 소속 리그와 다릅니다.");
                    result.Add(team);
                }
            }
            result.Sort((left, right) => left.TeamId.CompareTo(right.TeamId));
            return result;
        }

        private static List<PlayerState> CopyAndSortPlayers(IReadOnlyList<PlayerState> source)
        {
            if (source == null || source.Count == 0)
                throw new ArgumentException("월드에는 하나 이상의 선수가 필요합니다.", nameof(source));
            var result = new List<PlayerState>(source.Count);
            for (int index = 0; index < source.Count; index++)
                result.Add(source[index] ?? throw new ArgumentException("null 선수가 있습니다.", nameof(source)));
            result.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
            return result;
        }

        private static List<PlayerContractState> CopyAndSortContracts(IReadOnlyList<PlayerContractState> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var result = new List<PlayerContractState>(source.Count);
            for (int index = 0; index < source.Count; index++)
                result.Add(source[index] ?? throw new ArgumentException("null 계약이 있습니다.", nameof(source)));
            result.Sort((left, right) => left.ContractId.CompareTo(right.ContractId));
            return result;
        }
    }
}
