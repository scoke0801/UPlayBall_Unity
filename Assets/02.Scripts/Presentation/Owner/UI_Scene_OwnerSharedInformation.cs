using System;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>Owner 일정과 실제 진행 시즌 이력을 공용 가상화 기록표로 표시하는 읽기 전용 화면이다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UI_Scene_OwnerSharedInformation : MonoBehaviour
    {
        private Text _title;
        private Text _context;
        private Text _footer;
        private Button _nextMatchAnalysisButton;
        private RecordTableView _table;
        private bool _isBuilt;

        public event Action NextMatchAnalysisRequested;

        /// <summary>공용 Workspace 슬롯을 채우는 Owner 읽기 전용 정보 화면을 생성한다.</summary>
        public static UI_Scene_OwnerSharedInformation CreateRuntime(Transform parent)
        {
            RectTransform root = OwnerRuntimeUiFactory.CreateRect(
                "UI_Scene_OwnerSharedInformation", parent);
            return root.gameObject.AddComponent<UI_Scene_OwnerSharedInformation>();
        }

        /// <summary>Owner Runtime에서 복사한 Round 일정과 콘텐츠 상태를 표시한다.</summary>
        public void BindSchedule(SharedScreenPresentationModel<ScheduleScreenSnapshot> model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            EnsureHierarchy();
            ScheduleScreenSnapshot snapshot = model.Snapshot;
            _title.text = snapshot == null
                ? "구단 일정"
                : string.Concat(snapshot.SeasonLabel, " · ", snapshot.LeagueLabel, " 일정");
            _context.text = snapshot == null
                ? string.Empty
                : snapshot.CurrentPeriodLabel;
            _footer.text = string.Empty;
            RecordTableModel table = snapshot == null
                ? null
                : ScheduleRecordTableBuilder.CreateFocusedSchedule(snapshot);
            _table.Bind(table, model.ContentState);
            bool hasNextMatch = HasNextFocusTeamMatch(snapshot);
            _nextMatchAnalysisButton.gameObject.SetActive(true);
            _nextMatchAnalysisButton.interactable = hasNextMatch;
            if (!hasNextMatch) _footer.text = "예정된 내 구단 경기가 없습니다.";
        }

        /// <summary>현재 Save에서 진행한 시즌별 구단 성적과 콘텐츠 상태를 표시한다.</summary>
        public void BindRecords(SharedScreenPresentationModel<RecordsScreenSnapshot> model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            EnsureHierarchy();
            RecordsScreenSnapshot snapshot = model.Snapshot;
            _title.text = snapshot == null
                ? "역사 기록"
                : string.Concat(snapshot.SeasonLabel, " · ", snapshot.CategoryLabel);
            _context.text = snapshot == null
                ? string.Empty
                : string.Concat(snapshot.ScopeLabel, " · ", snapshot.QualificationText);
            _footer.text = "역대 시즌 성적";
            _table.HighlightBadge = "현재 시즌";
            _table.Bind(
                snapshot?.Table,
                model.ContentState,
                snapshot?.FocusedRowId);
            _nextMatchAnalysisButton.gameObject.SetActive(false);
            OwnerDashboardStyle.SetDataText(_footer);
        }

        /// <summary>다른 Workspace로 이동할 때 화면 표시를 전환한다.</summary>
        public void SetVisible(bool isVisible)
        {
            gameObject.SetActive(isVisible);
        }

        private void Awake()
        {
            EnsureHierarchy();
        }

        private void OnDestroy()
        {
            if (_nextMatchAnalysisButton != null)
                _nextMatchAnalysisButton.onClick.RemoveAllListeners();
        }

        private void EnsureHierarchy()
        {
            if (_isBuilt)
                return;
            _isBuilt = true;

            RectTransform root = GetComponent<RectTransform>();
            OwnerRuntimeUiFactory.Stretch(root);
            Image background = OwnerRuntimeUiFactory.CreateImage(
                "Background", root, OwnerDashboardStyle.TableSurface);
            OwnerRuntimeUiFactory.Stretch(background.rectTransform);
            OwnerDashboardStyle.SetDataSurface(background, OwnerDashboardStyle.TableSurface);
            UIOwnerFrontOfficePanel.ApplyWorkspace(root);
            background.enabled = false;

            RectTransform header = OwnerRuntimeUiFactory.CreateRect("InformationHeader", root);
            OwnerRuntimeUiFactory.SetAnchors(
                header,
                new Vector2(0f, 0.87f),
                Vector2.one,
                new Vector2(34f, 0f),
                new Vector2(-34f, 0f));
            _title = OwnerRuntimeUiFactory.CreateText(
                "Title", header, string.Empty, 18, FontStyle.Bold,
                TextAnchor.MiddleLeft, OwnerDashboardStyle.Ivory);
            OwnerRuntimeUiFactory.SetAnchors(
                _title.rectTransform,
                new Vector2(0f, 0.44f),
                new Vector2(0.74f, 1f),
                Vector2.zero,
                Vector2.zero);
            _nextMatchAnalysisButton = OwnerRuntimeUiFactory.CreateReferenceButton(
                "NextMatchAnalysisButton",
                header,
                "다음 경기 분석",
                14);
            OwnerRuntimeUiFactory.SetAnchors(
                _nextMatchAnalysisButton.GetComponent<RectTransform>(),
                new Vector2(0.78f, 0.25f),
                new Vector2(1f, 0.84f),
                Vector2.zero,
                Vector2.zero);
            _nextMatchAnalysisButton.onClick.AddListener(() => NextMatchAnalysisRequested?.Invoke());
            _context = OwnerRuntimeUiFactory.CreateText(
                "Context", header, string.Empty, 13, FontStyle.Normal,
                TextAnchor.MiddleLeft, OwnerDashboardStyle.TableSecondary);
            OwnerRuntimeUiFactory.SetAnchors(
                _context.rectTransform,
                Vector2.zero,
                new Vector2(0.74f, 0.44f),
                Vector2.zero,
                Vector2.zero);

            Image blueRule = OwnerRuntimeUiFactory.CreateImage(
                "BlueRule", root, OwnerDashboardStyle.Gold);
            OwnerRuntimeUiFactory.SetAnchors(
                blueRule.rectTransform,
                new Vector2(0.025f, 0.855f),
                new Vector2(0.975f, 0.855f),
                Vector2.zero,
                new Vector2(0f, 1f));

            RectTransform tableHost = OwnerRuntimeUiFactory.CreateRect("RecordTableHost", root);
            OwnerRuntimeUiFactory.SetAnchors(
                tableHost,
                new Vector2(0.035f, 0.12f),
                new Vector2(0.965f, 0.825f),
                Vector2.zero,
                Vector2.zero);
            _table = RecordTableView.CreateRuntime(tableHost, "SharedRecordTable");
            _table.SetVisualStyle(RecordTableVisualStyle.OwnerFrontOffice);
            OwnerDashboardStyle.SetDataText(_title, true);
            OwnerDashboardStyle.SetDataText(_context);
            OwnerDashboardStyle.SetDataSurface(blueRule, OwnerDashboardStyle.Line);

            _footer = OwnerRuntimeUiFactory.CreateText(
                "Footer", root, string.Empty, 13, FontStyle.Normal,
                TextAnchor.MiddleLeft, OwnerDashboardStyle.TableSecondary);
            OwnerRuntimeUiFactory.SetAnchors(
                _footer.rectTransform,
                new Vector2(0.035f, 0.025f),
                new Vector2(0.8f, 0.095f),
                Vector2.zero,
                Vector2.zero);
        }

        private static bool HasNextFocusTeamMatch(ScheduleScreenSnapshot snapshot)
        {
            if (snapshot == null)
                return false;
            for (int index = 0; index < snapshot.Games.Count; index++)
            {
                ScheduleGameSnapshot game = snapshot.Games[index];
                if (!game.IsCompleted && game.FocusSide != ScheduleFocusSide.None)
                    return true;
            }
            return false;
        }
    }
}
