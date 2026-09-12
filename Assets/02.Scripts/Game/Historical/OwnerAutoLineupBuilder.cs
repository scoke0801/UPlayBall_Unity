using System;
using System.Collections.Generic;
using System.Threading;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Historical
{
    /// <summary>보유 카드의 동일 인물·포지션·특수 카드 상한을 지키며 연도 구단 덱을 편성한다.</summary>
    public sealed class OwnerAutoLineupBuilder
    {
        private const int Size = ActiveRosterCompositionRule.ActiveRosterSize;
        private const long Infinity = long.MaxValue / 8;
        private readonly List<Candidate> _cards = new List<Candidate>();
        private readonly List<string> _persons = new List<string>();
        private readonly HashSet<string> _visited = new HashSet<string>(StringComparer.Ordinal);
        private readonly CancellationToken _cancellation;
        private Candidate[] _best;
        private long _bestScore = long.MinValue;

        private sealed class Candidate
        {
            public PlayerCardDefinition Card;
            public PlayerSeasonDefinition Season;
            public int Person;
            public bool IsTarget;
            public long Score;
        }

        /// <summary>메인 스레드에서 읽은 사용 가능한 카드 ID와 불변 카탈로그만 받아 작업 스레드에서 계산한다.</summary>
        public OwnerAutoLineupBuilder(WorldCardCatalog catalog, IReadOnlyList<string> availableCardIds,
            int year, string franchiseId, CancellationToken cancellation = default)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (availableCardIds == null) throw new ArgumentNullException(nameof(availableCardIds));
            if (year <= 0 || string.IsNullOrWhiteSpace(franchiseId))
                throw new ArgumentException("자동 배치할 연도와 구단을 선택하세요.");
            _cancellation = cancellation;
            var ids = new List<string>(availableCardIds);
            ids.Sort(StringComparer.Ordinal);
            var personIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            // 확률 계수가 아닌 사전식 우선순위다. 상위 항목 하나가 25명의 하위 점수 합보다 크다.
            const long costUnit = Size * 1100L + 1;
            const long targetUnit = Size * (10 * costUnit + 1100) + 1;
            foreach (string id in ids)
            {
                if (!catalog.TryGetCard(id, out var card)) continue;
                var season = catalog.GetPlayerSeason(card);
                if (!personIndices.TryGetValue(season.PlayerPersonId, out int person))
                {
                    person = _persons.Count;
                    personIndices.Add(season.PlayerPersonId, person);
                    _persons.Add(season.PlayerPersonId);
                }
                bool target = season.OriginYear == year && season.OriginFranchiseId == franchiseId;
                if (card.IsFranchiseWildcard && catalog.TeamColorLineages != null)
                    foreach (string franchise in catalog.TeamColorLineages.GetFranchises(card.TeamColorLineageId))
                        target |= franchise == franchiseId;
                int modifiers = 0;
                for (int i = 0; i < PlayerAbilityCatalog.AbilityCount; i++) modifiers += card.GetModifier((PlayerAbility)i);
                _cards.Add(new Candidate { Card = card, Season = season, Person = person, IsTarget = target,
                    Score = (target ? targetUnit : 0) + season.Cost * costUnit + Math.Max(0, Math.Min(49, modifiers)) });
            }
        }

        /// <summary>전체 덱 인원, 코스트, 적합 보직 순으로 비교하며 동일 입력은 동일 배치를 반환한다.</summary>
        public LineupPresetState Build(LineupPresetState source, out int targetCount)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (_persons.Count < Size) throw new InvalidOperationException("중복 선수를 제외한 사용 가능한 카드가 25명보다 적습니다.");
            var excluded = new HashSet<string>(StringComparer.Ordinal);
            // 특수 카드가 없는 실행 가능 해를 먼저 구하면 상한 위반 분기의 대부분을 일찍 제거할 수 있다.
            foreach (var card in _cards) if (card.Card.IsFranchiseWildcard) excluded.Add(card.Card.CardId);
            Search(excluded);
            SeedFeasibleSolution();
            Search(new HashSet<string>(StringComparer.Ordinal));
            if (_best == null) throw new InvalidOperationException("수비 8자리·백업 포수·선발 5명·구원 6명을 채울 수 없습니다. 부족한 포지션의 카드를 보강해 주세요.");
            targetCount = 0;
            var slots = new LineupPresetSlot[9];
            var batting = new string[9];
            for (int i = 0; i < Size; i++) if (_best[i].IsTarget) targetCount++;
            for (int i = 0; i < 9; i++)
            {
                slots[i] = new LineupPresetSlot(_best[i].Card.CardId, (PlayerPosition)(i + (int)PlayerPosition.Catcher));
                batting[i] = slots[i].CardId;
            }
            // 수비 위치와 타순은 독립적이다. 잔류 선수의 기존 타순을 유지하고 신규 선수는 코스트 순으로 넣는다.
            var order = new List<Candidate>();
            for (int i = 0; i < 9; i++) order.Add(_best[i]);
            order.Sort((a, b) =>
            {
                int first = IndexOf(source.BattingOrderCardIds, a.Card.CardId);
                int second = IndexOf(source.BattingOrderCardIds, b.Card.CardId);
                int comparison = first.CompareTo(second);
                if (comparison == 0) comparison = b.Season.Cost.CompareTo(a.Season.Cost);
                return comparison != 0 ? comparison : string.CompareOrdinal(a.Card.CardId, b.Card.CardId);
            });
            for (int i = 0; i < 9; i++) batting[i] = order[i].Card.CardId;
            return new LineupPresetState(source.PresetId, source.Name, slots, batting,
                Slice(9, 5), Slice(14, 5), Slice(19, 4), _best[23].Card.CardId, _best[24].Card.CardId,
                source.TeamColorIds, source.DefaultTacticCardIds);
        }

        private static int IndexOf(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++) if (ids[i] == id) return i;
            return ids.Count;
        }

        private string[] Slice(int start, int count)
        {
            var ids = new string[count];
            for (int i = 0; i < count; i++) ids[i] = _best[start + i].Card.CardId;
            return ids;
        }

        private void Search(HashSet<string> excluded)
        {
            _cancellation.ThrowIfCancellationRequested();
            var keys = new List<string>(excluded);
            keys.Sort(StringComparer.Ordinal);
            if (!_visited.Add(string.Join("\n", keys))) return;
            Candidate[] selected = Match(excluded, out long score);
            if (selected == null || score <= _bestScore) return;
            List<Candidate> conflict = FindConflict(selected, out int maximum);
            if (conflict == null) { _best = selected; _bestScore = score; return; }
            // 상한+1장 중 최소 한 장은 제외되어야 한다. 각 제외 분기를 풀어 탐욕 배치의 포지션 막힘을 피한다.
            conflict.Sort((a, b) => a.Score != b.Score ? a.Score.CompareTo(b.Score) : string.CompareOrdinal(a.Card.CardId, b.Card.CardId));
            for (int i = 0; i <= maximum; i++)
            {
                string id = conflict[i].Card.CardId;
                excluded.Add(id);
                Search(excluded);
                excluded.Remove(id);
            }
        }

        private void SeedFeasibleSolution()
        {
            var excluded = new HashSet<string>(StringComparer.Ordinal);
            Candidate[] selected = Match(excluded, out long score);
            while (selected != null && score > _bestScore)
            {
                _cancellation.ThrowIfCancellationRequested();
                var conflict = FindConflict(selected, out _);
                if (conflict == null) { _best = selected; _bestScore = score; return; }
                Candidate[] next = null;
                string excludedId = null;
                long nextScore = long.MinValue;
                foreach (var card in conflict)
                {
                    excluded.Add(card.Card.CardId);
                    var candidate = Match(excluded, out long candidateScore);
                    excluded.Remove(card.Card.CardId);
                    if (candidate == null || candidateScore <= nextScore) continue;
                    next = candidate; nextScore = candidateScore; excludedId = card.Card.CardId;
                }
                if (next == null) return;
                excluded.Add(excludedId); selected = next; score = nextScore;
            }
        }

        private static List<Candidate> FindConflict(Candidate[] selected, out int maximum)
        {
            var hitters = new List<Candidate>();
            var pitchers = new List<Candidate>();
            foreach (var card in selected)
            {
                if (!card.Card.IsFranchiseWildcard) continue;
                (card.Season.PlayerType == PlayerType.Batter ? hitters : pitchers).Add(card);
            }
            List<Candidate> conflict = null;
            maximum = 0;
            if (hitters.Count > OwnerSpecialCardRosterRule.MaxHitterCount)
            { conflict = hitters; maximum = OwnerSpecialCardRosterRule.MaxHitterCount; }
            else if (pitchers.Count > OwnerSpecialCardRosterRule.MaxPitcherCount)
            { conflict = pitchers; maximum = OwnerSpecialCardRosterRule.MaxPitcherCount; }
            else if (hitters.Count + pitchers.Count > OwnerSpecialCardRosterRule.MaxTotalCount)
            { conflict = hitters; conflict.AddRange(pitchers); maximum = OwnerSpecialCardRosterRule.MaxTotalCount; }
            return conflict;
        }

        private Candidate[] Match(HashSet<string> excluded, out long score)
        {
            int count = _persons.Count;
            var options = new Candidate[Size, count];
            var weights = new long[Size, count];
            foreach (var card in _cards)
            {
                if (excluded.Contains(card.Card.CardId)) continue;
                for (int slot = 0; slot < Size; slot++)
                {
                    int fit = GetFit(card.Season, slot);
                    if (fit < 0) continue;
                    // 같은 25인이라면 고 코스트 야수를 벤치보다 선발에 우선 배치한다.
                    long weight = card.Score + fit + (slot < 9 ? card.Season.Cost * 100 : 0);
                    if (options[slot, card.Person] != null && weights[slot, card.Person] >= weight) continue;
                    options[slot, card.Person] = card;
                    weights[slot, card.Person] = weight;
                }
            }
            // 직사각 Hungarian 배정: 열을 카드가 아닌 인물로 두어 다른 연도·등급 중복도 원천 차단한다.
            var u = new long[Size + 1]; var v = new long[count + 1];
            var p = new int[count + 1]; var way = new int[count + 1];
            for (int row = 1; row <= Size; row++)
            {
                _cancellation.ThrowIfCancellationRequested();
                p[0] = row;
                int column = 0;
                var min = new long[count + 1]; var used = new bool[count + 1];
                for (int j = 1; j <= count; j++) min[j] = Infinity;
                do
                {
                    used[column] = true;
                    int active = p[column], next = 0;
                    long delta = Infinity;
                    for (int j = 1; j <= count; j++)
                    {
                        if (used[j]) continue;
                        long cost = options[active - 1, j - 1] == null ? Infinity / 2 : -weights[active - 1, j - 1];
                        long current = cost - u[active] - v[j];
                        if (current < min[j]) { min[j] = current; way[j] = column; }
                        if (min[j] < delta) { delta = min[j]; next = j; }
                    }
                    for (int j = 0; j <= count; j++)
                        if (used[j]) { u[p[j]] += delta; v[j] -= delta; } else min[j] -= delta;
                    column = next;
                } while (p[column] != 0);
                do { int previous = way[column]; p[column] = p[previous]; column = previous; } while (column != 0);
            }
            var result = new Candidate[Size]; score = 0;
            for (int j = 1; j <= count; j++)
            {
                if (p[j] == 0) continue;
                int slot = p[j] - 1;
                result[slot] = options[slot, j - 1];
                if (result[slot] == null) return null;
                score += weights[slot, j - 1];
            }
            return result;
        }

        private static int GetFit(PlayerSeasonDefinition season, int slot)
        {
            if (slot >= 14)
            {
                if (season.PlayerType != PlayerType.Pitcher) return -1;
                if (slot < 19) return season.PitcherRole == PitcherRole.Starter ? 50 : -1;
                if (season.PitcherRole == PitcherRole.Starter) return -1;
                PitcherRole role = slot < 23 ? PitcherRole.MiddleRelief : slot == 23 ? PitcherRole.Setup : PitcherRole.Closer;
                return season.PitcherRole == role ? 50 : 0;
            }
            if (season.PlayerType != PlayerType.Batter) return -1;
            if (slot == 8 || slot >= 10) return 0;
            // 벤치 첫 자리는 백업 포수를 확보한다. DH와 나머지 벤치는 어느 야수든 맡을 수 있다.
            PlayerPosition position = slot == 9 ? PlayerPosition.Catcher : (PlayerPosition)(slot + (int)PlayerPosition.Catcher);
            if (season.Position == position) return 50;
            foreach (var secondary in season.SecondaryPositions)
                if (secondary.Position == position && secondary.Proficiency > 0) return 0;
            return -1;
        }
    }
}
