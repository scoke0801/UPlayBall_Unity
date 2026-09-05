using System;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>Owner 일정과 확정 역사 기록을 공용 가상화 기록표로 표시하는 읽기 전용 화면이다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UI_Scene_OwnerSharedInformation : MonoBehaviour
    {
        private Text _title;
        private Text _context;
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
                : string.Concat(snapshot.CurrentPeriodLabel, " · 확정 라운드/점수");
            RecordTableModel table = snapshot == null
                ? null
                : ScheduleRecordTableBuilder.CreateFocusedSchedule(snapshot);
            _table.Bind(table, model.ContentState);
            bool hasNextMatch = HasNextFocusTeamMatch(snapshot);
            _nextMatchAnalysisButton.gameObject.SetActive(true);
            _nextMatchAnalysisButton.interactable = hasNextMatch;
        }

        /// <summary>현재 시즌과 구분된 WorldHistory 확정 기록과 콘텐츠 상태를 표시한다.</summary>
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
            _table.Bind(
                snapshot?.Table,
                model.ContentState,
                snapshot?.FocusedRowId);
            _nextMatchAnalysisButton.gameObject.SetActive(false);
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
                "Background", root, CareerUiTheme.Background);
            OwnerRuntimeUiFactory.Stretch(background.rectTransform);

            OwnerWorkspaceUiFactory.Panel header = OwnerRuntimeUiFactory.CreatePanel(
                "InformationHeader", root, "리그 정보");
            OwnerRuntimeUiFactory.SetAnchors(
                header.Root,
                new Vector2(0f, 0.87f),
                Vector2.one,
                new Vector2(12f, 4f),
                new Vector2(-12f, -12f));
            _title = OwnerRuntimeUiFactory.CreateText(
                "Title", header.Content, string.Empty, 21, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.TextPrimary);
            OwnerRuntimeUiFactory.SetAnchors(
                _title.rectTransform,
                new Vector2(0f, 0.42f),
                Vector2.one,
                new Vector2(14f, 0f),
                new Vector2(-220f, 0f));
            _nextMatchAnalysisButton = OwnerRuntimeUiFactory.CreateButton(
                "NextMatchAnalysisButton",
                header.Content,
                "다음 경기 분석",
                CareerUiTheme.PrimaryAction);
            OwnerRuntimeUiFactory.SetAnchors(
                _nextMatchAnalysisButton.GetComponent<RectTransform>(),
                new Vector2(0.76f, 0.5f),
                new Vector2(1f, 1f),
                new Vector2(0f, 4f),
                new Vector2(-14f, -4f));
            _nextMatchAnalysisButton.onClick.AddListener(() => NextMatchAnalysisRequested?.Invoke());
            _context = OwnerRuntimeUiFactory.CreateText(
                "Context", header.Content, string.Empty, 13, FontStyle.Normal,
                TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerRuntimeUiFactory.SetAnchors(
                _context.rectTransform,
                Vector2.zero,
                new Vector2(1f, 0.42f),
                new Vector2(14f, 0f),
                new Vector2(-14f, 0f));

            RectTransform tableHost = OwnerRuntimeUiFactory.CreateRect("RecordTableHost", root);
            OwnerRuntimeUiFactory.SetAnchors(
                tableHost,
                Vector2.zero,
                new Vector2(1f, 0.87f),
                new Vector2(12f, 12f),
                new Vector2(-12f, -4f));
            _table = RecordTableView.CreateRuntime(tableHost, "SharedRecordTable");
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
