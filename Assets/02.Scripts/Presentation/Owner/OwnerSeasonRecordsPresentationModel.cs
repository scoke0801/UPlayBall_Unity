using System;
using System.Collections.Generic;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단주 기록 화면 한 부문의 확정된 표와 상태 문구다.</summary>
    public sealed class OwnerSeasonRecordsCategoryModel
    {
        public OwnerSeasonRecordsCategoryModel(
            CareerRecordCategory category,
            string displayName,
            RecordTableModel table,
            UiContentStateModel contentState,
            string qualificationText,
            string focusedRowId)
        {
            Category = category;
            DisplayName = displayName ?? string.Empty;
            Table = table ?? throw new ArgumentNullException(nameof(table));
            ContentState = contentState ?? throw new ArgumentNullException(nameof(contentState));
            QualificationText = qualificationText ?? string.Empty;
            FocusedRowId = focusedRowId ?? string.Empty;
        }

        public CareerRecordCategory Category { get; }
        public string DisplayName { get; }
        public RecordTableModel Table { get; }
        public UiContentStateModel ContentState { get; }
        public string QualificationText { get; }
        public string FocusedRowId { get; }
    }

    /// <summary>
    /// Game 레이어가 확정한 네 부문 리더보드를 화면이 부문 전환에서 다시 계산하지 않도록 한 번에 표로 만든다.
    /// </summary>
    public sealed class OwnerSeasonRecordsPresentationModel
    {
        private readonly OwnerSeasonRecordsCategoryModel[] _categories;

        public OwnerSeasonRecordsPresentationModel(OwnerSeasonRecordsView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            SeasonLabel = view.SeasonLabel;
            LeagueLabel = view.LeagueLabel;
            _categories = new OwnerSeasonRecordsCategoryModel[view.Categories.Count];
            for (int index = 0; index < _categories.Length; index++)
            {
                OwnerSeasonRecordsCategoryView source = view.Categories[index];
                RecordTableModel table = LeagueLeaderboardSnapshotAdapter.CreateLeaderboardTable(
                    source.Columns,
                    source.Leaderboard,
                    source.PrimaryMetric,
                    "내 구단 선수");
                _categories[index] = new OwnerSeasonRecordsCategoryModel(
                    source.Category,
                    source.DisplayName,
                    table,
                    source.Leaderboard.Length > 0
                        ? UiContentStateModel.Ready
                        : UiContentStateModel.CreateEmpty(
                            "기록 없음",
                            view.HasAnyRecord
                                ? source.DisplayName + " 부문 규정을 충족한 선수가 아직 없습니다."
                                : "경기를 진행하면 리그 전체 선수 기록이 쌓입니다."),
                    source.QualificationText,
                    LeagueLeaderboardSnapshotAdapter.FindHighlightedRowId(source.Leaderboard));
            }
        }

        public string SeasonLabel { get; }
        public string LeagueLabel { get; }
        public IReadOnlyList<OwnerSeasonRecordsCategoryModel> Categories => _categories;
    }
}
