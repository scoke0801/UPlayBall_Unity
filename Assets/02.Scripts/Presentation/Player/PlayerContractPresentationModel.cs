using System;
using System.Globalization;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Career;

namespace Baseball.Presentation.Player
{
    /// <summary>선수 계약 화면의 실제 Career 데이터를 공용 표와 콘텐츠 상태 계약으로 묶는다.</summary>
    public sealed class PlayerContractPresentationModel
    {
        public const string OfferRowPrefix = "contract-offer-";

        public PlayerContractPresentationModel(
            RecordTableModel contractHistory,
            UiContentStateModel contractHistoryState,
            RecordTableModel bonusProgress,
            UiContentStateModel bonusProgressState,
            RecordTableModel offers,
            UiContentStateModel offersState)
        {
            ContractHistory = contractHistory;
            ContractHistoryState = contractHistoryState ?? throw new ArgumentNullException(nameof(contractHistoryState));
            BonusProgress = bonusProgress;
            BonusProgressState = bonusProgressState ?? throw new ArgumentNullException(nameof(bonusProgressState));
            Offers = offers;
            OffersState = offersState ?? throw new ArgumentNullException(nameof(offersState));
        }

        public RecordTableModel ContractHistory { get; }
        public UiContentStateModel ContractHistoryState { get; }
        public RecordTableModel BonusProgress { get; }
        public UiContentStateModel BonusProgressState { get; }
        public RecordTableModel Offers { get; }
        public UiContentStateModel OffersState { get; }

        /// <summary>공용 표의 Stable Row ID에서 실제 계약 제안 구단 ID를 복원한다.</summary>
        public static bool TryGetOfferTeamId(string rowId, out int teamId)
        {
            teamId = 0;
            return !string.IsNullOrEmpty(rowId) &&
                   rowId.StartsWith(OfferRowPrefix, StringComparison.Ordinal) &&
                   int.TryParse(
                       rowId.Substring(OfferRowPrefix.Length),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out teamId);
        }
    }

    /// <summary>Career 계약 View를 구단주 경제 상태가 없는 선수 전용 표시 모델로 변환한다.</summary>
    public sealed class PlayerContractPresentationModelBuilder
    {
        private const int MaximumCompactRows = CompactRecordTableView.MaxRows;

        /// <summary>현재 계약 이력, 상여 진행과 실제 제안을 공용 표 모델로 만든다.</summary>
        public PlayerContractPresentationModel Build(CareerContractView view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            RecordTableModel history = BuildContractHistory(view.ContractHistory ?? Array.Empty<ContractHistoryView>());
            RecordTableModel bonuses = BuildBonusProgress(view.BonusProgress ?? Array.Empty<ContractBonusProgressView>());
            RecordTableModel offers = BuildOffers(view.RenewalOffers ?? Array.Empty<RenewalContractOfferView>());
            return new PlayerContractPresentationModel(
                history,
                CreateState(history, "계약 이력이 없습니다.", "첫 계약이 확정되면 이곳에 기록됩니다."),
                bonuses,
                CreateState(bonuses, "적용 중인 상여 조건이 없습니다.", "현재 계약에는 성과 상여 조항이 없습니다."),
                offers,
                CreateOfferState(offers, view.LastError));
        }

        private static RecordTableModel BuildContractHistory(ContractHistoryView[] source)
        {
            var columns = new[]
            {
                Column("Term", "기간", RecordSortValueKind.Number, 1f),
                Column("Team", "구단", RecordSortValueKind.Text, 1.7f, RecordCellAlignment.Left),
                Column("Salary", "연봉", RecordSortValueKind.Number, 1.2f, RecordCellAlignment.Right),
                Column("Guaranteed", "총 보장", RecordSortValueKind.Number, 1.2f, RecordCellAlignment.Right),
                Column("Role", "역할", RecordSortValueKind.Text, 1f),
                Column("Status", "상태", RecordSortValueKind.Text, 0.7f)
            };
            int count = Math.Min(source.Length, MaximumCompactRows);
            var rows = new RecordTableRowModel[count];
            for (int index = 0; index < count; index++)
            {
                ContractHistoryView contract = source[index];
                rows[index] = new RecordTableRowModel(
                    $"contract-history-{contract.SignedYear}-{index}",
                    new[]
                    {
                        NumberCell("Term", $"{contract.SignedYear}~{contract.EndYear}", contract.SignedYear),
                        TextCell("Team", contract.TeamName),
                        NumberCell("Salary", FormatMoney(contract.AnnualSalary), contract.AnnualSalary),
                        NumberCell("Guaranteed", FormatMoney(contract.GuaranteedValue), contract.GuaranteedValue),
                        TextCell("Role", FormatRole(contract.ExpectedRole)),
                        TextCell("Status", contract.IsCurrent ? "현재" : "종료")
                    },
                    contract.IsCurrent,
                    contract.IsCurrent ? "현재 계약" : string.Empty);
            }
            return new RecordTableModel(columns, rows);
        }

