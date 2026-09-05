using System;
using Baseball.Presentation.SharedScreens;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>
    /// 소규모 기록표 View가 전체 생성 경계를 명시적으로 지키는지 검증한다.
    /// </summary>
    public sealed class CompactRecordTableViewTests
    {
        private GameObject _root;
        private CompactRecordTableView _view;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CompactRecordTableViewTests_Root", typeof(RectTransform));
            _view = CompactRecordTableView.CreateRuntime(_root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void Bind_최대행까지표시한다()
        {
            RecordTableModel table = CreateTable(CompactRecordTableView.MaxRows);

            _view.Bind(table);

            Assert.That(_view.Model.Rows.Count, Is.EqualTo(CompactRecordTableView.MaxRows));
            Assert.That(_view.transform.Find("Table/Viewport/Content").childCount,
                Is.EqualTo(CompactRecordTableView.MaxRows));
        }

        [Test]
        public void Bind_최대행을넘으면가상화View사용을강제한다()
        {
            RecordTableModel table = CreateTable(CompactRecordTableView.MaxRows + 1);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => _view.Bind(table));

            StringAssert.Contains("Virtualization", exception.Message);
        }

        private static RecordTableModel CreateTable(int rowCount)
        {
            var rows = new RecordTableRowModel[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                rows[i] = new RecordTableRowModel($"row-{i}", new[]
                {
                    new RecordTableCellModel("Rank", (i + 1).ToString(), RecordSortValue.FromNumber(i + 1))
                });
            }
            return new RecordTableModel(
                new[]
                {
                    new RecordTableColumnModel(
                        "Rank", "순위", RecordSortValueKind.Number, true,
                        RecordSortDirection.Ascending)
                },
                rows);
        }
    }
}
