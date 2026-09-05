using Baseball.Presentation.SharedScreens;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>
    /// 공용 기록표가 숫자 정렬과 동률 순서를 결정론적으로 유지하는지 검증한다.
    /// </summary>
    public sealed class RecordTableModelTests
    {
        [Test]
        public void SortBy_동률행의기존순서를유지한다()
        {
            RecordTableModel table = CreateTable();

            RecordTableModel sorted = table.SortBy("Value", RecordSortDirection.Descending);

            Assert.That(sorted.Rows[0].RowId, Is.EqualTo("first-tie"));
            Assert.That(sorted.Rows[1].RowId, Is.EqualTo("second-tie"));
            Assert.That(sorted.Rows[2].RowId, Is.EqualTo("lower"));
            Assert.That(sorted.Rows[3].RowId, Is.EqualTo("empty"));
        }

        [Test]
        public void SortBy_오름차순에서도Empty를마지막에둔다()
        {
            RecordTableModel sorted = CreateTable().SortBy("Value", RecordSortDirection.Ascending);

            Assert.That(sorted.Rows[0].RowId, Is.EqualTo("lower"));
            Assert.That(sorted.Rows[3].RowId, Is.EqualTo("empty"));
        }

        [Test]
        public void Constructor_열과셀의정렬값종류가다르면거부한다()
        {
            Assert.Throws<System.ArgumentException>(() => new RecordTableModel(
                new[] { new RecordTableColumnModel("Value", "값", RecordSortValueKind.Number) },
                new[]
                {
                    new RecordTableRowModel("row", new[]
                    {
                        new RecordTableCellModel("Value", "문자", RecordSortValue.FromText("문자"))
                    })
                }));
        }

        private static RecordTableModel CreateTable()
        {
            var columns = new[]
            {
                new RecordTableColumnModel("Value", "값", RecordSortValueKind.Number)
            };
            return new RecordTableModel(columns, new[]
            {
                CreateRow("first-tie", "10", RecordSortValue.FromNumber(10d)),
                CreateRow("second-tie", "10", RecordSortValue.FromNumber(10d)),
                CreateRow("lower", "5", RecordSortValue.FromNumber(5d)),
                CreateRow("empty", "-", RecordSortValue.Empty())
            });
        }

        private static RecordTableRowModel CreateRow(string rowId, string displayValue, RecordSortValue sortValue)
        {
            return new RecordTableRowModel(rowId, new[]
            {
                new RecordTableCellModel("Value", displayValue, sortValue)
            });
        }
    }
}
