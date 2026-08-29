using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>플레이 중 사실을 누적하고 은퇴 시 한 번만 불변 회고 스냅샷을 만든다.</summary>
    public sealed partial class RetirementRecapService
    {
        public const int CurrentSnapshotVersion = 1;
        private const int OffseasonDateIndex = 10_000;

        private readonly BalanceTable _balance;

        public RetirementRecapService(BalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }
    }
}
