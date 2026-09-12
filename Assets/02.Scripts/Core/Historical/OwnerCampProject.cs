using System;

namespace Baseball.Core.Historical
{
    /// <summary>전지훈련 등록 시 확정한 주간 경험치와 자동 귀환 정책이다.</summary>
    public sealed class OwnerCampProject
    {
        public OwnerCampProject(string cardId, string facilityId, int weeklyExperience, int requiredExperience, bool automaticReturn = true)
        {
            if (string.IsNullOrWhiteSpace(cardId) || string.IsNullOrWhiteSpace(facilityId) || weeklyExperience <= 0 || requiredExperience <= 0)
                throw new ArgumentException("전지훈련 일정이 올바르지 않습니다.");
            CardId = cardId; FacilityId = facilityId; WeeklyExperience = weeklyExperience;
            RequiredExperience = requiredExperience; AutomaticReturn = automaticReturn;
        }
        public string CardId { get; }
        public string FacilityId { get; }
        public int WeeklyExperience { get; }
        public int RequiredExperience { get; }
        public bool AutomaticReturn { get; }
    }
}
