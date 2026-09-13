using System;

namespace Baseball.Presentation.Guide
{
    /// <summary>소식의 사실 구조와 분리된 한국어 저작 문구다.</summary>
    [Serializable]
    public sealed class OwnerManagerNewsCopy
    {
        public string trainingTitle, studyTitle, homeRunTitle, strikeoutTitle, weeklyTitle, decisionTitle, growthGroup;
        public string officialRecord, weeklyRecord, bullpenRecord, bullpenComparison, improved, worsened;
        public string keptRole, changedRole, pitchingObservation, battingObservation, observedOnly, noGrowth, growthTotal;
    }
}