        private static RecordTableModel BuildBonusProgress(ContractBonusProgressView[] source)
        {
            var columns = new[]
            {
                Column("Condition", "조건", RecordSortValueKind.Text, 1.8f, RecordCellAlignment.Left),
                Column("Reward", "상여", RecordSortValueKind.Number, 1f, RecordCellAlignment.Right),
                Column("Progress", "진행", RecordSortValueKind.Number, 1f, RecordCellAlignment.Right),
                Column("Status", "상태", RecordSortValueKind.Text, 0.8f)
            };
            int count = Math.Min(source.Length, MaximumCompactRows);
            var rows = new RecordTableRowModel[count];
            for (int index = 0; index < count; index++)
            {
                ContractBonusProgressView bonus = source[index];
                rows[index] = new RecordTableRowModel(
                    $"contract-bonus-{bonus.ClauseId}",
                    new[]
                    {
                        TextCell("Condition", FormatBonusCondition(bonus)),
                        NumberCell("Reward", FormatMoney(bonus.Reward), bonus.Reward),
                        NumberCell("Progress", FormatBonusProgress(bonus), bonus.NormalizedProgress),
                        TextCell("Status", bonus.IsCompleted ? "달성" : bonus.HasSample ? "진행 중" : "기록 없음")
                    },
                    bonus.IsCompleted,
                    bonus.IsCompleted ? "상여 달성" : string.Empty);
            }
            return new RecordTableModel(columns, rows);
        }

        private static RecordTableModel BuildOffers(RenewalContractOfferView[] source)
        {
            var columns = new[]
            {
                Column("Team", "제안 구단", RecordSortValueKind.Text, 1.8f, RecordCellAlignment.Left),
                Column("League", "리그", RecordSortValueKind.Text, 0.8f),
                Column("Term", "기간", RecordSortValueKind.Number, 0.65f),
                Column("Salary", "연봉", RecordSortValueKind.Number, 1.1f, RecordCellAlignment.Right),
                Column("Signing", "계약금", RecordSortValueKind.Number, 1.1f, RecordCellAlignment.Right),
                Column("Role", "예상 역할", RecordSortValueKind.Text, 0.9f),
                Column("Playing", "출장", RecordSortValueKind.Number, 0.7f),
                Column("Fit", "필요/육성/경쟁", RecordSortValueKind.Number, 1.25f, RecordCellAlignment.Left),
                Column("Clause", "이동 조항", RecordSortValueKind.Text, 1.5f, RecordCellAlignment.Left)
            };
            int count = Math.Min(source.Length, MaximumCompactRows);
            var rows = new RecordTableRowModel[count];
            for (int index = 0; index < count; index++)
            {
                RenewalContractOfferView offer = source[index];
                rows[index] = new RecordTableRowModel(
                    PlayerContractPresentationModel.OfferRowPrefix + offer.TeamId.ToString(CultureInfo.InvariantCulture),
                    new[]
                    {
                        TextCell("Team", $"{FormatChannel(offer.Channel)} · {offer.TeamName}"),
                        TextCell("League", FormatLeague(offer.LeagueLevel)),
                        NumberCell("Term", $"{offer.ContractYears}년", offer.ContractYears),
                        NumberCell("Salary", FormatMoney(offer.AnnualSalary), offer.AnnualSalary),
                        NumberCell("Signing", FormatMoney(offer.SigningBonus), offer.SigningBonus),
                        TextCell("Role", FormatRole(offer.ExpectedRole)),
                        NumberCell("Playing", offer.EstimatedPlayingTime.ToString("P0", CultureInfo.InvariantCulture), offer.EstimatedPlayingTime),
                        NumberCell(
                            "Fit",
                            $"{offer.PositionNeed}/{offer.DevelopmentRating} · {offer.CompetitorSummary}",
                            offer.PositionNeed + offer.DevelopmentRating / 100d),
                        TextCell("Clause", FormatMovementClause(offer))
                    },
                    offer.IsSelected,
                    offer.IsSelected ? "선택한 계약 제안" : string.Empty);
            }
            return new RecordTableModel(columns, rows);
        }

        private static UiContentStateModel CreateState(RecordTableModel table, string title, string message) =>
            table.Rows.Count > 0 ? UiContentStateModel.Ready : UiContentStateModel.CreateEmpty(title, message);

