using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.UI;
using UnityEngine;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// 4×4 성장판, 블록 상점, 오프시즌 주차 액션을 한 화면에서 관리한다.
    /// </summary>
    public sealed partial class UI_Scene_CareerGrowth : UISceneBase, ICareerTabScreen
    {
        private static readonly Color BackgroundColor = new(0.006f, 0.02f, 0.034f, 1f);
        private static readonly Color TopBarColor = new(0.008f, 0.027f, 0.052f, 1f);
        private static readonly Color PanelColor = new(0.018f, 0.065f, 0.108f, 0.99f);
        private static readonly Color PanelDarkColor = new(0.009f, 0.035f, 0.061f, 0.99f);
        private static readonly Color CardColor = new(0.024f, 0.086f, 0.139f, 0.97f);
        private static readonly Color BorderColor = new(0.28f, 0.46f, 0.62f, 1f);
        private static readonly Color DividerColor = new(0.14f, 0.31f, 0.45f, 1f);
        private static readonly Color AccentColor = new(0.13f, 0.55f, 0.92f, 1f);
        private static readonly Color BrightAccentColor = new(0.12f, 0.67f, 1f, 1f);
        private static readonly Color GreenColor = new(0.31f, 0.82f, 0.27f, 1f);
        private static readonly Color PurpleColor = new(0.74f, 0.31f, 0.93f, 1f);
        private static readonly Color GoldColor = new(0.96f, 0.72f, 0.22f, 1f);
        private static readonly Color WarningColor = new(0.94f, 0.56f, 0.16f, 1f);
        private static readonly Color PrimaryTextColor = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.62f, 0.71f, 0.8f, 1f);
        private static readonly Color MutedColor = new(0.34f, 0.40f, 0.49f, 1f);
        private static readonly Color ErrorColor = new(1f, 0.42f, 0.42f, 1f);

        private CareerManager _manager;
        private RectTransform _content;
        private int _selectedOwnedBlockId;
        private int _selectedPlacedBlockId;
        private int _selectedRotation;
        private bool _confirmPlacedBlockRemoval;
        private bool _confirmBoardRedesign;

        public CareerMainTab MainTab => CareerMainTab.Growth;
        public override bool BlocksLowerInput => true;

        public static UI_Scene_CareerGrowth CreateRuntime(Transform parent)
        {
            var screenObject = new GameObject(
                nameof(UI_Scene_CareerGrowth),
                typeof(RectTransform),
                typeof(CanvasGroup));
            screenObject.transform.SetParent(parent, false);
            UI_Scene_CareerGrowth screen = screenObject.AddComponent<UI_Scene_CareerGrowth>();
            Stretch(screenObject.GetComponent<RectTransform>());
            return screen;
        }

        protected override void OnInitialize()
        {
            _manager = GameManager.EnsureExists().EnsureManager<CareerManager>("CareerManager");
            _manager.CareerChanged += HandleCareerChanged;
            BuildHierarchy();
        }

        protected override void OnShow()
        {
            Render();
        }

        protected override void OnDestroy()
        {
            if (_manager != null)
                _manager.CareerChanged -= HandleCareerChanged;
            base.OnDestroy();
        }

        private void BuildHierarchy()
        {
            RectTransform root = (RectTransform)transform;
            Stretch(root);
            CreateImage("Background", root, BackgroundColor, Vector2.zero, Vector2.zero, stretch: true);
            _content = CreateRect("Content", root, new Vector2(1920f, 1080f), Vector2.zero);
        }

        private void HandleCareerChanged()
        {
            if (!_manager.HasActiveCareer)
            {
                Hide();
                return;
            }
            if (IsVisible)
                Render();
        }

        private void Render()
        {
            if (_content == null || _manager == null || !_manager.HasActiveCareer)
                return;

            CareerDashboardView dashboard = _manager.Dashboard;
            CareerGrowthView growth = _manager.GrowthDashboard;
            if (dashboard == null || growth == null)
                return;

            ValidateSelection(growth);
            ClearChildren(_content);
            RenderBackgroundAccents();
            RenderTopBar(dashboard, growth);
            RenderPlayerPanel(dashboard, growth);
            RenderGrowthLog(growth);
            RenderSkillBoard(growth);
            RenderSelectedBlockPanel(growth);
            RenderBlockShop(growth);
            RenderOffseasonActions(growth);
            CareerTabBar.Create(_content, CareerMainTab.Growth);
        }

        private void SelectOwnedBlock(int instanceId)
        {
            _selectedOwnedBlockId = instanceId;
            _selectedPlacedBlockId = 0;
            _selectedRotation = 0;
            _confirmPlacedBlockRemoval = false;
            _confirmBoardRedesign = false;
            Render();
        }

        private void SelectPlacedBlock(int instanceId)
        {
            _selectedOwnedBlockId = 0;
            _selectedPlacedBlockId = instanceId;
            _selectedRotation = 0;
            _confirmPlacedBlockRemoval = false;
            _confirmBoardRedesign = false;
            Render();
        }

        private void PlaceSelectedBlock(int x, int y)
        {
            if (_selectedOwnedBlockId <= 0)
                return;
            if (_manager.PlaceSkillBlock(_selectedOwnedBlockId, x, y, _selectedRotation))
            {
                _selectedOwnedBlockId = 0;
                _selectedRotation = 0;
            }
        }

        private void RotateSelectedBlock()
        {
            _selectedRotation = (_selectedRotation + 1) % 4;
            Render();
        }

        private void RemoveSelectedPlacedBlock()
        {
            if (!_confirmPlacedBlockRemoval)
            {
                _confirmPlacedBlockRemoval = true;
                Render();
                return;
            }

            if (_manager.RemoveSkillBlock(_selectedPlacedBlockId))
                _selectedPlacedBlockId = 0;
            _confirmPlacedBlockRemoval = false;
        }

        private void SellSelectedOwnedBlock()
        {
            if (_manager.SellOwnedSkillBlock(_selectedOwnedBlockId))
                _selectedOwnedBlockId = 0;
        }

        private void RedesignBoard()
        {
            if (!_confirmBoardRedesign)
            {
                _confirmBoardRedesign = true;
                Render();
                return;
            }

            if (_manager.RedesignSkillBoard())
            {
                _selectedOwnedBlockId = 0;
                _selectedPlacedBlockId = 0;
            }
            _confirmBoardRedesign = false;
        }

        private void ValidateSelection(CareerGrowthView growth)
        {
            if (_selectedOwnedBlockId > 0 && FindOwnedBlock(growth, _selectedOwnedBlockId).InstanceId == 0)
                _selectedOwnedBlockId = 0;
            if (_selectedPlacedBlockId > 0 && !ContainsPlacedBlock(growth, _selectedPlacedBlockId))
                _selectedPlacedBlockId = 0;
        }
    }
}
