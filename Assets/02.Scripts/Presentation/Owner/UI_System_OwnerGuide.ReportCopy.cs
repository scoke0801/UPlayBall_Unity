using System;
using Baseball.Core.Teams;
using Baseball.Game.Guide;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_System_OwnerGuide
    {
        private string FormatCaseTitle(ManagerReportCase item)
        {
            if (item.Primary.news != null)
                return item.Primary.news.kind == ManagerNewsKind.Training || item.Primary.news.kind == ManagerNewsKind.Study
                    ? string.Format(_copy.newsText.growthGroup, item.Members.Count) : FormatNewsTitle(item.Primary);
            if (item.Primary.evidence == "PitcherRoleMismatch")
                return string.Format(_copy.pitcherCaseTitle, item.Members.Count);
            string title = FormatReportTitle(item.Primary.ToGoal());
            return item.Members.Count > 1 ? string.Format(_copy.groupedTitle, title, item.Members.Count) : title;
        }

        private string FormatCaseSummary(ManagerReportCase item)
        {
            if (item.Primary.news != null)
            {
                string date = string.Format(_copy.reportDate, item.Primary.createdSeason, item.Primary.createdWeek + 1);
                return item.Members.Count > 1 ? string.Format(_copy.groupedTitle, date, item.Members.Count) : date;
            }
            string target = FormatTarget(item.Primary.ToGoal());
            return item.Members.Count > 1
                ? string.Format(_copy.remainingTargets, target, item.Members.Count - 1) : target;
        }

        private string FormatTarget(GuideGoal goal)
        {
            string name = string.IsNullOrEmpty(goal.CardId) ? null : _playerNameResolver?.Invoke(goal.CardId);
            if (!string.IsNullOrEmpty(goal.CardId) && string.IsNullOrEmpty(name)) name = _copy.targetUnknown;
            string location = goal.SlotIndex >= 0 && _copy.assignmentGroups != null &&
                (int)goal.Group >= 0 && (int)goal.Group < _copy.assignmentGroups.Length
                    ? string.Format(_copy.issueLocation, _copy.assignmentGroups[(int)goal.Group], goal.SlotIndex + 1)
                    : goal.Actual.HasValue && goal.Expected.HasValue
                        ? string.Format(_copy.reportCounts, goal.Actual.Value, goal.Expected.Value)
                        : ActionLabel(goal);
            return string.IsNullOrEmpty(name) ? location : name + " · " + location;
        }

        private string FormatRoleEvidence(GuideGoal goal)
        {
            string[] roles = goal.Context.Split(new[] { "->" }, StringSplitOptions.None);
            string role = roles.Length == 2 && Enum.TryParse(roles[0], out PitcherRole natural) &&
                Enum.TryParse(roles[1], out PitcherRole assigned) && _copy.pitcherRoles != null &&
                (int)natural >= 0 && (int)natural < _copy.pitcherRoles.Length &&
                (int)assigned >= 0 && (int)assigned < _copy.pitcherRoles.Length
                    ? string.Format(_copy.roleAssignment, _copy.pitcherRoles[(int)natural], _copy.pitcherRoles[(int)assigned])
                    : FindIssueCopy(goal)?.body ?? _copy.missing;
            return role + "\n" + (goal.ConditionPenalty > 0 ? _copy.rolePenalty : _copy.roleNoPenalty);
        }

        private string FormatReportDetail(ManagerReportData report)
        {
            if (report.news != null) return FormatNewsTitle(report) + "\n" +
                Baseball.Presentation.Guide.OwnerManagerNewsFormatter.FormatBody(report.news, _copy.newsText) + "\n" +
                string.Format(_copy.reportDate, report.createdSeason, report.createdWeek + 1);
            string body = FormatBody(report.ToGoal(), report.homeScore, report.awayScore);
            if (report.isExpired) return FormatTarget(report.ToGoal()) + "\n" + _copy.expired;
            string date = string.Format(_copy.reportDate, report.createdSeason, report.createdWeek + 1);
            string status = report.isAccepted ? _copy.accepted : report.priority == ManagerReportPriority.Critical
                ? _copy.urgent : report.occurrence > 1 ? _copy.recurring : report.isRead ? _copy.read : _copy.unreadLabel;
            string detail = body + "\n" + status + " · " + string.Format(_copy.firstSeen, date);
            if (report.updatedSeason != report.createdSeason || report.updatedWeek != report.createdWeek)
                detail += "\n" + string.Format(_copy.lastChanged,
                    string.Format(_copy.reportDate, report.updatedSeason, report.updatedWeek + 1));
            return detail;
        }

        private string FormatNewsTitle(ManagerReportData report) =>
            Baseball.Presentation.Guide.OwnerManagerNewsFormatter.FormatTitle(report,
                _playerNameResolver?.Invoke(report.cardId) ?? _copy.targetUnknown, _copy.newsText);
    }
}
