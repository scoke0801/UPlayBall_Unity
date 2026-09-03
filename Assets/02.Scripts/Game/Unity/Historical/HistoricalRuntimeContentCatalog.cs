using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baseball.Game.Historical
{
    /// <summary>Manifest의 논리 경로와 Player Build에 포함할 TextAsset을 연결한다.</summary>
    [Serializable]
    public sealed class HistoricalRuntimeContentFile
    {
        [SerializeField] private string _relativePath;
        [SerializeField] private TextAsset _content;

        public HistoricalRuntimeContentFile(string relativePath, TextAsset content)
        {
            _relativePath = RequirePath(relativePath);
            _content = content != null ? content : throw new ArgumentNullException(nameof(content));
        }

        public string RelativePath => _relativePath ?? string.Empty;
        public TextAsset Content => _content;

        private static string RequirePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Runtime Content의 논리 경로가 필요합니다.", nameof(value));
            return value.Trim();
        }
    }

    /// <summary>한 연도의 논리 식별자와 Runtime TextAsset을 연결한다.</summary>
    [Serializable]
    public sealed class HistoricalRuntimeYearContentFile
    {
        [SerializeField] private int _year;
        [SerializeField] private HistoricalRuntimeContentFile _file;

        public HistoricalRuntimeYearContentFile(int year, HistoricalRuntimeContentFile file)
        {
            if (year <= 0)
                throw new ArgumentOutOfRangeException(nameof(year));
            _year = year;
            _file = file ?? throw new ArgumentNullException(nameof(file));
        }

        public int Year => _year;
        public HistoricalRuntimeContentFile File => _file;
    }

    /// <summary>Editor 원본명 Archive에서 분리된 Runtime-safe payload를 Player Build 의존성으로 묶는 Catalog다.</summary>
    public sealed class HistoricalRuntimeContentCatalog : ScriptableObject
    {
        [SerializeField] private TextAsset _manifest;
        [SerializeField] private HistoricalRuntimeContentFile _playerPersons;
        [SerializeField] private HistoricalRuntimeYearContentFile[] _years =
            Array.Empty<HistoricalRuntimeYearContentFile>();

        public TextAsset Manifest => _manifest;
        public HistoricalRuntimeContentFile PlayerPersons => _playerPersons;
        public IReadOnlyList<HistoricalRuntimeYearContentFile> Years =>
            _years ?? Array.Empty<HistoricalRuntimeYearContentFile>();

        /// <summary>Editor Exporter와 집중 테스트가 완성된 payload 참조를 원자적으로 교체한다.</summary>
        public void Configure(
            TextAsset manifest,
            HistoricalRuntimeContentFile playerPersons,
            IReadOnlyList<HistoricalRuntimeYearContentFile> years)
        {
            _manifest = manifest != null ? manifest : throw new ArgumentNullException(nameof(manifest));
            _playerPersons = playerPersons ?? throw new ArgumentNullException(nameof(playerPersons));
            if (years == null || years.Count == 0)
                throw new ArgumentException("하나 이상의 연도 Runtime Content가 필요합니다.", nameof(years));

            _years = new HistoricalRuntimeYearContentFile[years.Count];
            for (int index = 0; index < years.Count; index++)
                _years[index] = years[index] ?? throw new ArgumentException("null 연도 Content가 있습니다.", nameof(years));
        }
    }
}
