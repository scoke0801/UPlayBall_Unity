using System;
using System.Collections.Generic;

namespace Baseball.Simulation.Match
{
    public enum SubstitutionType
    {
        PinchHitter = 0,
        PinchRunner = 1,
        DefensiveReplacement = 2,
        PositionSwitch = 3,
        InjuryReplacement = 4,
        PitchingChange = 5
    }

    public readonly struct SubstitutionRecord
    {
        public SubstitutionRecord(
            int inning,
            InningHalf half,
            int enteringPlayerId,
            int leavingPlayerId,
            SubstitutionType type,
            DecisionReasonCode reason)
        {
            Inning = inning;
            Half = half;
            EnteringPlayerId = enteringPlayerId;
            LeavingPlayerId = leavingPlayerId;
            Type = type;
            Reason = reason;
        }

        public int Inning { get; }
        public InningHalf Half { get; }
        public int EnteringPlayerId { get; }
        public int LeavingPlayerId { get; }
        public SubstitutionType Type { get; }
        public DecisionReasonCode Reason { get; }
    }

    /// <summary>
    /// 퇴장 선수 재출전과 같은 불법 교체를 한 경기에서 일관되게 차단한다.
    /// </summary>
    public sealed class SubstitutionLedger
    {
        private readonly HashSet<int> _usedPlayers = new HashSet<int>();
        private readonly HashSet<int> _removedPlayers = new HashSet<int>();
        private readonly List<SubstitutionRecord> _records = new List<SubstitutionRecord>(12);

        public IReadOnlyList<SubstitutionRecord> Records => _records;
        public bool HasBeenUsed(int playerId) => _usedPlayers.Contains(playerId);
        public bool HasBeenRemoved(int playerId) => _removedPlayers.Contains(playerId);

        public void RegisterStarter(int playerId)
        {
            if (!_usedPlayers.Add(playerId))
                throw new InvalidOperationException("선발 선수가 중복 등록되었습니다.");
        }

        public void Record(in SubstitutionRecord record)
        {
            if (_usedPlayers.Contains(record.EnteringPlayerId) || _removedPlayers.Contains(record.EnteringPlayerId))
                throw new InvalidOperationException("이미 출전했거나 퇴장한 선수는 재출전할 수 없습니다.");
            if (!_usedPlayers.Contains(record.LeavingPlayerId))
                throw new InvalidOperationException("현재 출전 중이 아닌 선수를 교체할 수 없습니다.");
            _usedPlayers.Add(record.EnteringPlayerId);
            _removedPlayers.Add(record.LeavingPlayerId);
            _records.Add(record);
        }
    }
}
