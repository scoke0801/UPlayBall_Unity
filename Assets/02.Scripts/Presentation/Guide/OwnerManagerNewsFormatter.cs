using System;
using System.Globalization;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Game.Guide;

namespace Baseball.Presentation.Guide
{
    /// <summary>확정된 소식 근거만 한국어로 변환한다. 성적 변화의 원인을 추정하지 않는다.</summary>
    public static class OwnerManagerNewsFormatter
    {
        public static string FormatTitle(ManagerReportData report, string name, OwnerManagerNewsCopy copy)
        {
            var evidence = report.news;
            return evidence.kind switch
            {
                ManagerNewsKind.Training => string.Format(copy.trainingTitle, name),
                ManagerNewsKind.Study => string.Format(copy.studyTitle, name),
                ManagerNewsKind.HomeRunMilestone => string.Format(copy.homeRunTitle, name, evidence.milestone),
                ManagerNewsKind.StrikeoutMilestone => string.Format(copy.strikeoutTitle, name, evidence.milestone),
                ManagerNewsKind.WeeklyReview => string.Format(copy.weeklyTitle, report.createdWeek + 1, evidence.wins, evidence.losses, evidence.draws),
                _ => string.Format(copy.decisionTitle, name)
            };
        }

        public static string FormatBody(ManagerNewsEvidence evidence, OwnerManagerNewsCopy copy)
        {
            if (evidence.kind == ManagerNewsKind.Training || evidence.kind == ManagerNewsKind.Study)
            {
                var text = new StringBuilder(string.IsNullOrEmpty(evidence.label) ? copy.growthTotal : evidence.label);
                bool hasGrowth = false;
                for (int i = 0; i < evidence.growth.Length; i++)
                {
                    if (evidence.growth[i] <= 0) continue;
                    hasGrowth = true;
                    text.Append('\n').Append(PlayerAbilityCatalog.GetDisplayName((PlayerAbility)i)).Append(" +").Append(evidence.growth[i]);
                }
                if (!hasGrowth) text.Append('\n').Append(copy.noGrowth);
                return text.ToString();
            }
            if (evidence.kind == ManagerNewsKind.HomeRunMilestone || evidence.kind == ManagerNewsKind.StrikeoutMilestone)
                return copy.officialRecord;
            if (evidence.kind == ManagerNewsKind.WeeklyReview)
            {
                string text = string.Format(copy.weeklyRecord, evidence.games, evidence.runsFor, evidence.runsAgainst) + "\n" +
                    string.Format(copy.bullpenRecord, Innings(evidence.bullpenOuts), evidence.bullpenWalks);
                if (evidence.hasComparison && evidence.bullpenOuts > 0 && evidence.previousOuts > 0)
                {
                    double current = evidence.bullpenWalks * 27d / evidence.bullpenOuts;
                    double previous = evidence.previousWalks * 27d / evidence.previousOuts;
                    text += "\n" + string.Format(copy.bullpenComparison, previous.ToString("F1", CultureInfo.InvariantCulture),
                        current.ToString("F1", CultureInfo.InvariantCulture), current < previous ? copy.improved : copy.worsened);
                }
                return text;
            }
            string observation = evidence.appearances > 0
                ? string.Format(copy.pitchingObservation, evidence.appearances, Innings(evidence.outs), evidence.earnedRuns, evidence.walks, evidence.strikeouts)
                : string.Format(copy.battingObservation, evidence.plateAppearances, evidence.hits);
            return (evidence.roleChanged ? copy.changedRole : copy.keptRole) + "\n" + observation + "\n" + copy.observedOnly;
        }

        private static string Innings(int outs) => (outs / 3).ToString(CultureInfo.InvariantCulture) + "." + outs % 3;
    }
}
