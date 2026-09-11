using System;
using System.Collections.Generic;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// 기록 열이 비교할 값의 종류를 구분한다.
    /// </summary>
    public enum RecordSortValueKind
    {
        Empty = 0,
        Text = 1,
        Number = 2
    }

    /// <summary>
    /// 기록표 정렬 방향을 구분한다.
    /// </summary>
    public enum RecordSortDirection
    {
        None = 0,
        Ascending = 1,
        Descending = 2
    }

    /// <summary>
    /// 기록표 셀의 숫자와 텍스트 정렬 위치를 구분한다.
    /// </summary>
    public enum RecordCellAlignment
    {
        Left = 0,
        Center = 1,
        Right = 2
    }

    /// <summary>
    /// 표시 문자열과 분리된 결정론적 정렬 값을 보관한다.
    /// </summary>
    public readonly struct RecordSortValue
    {
        private RecordSortValue(RecordSortValueKind kind, string text, double number)
        {
            Kind = kind;
            Text = text;
            Number = number;
        }

        public RecordSortValueKind Kind { get; }
        public string Text { get; }
        public double Number { get; }

        /// <summary>
        /// 값이 없는 셀이 항상 정렬 끝에 남도록 Empty 값을 만든다.
        /// </summary>
        public static RecordSortValue Empty()
        {
            return new RecordSortValue(RecordSortValueKind.Empty, string.Empty, 0d);
        }

        /// <summary>
        /// 대소문자 비의존 후 원문 순서로 비교할 Text 값을 만든다.
        /// </summary>
        public static RecordSortValue FromText(string value)
        {
            return string.IsNullOrEmpty(value)
                ? Empty()
                : new RecordSortValue(RecordSortValueKind.Text, value, 0d);
        }

        /// <summary>
        /// 이미 계산된 통계 숫자로 비교할 Number 값을 만든다.
        /// </summary>
        public static RecordSortValue FromNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "정렬 숫자는 유한해야 합니다.");
            return new RecordSortValue(RecordSortValueKind.Number, string.Empty, value);
        }
    }

    /// <summary>
    /// 공용 기록표 한 열의 표시와 정렬 계약을 정의한다.
    /// </summary>
    public sealed class RecordTableColumnModel
    {
        /// <summary>
        /// 열 ID와 표시 이름, 정렬 값 종류를 만든다.
        /// </summary>
        public RecordTableColumnModel(
            string columnId,
            string displayName,
            RecordSortValueKind sortValueKind,
            bool isSortable = true,
            RecordSortDirection defaultDirection = RecordSortDirection.Descending,
            float widthWeight = 1f,
            RecordCellAlignment alignment = RecordCellAlignment.Center)
        {
            if (string.IsNullOrWhiteSpace(columnId))
                throw new ArgumentException("기록표 열 ID는 비어 있을 수 없습니다.", nameof(columnId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("기록표 열 이름은 비어 있을 수 없습니다.", nameof(displayName));
            if (sortValueKind == RecordSortValueKind.Empty && isSortable)
                throw new ArgumentException("정렬 가능한 열에는 값 종류가 필요합니다.", nameof(sortValueKind));
            if (widthWeight <= 0f || float.IsNaN(widthWeight) || float.IsInfinity(widthWeight))
                throw new ArgumentOutOfRangeException(nameof(widthWeight), "열 너비 가중치는 양수여야 합니다.");

            ColumnId = columnId;
            DisplayName = displayName;
            SortValueKind = sortValueKind;
            IsSortable = isSortable;
            DefaultDirection = isSortable ? defaultDirection : RecordSortDirection.None;
            WidthWeight = widthWeight;
            Alignment = alignment;
        }

        public string ColumnId { get; }
        public string DisplayName { get; }
        public RecordSortValueKind SortValueKind { get; }
        public bool IsSortable { get; }
        public RecordSortDirection DefaultDirection { get; }
        public float WidthWeight { get; }
        public RecordCellAlignment Alignment { get; }
    }

    /// <summary>
    /// 공용 기록표 셀의 표시 문자열과 정렬 값을 묶는다.
    /// </summary>
    public sealed class RecordTableCellModel
    {
        /// <summary>
        /// 열 ID, 표시 문자열, 정렬 값으로 셀을 만든다.
        /// </summary>
        public RecordTableCellModel(string columnId, string displayValue, RecordSortValue sortValue)
        {
            if (string.IsNullOrWhiteSpace(columnId))
                throw new ArgumentException("기록표 셀의 열 ID는 비어 있을 수 없습니다.", nameof(columnId));

            ColumnId = columnId;
            DisplayValue = displayValue ?? string.Empty;
            SortValue = sortValue;
        }

        public string ColumnId { get; }
        public string DisplayValue { get; }
        public RecordSortValue SortValue { get; }
    }

    /// <summary>
    /// Stable ID와 강조 정보를 포함한 공용 기록표 한 행이다.
    /// </summary>
    public sealed class RecordTableRowModel
    {
        private readonly RecordTableCellModel[] _cells;

        /// <summary>
        /// 행 ID와 열 순서가 보존된 셀 목록으로 행을 만든다.
        /// </summary>
        public RecordTableRowModel(
            string rowId,
            IReadOnlyList<RecordTableCellModel> cells,
            bool isHighlighted = false,
            string highlightReason = null)
        {
            if (string.IsNullOrWhiteSpace(rowId))
                throw new ArgumentException("기록표 행 ID는 비어 있을 수 없습니다.", nameof(rowId));

            RowId = rowId;
            IsHighlighted = isHighlighted;
            HighlightReason = highlightReason ?? string.Empty;
            _cells = CopyCells(cells);
        }

        public string RowId { get; }
        public IReadOnlyList<RecordTableCellModel> Cells => _cells;
        public bool IsHighlighted { get; }
        public string HighlightReason { get; }

        /// <summary>
        /// 지정 열에 해당하는 셀을 찾고 없으면 null을 반환한다.
        /// </summary>
        public RecordTableCellModel FindCell(string columnId)
        {
            for (int i = 0; i < _cells.Length; i++)
            {
                if (string.Equals(_cells[i].ColumnId, columnId, StringComparison.Ordinal))
                    return _cells[i];
            }
            return null;
        }

        private static RecordTableCellModel[] CopyCells(IReadOnlyList<RecordTableCellModel> cells)
        {
            if (cells == null)
                throw new ArgumentNullException(nameof(cells));

            var copy = new RecordTableCellModel[cells.Count];
            var columnIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < cells.Count; i++)
            {
                RecordTableCellModel cell = cells[i] ??
                    throw new ArgumentException("기록표 셀은 null일 수 없습니다.", nameof(cells));
                if (!columnIds.Add(cell.ColumnId))
                    throw new ArgumentException($"한 행에 중복 열이 있습니다: {cell.ColumnId}", nameof(cells));
                copy[i] = cell;
            }
            return copy;
        }
    }

    /// <summary>
    /// Schedule, League, Records가 공유하는 열·행과 Stable Sort 결과를 보관한다.
    /// </summary>
    public sealed class RecordTableModel
    {
        private readonly RecordTableColumnModel[] _columns;
        private readonly RecordTableRowModel[] _rows;

        /// <summary>
        /// 열·행을 검증하고 원본 순서가 보존된 기록표를 만든다.
        /// </summary>
        public RecordTableModel(
            IReadOnlyList<RecordTableColumnModel> columns,
            IReadOnlyList<RecordTableRowModel> rows,
            string sortedColumnId = null,
            RecordSortDirection sortDirection = RecordSortDirection.None)
        {
            _columns = CopyColumns(columns);
            _rows = CopyRows(rows);
            SortedColumnId = sortedColumnId ?? string.Empty;
            SortDirection = sortDirection;
            ValidateRows();
            ValidateSortState();
        }

        public IReadOnlyList<RecordTableColumnModel> Columns => _columns;
        public IReadOnlyList<RecordTableRowModel> Rows => _rows;
        public string SortedColumnId { get; }
        public RecordSortDirection SortDirection { get; }

        /// <summary>
        /// 동률 행의 기존 순서를 유지하는 새 정렬 결과를 반환한다.
        /// </summary>
        public RecordTableModel SortBy(string columnId, RecordSortDirection direction)
        {
            if (direction == RecordSortDirection.None)
                throw new ArgumentException("정렬 방향을 지정해야 합니다.", nameof(direction));

            RecordTableColumnModel column = FindColumn(columnId);
            if (column == null)
                throw new ArgumentException($"존재하지 않는 기록표 열입니다: {columnId}", nameof(columnId));
            if (!column.IsSortable)
                throw new InvalidOperationException($"정렬할 수 없는 기록표 열입니다: {columnId}");

            var indexedRows = new IndexedRow[_rows.Length];
            for (int i = 0; i < _rows.Length; i++)
                indexedRows[i] = new IndexedRow(_rows[i], i);

            Array.Sort(indexedRows, (left, right) => CompareRows(left, right, column, direction));
            var sortedRows = new RecordTableRowModel[indexedRows.Length];
            for (int i = 0; i < indexedRows.Length; i++)
                sortedRows[i] = indexedRows[i].Row;

            return new RecordTableModel(_columns, sortedRows, columnId, direction);
        }

        private RecordTableColumnModel FindColumn(string columnId)
        {
            for (int i = 0; i < _columns.Length; i++)
            {
                if (string.Equals(_columns[i].ColumnId, columnId, StringComparison.Ordinal))
                    return _columns[i];
            }
            return null;
        }

        private static int CompareRows(
            IndexedRow left,
            IndexedRow right,
            RecordTableColumnModel column,
            RecordSortDirection direction)
        {
            RecordSortValue leftValue = left.Row.FindCell(column.ColumnId).SortValue;
            RecordSortValue rightValue = right.Row.FindCell(column.ColumnId).SortValue;
            int comparison = CompareValues(leftValue, rightValue);
            if (leftValue.Kind != RecordSortValueKind.Empty && rightValue.Kind != RecordSortValueKind.Empty &&
                direction == RecordSortDirection.Descending)
            {
                comparison = -comparison;
            }
            return comparison != 0 ? comparison : left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private static int CompareValues(RecordSortValue left, RecordSortValue right)
        {
            if (left.Kind == RecordSortValueKind.Empty)
                return right.Kind == RecordSortValueKind.Empty ? 0 : 1;
            if (right.Kind == RecordSortValueKind.Empty)
                return -1;
            if (left.Kind == RecordSortValueKind.Number)
                return left.Number.CompareTo(right.Number);

            int comparison = StringComparer.OrdinalIgnoreCase.Compare(left.Text, right.Text);
            return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left.Text, right.Text);
        }

        private static RecordTableColumnModel[] CopyColumns(IReadOnlyList<RecordTableColumnModel> columns)
        {
            if (columns == null || columns.Count == 0)
                throw new ArgumentException("기록표에는 하나 이상의 열이 필요합니다.", nameof(columns));

            var copy = new RecordTableColumnModel[columns.Count];
            var columnIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < columns.Count; i++)
            {
                RecordTableColumnModel column = columns[i] ??
                    throw new ArgumentException("기록표 열은 null일 수 없습니다.", nameof(columns));
                if (!columnIds.Add(column.ColumnId))
                    throw new ArgumentException($"중복 기록표 열 ID입니다: {column.ColumnId}", nameof(columns));
                copy[i] = column;
            }
            return copy;
        }

        private static RecordTableRowModel[] CopyRows(IReadOnlyList<RecordTableRowModel> rows)
        {
            if (rows == null || rows.Count == 0)
                return Array.Empty<RecordTableRowModel>();

            var copy = new RecordTableRowModel[rows.Count];
            var rowIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                RecordTableRowModel row = rows[i] ??
                    throw new ArgumentException("기록표 행은 null일 수 없습니다.", nameof(rows));
                if (!rowIds.Add(row.RowId))
                    throw new ArgumentException($"중복 기록표 행 ID입니다: {row.RowId}", nameof(rows));
                copy[i] = row;
            }
            return copy;
        }

        private void ValidateRows()
        {
            for (int rowIndex = 0; rowIndex < _rows.Length; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < _columns.Length; columnIndex++)
                {
                    RecordTableColumnModel column = _columns[columnIndex];
                    RecordTableCellModel cell = _rows[rowIndex].FindCell(column.ColumnId);
                    if (cell == null)
                        throw new ArgumentException($"{_rows[rowIndex].RowId} 행에 {column.ColumnId} 셀이 없습니다.");
                    if (cell.SortValue.Kind != RecordSortValueKind.Empty &&
                        cell.SortValue.Kind != column.SortValueKind)
                    {
                        throw new ArgumentException($"{column.ColumnId} 열의 정렬 값 종류가 일치하지 않습니다.");
                    }
                }
            }
        }

        private void ValidateSortState()
        {
            bool hasSortedColumn = !string.IsNullOrEmpty(SortedColumnId);
            bool hasSortDirection = SortDirection != RecordSortDirection.None;
            if (hasSortedColumn != hasSortDirection)
                throw new ArgumentException("정렬 열과 방향은 함께 지정해야 합니다.");
            if (hasSortedColumn && FindColumn(SortedColumnId) == null)
                throw new ArgumentException($"존재하지 않는 정렬 열입니다: {SortedColumnId}");
        }

        private readonly struct IndexedRow
        {
            public IndexedRow(RecordTableRowModel row, int originalIndex)
            {
                Row = row;
                OriginalIndex = originalIndex;
            }

            public RecordTableRowModel Row { get; }
            public int OriginalIndex { get; }
        }
    }
}