        private static UiContentStateModel CreateOfferState(RecordTableModel table, string lastError)
        {
            if (table.Rows.Count > 0)
                return UiContentStateModel.Ready;
            return string.IsNullOrWhiteSpace(lastError)
                ? UiContentStateModel.CreateEmpty(
                    "현재 선택 가능한 계약 제안이 없습니다.",
                    "협상 단계가 시작되면 실제 구단 제안이 이곳에 표시됩니다.")
                : UiContentStateModel.CreateError("계약 제안을 확인할 수 없습니다.", lastError);
        }

        private static RecordTableColumnModel Column(
            string id,
            string label,
            RecordSortValueKind kind,
            float width,
            RecordCellAlignment alignment = RecordCellAlignment.Center) =>
            new(id, label, kind, true, RecordSortDirection.Descending, width, alignment);

        private static RecordTableCellModel TextCell(string id, string value)
        {
            string safe = value ?? string.Empty;
            return new RecordTableCellModel(id, safe, RecordSortValue.FromText(safe));
        }

        private static RecordTableCellModel NumberCell(string id, string display, double value) =>
            new(id, display, RecordSortValue.FromNumber(value));

        private static string FormatMoney(long amount) => amount >= 100_000_000L
            ? $"{amount / 100_000_000d:0.##}억원"
            : $"{amount / 10_000d:N0}만원";

        private static string FormatRole(ExpectedRole role) => role switch
        {
            ExpectedRole.StartingCompetition => "주전 경쟁",
            ExpectedRole.RosterCompetition => "로스터 경쟁",
            _ => "백업 경쟁"
        };

        private static string FormatLeague(LeagueLevel level) =>
            WorldGenerationConfiguration.GetDefaultDefinition(level).UiDisplayName;

        private static string FormatChannel(ContractOfferChannel channel) => channel switch
        {
            ContractOfferChannel.CurrentTeamRenewal => "기존 구단",
            ContractOfferChannel.CurrentTeamExtension => "연장 계약",
            ContractOfferChannel.ContractContinuation => "현 계약 유지",
            ContractOfferChannel.OpenMarket => "FA",
            ContractOfferChannel.Promotion => "승격",
            ContractOfferChannel.Rehabilitation => "재기",
            ContractOfferChannel.DevelopmentFallback => "육성",
            ContractOfferChannel.TryoutContract => "테스트",
            _ => "신인 계약"
        };

        private static string FormatMovementClause(RenewalContractOfferView offer)
        {
            if (offer.HasUpperLeagueReleaseClause)
                return $"상위 리그 이적 · 보상 {FormatMoney(offer.UpperLeagueReleaseCompensation)}";
            return offer.HasRelegationTransferRequestClause ? "강등 시 이적 요청" : "별도 조항 없음";
        }

        private static string FormatBonusCondition(ContractBonusProgressView bonus) => bonus.Metric switch
        {
            ContractBonusMetric.GamesPlayed => $"{bonus.TargetValue:0}경기 출장",
            ContractBonusMetric.HomeRuns => $"홈런 {bonus.TargetValue:0}개",
            ContractBonusMetric.RunsBattedIn => $"타점 {bonus.TargetValue:0}개",
            ContractBonusMetric.OnBasePlusSlugging => $"OPS {bonus.TargetValue:.000}",
            ContractBonusMetric.PitchingAppearances => $"{bonus.TargetValue:0}경기 등판",
            ContractBonusMetric.PitchingOuts => $"{(int)bonus.TargetValue / 3}.{(int)bonus.TargetValue % 3}이닝",
            ContractBonusMetric.PitchingStrikeouts => $"탈삼진 {bonus.TargetValue:0}개",
            ContractBonusMetric.EarnedRunAverage => $"평균자책 {bonus.TargetValue:0.00} 이하",
            ContractBonusMetric.IndividualAward => "개인상 수상",
            ContractBonusMetric.Championship => "리그 우승",
            _ => bonus.Metric.ToString()
        };

        private static string FormatBonusProgress(ContractBonusProgressView bonus)
        {
            if (!bonus.HasSample)
                return "-";
            return bonus.Metric switch
            {
                ContractBonusMetric.OnBasePlusSlugging => $"{bonus.CurrentValue:.000}/{bonus.TargetValue:.000}",
                ContractBonusMetric.EarnedRunAverage => $"{bonus.CurrentValue:0.00}/{bonus.TargetValue:0.00}",
                ContractBonusMetric.PitchingOuts =>
                    $"{(int)bonus.CurrentValue / 3}.{(int)bonus.CurrentValue % 3}/" +
                    $"{(int)bonus.TargetValue / 3}.{(int)bonus.TargetValue % 3}",
                _ => $"{bonus.CurrentValue:0}/{bonus.TargetValue:0}"
            };
        }
    }
}
