using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Guide
{
    /// <summary>ActiveRosterValidator의 확정 결과를 Owner Guide Fact로만 변환한다.</summary>
    public sealed class OwnerRosterGuideEventAdapter
    {
        /// <summary>로스터 규칙을 다시 판정하지 않고 Validator issue와 현재 집계값을 Fact payload로 옮긴다.</summary>
        public GuideFact[] CreateFacts(
            CurrentRosterState roster,
            RosterValidationResult validation,
            GuideFactIdentity identity,
            int rosterRevision,
            Func<string, string> playerNameByPersonId)
        {
            if (roster == null)
                throw new ArgumentNullException(nameof(roster));
            if (validation == null)
                throw new ArgumentNullException(nameof(validation));
            if (rosterRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(rosterRevision));

            var facts = new List<GuideFact>();
            if (validation.IsValid)
            {
                facts.Add(Create("RosterValidated", identity, rosterRevision).Build());
                return facts.ToArray();
            }

            CountRoles(roster, out int hitters, out int pitchers);
            bool addedRoleRatio = false;
            for (int index = 0; index < validation.Issues.Count; index++)
            {
                RosterValidationIssue issue = validation.Issues[index];
                switch (issue.Code)
                {
                    case RosterValidationIssueCode.TotalCount:
                        facts.Add(Create("RosterInvalidTotal", identity, rosterRevision)
                            .AddPayload("current", issue.Actual)
                            .AddPayload("required", issue.Expected)
                            .Build());
                        break;
                    case RosterValidationIssueCode.HitterCount:
                    case RosterValidationIssueCode.PitcherCount:
                        if (addedRoleRatio)
                            break;
                        facts.Add(Create("RosterRoleCountInvalid", identity, rosterRevision)
                            .AddPayload("hitters", hitters)
                            .AddPayload("pitchers", pitchers)
                            .Build());
                        addedRoleRatio = true;
                        break;
                    case RosterValidationIssueCode.ForeignPlayerCount:
                        facts.Add(Create("ForeignPlayerLimitExceeded", identity, rosterRevision)
                            .AddPayload("current", issue.Actual)
                            .AddPayload("limit", issue.Expected)
                            .Build());
                        break;
                    case RosterValidationIssueCode.DuplicatePlayerPersonId:
                        if (playerNameByPersonId == null)
                            throw new ArgumentNullException(nameof(playerNameByPersonId));
                        facts.Add(Create("DuplicatePlayerPerson", identity, rosterRevision)
                            .AddPayload("playerName", playerNameByPersonId(issue.Context))
                            .AddContext("playerPersonId", issue.Context)
                            .Build());
                        break;
                    case RosterValidationIssueCode.BenchHitterCount when issue.Actual < issue.Expected:
                        facts.Add(Create("BenchSlotShortage", identity, rosterRevision)
                            .AddPayload("current", issue.Actual)
                            .AddPayload("required", issue.Expected)
                            .Build());
                        break;
                    case RosterValidationIssueCode.FixedRoleCount when issue.Actual < issue.Expected:
                        if (Enum.TryParse(issue.Context, false, out ActiveRosterRole role) &&
                            ActiveRosterCompositionRule.Standard.IsPitcherRole(role))
                        {
                            facts.Add(Create("PitchingRoleSlotEmpty", identity, rosterRevision)
                                .AddPayload("missingCount", issue.Expected - issue.Actual)
                                .AddPayload("roleName", FormatRole(role))
                                .Build());
                        }
                        break;
                }
            }
            return facts.ToArray();
        }

        private static GuideFactBuilder Create(
            string factType,
            GuideFactIdentity identity,
            int rosterRevision) =>
            new GuideFactBuilder(GuideModeScope.Owner, factType, identity)
                .AddContext("rosterRevision", rosterRevision);

        private static void CountRoles(CurrentRosterState roster, out int hitters, out int pitchers)
        {
            hitters = 0;
            pitchers = 0;
            ActiveRosterCompositionRule rule = ActiveRosterCompositionRule.Standard;
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterRole role = roster.Entries[index].Role;
                if (rule.IsHitterRole(role))
                    hitters++;
                if (rule.IsPitcherRole(role))
                    pitchers++;
            }
        }

        private static string FormatRole(ActiveRosterRole role)
        {
            if (ActiveRosterCompositionRule.Standard.IsStartingPitcherRole(role))
                return "선발투수";
            if (ActiveRosterCompositionRule.Standard.IsBullpenRole(role))
                return "중간계투";
            return role switch
            {
                ActiveRosterRole.Setup => "셋업맨",
                ActiveRosterRole.Closer => "마무리투수",
                _ => role.ToString()
            };
        }
    }

    /// <summary>1군 구성 자체에서 Guide dedupe용 Revision을 만든다.</summary>
    public static class OwnerRosterRevision
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        /// <summary>
        /// Revision을 증가 순번이 아니라 내용 해시로 정의한다. Guide dedupe key가 "ROSTER_*:{rosterRevision}"이고
        /// 반복 정책이 Revision당 1회이므로, 로스터가 그대로인데 시설 업그레이드·저장 같은 다른 Runtime 변경으로
        /// 값이 바뀌면 같은 경고가 다시 뜬다. 반대로 세이브·로드를 거쳐도 구성이 같으면 같은 값이어야 한다.
        /// FNV-1a를 쓰는 이유는 GuideWeightedHash와 같이 플랫폼·프로세스에 무관한 값이 필요하기 때문이다.
        /// </summary>
        public static int Compute(CurrentRosterState roster)
        {
            if (roster == null)
                throw new ArgumentNullException(nameof(roster));

            ulong hash = Combine(OffsetBasis, roster.TeamSeasonKey);
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                hash = Combine(hash, entry.PlayerPersonId);
                hash = Combine(hash, entry.CardId);
                hash = Combine(hash, (byte)entry.Role);
                hash = Combine(hash, (byte)entry.RegistrationType);
            }
            // Adapter가 음수가 아닌 rosterRevision을 요구하므로 부호 비트만 떨어뜨린다.
            return (int)(hash & int.MaxValue);
        }

        private static ulong Combine(ulong hash, string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                hash = Combine(hash, (byte)character);
                hash = Combine(hash, (byte)(character >> 8));
            }
            // 항목 경계를 남겨 "ab"+"c"와 "a"+"bc"가 같은 값이 되지 않게 한다.
            return Combine(hash, (byte)0);
        }

        private static ulong Combine(ulong hash, byte value) => (hash ^ value) * Prime;
    }
}
