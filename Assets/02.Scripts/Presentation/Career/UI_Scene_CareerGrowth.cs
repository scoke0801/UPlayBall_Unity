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

        private static readonly Color BackgroundColor = CareerUiTheme.Background;
        private static readonly Color TopBarColor = CareerUiTheme.TopBar;
        private static readonly Color PanelColor = CareerUiTheme.Panel;
        private static readonly Color PanelDarkColor = CareerUiTheme.PanelDark;
        private static readonly Color CardColor = CareerUiTheme.Surface;
        private static readonly Color BorderColor = CareerUiTheme.Border;
        private static readonly Color DividerColor = CareerUiTheme.Divider;
        private static readonly Color AccentColor = CareerUiTheme.Primary;
        private static readonly Color BrightAccentColor = CareerUiTheme.PrimaryBright;
        private static readonly Color GreenColor = CareerUiTheme.Success;
        private static readonly Color PurpleColor = CareerUiTheme.PrimaryBright;
        private static readonly Color GoldColor = CareerUiTheme.AccentGold;
        private static readonly Color WarningColor = CareerUiTheme.Warning;
        private static readonly Color PrimaryTextColor = CareerUiTheme.TextPrimary;
        private static readonly Color SecondaryTextColor = CareerUiTheme.TextSecondary;
        private static readonly Color MutedColor = CareerUiTheme.TextMuted;
        private static readonly Color ErrorColor = CareerUiTheme.Error;
        private static readonly Vector2 SharedShellWorkspaceOffset = new(
            0f,
            -(CareerUiTheme.SharedShellChromeHeight * 0.5f + CareerUiTheme.Space2));

        private CareerManager _manager;
        private RectTransform _content;
        private int _selectedOwnedBlockId;
        private int _selectedPlacedBlockId;
        private int _selectedRotation;
        private int _inventoryPage;
        private int _programPage;
        private bool _confirmPlacedBlockRemoval;
        private bool _confirmBoardRedesign;
        private bool _confirmBoardCommit;
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
            _content = CreateRect("Content", root, new Vector2(1920f, 1080f), SharedShellWorkspaceOffset);
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
                SharedShellWorkspaceOffset);
            nextContent.gameObject.SetActive(false);
            _content = nextContent;
            try
            {
                RenderBackgroundAccents();
                RenderGrowthSubNavigation(growth);
                if (_growthSection == GrowthSection.Board)
                    RenderGrowthBoardWorkspace(dashboard, growth);
                else
                    RenderOffseasonActionWorkspace(dashboard, growth);
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

        private void CycleMasterTrainingFocus(CareerGrowthView growth)
        {
            if (!growth.MasterFocusAbility.HasValue)
                return;
            PlayerAbility[] abilities = GetVisibleAbilities(growth.PlayerType);
            int currentIndex = 0;
            for (int index = 0; index < abilities.Length; index++)
            {
                if (abilities[index] == growth.MasterFocusAbility.Value)
                {
                    currentIndex = index;
                    break;
                }
            }
            PlayerAbility next = abilities[(currentIndex + 1) % abilities.Length];
            _manager.SetMasterTrainingFocus(next);
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

        private void CommitBoardForSeason()
        {
            if (!_confirmBoardCommit)
            {
                _confirmBoardCommit = true;
                Render();
                return;
            }

            if (_manager.CommitSkillBoardForSeason())
            {
                _selectedOwnedBlockId = 0;
                _selectedPlacedBlockId = 0;
            }
            _confirmBoardCommit = false;
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
