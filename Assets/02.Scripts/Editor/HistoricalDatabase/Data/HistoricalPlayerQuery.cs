using System;
using System.Collections.Generic;

namespace Baseball.Editor.HistoricalDatabase
{
    /// <summary>Players 탭의 검색·원본 분류·Cost·Award 조건을 한 행에 적용한다.</summary>
    public sealed class HistoricalPlayerFilter
    {
        private const string ReferenceSearchPrefix = "ref:";
        private const string AliasSearchPrefix = "alias:";

        private string _searchText = string.Empty;
        private string[] _searchTerms = Array.Empty<string>();

        public string SearchText
        {
            get => _searchText;
            set
            {
                string normalized = value?.Trim() ?? string.Empty;
                if (string.Equals(_searchText, normalized, StringComparison.Ordinal))
                    return;
                _searchText = normalized;
                _searchTerms = normalized.Length == 0
                    ? Array.Empty<string>()
                    : normalized.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }
        public int? Year { get; set; }
        public string FranchiseId { get; set; } = string.Empty;
        public string TeamSeasonKey { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string PitcherRole { get; set; } = string.Empty;
        public string PlayerType { get; set; } = string.Empty;
        public string RegistrationType { get; set; } = string.Empty;
        public int? Cost { get; set; }
        public int? MinimumCost { get; set; }
        public int? MaximumCost { get; set; }
        public int? AbilityIndex { get; set; }
        public int? MinimumAbility { get; set; }
        public int? MaximumAbility { get; set; }
        public bool? HasAnyAward { get; set; }
        public string AwardType { get; set; } = string.Empty;

        /// <summary>현재 조건을 원본 PlayerSeason Join 행 하나에 적용한다.</summary>
        public bool Matches(HistoricalPlayerRow row)
        {
            if (row == null)
                return false;
            if (!MatchesSearch(row, _searchTerms))
                return false;
            if (Year.HasValue && row.OriginYear != Year.Value)
                return false;
            if (!MatchesExact(row.OriginFranchiseId, FranchiseId))
                return false;
            if (!MatchesExact(row.OriginTeamSeasonKey, TeamSeasonKey))
                return false;
            if (!MatchesExact(row.Position, Position))
                return false;
            if (!string.IsNullOrWhiteSpace(PitcherRole)
                && (!row.IsPitcher || !MatchesExact(row.PitcherRole, PitcherRole)))
            {
                return false;
            }
            if (!MatchesExact(row.PlayerType, PlayerType))
                return false;
            if (!MatchesExact(row.RegistrationType, RegistrationType))
                return false;
            if (Cost.HasValue && row.Cost != Cost.Value)
                return false;
            if (MinimumCost.HasValue && row.Cost < MinimumCost.Value)
                return false;
            if (MaximumCost.HasValue && row.Cost > MaximumCost.Value)
                return false;
            if (AbilityIndex.HasValue)
            {
                int abilityIndex = AbilityIndex.Value;
                bool isApplicable = abilityIndex < 6 ? row.IsHitter : row.IsPitcher;
                if (!isApplicable)
                    return false;
                int value = row.GetBaseAbility(abilityIndex);
                if (MinimumAbility.HasValue && value < MinimumAbility.Value)
                    return false;
                if (MaximumAbility.HasValue && value > MaximumAbility.Value)
                    return false;
            }
            if (HasAnyAward.HasValue && (row.AwardCount > 0) != HasAnyAward.Value)
                return false;
            if (!string.IsNullOrWhiteSpace(AwardType) && !row.HasAward(AwardType))
                return false;
            return true;
        }

        /// <summary>모든 검색 조건을 초기 상태로 되돌린다.</summary>
        public void Reset()
        {
            SearchText = string.Empty;
            Year = null;
            FranchiseId = string.Empty;
            TeamSeasonKey = string.Empty;
            Position = string.Empty;
            PitcherRole = string.Empty;
            PlayerType = string.Empty;
            RegistrationType = string.Empty;
            Cost = null;
            MinimumCost = null;
            MaximumCost = null;
            AbilityIndex = null;
            MinimumAbility = null;
            MaximumAbility = null;
            HasAnyAward = null;
            AwardType = string.Empty;
        }

        private static bool MatchesSearch(HistoricalPlayerRow row, IReadOnlyList<string> terms)
        {
            if (terms.Count == 0)
                return true;

            for (int termIndex = 0; termIndex < terms.Count; termIndex++)
            {
                string term = terms[termIndex];
                if (!MatchesSearchTerm(row, term))
                    return false;
            }
            return true;
        }

        private static bool MatchesSearchTerm(HistoricalPlayerRow row, string term)
        {
            if (TryGetScopedSearchValue(term, ReferenceSearchPrefix, out string referenceName))
                return referenceName.Length > 0 && ContainsAny(row.SourceReferenceNames, referenceName);

            if (TryGetScopedSearchValue(term, AliasSearchPrefix, out string runtimeAlias))
                return runtimeAlias.Length > 0 && Contains(row.RuntimeName, runtimeAlias);

            // 기본 검색 결과는 화면에 보이는 값만 대상으로 삼는다. 숨은 Reference나 Runtime 가명이
            // 매칭되면 검색어와 무관한 이름이 결과에 나타나므로 명시적 Scope에서만 찾는다.
            return Contains(row.Name, term)
                || Contains(row.PlayerPersonId, term)
                || Contains(row.PlayerSeasonId, term)
                || Contains(row.OriginFranchiseId, term)
                || Contains(row.OriginTeamSeasonKey, term)
                || Contains(row.Position, term)
                || (row.IsPitcher && Contains(row.PitcherRole, term))
                || Contains(row.RosterRole, term);
        }

        private static bool TryGetScopedSearchValue(string term, string prefix, out string value)
        {
            if (term.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = term.Substring(prefix.Length);
                return true;
            }

            value = string.Empty;
            return false;
        }

        private static bool MatchesExact(string value, string condition)
        {
            return string.IsNullOrWhiteSpace(condition)
                || string.Equals(value, condition, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(string value, string term)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAny(IReadOnlyList<string> values, string term)
        {
            for (int index = 0; index < values.Count; index++)
                if (Contains(values[index], term)) return true;
            return false;
        }
    }

    /// <summary>Player Browser가 지원하는 원본·능력치·단순 파생값 정렬 열이다.</summary>
    public enum HistoricalPlayerSortField
    {
        Name,
        Year,
        Franchise,
        TeamSeason,
        Position,
        PitcherRole,
        PlayerType,
        RegistrationType,
        Cost,
        Contact,
        Power,
        Speed,
        Arm,
        Defense,
        BatterMental,
        Stamina,
        Velocity,
        Stuff,
        Breaking,
        Control,
        PitcherMental,
        PlateAppearances,
        Hits,
        HomeRuns,
        Walks,
        Strikeouts,
        HitsPerPlateAppearance,
        BattingAverage,
        OnBasePercentage,
        SluggingPercentage,
        OnBasePlusSlugging,
        PitchingOuts,
        EarnedRuns,
        PitchingStrikeouts,
        EarnedRunAverage,
        WalksAndHitsPerInningPitched,
        StrikeoutsPerNine,
        AwardCount,
        ReferenceSimilarityDistance
    }

    /// <summary>정렬 열의 오름차순과 내림차순을 구분한다.</summary>
    public enum HistoricalSortDirection
    {
        Ascending,
        Descending
    }

    /// <summary>다중 열 정렬에서 한 열의 우선순위와 방향을 보관한다.</summary>
    public readonly struct HistoricalPlayerSortDescriptor
    {
        public HistoricalPlayerSortDescriptor(HistoricalPlayerSortField field, HistoricalSortDirection direction)
        {
            Field = field;
            Direction = direction;
        }

        public HistoricalPlayerSortField Field { get; }
        public HistoricalSortDirection Direction { get; }
    }

    /// <summary>Player 행을 안정 ID Tie-break와 함께 단일 또는 다중 열로 정렬한다.</summary>
    public static class HistoricalPlayerSorter
    {
        /// <summary>지정한 열 하나로 새 List를 정렬해 반환한다.</summary>
        public static List<HistoricalPlayerRow> Sort(
            IEnumerable<HistoricalPlayerRow> source,
            HistoricalPlayerSortField field,
            HistoricalSortDirection direction)
        {
            return Sort(source, new[] { new HistoricalPlayerSortDescriptor(field, direction) });
        }

        /// <summary>앞쪽 Descriptor를 우선하는 다중 열 정렬 결과를 새 List로 반환한다.</summary>
        public static List<HistoricalPlayerRow> Sort(
            IEnumerable<HistoricalPlayerRow> source,
            IReadOnlyList<HistoricalPlayerSortDescriptor> descriptors)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var result = new List<HistoricalPlayerRow>(source);
            result.Sort((left, right) => Compare(left, right, descriptors));
            return result;
        }

        private static int Compare(
            HistoricalPlayerRow left,
            HistoricalPlayerRow right,
            IReadOnlyList<HistoricalPlayerSortDescriptor> descriptors)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            if (descriptors != null)
            {
                for (int index = 0; index < descriptors.Count; index++)
                {
                    HistoricalPlayerSortDescriptor descriptor = descriptors[index];
                    int comparison = CompareField(left, right, descriptor.Field);
                    if (comparison == 0)
                        continue;
                    bool leftMissing = HasMissingSortValue(left, descriptor.Field);
                    bool rightMissing = HasMissingSortValue(right, descriptor.Field);
                    if (leftMissing != rightMissing)
                        return leftMissing ? 1 : -1;
                    return descriptor.Direction == HistoricalSortDirection.Descending
                        ? -comparison
                        : comparison;
                }
            }

            return string.CompareOrdinal(left.PlayerSeasonId, right.PlayerSeasonId);
        }

        private static int CompareField(
            HistoricalPlayerRow left,
            HistoricalPlayerRow right,
            HistoricalPlayerSortField field)
        {
            return field switch
            {
                HistoricalPlayerSortField.Name => CompareText(left.Name, right.Name),
                HistoricalPlayerSortField.Year => left.OriginYear.CompareTo(right.OriginYear),
                HistoricalPlayerSortField.Franchise => CompareText(left.OriginFranchiseId, right.OriginFranchiseId),
                HistoricalPlayerSortField.TeamSeason => CompareText(left.OriginTeamSeasonKey, right.OriginTeamSeasonKey),
                HistoricalPlayerSortField.Position => CompareText(left.Position, right.Position),
                HistoricalPlayerSortField.PitcherRole => CompareText(left.PitcherRole, right.PitcherRole),
                HistoricalPlayerSortField.PlayerType => CompareText(left.PlayerType, right.PlayerType),
                HistoricalPlayerSortField.RegistrationType => CompareText(left.RegistrationType, right.RegistrationType),
                HistoricalPlayerSortField.Cost => left.Cost.CompareTo(right.Cost),
                HistoricalPlayerSortField.Contact => CompareAbility(left, right, 0),
                HistoricalPlayerSortField.Power => CompareAbility(left, right, 1),
                HistoricalPlayerSortField.Speed => CompareAbility(left, right, 2),
                HistoricalPlayerSortField.Arm => CompareAbility(left, right, 3),
                HistoricalPlayerSortField.Defense => CompareAbility(left, right, 4),
                HistoricalPlayerSortField.BatterMental => CompareAbility(left, right, 5),
                HistoricalPlayerSortField.Stamina => CompareAbility(left, right, 6),
                HistoricalPlayerSortField.Velocity => CompareAbility(left, right, 7),
                HistoricalPlayerSortField.Stuff => CompareAbility(left, right, 8),
                HistoricalPlayerSortField.Breaking => CompareAbility(left, right, 9),
                HistoricalPlayerSortField.Control => CompareAbility(left, right, 10),
                HistoricalPlayerSortField.PitcherMental => CompareAbility(left, right, 11),
                HistoricalPlayerSortField.PlateAppearances => CompareRecord(left, right, record => record.PlateAppearances),
                HistoricalPlayerSortField.Hits => CompareRecord(left, right, record => record.Hits),
                HistoricalPlayerSortField.HomeRuns => CompareRecord(left, right, record => record.HomeRuns),
                HistoricalPlayerSortField.Walks => CompareRecord(left, right, record => record.Walks),
                HistoricalPlayerSortField.Strikeouts => CompareRecord(left, right, record => record.Strikeouts),
                HistoricalPlayerSortField.HitsPerPlateAppearance => CompareNullable(left.HitsPerPlateAppearance, right.HitsPerPlateAppearance),
                HistoricalPlayerSortField.BattingAverage => CompareNullable(left.BattingAverage, right.BattingAverage),
                HistoricalPlayerSortField.OnBasePercentage => CompareNullable(left.OnBasePercentage, right.OnBasePercentage),
                HistoricalPlayerSortField.SluggingPercentage => CompareNullable(left.SluggingPercentage, right.SluggingPercentage),
                HistoricalPlayerSortField.OnBasePlusSlugging => CompareNullable(left.OnBasePlusSlugging, right.OnBasePlusSlugging),
                HistoricalPlayerSortField.PitchingOuts => CompareRecord(left, right, record => record.PitchingOuts),
                HistoricalPlayerSortField.EarnedRuns => CompareRecord(left, right, record => record.EarnedRuns),
                HistoricalPlayerSortField.PitchingStrikeouts => CompareRecord(left, right, record => record.PitchingStrikeouts),
                HistoricalPlayerSortField.EarnedRunAverage => CompareNullable(left.EarnedRunAverage, right.EarnedRunAverage),
                HistoricalPlayerSortField.WalksAndHitsPerInningPitched => CompareNullable(left.WalksAndHitsPerInningPitched, right.WalksAndHitsPerInningPitched),
                HistoricalPlayerSortField.StrikeoutsPerNine => CompareNullable(left.StrikeoutsPerNine, right.StrikeoutsPerNine),
                HistoricalPlayerSortField.AwardCount => left.AwardCount.CompareTo(right.AwardCount),
                HistoricalPlayerSortField.ReferenceSimilarityDistance =>
                    left.Season.ReferenceSimilarityDistance.CompareTo(right.Season.ReferenceSimilarityDistance),
                _ => 0
            };
        }

        private static bool HasMissingSortValue(HistoricalPlayerRow row, HistoricalPlayerSortField field)
        {
            switch (field)
            {
                case HistoricalPlayerSortField.Contact:
                case HistoricalPlayerSortField.Power:
                case HistoricalPlayerSortField.Speed:
                case HistoricalPlayerSortField.Arm:
                case HistoricalPlayerSortField.Defense:
                case HistoricalPlayerSortField.BatterMental:
                    return !row.IsHitter;
                case HistoricalPlayerSortField.Stamina:
                case HistoricalPlayerSortField.Velocity:
                case HistoricalPlayerSortField.Stuff:
                case HistoricalPlayerSortField.Breaking:
                case HistoricalPlayerSortField.Control:
                case HistoricalPlayerSortField.PitcherMental:
                    return !row.IsPitcher;
                case HistoricalPlayerSortField.PlateAppearances:
                case HistoricalPlayerSortField.Hits:
                case HistoricalPlayerSortField.HomeRuns:
                case HistoricalPlayerSortField.Walks:
                case HistoricalPlayerSortField.Strikeouts:
                    return !row.IsHitter || row.Record == null;
                case HistoricalPlayerSortField.PitchingOuts:
                case HistoricalPlayerSortField.EarnedRuns:
                case HistoricalPlayerSortField.PitchingStrikeouts:
                    return !row.IsPitcher || row.Record == null;
                case HistoricalPlayerSortField.HitsPerPlateAppearance:
                    return !row.HitsPerPlateAppearance.HasValue;
                case HistoricalPlayerSortField.BattingAverage:
                    return !row.BattingAverage.HasValue;
                case HistoricalPlayerSortField.OnBasePercentage:
                    return !row.OnBasePercentage.HasValue;
                case HistoricalPlayerSortField.SluggingPercentage:
                    return !row.SluggingPercentage.HasValue;
                case HistoricalPlayerSortField.OnBasePlusSlugging:
                    return !row.OnBasePlusSlugging.HasValue;
                case HistoricalPlayerSortField.EarnedRunAverage:
                    return !row.EarnedRunAverage.HasValue;
                case HistoricalPlayerSortField.WalksAndHitsPerInningPitched:
                    return !row.WalksAndHitsPerInningPitched.HasValue;
                case HistoricalPlayerSortField.StrikeoutsPerNine:
                    return !row.StrikeoutsPerNine.HasValue;
                default:
                    return false;
            }
        }

        private static int CompareText(string left, string right)
        {
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareAbility(HistoricalPlayerRow left, HistoricalPlayerRow right, int abilityIndex)
        {
            return left.GetBaseAbility(abilityIndex).CompareTo(right.GetBaseAbility(abilityIndex));
        }

        private static int CompareRecord(
            HistoricalPlayerRow left,
            HistoricalPlayerRow right,
            Func<HistoricalSeasonRecord, int> selector)
        {
            int? leftValue = left.Record == null ? null : selector(left.Record);
            int? rightValue = right.Record == null ? null : selector(right.Record);
            return CompareNullable(leftValue, rightValue);
        }

        private static int CompareNullable<T>(T? left, T? right) where T : struct, IComparable<T>
        {
            if (!left.HasValue) return right.HasValue ? 1 : 0;
            if (!right.HasValue) return -1;
            return left.Value.CompareTo(right.Value);
        }
    }
}
