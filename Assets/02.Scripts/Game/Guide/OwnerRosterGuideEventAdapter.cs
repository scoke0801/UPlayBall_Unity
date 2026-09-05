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
}
