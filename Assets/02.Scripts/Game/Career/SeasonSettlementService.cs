using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;

namespace Baseball.Game.Career
{
    public enum SettlementEntryType
    {
        Salary,
        PerformanceBonus,
        AwardBonus,
        PostseasonBonus,
        ChampionshipBonus
    }

    public readonly struct SettlementEntry
    {
        public SettlementEntry(string rewardId, SettlementEntryType type, long amount)
        {
            RewardId = rewardId ?? string.Empty;
            Type = type;
            Amount = amount;
        }

        public string RewardId { get; }
        public SettlementEntryType Type { get; }
        public long Amount { get; }
    }

    /// <summary>시즌 결산 Money가 저장·불러오기 뒤에도 한 번만 적용되도록 지급 원장을 소유한다.</summary>
    public sealed class SeasonSettlementState
    {
        private readonly HashSet<string> _appliedRewardIds = new(StringComparer.Ordinal);
        private readonly List<SettlementEntry> _entries = new();

        public bool IsApplied { get; private set; }
        public IReadOnlyCollection<string> AppliedRewardIds => _appliedRewardIds;
        public IReadOnlyList<SettlementEntry> Entries => _entries;
        public long SalaryIncome { get; private set; }
        public long BonusIncome { get; private set; }
        public int ContractEvaluationBonus { get; private set; }

        internal bool TryAdd(SettlementEntry entry)
        {
            if (!_appliedRewardIds.Add(entry.RewardId)) return false;
            _entries.Add(entry);
            if (entry.Type == SettlementEntryType.Salary) SalaryIncome += entry.Amount;
            else BonusIncome += entry.Amount;
            return true;
        }

        internal void Complete(int contractEvaluationBonus)
        {
            ContractEvaluationBonus = contractEvaluationBonus;
            IsApplied = true;
        }
    }

    /// <summary>급여·수상·포스트시즌 보너스를 원장 ID로 중복 방지하며 Money와 계약 평가에 반영한다.</summary>
    public sealed class SeasonSettlementService
    {
        private readonly CareerState _career;
        private readonly SeasonSettlementBalance _balance;
        private readonly ContractBonusBalance? _contractBonusBalance;

        public SeasonSettlementService(
            CareerState career,
            SeasonSettlementBalance balance,
            ContractBonusBalance? contractBonusBalance = null)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance;
            _contractBonusBalance = contractBonusBalance;
        }

        public SeasonSettlementState ApplyOnce(long performanceBonus = 0L)
        {
            if (performanceBonus < 0L) throw new ArgumentOutOfRangeException(nameof(performanceBonus));
            SeasonState season = _career.CurrentLeague.CurrentSeason ??
                                 throw new InvalidOperationException("현재 시즌이 없습니다.");
            if (season.Phase != SeasonPhase.SeasonReview)
                throw new InvalidOperationException("시즌 결산 단계에서만 정산할 수 있습니다.");
            SeasonSettlementState settlement = season.Settlement;
            if (settlement.IsApplied) return settlement;

            long salary = _career.CurrentContract.AnnualSalary;
            AddIncome(settlement, $"season_{season.SeasonId}_salary", SettlementEntryType.Salary, salary);
            AddIncome(settlement, $"season_{season.SeasonId}_performance_bonus",
                SettlementEntryType.PerformanceBonus, performanceBonus);
            AddContractBonuses(settlement, season);

            int playerId = _career.MyPlayer.PlayerId;
            int contractBonus = 0;
            if (season.Awards != null)
            {
                for (int index = 0; index < season.Awards.Results.Count; index++)
                {
                    SeasonAwardResultState award = season.Awards.Results[index];
                    if (!award.IncludesWinner(playerId)) continue;
                    double rate = GetAwardMoneyRate(award.Category);
                    int evaluation = GetAwardContractBonus(award.Category);
                    if (rate <= 0d && evaluation <= 0) continue;
                    AddIncome(
                        settlement,
                        $"season_{season.SeasonId}_{award.AwardId}_reward",
                        SettlementEntryType.AwardBonus,
                        CalculateBonus(salary, rate));
                    contractBonus += evaluation;
                }
            }

            PlayerTeamPostseasonResult teamResult = season.Postseason?.PlayerTeamResult ??
                                                    PlayerTeamPostseasonResult.DidNotQualify;
            if (teamResult != PlayerTeamPostseasonResult.DidNotQualify)
            {
                AddIncome(settlement, $"season_{season.SeasonId}_postseason_reward",
                    SettlementEntryType.PostseasonBonus,
                    CalculateBonus(salary, _balance.PostseasonQualificationSalaryRate));
            }
            if (teamResult == PlayerTeamPostseasonResult.Champion)
            {
                AddIncome(settlement, $"season_{season.SeasonId}_championship_reward",
                    SettlementEntryType.ChampionshipBonus,
                    CalculateBonus(salary, _balance.ChampionshipSalaryRate));
                contractBonus += _balance.ChampionshipContractBonus;
            }
            else if (teamResult == PlayerTeamPostseasonResult.RunnerUp)
            {
                contractBonus += _balance.RunnerUpContractBonus;
            }

            if (contractBonus > _balance.MaximumAwardContractBonus)
                contractBonus = _balance.MaximumAwardContractBonus;
            settlement.Complete(contractBonus);
            return settlement;
        }

