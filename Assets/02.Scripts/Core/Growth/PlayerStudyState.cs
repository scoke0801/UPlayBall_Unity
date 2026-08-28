using System;
using System.Collections.Generic;

namespace Baseball.Core.Growth
{
    public sealed class StudyProgramVisitState
    {
        public StudyProgramVisitState(string programId, int visitCount, int lastVisitedSeason, int consecutiveVisits)
        {
            ProgramId = programId ?? throw new ArgumentNullException(nameof(programId));
            VisitCount = visitCount;
            LastVisitedSeason = lastVisitedSeason;
            ConsecutiveVisits = consecutiveVisits;
        }

        public string ProgramId { get; }
        public int VisitCount { get; private set; }
        public int LastVisitedSeason { get; private set; }
        public int ConsecutiveVisits { get; private set; }

        public void RecordVisit(int seasonYear)
        {
            ConsecutiveVisits = LastVisitedSeason == seasonYear - 1 ? ConsecutiveVisits + 1 : 1;
            LastVisitedSeason = seasonYear;
            VisitCount++;
        }
    }

    /// <summary>
    /// 유학 사용 여부와 프로그램별 방문 이력만 저장하며 성장 용량 자원은 두지 않는다.
    /// </summary>
    public sealed class PlayerStudyState
    {
        private readonly List<StudyProgramVisitState> _visits = new List<StudyProgramVisitState>();
        private readonly List<string> _unlockedPrograms = new List<string>();
        private readonly List<string> _uniqueRewardHistory = new List<string>();

        public bool StudyUsedThisOffseason { get; private set; }
        public IReadOnlyList<StudyProgramVisitState> Visits => _visits;
        public IReadOnlyList<string> UnlockedPrograms => _unlockedPrograms;
        public IReadOnlyList<string> UniqueRewardHistory => _uniqueRewardHistory;

        public void BeginOffseason()
        {
            StudyUsedThisOffseason = false;
        }

        public void RecordVisit(string programId, int seasonYear)
        {
            if (StudyUsedThisOffseason)
                throw new InvalidOperationException("유학은 오프시즌당 한 번만 가능합니다.");
            StudyProgramVisitState visit = FindVisit(programId);
            if (visit == null)
            {
                visit = new StudyProgramVisitState(programId, 0, 0, 0);
                _visits.Add(visit);
            }
            visit.RecordVisit(seasonYear);
            StudyUsedThisOffseason = true;
        }

        public int GetConsecutiveVisits(string programId)
        {
            StudyProgramVisitState visit = FindVisit(programId);
            return visit?.ConsecutiveVisits ?? 0;
        }

        private StudyProgramVisitState FindVisit(string programId)
        {
            for (int index = 0; index < _visits.Count; index++)
            {
                if (string.Equals(_visits[index].ProgramId, programId, StringComparison.Ordinal))
                    return _visits[index];
            }
            return null;
        }
    }
}
