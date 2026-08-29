using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// 성장 보드 편집·블록 보관함·뽑기 오버레이·오프시즌 액션을 분리해 관리한다.
    /// </summary>
    public sealed partial class UI_Scene_CareerGrowth : UISceneBase, ICareerTabScreen
    {
        private const int InventoryPageSize = 12;
        private const int ProgramPageSize = 5;

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
        private int _inventoryPage;
        private int _programPage;
        private bool _confirmPlacedBlockRemoval;
        private bool _confirmBoardRedesign;
        private GrowthSection _growthSection;
        private bool _isGachaOpen;
        private bool _isProbabilityOpen;
        private SkillGachaPurchaseTier _selectedGachaTier = SkillGachaPurchaseTier.Normal;
        private SkillBlockCategory? _selectedGachaCategory;
        private SkillBlockCategory? _inventoryCategory;
        private SkillBlockRarity? _inventoryRarity;
        private bool _inventoryPlaceableOnly;
        private bool _inventoryNewOnly;
        private bool _isBoardDraftInitialized;
        private bool _isBoardDraftDirty;
        private bool _confirmBoardApply;
        private readonly List<GrowthBoardLayoutPlacement> _draftLayout =
            new List<GrowthBoardLayoutPlacement>();

        private enum GrowthSection
        {
            Board,
            OffseasonActions
        }

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
            _isBoardDraftInitialized = false;
            _programPage = 0;
            Render();
        }

        protected override void OnDestroy()
        {
            if (_manager != null)
                _manager.CareerChanged -= HandleCareerChanged;
            base.OnDestroy();
        }

        private void Update()
        {
            if (UI_CareerPresentation.IsPlaying)
                return;
            if (!IsVisible || Keyboard.current == null || _manager?.HasActiveCareer != true)
                return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_isGachaOpen)
                {
                    CloseGachaOverlay();
                    return;
                }
                if (_selectedOwnedBlockId > 0 || _selectedPlacedBlockId > 0)
                {
                    _selectedOwnedBlockId = 0;
                    _selectedPlacedBlockId = 0;
                    _selectedRotation = 0;
                    Render();
                }
                return;
            }
            if (!keyboard.rKey.wasPressedThisFrame ||
                _isGachaOpen ||
                _growthSection != GrowthSection.Board ||
                _selectedOwnedBlockId <= 0)
            {
                return;
            }
            CareerGrowthView growth = _manager.GrowthDashboard;
            GrowthSkillBlockView block = FindAnyBlock(growth, _selectedOwnedBlockId);
            if (growth.CanEditBoard && block.CanRotate)
                RotateSelectedBlock();
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
            if (!_isBoardDraftDirty)
                _isBoardDraftInitialized = false;
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

            EnsureBoardDraft(growth);
            ValidateWorkspaceSelection(growth);
            RectTransform previousContent = _content;
            RectTransform nextContent = CreateRect(
                "Content",
                transform,
                new Vector2(1920f, 1080f),
                Vector2.zero);
            nextContent.gameObject.SetActive(false);
            _content = nextContent;
            try
            {
                RenderBackgroundAccents();
                RenderTopBar(dashboard, growth);
                RenderGrowthSubNavigation(growth);
                if (_growthSection == GrowthSection.Board)
                    RenderGrowthBoardWorkspace(dashboard, growth);
                else
                    RenderOffseasonActionWorkspace(dashboard, growth);
                CareerTabBar.Create(_content, CareerMainTab.Growth);
                if (_isGachaOpen)
                    RenderGachaOverlay(dashboard, growth);
            }
            catch
            {
                _content = previousContent;
                DestroyRenderedContent(nextContent.gameObject);
                throw;
            }

            previousContent.gameObject.SetActive(false);
            nextContent.gameObject.SetActive(true);
            DestroyRenderedContent(previousContent.gameObject);
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

        private void OpenActivityConfirmation(string programId)
        {
            GrowthProgramView preview = _manager.BuildGrowthProgramPreview(
                programId,
                TrainingIntensity.Standard);
            if (preview.CanSelect)
                _manager.SelectGrowthProgram(programId, TrainingIntensity.Standard);

            UI_Popup_GrowthActivityConfirmation popup =
                FindFirstObjectByType<UI_Popup_GrowthActivityConfirmation>(FindObjectsInactive.Include);
            if (popup == null)
            {
                UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
                popup = UI_Popup_GrowthActivityConfirmation.CreateRuntime(
                    uiManager.Root.GetLayerRoot(UILayer.Popup));
            }
            popup.ShowProgram(programId);
        }

        private void PurchaseSkillBlock(
            SkillBlockCategory category,
            SkillGachaPurchaseTier tier)
        {
            _inventoryPage = 0;
            if (!_manager.PurchaseSkillBlock(category, tier))
                return;

            CareerGrowthView growth = _manager.GrowthDashboard;
            if (growth.LastPulledBlocks.Length == 0)
                return;
            _selectedOwnedBlockId = growth.LastPulledBlocks[0].InstanceId;
            _selectedPlacedBlockId = 0;
            _selectedRotation = 0;
            _confirmPlacedBlockRemoval = false;
            _confirmBoardRedesign = false;
            Render();
        }

        private void ShowNewerInventoryPage()
        {
            if (_inventoryPage <= 0)
                return;
            _inventoryPage--;
            Render();
        }

        private void ShowOlderInventoryPage()
        {
            _inventoryPage++;
            Render();
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
            int pageCount = Math.Max(
                1,
                (growth.OwnedBlocks.Length + InventoryPageSize - 1) / InventoryPageSize);
            if (_inventoryPage >= pageCount)
                _inventoryPage = pageCount - 1;
        }

        private static void DestroyRenderedContent(GameObject content)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(content);
            else
#endif
                Destroy(content);
        }
    }
}
