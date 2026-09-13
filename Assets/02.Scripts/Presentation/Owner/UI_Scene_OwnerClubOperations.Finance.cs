using Baseball.Core.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerClubOperations
    {
        private static readonly Color FinanceBorder = OwnerDashboardStyle.Line;
        private static readonly Color FinancePositive = CareerUiTheme.Success;
        private static readonly Color FinanceNegative = CareerUiTheme.Error;
        private FinanceStatement _weeklyStatement;
        private FinanceStatement _seasonStatement;
        private Image _fanBaseFill;
        private Image _popularityFill;

        private void BuildFinanceDashboard(Transform content)
        {
            Image background = FinanceSurface(content, "FinanceCanvas", UIClubOfficeStyle.Paper, 0f, 0f, 1f, 1f);
            background.transform.SetAsFirstSibling();

            Image stadium = FinanceSurface(content, "FinanceBallpark", Color.white, .01f, .69f, .38f, .99f);
            stadium.sprite = LoadGeneratedSprite("UI/Generated/owner_finance_ballpark_v1");
            // 생성 구장은 분위기용 삽화이며 실제 구장 규모나 관중 분포를 나타내지 않는다.
            stadium.type = Image.Type.Simple;
            stadium.preserveAspect = false;
            FinanceSurface(stadium.transform, "Caption", new Color32(20, 34, 47, 235), 0f, 0f, 1f, .26f);
            FinanceSurface(content, "ExpansionSurface", OwnerDashboardStyle.TableSurface, .01f, .52f, .38f, .69f);
            FinanceLabel(content, "ExpansionTitle", "구장 증축", 14, true, .025f, .53f, .21f, .59f);

            _fanBaseFill = BuildFinanceMetric(content, "FanBaseMetric", "팬 기반", .41f, .81f, .68f, .99f);
            _popularityFill = BuildFinanceMetric(content, "PopularityMetric", "인기도", .70f, .81f, .99f, .99f);
            BuildFinanceMetric(content, "ExpectedMetric", "예상 관중", .41f, .64f, .68f, .79f, false);
            BuildFinanceMetric(content, "RecentMetric", "최근 관중", .70f, .64f, .99f, .79f, false);
            FinanceSurface(content, "TicketSurface", OwnerDashboardStyle.TableHeader, .41f, .52f, .99f, .63f);

            foreach (Text label in new[] { _stadiumText, _stadiumUpgradeText, _fanBaseText,
                _popularityText, _expectedAttendanceText, _recentAttendanceText, _ticketPolicyText, _feedbackText })
            {
                if (label.GetComponent<CareerUiPreserveTextColor>() == null)
                    label.gameObject.AddComponent<CareerUiPreserveTextColor>();
                label.color = UIClubOfficeStyle.Ink;
                label.transform.SetAsLastSibling();
                label.resizeTextForBestFit = false;
                label.resizeTextMinSize = 12;
                label.resizeTextMaxSize = label.fontSize;
            }
            _stadiumUpgradeText.color = UIClubOfficeStyle.Muted;
            _feedbackText.color = UIClubOfficeStyle.Muted;
            foreach (Text metric in new[] { _fanBaseText, _popularityText, _expectedAttendanceText, _recentAttendanceText })
            {
                metric.fontSize = 26;
                metric.resizeTextMaxSize = 26;
                OwnerDashboardStyle.SetTypography(metric, true);
            }
            _stadiumUpgradeButton.transform.SetAsLastSibling();
            foreach (Button button in _ticketButtons.Values) button.transform.SetAsLastSibling();
            _weeklyStatement = new FinanceStatement(_weeklyFinanceText);
            _seasonStatement = new FinanceStatement(_seasonFinanceText);
            PlaceFinanceAction(content, "FinanceAdvanceWeek", .82f, .99f, true);
        }

        private static Image BuildFinanceMetric(Transform parent, string name, string title,
            float x, float y, float right, float top, bool showMeter = true)
        {
            Image surface = FinanceSurface(parent, name, OwnerDashboardStyle.TableSurface, x, y, right, top);
            FinanceLabel(surface.transform, "Title", title, 12, false, .06f, .65f, .94f, .96f);
            if (!showMeter) return null;
            Image track = FinanceSurface(surface.transform, "MeterTrack", FinanceBorder, .06f, .14f, .94f, .18f);
            return FinanceSurface(track.transform, "Fill", FinancePositive, 0f, 0f, 0f, 1f);
        }

        private void BindFinanceDashboard(OwnerClubOperationPresentationModel model)
        {
            _fanBaseText.text = $"{model.Snapshot.FanBase:0.0} / 100";
            _popularityText.text = $"{model.Snapshot.Popularity:0.0} / 100";
            _expectedAttendanceText.text = model.Snapshot.ExpectedAttendance.HasValue
                ? $"{model.Snapshot.ExpectedAttendance.Value:N0}명" : "정보 부족";
            _recentAttendanceText.text = model.Snapshot.RecentAttendance.HasValue
                ? $"{model.Snapshot.RecentAttendance.Value:N0}명" : "아직 집계 전";
            _fanBaseFill.rectTransform.anchorMax = new Vector2((float)model.Snapshot.FanBase / 100f, 1f);
            _popularityFill.rectTransform.anchorMax = new Vector2((float)model.Snapshot.Popularity / 100f, 1f);
            _weeklyStatement.Bind(model.WeeklyFinance, model.Snapshot.WeeklyFinance);
            _seasonStatement.Bind(model.SeasonFinance, model.Snapshot.SeasonFinance);
            _feedbackText.text = model.Snapshot.ContractArrears > 0
                ? $"미지급금 {OwnerMoneyFormatter.Format(model.Snapshot.ContractArrears)} · 다음 수입에서 우선 지급"
                : string.Empty;
        }

        private static void PlaceFinanceAction(Transform parent, string name, float x, float right, bool primary)
        {
            Button button = parent.Find(name).GetComponent<Button>();
            SetLayout(button.GetComponent<RectTransform>(), x, .025f, right, .085f, 0f);
            button.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            Text label = button.transform.Find("Label").GetComponent<Text>();
            button.GetComponent<Image>().color = primary ? UIClubOfficeStyle.Blue : Color.white;
            label.color = primary ? Color.white : UIClubOfficeStyle.Ink;
            OwnerUiButtonSkin.Apply(button, primary ? OwnerButtonRole.Primary : OwnerButtonRole.Secondary);
        }

        private static void StyleFinanceTicket(Button button, bool selected)
        {
            OwnerUiButtonSkin.Apply(button, OwnerButtonRole.Tab);
            OwnerUiButtonSkin.SetSelected(button, selected);

            button.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            Text label = button.transform.Find("Label").GetComponent<Text>();
            label.fontSize = 14;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = 14;
        }

        private static Image FinanceSurface(Transform parent, string name, Color color,
            float x, float y, float right, float top)
        {
            Image surface = UIClubOfficeStyle.Surface(name, parent, color);
            UIClubOfficeStyle.Place(surface.rectTransform, x, y, right, top);
            return surface;
        }

        private static Text FinanceLabel(Transform parent, string name, string value, int size, bool bold,
            float x, float y, float right, float top, Color? color = null)
        {
            Text label = UIClubOfficeStyle.Label(name, parent, value, size, bold);
            UIClubOfficeStyle.Place(label.rectTransform, x, y, right, top);
            label.color = color ?? (bold ? UIClubOfficeStyle.Ink : UIClubOfficeStyle.Muted);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = size;
            return label;
        }

        /// <summary>같은 회계 입력을 수입·지출·순이익의 독립된 행으로 표시한다.</summary>
        private sealed class FinanceStatement
        {
            private readonly Text _title;
            private readonly Text _income;
            private readonly Text _expense;
            private readonly Text _net;
            private readonly Text _production;
            private readonly Text _attendance;

            internal FinanceStatement(Text previousSummary)
            {
                Transform root = previousSummary.transform.parent;
                previousSummary.gameObject.SetActive(false);
                root.GetComponent<Image>().color = OwnerDashboardStyle.TableSurface;
                root.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
                FinanceSurface(root, "TopRule", OwnerDashboardStyle.Line, 0f, .997f, 1f, 1f);
                _title = FinanceLabel(root, "Title", "", 18, true, .035f, .78f, .4f, .96f);
                _attendance = FinanceLabel(root, "Attendance", "", 12, false, .40f, .78f, .965f, .96f);
                _attendance.alignment = TextAnchor.MiddleRight;
                FinanceLabel(root, "IncomeLabel", "수입", 14, false, .04f, .59f, .28f, .77f);
                FinanceLabel(root, "ExpenseLabel", "지출", 14, false, .04f, .40f, .28f, .58f);
                _income = FinanceLabel(root, "IncomeValue", "", 23, true, .30f, .59f, .95f, .77f, FinancePositive);
                _expense = FinanceLabel(root, "ExpenseValue", "", 23, true, .30f, .40f, .95f, .58f, FinanceNegative);
                FinanceSurface(root, "NetSurface", OwnerDashboardStyle.TableAlternate, .025f, .16f, .975f, .39f);
                FinanceLabel(root, "NetLabel", "순이익", 14, true, .04f, .17f, .28f, .38f);
                _net = FinanceLabel(root, "NetValue", "", 27, true, .30f, .17f, .95f, .38f);
                _income.alignment = _expense.alignment = _net.alignment = TextAnchor.MiddleRight;
                _production = FinanceLabel(root, "Production", "", 12, false, .04f, .015f, .96f, .145f);
            }

            internal void Bind(OwnerFinancePresentationModel model, OwnerFinanceSnapshot snapshot)
            {
                _title.text = model.Title + " 결산";
                _income.text = OwnerMoneyFormatter.Format(snapshot.MoneyIncome);
                _expense.text = OwnerMoneyFormatter.Format(snapshot.MoneyExpense);
                long net = snapshot.MoneyIncome - snapshot.MoneyExpense;
                _net.text = OwnerMoneyFormatter.FormatSigned(net);
                _net.color = net > 0 ? FinancePositive : net < 0 ? FinanceNegative : UIClubOfficeStyle.Ink;
                _production.text = model.ProductionText;
                _attendance.text = model.AttendanceText;
            }
        }
    }
}