        private void AddContractBonuses(SeasonSettlementState settlement, SeasonState season)
        {
            if (!_contractBonusBalance.HasValue)
                return;

            var service = new ContractBonusService(_contractBonusBalance.Value);
            int regularSeasonGames = _career.CurrentLeague.CurrentSeason.PlayerStatistics.TeamGames;
            if (regularSeasonGames <= 0 && _career.CurrentLeague.CurrentSeason.Schedule != null)
                regularSeasonGames = CountTeamGamesPerSeason(_career.CurrentLeague.CurrentSeason);
            ContractBonusProgress[] progress = service.Evaluate(
                _career,
                regularSeasonGames);
            for (int index = 0; index < progress.Length; index++)
            {
                ContractBonusProgress item = progress[index];
                if (!item.IsCompleted)
                    continue;
                AddIncome(
                    settlement,
                    $"season_{season.SeasonId}_contract_{item.Clause.ClauseId}",
                    SettlementEntryType.PerformanceBonus,
                    item.Clause.Reward);
            }
        }

        private int CountTeamGamesPerSeason(SeasonState season)
        {
            int teamId = _career.MyPlayer.CurrentTeamId;
            int count = 0;
            for (int index = 0; index < season.Schedule.Games.Count; index++)
            {
                if (season.Schedule.Games[index].IncludesTeam(teamId))
                    count++;
            }
            return count;
        }

        private void AddIncome(
            SeasonSettlementState settlement,
            string rewardId,
            SettlementEntryType type,
            long amount)
        {
            if (amount <= 0L) return;
            if (!settlement.TryAdd(new SettlementEntry(rewardId, type, amount))) return;
            MoneyTransactionType transactionType = type == SettlementEntryType.Salary
                ? MoneyTransactionType.SalaryIncome
                : MoneyTransactionType.BonusIncome;
            _career.Economy.Earn(
                _career.CurrentLeague.CurrentSeason.Year,
                transactionType,
                rewardId,
                amount);
        }

        private long CalculateBonus(long salary, double rate)
        {
            if (rate <= 0d) return 0L;
            long proportional = (long)Math.Round(salary * rate, MidpointRounding.AwayFromZero);
            return Math.Max(_balance.MinimumAwardMoney, proportional);
        }

        private double GetAwardMoneyRate(AwardCategory category)
        {
            return category switch
            {
                AwardCategory.RegularSeasonMvp => _balance.RegularSeasonMvpSalaryRate,
                AwardCategory.PostseasonMvp => _balance.PostseasonMvpSalaryRate,
                AwardCategory.RookieOfYear => _balance.RookieOfYearSalaryRate,
                AwardCategory.GoldGlove => _balance.GoldGloveSalaryRate,
                AwardCategory.BattingAverage or AwardCategory.HomeRun or AwardCategory.RunsBattedIn or
                    AwardCategory.StolenBase or AwardCategory.EarnedRunAverage or AwardCategory.Win or
                    AwardCategory.Strikeout or AwardCategory.Save => _balance.RecordAwardSalaryRate,
                _ => 0d
            };
        }

        private int GetAwardContractBonus(AwardCategory category)
        {
            return category switch
            {
                AwardCategory.RegularSeasonMvp => _balance.RegularSeasonMvpContractBonus,
                AwardCategory.PostseasonMvp => _balance.PostseasonMvpContractBonus,
                AwardCategory.RookieOfYear => _balance.RookieOfYearContractBonus,
                AwardCategory.GoldGlove => _balance.GoldGloveContractBonus,
                AwardCategory.BattingAverage or AwardCategory.HomeRun or AwardCategory.RunsBattedIn or
                    AwardCategory.StolenBase or AwardCategory.EarnedRunAverage or AwardCategory.Win or
                    AwardCategory.Strikeout or AwardCategory.Save => _balance.RecordAwardContractBonus,
                _ => 0
            };
        }
    }
}
