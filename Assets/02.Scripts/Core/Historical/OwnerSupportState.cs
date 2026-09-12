using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Players;

namespace Baseball.Core.Historical
{
    /// <summary>서포트의 슬롯 종류와 선수 유형 필터다.</summary>
    public enum OwnerSupportScope { Team, Player }
    public enum OwnerSupportTarget { All, Batter, Pitcher }

    /// <summary>JSON에서 주입하는 서포트 카드의 비용과 효과다.</summary>
    [Serializable]
    public sealed class OwnerSupportDefinition
    {
        public string id;
        public string displayName;
        public string description;
        public OwnerSupportScope scope;
        public OwnerSupportTarget target;
        public long price;
        public LeagueGrade unlockGrade;
        public int maximumAge;
        public int conditionPoints;
        public int[] bonuses;
        public OwnerSupportDefinition Copy() => new OwnerSupportDefinition { id = id, displayName = displayName,
            description = description, unlockGrade = unlockGrade, scope = scope, target = target, price = price, maximumAge = maximumAge,
            conditionPoints = conditionPoints, bonuses = (int[])bonuses.Clone() };
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName) || price <= 0 || maximumAge < 0
                || !Enum.IsDefined(typeof(LeagueGrade), unlockGrade)
                || !Enum.IsDefined(typeof(OwnerSupportScope), scope) || !Enum.IsDefined(typeof(OwnerSupportTarget), target)
                || conditionPoints < 0 || conditionPoints > 100 || bonuses == null || bonuses.Length != PlayerAbilityCatalog.AbilityCount)
                throw new ArgumentException("서포트 카드 정의가 올바르지 않습니다.");
            bool hasEffect = conditionPoints > 0;
            foreach (int value in bonuses) { if (value < 0) throw new ArgumentException("서포트 증가량이 음수입니다."); hasEffect |= value > 0; }
            if (!hasEffect) throw new ArgumentException("서포트 효과가 없습니다.");
        }
    }

    /// <summary>이미 소비한 서포트의 슬롯과 남은 경기 수를 기록한다.</summary>
    public sealed class OwnerSupportAssignment
    {
        public OwnerSupportAssignment(string sourceId, string definitionId, string cardId, int remainingGames = 2,
            OwnerSupportDefinition definition = null)
        {
            if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(definitionId)
                || remainingGames < 1 || remainingGames > 2) throw new ArgumentException("서포트 장착 상태가 올바르지 않습니다.");
            SourceId = sourceId; DefinitionId = definitionId; CardId = cardId ?? ""; RemainingGames = remainingGames;
            definition?.Validate();
            if (definition != null && (definition.id != definitionId || (definition.scope == OwnerSupportScope.Team) != (CardId.Length == 0)))
                throw new ArgumentException("서포트 정의와 적용 슬롯이 다릅니다.");
            Definition = definition?.Copy();
        }
        public string SourceId { get; }
        public OwnerSupportDefinition Definition { get; }
        public string DefinitionId { get; }
        public string CardId { get; }
        public int RemainingGames { get; private set; }
        public bool IsTeam => CardId.Length == 0;
        public bool AdvanceMatch() => --RemainingGames == 0;
    }

    /// <summary>팀 한 슬롯·개인 세 슬롯과 서포트 재고를 저장한다.</summary>
    public sealed class OwnerSupportState
    {
        private readonly SortedDictionary<string, int> _inventory = new SortedDictionary<string, int>(StringComparer.Ordinal);
        private readonly List<OwnerSupportAssignment> _assignments = new List<OwnerSupportAssignment>();
        public IReadOnlyDictionary<string, int> Inventory => _inventory;
        public IReadOnlyList<OwnerSupportAssignment> Assignments => _assignments;
        public int NextSequence { get; private set; }
        public OwnerSupportState(int nextSequence = 0)
        {
            if (nextSequence < 0) throw new ArgumentOutOfRangeException(nameof(nextSequence));
            NextSequence = nextSequence;
        }
        public int GetCount(string definitionId) => _inventory.TryGetValue(definitionId, out int count) ? count : 0;
        public void Add(string definitionId, int count = 1)
        {
            if (string.IsNullOrWhiteSpace(definitionId) || count < 1) throw new ArgumentException("서포트 지급 값이 올바르지 않습니다.");
            _inventory[definitionId] = checked(GetCount(definitionId) + count);
        }
        public void RestoreAssignment(OwnerSupportAssignment assignment)
        {
            ValidateSlot(assignment.CardId);
            foreach (var existing in _assignments)
                if (existing.SourceId == assignment.SourceId) throw new ArgumentException("서포트 출처가 중복되었습니다.");
            _assignments.Add(assignment);
        }
        public void ValidateSlot(string cardId)
        {
            bool isTeam = string.IsNullOrEmpty(cardId); int players = 0;
            foreach (var assignment in _assignments)
            {
                if (assignment.IsTeam && isTeam) throw new InvalidOperationException("팀 서포트 슬롯이 사용 중입니다.");
                if (!assignment.IsTeam) players++;
                if (!isTeam && assignment.CardId == cardId) throw new InvalidOperationException("이 선수는 이미 개인 서포트를 사용 중입니다.");
            }
            if (!isTeam && players >= 3) throw new InvalidOperationException("개인 서포트 세 슬롯이 모두 사용 중입니다.");
        }
        public OwnerSupportAssignment Equip(OwnerSupportDefinition definition, string cardId)
        {
            string definitionId = definition.id;
            ValidateSlot(cardId);
            if (GetCount(definitionId) < 1) throw new InvalidOperationException("보유한 서포트 카드가 없습니다.");
            int sequence = checked(NextSequence + 1);
            var assignment = new OwnerSupportAssignment("support_" + NextSequence, definitionId, cardId, definition: definition);
            _inventory[definitionId]--; NextSequence = sequence; _assignments.Add(assignment);
            return assignment;
        }
        public void RemoveAt(int index) => _assignments.RemoveAt(index);
    }
}
