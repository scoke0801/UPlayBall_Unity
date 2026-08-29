using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Game.Career.Narrative;
using Baseball.Game.Career.News;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 새 게임 계약 완료 후 세이브 루트가 소유할 선수·리그·계약 상태를 묶는다.
    /// </summary>
    public sealed class CareerState
    {
        private readonly List<PlayerContractState> _contractHistory = new List<PlayerContractState>();
        private readonly List<CareerSeasonHistoryRecord> _seasonHistory = new List<CareerSeasonHistoryRecord>();
        private readonly HashSet<int> _resolvedExtensionSeasonIds = new HashSet<int>();

        public CareerState(
            int saveVersion,
            PlayerState myPlayer,
            LeagueState league,
            PlayerContractState currentContract,
            long availableMoney)
            : this(
                saveVersion,
                myPlayer,
                CreateSingleLeagueWorld(myPlayer, league, currentContract),
                currentContract,
                availableMoney)
        {
        }

        /// <summary>
        /// 다중 리그 월드를 소유하는 v8 커리어 세이브 루트를 생성한다.
        /// </summary>
        public CareerState(
            int saveVersion,
            PlayerState myPlayer,
            WorldState world,
            PlayerContractState currentContract,
            long availableMoney,
            CareerCreationProfile creationProfile = null)
        {
            SaveVersion = saveVersion;
            MyPlayer = myPlayer ?? throw new ArgumentNullException(nameof(myPlayer));
            World = world ?? throw new ArgumentNullException(nameof(world));
            if (!ReferenceEquals(World.GetPlayer(MyPlayer.PlayerId), MyPlayer))
                throw new InvalidOperationException("내 선수는 WorldState.PlayerRegistry의 동일 인스턴스여야 합니다.");
            CurrentContract = currentContract ?? throw new ArgumentNullException(nameof(currentContract));
            if (!ContainsContract(World.Contracts, currentContract.ContractId))
                World.RegisterContract(currentContract, MyPlayer.PlayerId);
            else
                MyPlayer.ReplaceActiveContract(currentContract.ContractId, currentContract.CurrentLeagueId);
            Economy = new CareerEconomyState(availableMoney);
            TradeState = new PlayerTradeState();
            News = new CareerNewsState(saveVersion);
            Narrative = new CareerNarrativeState(saveVersion);
            Retirement = new CareerRetirementState(saveVersion);
            Reputation = new CareerReputationState(World.GetLeague(MyPlayer.CurrentLeagueId).LeagueLevel);
            CreationProfile = creationProfile ?? new CareerCreationProfile(
                GameMode.PlayerCareer,
                myPlayer.PrimaryPosition is Baseball.Core.Players.PlayerPosition.StartingPitcher or
                    Baseball.Core.Players.PlayerPosition.ReliefPitcher
                    ? Baseball.Core.Players.PlayerType.Pitcher
                    : Baseball.Core.Players.PlayerType.Batter,
                myPlayer.PrimaryPosition,
                myPlayer.PrimaryPosition == Baseball.Core.Players.PlayerPosition.ReliefPitcher
                    ? Baseball.Core.Teams.PitcherRole.MiddleRelief
                    : Baseball.Core.Teams.PitcherRole.Starter,
                BatterStyle.Balanced,
                Array.Empty<int>(),
                Array.Empty<Baseball.Core.Players.PitchRepertoireEntry>(),
                CareerGameSettings.CreateDefault());
            _contractHistory.Add(CurrentContract);
        }

        public int SaveVersion { get; private set; }
        public int MyPlayerId => MyPlayer.PlayerId;
        public PlayerState MyPlayer { get; }
        public WorldState World { get; private set; }
        public LeagueState CurrentLeague => World.GetLeague(
            MyPlayer.CurrentLeagueId.IsAssigned
                ? MyPlayer.CurrentLeagueId
                : Retirement.LastLeagueId);
        public PlayerContractState CurrentContract { get; private set; }
        public CareerEconomyState Economy { get; }
        public PlayerTradeState TradeState { get; }
        public CareerNewsState News { get; }
        public CareerNarrativeState Narrative { get; }
        public CareerRetirementState Retirement { get; }
        public CareerReputationState Reputation { get; }
        public CareerCreationProfile CreationProfile { get; }
        public CareerGameSettings GameSettings => CreationProfile.GameSettings;
        public OffseasonState CurrentOffseason { get; private set; }
        public long AvailableMoney => Economy.Money;
        public Baseball.Core.Teams.ExpectedRole CurrentExpectedRole =>
            TradeState.CurrentTeamRole ?? CurrentContract.ExpectedRole;
        public IReadOnlyList<PlayerContractState> ContractHistory => _contractHistory;
        public IReadOnlyList<CareerSeasonHistoryRecord> SeasonHistory => _seasonHistory;

        public bool HasResolvedExtension(int seasonId) => _resolvedExtensionSeasonIds.Contains(seasonId);

        public void ResolveExtension(int seasonId)
        {
            _resolvedExtensionSeasonIds.Add(seasonId);
        }

        /// <summary>
        /// 시즌 결산이 끝난 뒤 생성된 오프시즌 상태를 세이브 루트에 연결한다.
        /// </summary>
        public void BeginOffseason(OffseasonState offseason)
        {
            if (offseason == null)
                throw new ArgumentNullException(nameof(offseason));
            if (CurrentOffseason != null)
                throw new InvalidOperationException("이미 진행 중인 오프시즌이 있습니다.");

            MyPlayer.StudyState.BeginOffseason();
            CurrentOffseason = offseason;
        }

        /// <summary>
        /// 완료된 시즌 기록을 커리어 이력에 남기고, 오프시즌을 마감하며 다음 시즌 리그로 교체한다.
        /// </summary>
        public void AdvanceToNextSeason(LeagueState nextLeague, CareerSeasonHistoryRecord completedSeason)
        {
            if (nextLeague == null)
                throw new ArgumentNullException(nameof(nextLeague));
            if (CurrentOffseason == null)
                throw new InvalidOperationException("마감할 오프시즌이 없습니다.");

            News.CompactCompletedSeason(CurrentLeague.CurrentSeason.SeasonId);
            _seasonHistory.Add(completedSeason);
            World.ReplaceLeague(nextLeague);
            CurrentOffseason = null;
        }

        /// <summary>세 리그 로스터와 AI 시장 결과를 하나의 월드 시즌 경계에서 교체한다.</summary>
        public void AdvanceToNextSeason(
            IReadOnlyList<LeagueState> nextLeagues,
            CareerSeasonHistoryRecord completedSeason,
            WorldOffseasonMarketPlan marketPlan,
            int completedSeasonId,
            int nextSeasonId,
            int nextYear)
        {
            if (nextLeagues == null) throw new ArgumentNullException(nameof(nextLeagues));
            if (marketPlan == null) throw new ArgumentNullException(nameof(marketPlan));
            if (CurrentOffseason == null)
                throw new InvalidOperationException("마감할 오프시즌이 없습니다.");

            News.CompactCompletedSeason(completedSeasonId);
            _seasonHistory.Add(completedSeason);
            World.CommitOffseasonMarket(nextLeagues, marketPlan, MyPlayerId, nextSeasonId, nextYear);
            CurrentOffseason = null;
        }

        /// <summary>
        /// 계약 만료로 새 계약이 체결되면 현재 계약을 교체하고 이전 계약을 이력에 남긴다.
        /// </summary>
        public void RenewContract(
            PlayerContractState newContract,
            int movementSeasonId = 0,
            LeagueId targetLeagueId = default,
            long transferCompensation = 0L)
        {
            if (newContract == null)
                throw new ArgumentNullException(nameof(newContract));
            if (transferCompensation < 0L)
                throw new ArgumentOutOfRangeException(nameof(transferCompensation));
            int previousTeamId = CurrentContract.TeamId;
            LeagueId previousLeagueId = CurrentContract.CurrentLeagueId;
            Baseball.Core.Teams.ExpectedRole previousRole = CurrentExpectedRole;
            CurrentContract = newContract ?? throw new ArgumentNullException(nameof(newContract));
            World.RegisterContract(CurrentContract, MyPlayer.PlayerId, targetLeagueId);
            _contractHistory.Add(CurrentContract);
            PlayerMovementType movementType;
            if (previousLeagueId == CurrentContract.CurrentLeagueId)
            {
                movementType = previousTeamId == CurrentContract.TeamId
                    ? PlayerMovementType.CurrentTeamRenewal
                    : PlayerMovementType.SameLeagueTransfer;
            }
            else
            {
                LeagueLevel previousLevel = World.GetLeague(previousLeagueId).LeagueLevel;
                LeagueLevel targetLevel = World.GetLeague(CurrentContract.CurrentLeagueId).LeagueLevel;
                movementType = targetLevel > previousLevel
                    ? PlayerMovementType.Promotion
                    : PlayerMovementType.Rehabilitation;
            }
            World.MovementLedger.Record(new PlayerMovementRecord(
                World.Calendar.CurrentDate,
                movementSeasonId > 0 ? movementSeasonId : CurrentLeague.CurrentSeason.SeasonId,
                MyPlayer.PlayerId,
                movementType,
                previousLeagueId,
                previousTeamId,
                CurrentContract.CurrentLeagueId,
                CurrentContract.TeamId,
                previousRole,
                CurrentContract.PromisedRole,
                CurrentContract.ExpectedRole,
                CurrentContract.ContractId,
                transferCompensation > 0L
                    ? "상위 리그 이적 허용 조항 발동"
                    : movementType switch
                {
                    PlayerMovementType.CurrentTeamRenewal => "기존 구단 재계약",
                    PlayerMovementType.Promotion => "상위 리그 승격 계약",
                    PlayerMovementType.Rehabilitation => "하위 리그 재기 계약",
                    _ => "공개 시장 이적"
                },
                transferCompensation));
            if (transferCompensation > 0L)
            {
                World.DomainEvents.Append(new WorldDomainEvent(
                    $"upper-release-clause:{CurrentContract.SignedYear}:{MyPlayer.PlayerId}:{CurrentContract.TeamId}",
                    "UpperLeagueReleaseClauseActivated",
                    World.Calendar.CurrentDate,
                    MyPlayer.PlayerId,
                    CurrentContract.TeamId));
            }
            if (movementType is PlayerMovementType.Promotion or PlayerMovementType.Rehabilitation)
            {
                World.DomainEvents.Append(new WorldDomainEvent(
                    $"cross-league-contract:{CurrentContract.SignedYear}:{MyPlayer.PlayerId}:{CurrentContract.TeamId}",
                    "CrossLeagueContractSigned",
                    World.Calendar.CurrentDate,
                    MyPlayer.PlayerId,
                    CurrentContract.TeamId));
                World.DomainEvents.Append(new WorldDomainEvent(
                    $"player-league-move:{CurrentContract.SignedYear}:{MyPlayer.PlayerId}:{(int)movementType}",
                    movementType == PlayerMovementType.Promotion
                        ? "PlayerPromoted"
                        : "PlayerRehabilitationSigned",
                    World.Calendar.CurrentDate,
                    MyPlayer.PlayerId,
                    CurrentContract.TeamId));
            }
        }

        /// <summary>
        /// v7 커리어의 성장·기록·계약 이력을 유지한 채 월드 소유 구조만 v8로 승격한다.
        /// </summary>
        public void UpgradeToWorld(int saveVersion, WorldState world)
        {
            if (saveVersion <= SaveVersion)
                throw new ArgumentOutOfRangeException(nameof(saveVersion));
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            if (!ReferenceEquals(world.GetPlayer(MyPlayer.PlayerId), MyPlayer))
                throw new InvalidOperationException("마이그레이션 월드는 기존 내 선수 인스턴스를 보존해야 합니다.");
            World = world;
            SaveVersion = saveVersion;
        }

        /// <summary>월드 소유 구조를 유지한 채 선택 필드가 추가된 다음 세이브 스키마로 승격한다.</summary>
        public void UpgradeSaveVersion(int saveVersion)
        {
            if (saveVersion <= SaveVersion)
                throw new ArgumentOutOfRangeException(nameof(saveVersion));
            SaveVersion = saveVersion;
        }

        private static WorldState CreateSingleLeagueWorld(
            PlayerState myPlayer,
            LeagueState league,
            PlayerContractState currentContract)
        {
            if (myPlayer == null) throw new ArgumentNullException(nameof(myPlayer));
            if (league == null) throw new ArgumentNullException(nameof(league));
            if (currentContract == null) throw new ArgumentNullException(nameof(currentContract));
            league = EnsurePlayerIsRostered(league, myPlayer);
            myPlayer.AssignLeague(league.LeagueId);
            if (currentContract.ContractId <= 0)
                currentContract.AttachIdentity(1, myPlayer.PlayerId, league.LeagueId);
            myPlayer.AttachContract(currentContract.ContractId, league.LeagueId);
            return new WorldState(
                league.RandomSeed,
                new GlobalCalendarState(new DateTime(league.LeagueYear, 1, 1)),
                new[] { league },
                CreateSingleLeaguePlayerRegistry(league, myPlayer),
                new[] { currentContract },
                league.LeagueYear);
        }

        private static LeagueState EnsurePlayerIsRostered(LeagueState league, PlayerState myPlayer)
        {
            var teams = new TeamState[league.Teams.Count];
            bool foundTeam = false;
            for (int index = 0; index < teams.Length; index++)
            {
                TeamState team = league.Teams[index];
                if (team.TeamId == myPlayer.CurrentTeamId)
                {
                    team = team.WithRosteredPlayer(myPlayer.PlayerId);
                    foundTeam = true;
                }
                teams[index] = team;
            }
            if (!foundTeam)
                throw new InvalidOperationException("내 선수의 소속 구단을 리그에서 찾을 수 없습니다.");
            return new LeagueState(
                league.SaveVersion,
                league.LeagueId,
                league.LeagueLevel,
                league.LeagueRulesetId,
                league.LeagueYear,
                league.RandomSeed,
                teams,
                league.CurrentSeason);
        }

        private static PlayerState[] CreateSingleLeaguePlayerRegistry(
            LeagueState league,
            PlayerState myPlayer)
        {
            var players = new List<PlayerState>();
            for (int teamIndex = 0; teamIndex < league.Teams.Count; teamIndex++)
            {
                TeamState team = league.Teams[teamIndex];
                for (int playerIndex = 0; playerIndex < team.RosterCompetitors.Count; playerIndex++)
                {
                    players.Add(CareerWorldFactory.CreateRosterPlayerState(
                        league.LeagueId,
                        league.LeagueLevel,
                        team.TeamId,
                        team.RosterCompetitors[playerIndex],
                        league.RandomSeed));
                }
            }
            players.Add(myPlayer);
            return players.ToArray();
        }

        private static bool ContainsContract(IReadOnlyList<PlayerContractState> contracts, int contractId)
        {
            for (int index = 0; index < contracts.Count; index++)
            {
                if (contracts[index].ContractId == contractId)
                    return true;
            }
            return false;
        }
    }
}
