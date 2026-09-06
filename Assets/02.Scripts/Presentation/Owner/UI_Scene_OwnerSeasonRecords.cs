using System;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>현재·완료 시즌의 선수 기록을 시즌 선택과 부문 탭, 공용 기록표로 표시한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UI_Scene_OwnerSeasonRecords : MonoBehaviour
    {
        private const int MaxCategoryButtons = 4;

        private Text _title;
        private Text _context;
        private Button _previousSeason;
        private Button _nextSeason;
        private Action<int> _selectSeason;
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
        public void Bind(OwnerSeasonRecordsPresentationModel model, Action<int> selectSeason = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _selectSeason = selectSeason;
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
            _selectSeason = null;
            if (_previousSeason != null) _previousSeason.onClick.RemoveAllListeners();
            if (_nextSeason != null) _nextSeason.onClick.RemoveAllListeners();
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

        private void SelectSeason(int direction)
        {
            if (_model == null || _selectSeason == null) return;
            int index = _model.SelectedSeasonIndex + direction;
            if (index >= 0 && index < _model.SeasonNumbers.Count) _selectSeason(_model.SeasonNumbers[index]);
        }

        private void Render()
        {
            if (_model == null)
                return;

            OwnerSeasonRecordsCategoryModel category = _model.Categories[_categoryIndex];
            _title.text = string.Concat(_model.SeasonLabel, " · ", category.DisplayName, " 기록");
            _context.text = string.Concat(_model.LeagueLabel, " · 정규시즌 · ", category.QualificationText);
            bool canSelectSeason = _selectSeason != null && _model.SeasonNumbers.Count > 1;
            _previousSeason.gameObject.SetActive(canSelectSeason);
            _nextSeason.gameObject.SetActive(canSelectSeason);
            _previousSeason.interactable = _model.SelectedSeasonIndex + 1 < _model.SeasonNumbers.Count;
            _nextSeason.interactable = _model.SelectedSeasonIndex > 0;

            for (int index = 0; index < _categoryButtons.Length; index++)
            {
                bool hasCategory = index < _model.Categories.Count;
                _categoryButtons[index].gameObject.SetActive(hasCategory);
                if (!hasCategory)
                    continue;
                _categoryLabels[index].text = _model.Categories[index].DisplayName;
                _categoryButtons[index].GetComponent<Image>().color = index == _categoryIndex
                    ? CareerUiTheme.ReferenceDataAccent
                    : CareerUiTheme.ReferenceDataHeader;
                _categoryLabels[index].color = index == _categoryIndex
                    ? Color.white
                    : CareerUiTheme.ReferenceDataInk;
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
                "Background", root, CareerUiTheme.ReferenceDataCanvas);
            OwnerRuntimeUiFactory.Stretch(background.rectTransform);

            RectTransform header = OwnerRuntimeUiFactory.CreateRect("SeasonRecordsHeader", root);
            OwnerRuntimeUiFactory.SetAnchors(
                header,
                new Vector2(0f, 0.84f),
                Vector2.one,
                new Vector2(34f, 0f),
                new Vector2(-34f, 0f));
            _title = OwnerRuntimeUiFactory.CreateText(
                "Title", header, string.Empty, 18, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceDataInk);
            OwnerRuntimeUiFactory.SetAnchors(
                _title.rectTransform,
                new Vector2(0f, 0.52f),
                new Vector2(0.64f, 1f),
                Vector2.zero,
                Vector2.zero);
            _context = OwnerRuntimeUiFactory.CreateText(
                "Context", header, string.Empty, 13, FontStyle.Normal,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceDataInkSecondary);
            OwnerRuntimeUiFactory.SetAnchors(
                _context.rectTransform,
                Vector2.zero,
                new Vector2(0.64f, 0.52f),
                Vector2.zero,
                Vector2.zero);

            _previousSeason = OwnerRuntimeUiFactory.CreateReferenceButton(
                "PreviousSeason", header, "◀ 이전 시즌", 13);
            OwnerRuntimeUiFactory.SetAnchors(_previousSeason.GetComponent<RectTransform>(),
                new Vector2(0.69f, 0.25f), new Vector2(0.84f, 0.78f), Vector2.zero, new Vector2(-5f, 0f));
            _previousSeason.onClick.AddListener(() => SelectSeason(1));
            _nextSeason = OwnerRuntimeUiFactory.CreateReferenceButton(
                "NextSeason", header, "다음 시즌 ▶", 13);
            OwnerRuntimeUiFactory.SetAnchors(_nextSeason.GetComponent<RectTransform>(),
                new Vector2(0.84f, 0.25f), new Vector2(1f, 0.78f), new Vector2(5f, 0f), Vector2.zero);
            _nextSeason.onClick.AddListener(() => SelectSeason(-1));

            Image blueRule = OwnerRuntimeUiFactory.CreateImage(
                "BlueRule", root, CareerUiTheme.ReferenceDataAccent);
            OwnerRuntimeUiFactory.SetAnchors(
                blueRule.rectTransform,
                new Vector2(0.025f, 0.855f),
                new Vector2(0.975f, 0.855f),
                Vector2.zero,
                new Vector2(0f, 3f));

            RectTransform categoryBar = OwnerRuntimeUiFactory.CreateRect("CategoryBar", root);
            OwnerRuntimeUiFactory.SetAnchors(
                categoryBar,
                new Vector2(0.035f, 0.775f),
                new Vector2(0.62f, 0.84f),
                Vector2.zero,
                Vector2.zero);
            for (int index = 0; index < _categoryButtons.Length; index++)
            {
                int captured = index;
                Button button = OwnerRuntimeUiFactory.CreateReferenceButton(
                    "CategoryButton" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    categoryBar,
                    string.Empty,
                    14);
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
                new Vector2(0.035f, 0.12f),
                new Vector2(0.965f, 0.75f),
                Vector2.zero,
                Vector2.zero);
            _table = RecordTableView.CreateRuntime(tableHost, "SeasonRecordTable");
            _table.SetVisualStyle(RecordTableVisualStyle.ReferenceLight);

            Text footer = OwnerRuntimeUiFactory.CreateText(
                "Footer", root, "정규시즌 누적 · 열 제목을 누르면 정렬 · 강조 행은 내 구단 선수",
                13, FontStyle.Normal, TextAnchor.MiddleLeft, CareerUiTheme.ReferenceDataInkSecondary);
            OwnerRuntimeUiFactory.SetAnchors(
                footer.rectTransform,
                new Vector2(0.035f, 0.025f),
                new Vector2(0.82f, 0.095f),
                Vector2.zero,
                Vector2.zero);
        }

    }
}
