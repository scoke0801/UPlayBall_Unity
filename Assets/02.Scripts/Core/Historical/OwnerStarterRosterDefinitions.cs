using System;
using System.Collections.Generic;

namespace Baseball.Core.Historical
{
    /// <summary>구단주 새 게임의 핵심 선수 선택과 자동 보충 로스터 계약을 보관한다.</summary>
    public sealed class OwnerStarterRosterRule
    {
        private readonly int[] _fillerCountByCost;

        public OwnerStarterRosterRule(
            int mainCardCount,
            int mainHitterCount,
            int mainPitcherCount,
            int maximumMainCost,
            int eliteCostThreshold,
            int maximumEliteCards,
            int premiumCostThreshold,
            int maximumPremiumCards,
            int maximumSameYearPremiumCards,
            int maximumFillerRerolls,
            int fillerMinimumCost,
            IReadOnlyList<int> fillerCountByCost,
            int fillerHitterCount,
            int fillerPitcherCount,
            int duplicateRetryCount)
        {
            if (mainCardCount <= 0 || mainHitterCount < 0 || mainPitcherCount < 0 ||
                mainHitterCount + mainPitcherCount != mainCardCount)
                throw new ArgumentException("메인 카드의 인원 구성이 잘못되었습니다.");
            if (maximumMainCost < mainCardCount || eliteCostThreshold < 1 || premiumCostThreshold < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumMainCost));
            if (maximumEliteCards < 0 || maximumPremiumCards < maximumEliteCards ||
                maximumSameYearPremiumCards < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumEliteCards));
            if (maximumFillerRerolls < 0 || maximumFillerRerolls > 30)
                throw new ArgumentOutOfRangeException(nameof(maximumFillerRerolls));
            if (fillerMinimumCost < 1 || fillerCountByCost == null || fillerCountByCost.Count == 0)
                throw new ArgumentException("보충 선수 Cost 구간이 필요합니다.", nameof(fillerCountByCost));
            if (fillerHitterCount < 0 || fillerPitcherCount < 0 || duplicateRetryCount < 1)
                throw new ArgumentOutOfRangeException(nameof(fillerHitterCount));

            int fillerCount = 0;
            _fillerCountByCost = new int[fillerCountByCost.Count];
            for (int index = 0; index < _fillerCountByCost.Length; index++)
            {
                if (fillerCountByCost[index] < 0)
                    throw new ArgumentOutOfRangeException(nameof(fillerCountByCost));
                _fillerCountByCost[index] = fillerCountByCost[index];
                fillerCount += fillerCountByCost[index];
            }
            if (fillerHitterCount + fillerPitcherCount != fillerCount ||
                mainCardCount + fillerCount != ActiveRosterCompositionRule.ActiveRosterSize)
                throw new ArgumentException("메인·보충 선수 합계는 ActiveRoster 25명이어야 합니다.");

            MainCardCount = mainCardCount;
            MainHitterCount = mainHitterCount;
            MainPitcherCount = mainPitcherCount;
            MaximumMainCost = maximumMainCost;
            EliteCostThreshold = eliteCostThreshold;
            MaximumEliteCards = maximumEliteCards;
            PremiumCostThreshold = premiumCostThreshold;
            MaximumPremiumCards = maximumPremiumCards;
            MaximumSameYearPremiumCards = maximumSameYearPremiumCards;
            MaximumFillerRerolls = maximumFillerRerolls;
            FillerMinimumCost = fillerMinimumCost;
            FillerHitterCount = fillerHitterCount;
            FillerPitcherCount = fillerPitcherCount;
            DuplicateRetryCount = duplicateRetryCount;
        }

        public int MainCardCount { get; }
        public int MainHitterCount { get; }
        public int MainPitcherCount { get; }
        public int MaximumMainCost { get; }
        public int EliteCostThreshold { get; }
        public int MaximumEliteCards { get; }
        public int PremiumCostThreshold { get; }
        public int MaximumPremiumCards { get; }
        public int MaximumSameYearPremiumCards { get; }
        public int MaximumFillerRerolls { get; }
        public int FillerMinimumCost { get; }
        public int FillerMaximumCost => FillerMinimumCost + _fillerCountByCost.Length - 1;
        public int FillerHitterCount { get; }
        public int FillerPitcherCount { get; }
        public int DuplicateRetryCount { get; }
        public int FillerCardCount => FillerHitterCount + FillerPitcherCount;

        public int GetFillerCount(int cost)
        {
            int index = cost - FillerMinimumCost;
            if (index < 0 || index >= _fillerCountByCost.Length)
                return 0;
            return _fillerCountByCost[index];
        }

        public static OwnerStarterRosterRule CreateInitial(
            int maximumFillerRerolls = 30,
            int maximumMainCost = 60) => new OwnerStarterRosterRule(
            10, 6, 4, maximumMainCost, 9, 2, 7, 4, 2,
            maximumFillerRerolls, 2, new[] { 10, 5 }, 8, 7, 8);
    }

    /// <summary>메인 카드 선택 검증 결과를 UI가 그대로 표시할 수 있게 보관한다.</summary>
    public readonly struct OwnerMainCardSelectionStatus
    {
        public OwnerMainCardSelectionStatus(
            bool isValid,
            string errorCode,
            string message,
            int selectedCount,
            int hitterCount,
            int pitcherCount,
            int totalCost)
        {
            IsValid = isValid;
            ErrorCode = errorCode ?? string.Empty;
            Message = message ?? string.Empty;
            SelectedCount = selectedCount;
            HitterCount = hitterCount;
            PitcherCount = pitcherCount;
            TotalCost = totalCost;
        }

        public bool IsValid { get; }
        public string ErrorCode { get; }
        public string Message { get; }
        public int SelectedCount { get; }
        public int HitterCount { get; }
        public int PitcherCount { get; }
        public int TotalCost { get; }
    }

    /// <summary>보충 추첨 한 회의 카드 목록과 완성된 25인 역할 배치를 보관한다.</summary>
    public sealed class OwnerStarterRosterResult
    {
        private readonly string[] _mainCardIds;
        private readonly string[] _fillerCardIds;

        public OwnerStarterRosterResult(
            IReadOnlyList<string> mainCardIds,
            IReadOnlyList<string> fillerCardIds,
            CurrentRosterState roster,
            int rerollIndex,
            ulong resultSeed)
        {
            _mainCardIds = Copy(mainCardIds, nameof(mainCardIds));
            _fillerCardIds = Copy(fillerCardIds, nameof(fillerCardIds));
            Roster = roster ?? throw new ArgumentNullException(nameof(roster));
            if (_mainCardIds.Length + _fillerCardIds.Length != ActiveRosterCompositionRule.ActiveRosterSize)
                throw new ArgumentException("스타터 로스터는 정확히 25명이어야 합니다.");
            if (rerollIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(rerollIndex));
            RerollIndex = rerollIndex;
            ResultSeed = resultSeed;
        }

        public IReadOnlyList<string> MainCardIds => _mainCardIds;
        public IReadOnlyList<string> FillerCardIds => _fillerCardIds;
        public CurrentRosterState Roster { get; }
        public int RerollIndex { get; }
        public ulong ResultSeed { get; }

        private static string[] Copy(IReadOnlyList<string> source, string name)
        {
            if (source == null)
                throw new ArgumentNullException(name);
            var result = new string[source.Count];
            for (int index = 0; index < result.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(source[index]))
                    throw new ArgumentException("CardId는 비어 있을 수 없습니다.", name);
                result[index] = source[index].Trim();
            }
            return result;
        }
    }
}
