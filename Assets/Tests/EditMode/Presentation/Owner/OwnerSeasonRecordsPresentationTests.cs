using System;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>구단주 선수 기록 화면이 Game 레이어 확정 결과만 표시하고 리그 탭에서 도달 가능한지 검증한다.</summary>
    public sealed class OwnerSeasonRecordsPresentationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Scroll_드래그방향을고정하고가로이동량을절반으로낮춘다(bool horizontalDrag)
        {
            var root = new GameObject("RecordsInputTests", typeof(RectTransform));
            var events = new GameObject("Events", typeof(EventSystem));
            try
            {
                var scroll = root.AddComponent<UIRecordTableScrollRect>();
                var content = new GameObject("Content", typeof(RectTransform));
                content.transform.SetParent(root.transform, false);
                scroll.content = content.GetComponent<RectTransform>();
                scroll.movementType = ScrollRect.MovementType.Unrestricted;
                var input = new PointerEventData(events.GetComponent<EventSystem>())
                {
                    button = PointerEventData.InputButton.Left,
                    position = Vector2.zero
                };
                scroll.OnBeginDrag(input);
                Vector2 first = horizontalDrag ? new Vector2(-100f, 5f) : new Vector2(-5f, 100f);
                input.position = first;
                scroll.OnDrag(input);
                Assert.That(scroll.content.anchoredPosition, Is.EqualTo(horizontalDrag
                    ? new Vector2(-50f, 0f) : new Vector2(0f, 100f)));
                Assert.That(input.position, Is.EqualTo(first));
                input.position = new Vector2(-200f, 200f);
                scroll.OnDrag(input);
                Assert.That(scroll.content.anchoredPosition, Is.EqualTo(horizontalDrag
                    ? new Vector2(-100f, 0f) : new Vector2(0f, 200f)));
                scroll.OnEndDrag(input);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(events);
            }
        }

        [TestCase(1f, 0.1f, 18f, 0f)]
        [TestCase(0.1f, -1f, 0f, 36f)]
        public void Scroll_휠은주축만이동하고입력값을복원한다(float x, float y, float expectedX, float expectedY)
        {
            var root = new GameObject("RecordsWheelTests", typeof(RectTransform));
            var events = new GameObject("Events", typeof(EventSystem));
            try
            {
                RecordTableView table = RecordTableView.CreateRuntime(root.transform);
                table.Bind(CreateModel(true).Categories[0].Table);
                ScrollRect scroll = table.ScrollRect;
                scroll.horizontal = true;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Unrestricted;
                var input = new PointerEventData(events.GetComponent<EventSystem>())
                {
                    scrollDelta = new Vector2(x, y)
                };
                scroll.OnScroll(input);
                Assert.That(scroll.content.anchoredPosition, Is.EqualTo(new Vector2(expectedX, expectedY)));
                Assert.That(input.scrollDelta, Is.EqualTo(new Vector2(x, y)));
                Assert.That(scroll.inertia, Is.False);
                scroll.vertical = false;
                Vector2 before = scroll.content.anchoredPosition;
                input.scrollDelta = new Vector2(0f, -1f);
                scroll.OnScroll(input);
                Assert.That(scroll.content.anchoredPosition, Is.EqualTo(before),
                    "세로 스크롤이 필요 없어도 세로 휠이 가로 이동으로 바뀌면 안 된다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(events);
            }
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void View_상세30행스크롤은열제목을세로고정하고가로동기화한다(int width, int height)
        {
            var root = new GameObject("RecordsScrollTests", typeof(RectTransform));
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
                UI_Scene_OwnerSeasonRecords view = UI_Scene_OwnerSeasonRecords.CreateRuntime(root.transform);
                view.Bind(CreateModel(true));
                RecordTableView table = view.GetComponentInChildren<RecordTableView>();
                CareerRecordMetric[] metrics = CareerRecordsService.GetColumns(
                    CareerRecordCategory.Batting, CareerRecordViewMode.Expanded);
                var rows = new CareerRecordLeaderboardRow[30];
                for (int i = 0; i < rows.Length; i++)
                    rows[i] = CreateRow(i + 1, i + 1, "긴이름선수", 1, "2025 롯데 자이언츠", false, metrics, .400 - i * .001);
                table.Bind(LeagueLeaderboardSnapshotAdapter.CreateLeaderboardTable(
                    metrics, rows, metrics[0], "내 구단 선수"));
                var header = (RectTransform)table.transform.Find("Table/HeaderViewport/Header");
                var tabs = (RectTransform)view.transform.Find("CategoryBar");
                Vector3 tabPosition = tabs.position;
                table.ScrollRect.content.anchoredPosition = new Vector2(-250f, 450f);
                table.ScrollRect.onValueChanged.Invoke(Vector2.zero);
                Assert.That(header.anchoredPosition, Is.EqualTo(new Vector2(-250f, 0f)));
                Assert.That(tabs.position, Is.EqualTo(tabPosition));
                Assert.That(table.FirstRenderedRowIndex, Is.GreaterThan(0));
                table.TrySelectRow("player-30", true);
                Assert.That(table.SelectedRowId, Is.EqualTo("player-30"));
                table.Bind(table.Model);
                Assert.That(header.anchoredPosition, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Model_네부문표를한번에만들고내구단선수를강조한다()
        {
            OwnerSeasonRecordsPresentationModel model = CreateModel(hasRecord: true);

            Assert.That(model.Categories.Count, Is.EqualTo(4));
            Assert.That(model.SeasonLabel, Is.EqualTo("2028 시즌 1년차"));

            OwnerSeasonRecordsCategoryModel batting = model.Categories[0];
            Assert.That(batting.ContentState.Kind, Is.EqualTo(UiContentStateKind.Ready));
            Assert.That(batting.Table.Rows.Count, Is.EqualTo(2));
            Assert.That(batting.Table.Columns[0].ColumnId, Is.EqualTo("Rank"));
            Assert.That(batting.Table.Columns[1].ColumnId, Is.EqualTo("Player"));
            Assert.That(batting.Table.Columns[2].ColumnId, Is.EqualTo("Team"));
            Assert.That(batting.QualificationText, Is.EqualTo("규정 충족 2명"));

            RecordTableRowModel highlighted = FindRow(batting.Table, "player-2");
            Assert.That(highlighted.IsHighlighted, Is.True);
            Assert.That(highlighted.HighlightReason, Is.EqualTo("내 구단 선수"));
            Assert.That(FindRow(batting.Table, "player-1").IsHighlighted, Is.False);
            Assert.That(batting.FocusedRowId, Is.EqualTo("player-2"));
        }

        [Test]
        public void Model_기록이없으면빈상태문구를만든다()
        {
            OwnerSeasonRecordsPresentationModel model = CreateModel(hasRecord: false);

            OwnerSeasonRecordsCategoryModel batting = model.Categories[0];
            Assert.That(batting.ContentState.Kind, Is.EqualTo(UiContentStateKind.Empty));
            Assert.That(batting.ContentState.Message, Does.Contain("경기를 진행하면"));
        }

        [Test]
        public void Profile_리그탭에서선수기록과일정역사기록에도달할수있다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();
            NavigationEntry league = profile.Navigation.FindEntry(OwnerNavigationRoutes.League);

            AssertReachableLeagueTab(profile, league,
                OwnerSharedInformationWorkspaceCoordinator.SeasonRecordsRouteId, "선수 기록");
            AssertReachableLeagueTab(profile, league,
                OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId, "일정");
            AssertReachableLeagueTab(profile, league,
                OwnerSharedInformationWorkspaceCoordinator.RecordsRouteId, "역사 기록");
        }

        [Test]
        public void View_리그RefSkin과파란부문선택을사용한다()
        {
            var root = new GameObject("OwnerSeasonRecordsViewTests_Root", typeof(RectTransform));
            try
            {
                UI_Scene_OwnerSeasonRecords view = UI_Scene_OwnerSeasonRecords.CreateRuntime(root.transform);
                view.Bind(CreateModel(hasRecord: true));

                RecordTableView table = view.transform.Find(
                    "SeasonRecordTableHost/SeasonRecordTable").GetComponent<RecordTableView>();
                Image selectedCategory = view.transform.Find(
                    "CategoryBar/CategoryButton0").GetComponent<Image>();

                Assert.That(table.VisualStyle, Is.EqualTo(RecordTableVisualStyle.ReferenceLight));
                Assert.That(selectedCategory.color, Is.EqualTo(CareerUiTheme.ReferenceDataAccent));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AssertReachableLeagueTab(
            GameModeUiProfile profile,
            NavigationEntry league,
            string routeId,
            string displayName)
        {
            NavigationEntry entry = profile.Navigation.FindEntry(routeId);
            Assert.That(entry, Is.Not.Null, routeId);
            Assert.That(entry.DisplayName, Is.EqualTo(displayName));
            Assert.That(entry.IsEnabled, Is.True, routeId);
            Assert.That(entry.IsVisible(profile.Capabilities), Is.True, routeId);

            bool isLeagueChild = false;
            for (int index = 0; index < league.Children.Count; index++)
                if (string.Equals(league.Children[index].RouteId, routeId, StringComparison.Ordinal))
                    isLeagueChild = true;
            Assert.That(isLeagueChild, Is.True, routeId + "가 리그 탭에 없다.");
        }

        private static RecordTableRowModel FindRow(RecordTableModel table, string rowId)
        {
            for (int index = 0; index < table.Rows.Count; index++)
                if (string.Equals(table.Rows[index].RowId, rowId, StringComparison.Ordinal))
                    return table.Rows[index];
            throw new InvalidOperationException($"{rowId} 행이 없습니다.");
        }

        private static OwnerSeasonRecordsPresentationModel CreateModel(bool hasRecord)
        {
            CareerRecordMetric[] columns = LeagueLeaderboardService.GetBasicColumns(CareerRecordCategory.Batting);
            CareerRecordLeaderboardRow[] leaderboard = hasRecord
                ? new[]
                {
                    CreateRow(1, 1, "리그 1위", 1, "상대 구단", isMyPlayer: false, columns, 0.352d),
                    CreateRow(2, 2, "내 구단 4번", 2, "내 구단", isMyPlayer: true, columns, 0.318d)
                }
                : Array.Empty<CareerRecordLeaderboardRow>();

            var categories = new OwnerSeasonRecordsCategoryView[4];
            var names = new[] { "타격", "투구", "수비", "주루" };
            var kinds = new[]
            {
                CareerRecordCategory.Batting,
                CareerRecordCategory.Pitching,
                CareerRecordCategory.Fielding,
                CareerRecordCategory.Baserunning
            };
            for (int index = 0; index < categories.Length; index++)
            {
                CareerRecordLeaderboardRow[] rows = index == 0
                    ? leaderboard
                    : Array.Empty<CareerRecordLeaderboardRow>();
                categories[index] = new OwnerSeasonRecordsCategoryView(
                    kinds[index],
                    names[index],
                    LeagueLeaderboardService.GetBasicColumns(kinds[index]),
                    rows,
                    rows.Length,
                    hasRecord
                        ? "규정 충족 " + rows.Length + "명"
                        : "아직 진행한 경기가 없습니다.");
            }

            return new OwnerSeasonRecordsPresentationModel(
                new OwnerSeasonRecordsView("2028 시즌 1년차", "루키 리그", 2, hasRecord, categories));
        }

        private static CareerRecordLeaderboardRow CreateRow(
            int rank,
            int playerId,
            string playerName,
            int teamId,
            string teamName,
            bool isMyPlayer,
            CareerRecordMetric[] columns,
            double primaryValue)
        {
            var metrics = new CareerRecordMetricValue[columns.Length];
            for (int index = 0; index < columns.Length; index++)
                metrics[index] = new CareerRecordMetricValue(columns[index], index == 0 ? primaryValue : index);
            return new CareerRecordLeaderboardRow(
                rank, playerId, playerName, teamId, teamName, isMyPlayer, metrics);
        }
    }
}
