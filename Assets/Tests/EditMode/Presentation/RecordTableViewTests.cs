using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>
    /// 대량 기록표가 데이터 Row 수와 무관한 풀을 재사용하고 Stable ID 선택을 유지하는지 검증한다.
    /// </summary>
    public sealed class RecordTableViewTests
    {
        private GameObject _root;
        private RecordTableView _view;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("RecordTableViewTests_Root", typeof(RectTransform));
            _root.GetComponent<RectTransform>().sizeDelta = new Vector2(1200f, 600f);
            _view = RecordTableView.CreateRuntime(
                _root.transform,
                new Vector2(1000f, 360f),
                Vector2.zero);
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void Bind_천개행에서도ViewportRow만생성한다()
        {
            _view.Bind(CreateTable(1000));

            Assert.That(_view.Model.Rows.Count, Is.EqualTo(1000));
            Assert.That(_view.CreatedRowViewCount, Is.GreaterThan(0));
            Assert.That(_view.CreatedRowViewCount, Is.LessThan(20));
        }

        [Test]
        public void Scroll_생성Row수를늘리지않고Pool을재사용한다()
        {
            _view.Bind(CreateTable(1000));
            int createdCount = _view.CreatedRowViewCount;

            Assert.That(_view.TrySelectRow("row-999", bringIntoView: true), Is.True);

            Assert.That(_view.FirstRenderedRowIndex, Is.GreaterThan(0));
            Assert.That(_view.CreatedRowViewCount, Is.EqualTo(createdCount));
        }

        [Test]
        public void Bind_정렬후에도StableId선택을유지한다()
        {
            RecordTableModel source = CreateTable(100);
            _view.Bind(source);
            Assert.That(_view.TrySelectRow("row-73", bringIntoView: true), Is.True);

            _view.Bind(source.SortBy("Value", RecordSortDirection.Ascending));

            Assert.That(_view.SelectedRowId, Is.EqualTo("row-73"));
        }

        [Test]
        public void HeaderClick_모델을StableSort하고변경Event를보낸다()
        {
            _view.Bind(CreateTable(20));
            string changedColumn = null;
            RecordSortDirection changedDirection = RecordSortDirection.None;
            _view.SortChanged += (columnId, direction) =>
            {
                changedColumn = columnId;
                changedDirection = direction;
            };

            Transform header = _view.transform.Find("Table/HeaderViewport/Header/Value");
            Assert.That(header, Is.Not.Null);
            header.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            Assert.That(changedColumn, Is.EqualTo("Value"));
            Assert.That(changedDirection, Is.EqualTo(RecordSortDirection.Descending));
            Assert.That(_view.Model.Rows[0].RowId, Is.EqualTo("row-19"));
        }

        [Test]
        public void Bind_Empty상태에서는데이터표대신상태를표시한다()
        {
            UiContentStateModel state = UiContentStateModel.CreateEmpty(
                "기록 없음",
                "아직 집계된 기록이 없습니다.");

            _view.Bind(null, state);

            Assert.That(_view.ContentState, Is.SameAs(state));
            Assert.That(_view.transform.Find("Table").gameObject.activeSelf, Is.False);
            Assert.That(_view.transform.Find("ContentState").gameObject.activeSelf, Is.True);
        }

        private static RecordTableModel CreateTable(int rowCount)
        {
            var rows = new RecordTableRowModel[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                rows[i] = new RecordTableRowModel($"row-{i}", new[]
                {
                    new RecordTableCellModel(
                        "Name",
                        $"선수 {i}",
                        RecordSortValue.FromText($"선수 {i}")),
                    new RecordTableCellModel(
                        "Value",
                        i.ToString(),
                        RecordSortValue.FromNumber(i))
                });
            }

            return new RecordTableModel(
                new[]
                {
                    new RecordTableColumnModel(
                        "Name", "선수", RecordSortValueKind.Text, true,
                        RecordSortDirection.Ascending, 2f, RecordCellAlignment.Left),
                    new RecordTableColumnModel(
                        "Value", "기록", RecordSortValueKind.Number, true,
                        RecordSortDirection.Descending)
                },
                rows);
        }
    }
}
