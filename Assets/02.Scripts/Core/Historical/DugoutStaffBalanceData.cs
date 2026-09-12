using System;
using Baseball.Core.Teams;

namespace Baseball.Core.Historical
{
    /// <summary>덕아웃 JSON 저작값을 Unity 참조 없는 인선 카탈로그로 변환한다.</summary>
    [Serializable]
    public sealed class DugoutStaffBalanceData
    {
        public ManagerEntry[] Managers;
        public CoachEntry[] Coaches;

        [Serializable]
        public sealed class ManagerEntry
        {
            public string Id;
            public int[] Profile;
            public string Effect;
        }

        [Serializable]
        public sealed class CoachEntry
        {
            public string Id;
            public int Matchup;
            public int Defense;
            public int Role;
            public int Trust;
            public string Effect;
        }

        /// <summary>전체 인선의 누락·중복과 범위를 검증한 뒤 저작된 효과를 적용한다.</summary>
        public DugoutStaffCatalog BuildCatalog()
        {
            var source = DugoutStaffCatalog.CreateDefault();
            if (Managers == null || Managers.Length != source.Managers.Count ||
                Coaches == null || Coaches.Length != source.HeadCoaches.Count)
                throw new ArgumentException("덕아웃 인선별 효과 정의가 모두 필요합니다.");
            var managers = new ManagerDefinition[Managers.Length];
            for (int i = 0; i < managers.Length; i++)
            {
                // 저작 파일 행 순서가 AI 구단 인선 배정을 바꾸지 않도록 카탈로그 순서를 유지한다.
                var entry = Array.Find(Managers, item => item != null && item.Id == source.Managers[i].ManagerId)
                    ?? throw new ArgumentException("감독 효과 정의가 누락됐거나 중복됐습니다.");
                var old = source.GetManager(entry.Id);
                var p = entry.Profile;
                if (p == null || p.Length != 10) throw new ArgumentException("감독 성향은 10개 축이 필요합니다.");
                foreach (int value in p)
                    if (value < 0 || value > 100) throw new ArgumentOutOfRangeException(nameof(entry.Profile));
                managers[i] = new ManagerDefinition(old.ManagerId, old.DisplayName, old.StyleName,
                    old.Description, entry.Effect, new ManagerTacticalProfile(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9]));
            }
            var coaches = new HeadCoachDefinition[Coaches.Length];
            for (int i = 0; i < coaches.Length; i++)
            {
                var entry = Array.Find(Coaches, item => item != null && item.Id == source.HeadCoaches[i].HeadCoachId)
                    ?? throw new ArgumentException("수석코치 효과 정의가 누락됐거나 중복됐습니다.");
                var old = source.GetHeadCoach(entry.Id);
                coaches[i] = new HeadCoachDefinition(old.HeadCoachId, old.DisplayName, old.SpecialtyName,
                    entry.Effect, old.PrimaryAxis, old.PrimaryModifier, old.SecondaryAxis, old.SecondaryModifier,
                    old.HasConditionSupport, entry.Matchup, entry.Defense, entry.Role, entry.Trust);
            }
            return new DugoutStaffCatalog(managers, coaches);
        }
    }
}
