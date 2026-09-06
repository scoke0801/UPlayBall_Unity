using System;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>현재 시즌 리그 전체 선수 기록을 부문 탭과 공용 가상화 기록표로 표시하는 읽기 전용 화면이다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UI_Scene_OwnerSeasonRecords : MonoBehaviour
    {
        private const int MaxCategoryButtons = 4;

        private Text _title;
        private Text _context;
        private readonly Button[] _categoryButtons = new Button[MaxCategoryButtons];
        private readonly Text[] _categoryLabels = new Text[MaxCategoryButtons];
        private RecordTableView _table;
        private OwnerSeasonRecordsPresentationModel _model;
        private int _categoryIndex;
        private bool _isBuilt;

        /// <summary>공용 셸의 리그 작업 영역에 기록 화면을 만든다.</summary>
        public static UI_Scene_OwnerSeasonRecords CreateRuntime(Transform parent)
        {
            RectTransform root = OwnerRuntimeUiFactory.CreateRect(
                nameof(UI_Scene_OwnerSeasonRecords), parent);
            return root.gameObject.AddComponent<UI_Scene_OwnerSeasonRecords>();
        }

        /// <summary>Game 레이어가 확정한 네 부문 기록으로 화면을 교체한다.</summary>
        public void Bind(OwnerSeasonRecordsPresentationModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            EnsureHierarchy();
            if (_categoryIndex >= _model.Categories.Count)
                _categoryIndex = 0;
            Render();
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
            for (int index = 0; index < _categoryButtons.Length; index++)
                if (_categoryButtons[index] != null)
                    _categoryButtons[index].onClick.RemoveAllListeners();
        }

        private void SelectCategory(int index)
        {
            if (_model == null || index < 0 || index >= _model.Categories.Count)
                return;
            _categoryIndex = index;
            Render();
        }

        private void Render()
        {
            if (_model == null)
                return;

            OwnerSeasonRecordsCategoryModel category = _model.Categories[_categoryIndex];
            _title.text = string.Concat(_model.SeasonLabel, " · ", category.DisplayName, " 기록");
            _context.text = string.Concat(_model.LeagueLabel, " · 정규시즌 · ", category.QualificationText);

            for (int index = 0; index < _categoryButtons.Length; index++)
            {
                bool hasCategory = index < _model.Categories.Count;
                _categoryButtons[index].gameObject.SetActive(hasCategory);
                if (!hasCategory)
                    continue;
                _categoryLabels[index].text = _model.Categories[index].DisplayName;
                _categoryButtons[index].GetComponent<Image>().color = index == _categoryIndex
                    ? CareerUiTheme.PrimaryAction
                    : CareerUiTheme.SecondaryAction;
                _categoryLabels[index].color = index == _categoryIndex
                    ? CareerUiTheme.TextPrimary
                    : CareerUiTheme.TextSecondary;
            }

            _table.Bind(category.Table, category.ContentState, category.FocusedRowId);
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
                "SeasonRecordsHeader", root, "선수 기록");
            OwnerRuntimeUiFactory.SetAnchors(
                header.Root,
                new Vector2(0f, 0.84f),
                Vector2.one,
                new Vector2(12f, 4f),
                new Vector2(-12f, -12f));
            _title = OwnerRuntimeUiFactory.CreateText(
                "Title", header.Content, string.Empty, 21, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.TextPrimary);
            OwnerRuntimeUiFactory.SetAnchors(
                _title.rectTransform,
                new Vector2(0f, 0.55f),
                Vector2.one,
                new Vector2(14f, 0f),
                new Vector2(-14f, 0f));
            _context = OwnerRuntimeUiFactory.CreateText(
                "Context", header.Content, string.Empty, 13, FontStyle.Normal,
                TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerRuntimeUiFactory.SetAnchors(
                _context.rectTransform,
                Vector2.zero,
                new Vector2(1f, 0.55f),
                new Vector2(14f, 0f),
                new Vector2(-14f, 0f));

            RectTransform categoryBar = OwnerRuntimeUiFactory.CreateRect("CategoryBar", root);
            OwnerRuntimeUiFactory.SetAnchors(
                categoryBar,
                new Vector2(0f, 0.775f),
                new Vector2(1f, 0.84f),
                new Vector2(12f, 2f),
                new Vector2(-12f, -2f));
            for (int index = 0; index < _categoryButtons.Length; index++)
            {
                int captured = index;
                Button button = OwnerRuntimeUiFactory.CreateButton(
                    "CategoryButton" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    categoryBar,
                    string.Empty,
                    CareerUiTheme.SecondaryAction);
                float width = 1f / _categoryButtons.Length;
                OwnerRuntimeUiFactory.SetAnchors(
                    button.GetComponent<RectTransform>(),
                    new Vector2(width * index, 0f),
                    new Vector2(width * (index + 1), 1f),
                    new Vector2(2f, 0f),
                    new Vector2(-2f, 0f));
                button.onClick.AddListener(() => SelectCategory(captured));
                _categoryButtons[index] = button;
                _categoryLabels[index] = button.transform.Find("Label").GetComponent<Text>();
            }

            RectTransform tableHost = OwnerRuntimeUiFactory.CreateRect("SeasonRecordTableHost", root);
            OwnerRuntimeUiFactory.SetAnchors(
                tableHost,
                Vector2.zero,
                new Vector2(1f, 0.775f),
                new Vector2(12f, 12f),
                new Vector2(-12f, -4f));
            _table = RecordTableView.CreateRuntime(tableHost, "SeasonRecordTable");
        }
    }
}
