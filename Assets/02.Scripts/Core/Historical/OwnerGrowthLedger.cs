using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Growth;

namespace Baseball.Core.Historical
{
    /// <summary>카드 성장 기여분의 저장 가능한 출처다.</summary>
    public enum OwnerGrowthSource { Training, OverseasTraining, Mentoring, Correction, Support, Slogan, Staff }

    /// <summary>한 명령의 능력치 변화와 수명을 보존한다. 만료돼도 이력에서 제거하지 않는다.</summary>
    public sealed class OwnerGrowthModifier
    {
        private readonly int[] _values;
        public OwnerGrowthModifier(string sourceId, OwnerGrowthSource source, string displayName,
            IReadOnlyList<int> values, int seasonNumber = 0, int remainingGames = 0, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("성장 출처와 표시 이름이 필요합니다.");
            if (!Enum.IsDefined(typeof(OwnerGrowthSource), source) || seasonNumber < 0 || remainingGames < 0)
                throw new ArgumentOutOfRangeException(nameof(source));
            if (values == null || values.Count != PlayerAbilityCatalog.AbilityCount)
                throw new ArgumentException("모든 능력치의 변화량이 필요합니다.", nameof(values));
            SourceId = sourceId; Source = source; DisplayName = displayName;
            SeasonNumber = seasonNumber; RemainingGames = remainingGames; IsActive = isActive;
            _values = new int[values.Count];
            for (int i = 0; i < values.Count; i++) _values[i] = values[i];
            if (source == OwnerGrowthSource.Support && isActive && remainingGames == 0)
                throw new ArgumentException("활성 서포트에는 남은 경기가 필요합니다.");
        }
        public string SourceId { get; }
        public OwnerGrowthSource Source { get; }
        public string DisplayName { get; }
        public int SeasonNumber { get; }
        public int RemainingGames { get; private set; }
        public bool IsActive { get; private set; }
        public int Get(PlayerAbility ability) => _values[(int)ability];
        public int[] CopyValues() => (int[])_values.Clone();
        internal void Expire() => IsActive = false;
        internal void SetApplicability(bool isActive, int remainingGames)
        {
            IsActive = isActive; RemainingGames = remainingGames;
        }
        internal void AdvanceMatch()
        {
            if (IsActive && RemainingGames > 0 && --RemainingGames == 0) IsActive = false;
        }
    }

    /// <summary>출처 중복을 차단하고 활성 기여분을 캐시하는 카드별 성장 원장이다.</summary>
    public sealed class OwnerGrowthLedger
    {
        private readonly List<OwnerGrowthModifier> _entries = new List<OwnerGrowthModifier>();
        private readonly HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);
        private readonly int[,] _totals = new int[Enum.GetValues(typeof(OwnerGrowthSource)).Length, PlayerAbilityCatalog.AbilityCount];
        public IReadOnlyList<OwnerGrowthModifier> Entries => _entries.AsReadOnly();
        public int Count => _entries.Count;
        /// <summary>같은 명령 출처는 만료 후에도 다시 등록하지 않는다.</summary>
        public void Add(OwnerGrowthModifier modifier)
        {
            if (modifier == null) throw new ArgumentNullException(nameof(modifier));
            if (_ids.Contains(modifier.SourceId)) throw new InvalidOperationException("이미 반영된 성장 결과입니다.");
            if (modifier.IsActive)
                for (int i = 0; i < PlayerAbilityCatalog.AbilityCount; i++)
                    _ = checked(_totals[(int)modifier.Source, i] + modifier.Get((PlayerAbility)i));
            _ids.Add(modifier.SourceId);
            _entries.Add(new OwnerGrowthModifier(modifier.SourceId, modifier.Source, modifier.DisplayName,
                modifier.CopyValues(), modifier.SeasonNumber, modifier.RemainingGames, modifier.IsActive));
            Rebuild();
        }
        public int Get(OwnerGrowthSource source, PlayerAbility ability) => _totals[(int)source, (int)ability];
        public bool Contains(string sourceId) => _ids.Contains(sourceId);
        /// <summary>팀 서포트는 편성 조건이 바뀌어도 원래 만료 시점을 연장하지 않는다.</summary>
        public void SetSupportApplicability(string sourceId, bool isApplicable, int remainingGames)
        {
            if (remainingGames < 0 || remainingGames > 2) throw new ArgumentOutOfRangeException(nameof(remainingGames));
            foreach (var entry in _entries)
                if (entry.SourceId == sourceId && entry.Source == OwnerGrowthSource.Support)
                    entry.SetApplicability(isApplicable && remainingGames > 0, remainingGames);
            Rebuild();
        }
        /// <summary>초기화 상품도 과거 성장 내역은 보존하고 활성 효과만 종료한다.</summary>
        public void Expire(OwnerGrowthSource source)
        {
            foreach (OwnerGrowthModifier entry in _entries) if (entry.Source == source) entry.Expire();
            Rebuild();
        }
        public void AdvanceMatch()
        {
            foreach (OwnerGrowthModifier entry in _entries) entry.AdvanceMatch();
            Rebuild();
        }
        private void Rebuild()
        {
            Array.Clear(_totals, 0, _totals.Length);
            foreach (OwnerGrowthModifier entry in _entries)
                if (entry.IsActive)
                    for (int i = 0; i < PlayerAbilityCatalog.AbilityCount; i++)
                        _totals[(int)entry.Source, i] = checked(_totals[(int)entry.Source, i] + entry.Get((PlayerAbility)i));
        }
    }
}
