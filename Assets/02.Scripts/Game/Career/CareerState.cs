using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
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
        {
            SaveVersion = saveVersion;
            MyPlayer = myPlayer ?? throw new ArgumentNullException(nameof(myPlayer));
            League = league ?? throw new ArgumentNullException(nameof(league));
            CurrentContract = currentContract ?? throw new ArgumentNullException(nameof(currentContract));
            Economy = new CareerEconomyState(availableMoney);
            TradeState = new PlayerTradeState();
            News = new CareerNewsState(saveVersion);
            _contractHistory.Add(CurrentContract);
        }

        public int SaveVersion { get; }
        public PlayerState MyPlayer { get; }
        public LeagueState League { get; private set; }
        public PlayerContractState CurrentContract { get; private set; }
        public CareerEconomyState Economy { get; }
        public PlayerTradeState TradeState { get; }
        public CareerNewsState News { get; }
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

            News.CompactCompletedSeason(League.CurrentSeason.SeasonId);
            _seasonHistory.Add(completedSeason);
            League = nextLeague;
            CurrentOffseason = null;
        }

        /// <summary>
        /// 계약 만료로 새 계약이 체결되면 현재 계약을 교체하고 이전 계약을 이력에 남긴다.
        /// </summary>
        public void RenewContract(PlayerContractState newContract)
        {
            CurrentContract = newContract ?? throw new ArgumentNullException(nameof(newContract));
            _contractHistory.Add(CurrentContract);
        }
    }
}
