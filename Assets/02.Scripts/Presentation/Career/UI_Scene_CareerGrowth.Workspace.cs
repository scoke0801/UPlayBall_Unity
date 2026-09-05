using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerGrowth
    {
        /// <summary>
        /// 뽑기 풀 미리보기는 계통·등급마다 나올 수 있는 표준 테트로미노 7종을 모두 보여준다.
        /// 카드 폭 132에 간격 13을 더한 145 간격으로 1160 폭 패널 안에 가운데 정렬한다.
        /// </summary>
        private const int GachaPoolPreviewCount = 7;
        private const float GachaPoolPreviewStepX = 145f;
        private const float GachaPoolPreviewFirstX =
            -GachaPoolPreviewStepX * (GachaPoolPreviewCount - 1) * 0.5f;

        private sealed class InventoryStack
        {
            public GrowthSkillBlockView Block;
            public int Count;
            public bool IsNew;
            public int PlacementCount;
        }

        private void EnsureBoardDraft(CareerGrowthView growth)
        {
            if (_isBoardDraftInitialized)
                return;
            _draftLayout.Clear();
            if (growth.AppliedLayout != null)
                _draftLayout.AddRange(growth.AppliedLayout);
            _isBoardDraftInitialized = true;
            _isBoardDraftDirty = false;
            _confirmBoardApply = false;
        }

        private void ValidateWorkspaceSelection(CareerGrowthView growth)
        {
            if (_selectedOwnedBlockId > 0 &&
                (IsDraftPlaced(_selectedOwnedBlockId) ||
                 FindAnyBlock(growth, _selectedOwnedBlockId).InstanceId == 0))
            {
                _selectedOwnedBlockId = 0;
            }
            if (_selectedPlacedBlockId > 0 && !IsDraftPlaced(_selectedPlacedBlockId))
                _selectedPlacedBlockId = 0;
            int programPageCount = GetProgramPageCount(growth);
            if (_programPage >= programPageCount)
                _programPage = programPageCount - 1;
        }

        private void RenderGrowthSubNavigation(CareerGrowthView growth)
        {
            RectTransform bar = CreateImage(
                "GrowthSubNavigation",
                _content,
                new Color(0.01f, 0.035f, 0.061f, 0.98f),
                new Vector2(1920f, 58f),
                new Vector2(0f, 420f));
            Button board = CreateButton(
                "GrowthBoardTab",
                bar,
                "성장 보드",
                new Vector2(200f, 44f),
                new Vector2(-845f, 0f),
                _growthSection == GrowthSection.Board
                    ? new Color(0.02f, 0.36f, 0.68f, 1f)
                    : PanelDarkColor,
                out _);
            board.onClick.AddListener(() =>
            {
                _growthSection = GrowthSection.Board;
                Render();
            });
            Button actions = CreateButton(
                "OffseasonActionsTab",
                bar,
                "오프시즌 액션",
                new Vector2(210f, 44f),
                new Vector2(-630f, 0f),
                _growthSection == GrowthSection.OffseasonActions
                    ? new Color(0.02f, 0.36f, 0.68f, 1f)
                    : PanelDarkColor,
                out _);
            actions.onClick.AddListener(() =>
            {
                _growthSection = GrowthSection.OffseasonActions;
                Render();
            });
            CreateText(
                "EditState",
                bar,
                _isBoardDraftDirty ? "편집 내용이 아직 적용되지 않았습니다." : "현재 보드가 적용 중입니다.",
                12,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(360f, 32f),
                new Vector2(140f, 0f),
                _isBoardDraftDirty ? GoldColor : SecondaryTextColor);
            Button gacha = CreateButton(
                "OpenGachaOverlay",
                bar,
                "▣  블록 뽑기",
                new Vector2(190f, 44f),
                new Vector2(845f, 0f),
                new Color(0.02f, 0.38f, 0.72f, 1f),
                out _);
            gacha.onClick.AddListener(() =>
            {
                _isGachaOpen = true;
                _isProbabilityOpen = false;
                Render();
            });
        }

        private void RenderGrowthBoardWorkspace(CareerDashboardView dashboard, CareerGrowthView growth)
        {
            RenderCompactPlayerSummary(dashboard, growth);
            RenderDraftBoardPanel(growth);
            RenderBlockInventory(growth);
        }

        private void RenderCompactPlayerSummary(CareerDashboardView dashboard, CareerGrowthView growth)
        {
            RectTransform panel = CreatePanel(
                "PlayerSummary",
                string.Empty,
                "선수 요약",
                new Vector2(320f, 760f),
                new Vector2(-790f, -3f));
            RectTransform identity = CreateFramedSection(
                "Identity",
                panel,
                new Vector2(272f, 104f),
                new Vector2(0f, 248f),
                new Color(0.02f, 0.12f, 0.20f, 1f));
            CreateText(
                "Name",
                identity,
                dashboard.PlayerName,
                24,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(180f, 36f),
                new Vector2(42f, 28f),
                PrimaryTextColor);
            CreateText(
                "Profile",
                identity,
                $"{GetPositionCode(dashboard.Position)} · {dashboard.Age}세 · {dashboard.TeamName}",
                12,
                FontStyle.Normal,
                TextAnchor.MiddleLeft,
                new Vector2(210f, 26f),
                new Vector2(57f, -7f),
                SecondaryTextColor);
            RectTransform overall = CreateImage(
                "Overall",
                identity,
                new Color(0.08f, 0.09f, 0.07f, 0.96f),
                new Vector2(68f, 68f),
                new Vector2(-100f, 2f));
            CreateText(
                "OverallLabel", overall, "OVR", 10, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(60f, 18f), new Vector2(0f, 18f), GoldColor);
            CreateText(
                "OverallValue", overall, dashboard.Overall.ToString(), 28, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(62f, 36f), new Vector2(0f, -8f), GoldColor);

            PlayerAbility[] abilities = GetVisibleAbilities(growth.PlayerType);
            int[] draftBonuses = BuildDraftBonuses(growth);
            for (int index = 0; index < abilities.Length; index++)
            {
                PlayerAbility ability = abilities[index];
                int baseValue = growth.BaseAbilities[(int)ability];
                int value = Math.Min(100, baseValue + draftBonuses[(int)ability]);
                float y = 135f - index * 45f;
                CreateText(
                    "AbilityLabel_" + ability,
                    panel,
                    GetAbilityLabel(ability),
                    12,
                    FontStyle.Bold,
                    TextAnchor.MiddleLeft,
                    new Vector2(82f, 24f),
                    new Vector2(-98f, y),
                    SecondaryTextColor);
                CreateProgressBar(
                    panel,
                    value / 100f,
                    new Vector2(126f, 9f),
                    new Vector2(24f, y),
                    GetRatingColor(value));
                string delta = draftBonuses[(int)ability] > 0
                    ? $"{value}  +{draftBonuses[(int)ability]}"
                    : value.ToString();
                CreateText(
                    "AbilityValue_" + ability,
                    panel,
                    delta,
                    12,
                    FontStyle.Bold,
                    TextAnchor.MiddleRight,
                    new Vector2(64f, 24f),
                    new Vector2(96f, y),
                    draftBonuses[(int)ability] > 0 ? GreenColor : PrimaryTextColor);
            }

            RectTransform condition = CreateFramedSection(
                "Condition",
                panel,
                new Vector2(252f, 48f),
                new Vector2(0f, -123f),
                PanelDarkColor);
            CreateText(
                "ConditionValue",
                condition,
                $"컨디션  {dashboard.Condition}     감독 평가  {dashboard.ManagerEvaluation}",
                13,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(244f, 30f),
                Vector2.zero,
                SecondaryTextColor);

            RectTransform latest = CreateFramedSection(
                "LatestGrowth",
                panel,
                new Vector2(252f, 48f),
                new Vector2(0f, -174f),
                PanelDarkColor);
            CreateText(
                "Title", latest, "최근 성장", 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(100f, 18f), new Vector2(-72f, 14f), AccentColor);
            GrowthResultRecord latestExplainedGrowth = FindLatestExplainedGrowth(growth.RecentGrowth);
            string latestText = latestExplainedGrowth == null
                ? "아직 설명 가능한 성장 기록이 없습니다."
                : $"{GetGrowthSourceLabel(latestExplainedGrowth)} · " +
                  $"{FormatGrowthChanges(latestExplainedGrowth)}\n" +
                  FormatDecisionExplanation(latestExplainedGrowth.Explanation, 1);
            CreateText(
                "Value",
                latest,
                latestText,
                12,
                FontStyle.Normal,
                TextAnchor.MiddleLeft,
                new Vector2(232f, 30f),
                new Vector2(0f, -9f),
                SecondaryTextColor);

            RectTransform role = CreateFramedSection(
                "RoleCompetition",
                panel,
                new Vector2(252f, 84f),
                new Vector2(0f, -243f),
                new Color(0.035f, 0.10f, 0.16f, 1f));
            CreateText(
                "RoleTitle", role,
                $"현재 역할  {GetExpectedRoleLabel(growth.CurrentRole)}",
                13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(232f, 22f), new Vector2(0f, 27f), GoldColor);
            string gap = growth.RoleScore == 0d && growth.CompetitorRoleScore == 0d
                ? "경쟁 점수 산정 전"
                : $"내 점수 {growth.RoleScore:0.0} · 경쟁자 {growth.CompetitorRoleScore:0.0} · " +
                  $"격차 {growth.RoleScore - growth.CompetitorRoleScore:+0.0;-0.0;0.0}";
            CreateText(
                "RoleGap", role, gap, 11, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(232f, 20f), new Vector2(0f, 5f), SecondaryTextColor);
            string protection = growth.WasInjuryReturnProtected
                ? "\n부상 복귀 보호로 역할 하락 보류"
                : growth.WasRoleCooldownProtected
                    ? "\n역할 변경 쿨다운으로 현재 역할 유지"
                    : string.Empty;
            CreateText(
                "RoleReasons", role,
                FormatDecisionExplanation(growth.RoleExplanation, 2) + protection,
                10, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(232f, 34f), new Vector2(0f, -22f), SecondaryTextColor);
        }

        private static GrowthResultRecord FindLatestExplainedGrowth(GrowthResultRecord[] records)
        {
            for (int index = 0; index < (records?.Length ?? 0); index++)
                if (records[index]?.Explanation != null) return records[index];
            return null;
        }

        private void RenderDraftBoardPanel(CareerGrowthView growth)
        {
            _draftPlacementPreviewVisual = null;
            RectTransform panel = CreatePanel(
                "DraftBoard",
                string.Empty,
                "4×4 성장 보드",
                new Vector2(700f, 760f),
                new Vector2(-260f, -3f));
            string guide = !growth.CanEditBoard
                ? "지금은 현재 보드만 열람할 수 있습니다."
                : growth.IsBoardSeasonLocked
                    ? $"시즌 중 교체는 확정 비용 {FormatMoney(growth.InSeasonBoardCommitCost)}이 듭니다. 확정 전까지 경기에는 기존 보드가 적용됩니다."
                    : _selectedOwnedBlockId > 0
                    ? "청록색 칸을 눌러 임시 배치하세요. R 버튼은 회전입니다."
                    : "보관함 카드를 선택하면 배치 가능한 위치를 먼저 계산합니다.";
            CreateText(
                "Guide",
                panel,
                guide,
                12,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(590f, 24f),
                new Vector2(0f, 280f),
                growth.CanEditBoard ? SecondaryTextColor : WarningColor);

            RectTransform board = CreateSection(
                "BoardGrid",
                panel,
                new Vector2(440f, 440f),
                new Vector2(0f, 56f),
                new Color(0.006f, 0.022f, 0.036f, 1f));
            board.gameObject.AddComponent<RectMask2D>();
            const float cellSize = 100f;
            const float gap = 7f;
            float span = growth.BoardWidth * cellSize + (growth.BoardWidth - 1) * gap;
            _placementPreviewImages = new Image[growth.BoardWidth * growth.BoardHeight];
            for (int y = 0; y < growth.BoardHeight; y++)
            {
                for (int x = 0; x < growth.BoardWidth; x++)
                {
                    int instanceId = FindDraftInstanceAt(growth, x, y);
                    GrowthSkillBlockView block = instanceId > 0
                        ? FindAnyBlock(growth, instanceId)
                        : default;
                    int originXAtCursor = x;
                    int originYAtCursor = y;
                    bool canPlace = instanceId == 0 &&
                                    growth.CanEditBoard &&
                                    _selectedOwnedBlockId > 0 &&
                                    TryResolvePlacementOrigin(
                                        growth,
                                        _selectedOwnedBlockId,
                                        _selectedRotation,
                                        x,
                                        y,
                                        out originXAtCursor,
                                        out originYAtCursor);
                    Color cellColor = instanceId > 0
                        ? Color.clear
                        : new Color(0.07f, 0.10f, 0.12f, 1f);
                    float px = -span * 0.5f + cellSize * 0.5f + x * (cellSize + gap);
                    float py = span * 0.5f - cellSize * 0.5f - y * (cellSize + gap);
                    Button cell = CreateButton(
                        $"DraftCell_{x}_{y}",
                        board,
                        string.Empty,
                        new Vector2(cellSize, cellSize),
                        new Vector2(px, py),
                        cellColor,
                        out _);
                    MarkVisual((RectTransform)cell.transform, CareerUiVisualRole.FlatSurface);
                    cell.interactable = instanceId > 0 || canPlace;
                    if (instanceId > 0)
                    {
                        int selectedId = instanceId;
                        cell.onClick.AddListener(() => SelectDraftPlacedBlock(selectedId));
                        DrawDraftBlockEdges(cell.transform, growth, selectedId, x, y, cellSize);
                        if (IsDraftOrigin(selectedId, x, y))
                        {
                            CreateText(
                                "Badge",
                                cell.transform,
                                GetRarityCode(block.Rarity),
                                12,
                                FontStyle.Bold,
                                TextAnchor.UpperLeft,
                                new Vector2(32f, 24f),
                                new Vector2(-31f, 31f),
                                GetRarityFrameColor(block.Rarity));
                        }
                    }
                    else if (canPlace)
                    {
                        int targetX = originXAtCursor;
                        int targetY = originYAtCursor;
                        cell.onClick.AddListener(() => StageSelectedBlock(targetX, targetY, growth));
                    }

                    RectTransform preview = CreateImage(
                        "DraftPreview",
                        cell.transform,
                        Color.clear,
                        Vector2.zero,
                        Vector2.zero,
                        stretch: true);
                    Image previewImage = preview.GetComponent<Image>();
                    previewImage.enabled = false;
                    _placementPreviewImages[y * growth.BoardWidth + x] = previewImage;
                    if (_selectedOwnedBlockId > 0)
                    {
                        int hoverX = x;
                        int hoverY = y;
                        AddPointerListener(
                            cell.gameObject,
                            EventTriggerType.PointerEnter,
                            () => ShowDraftPlacementPreview(
                                board,
                                growth,
                                hoverX,
                                hoverY,
                                span,
                                cellSize,
                                gap));
                        AddPointerListener(
                            cell.gameObject,
                            EventTriggerType.PointerExit,
                            ClearPlacementPreview);
                    }
                }
            }
            RenderDraftBlockVisuals(board, growth, span, cellSize, gap);

            int[] current = growth.BoardBonuses;
            int[] draft = BuildDraftBonuses(growth);
            RectTransform comparison = CreateFramedSection(
                "BonusComparison",
                panel,
                new Vector2(640f, 88f),
                new Vector2(0f, -192f),
                PanelDarkColor);
            CreateText(
                "Current",
                comparison,
                "현재 효과\n" + FormatBoardBonuses(current, growth.PlayerType),
                12,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(285f, 50f),
                new Vector2(-162f, 8f),
                SecondaryTextColor);
            CreateText(
                "Draft",
                comparison,
                "변경 후\n" + FormatBoardBonusDifference(current, draft, growth.PlayerType),
                12,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(285f, 50f),
                new Vector2(162f, 8f),
                _isBoardDraftDirty ? GreenColor : SecondaryTextColor);

            bool requiresRecovery = DraftRequiresSafeRecovery(growth);
            string costText = growth.IsBoardSeasonLocked
                ? $"시즌 중 확정 {FormatMoney(growth.InSeasonBoardCommitCost)}" +
                  (growth.InSeasonBoardCommitCount > 0
                      ? $" (이번 시즌 {growth.InSeasonBoardCommitCount}회 교체)"
                      : string.Empty)
                : requiresRecovery
                    ? $"안전 회수 {FormatMoney(growth.BoardRedesignCost)}"
                    : "추가 비용 없음";
            CreateText(
                "ApplyCost",
                panel,
                costText,
                10,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(220f, 18f),
                new Vector2(0f, -220f),
                requiresRecovery || growth.IsBoardSeasonLocked ? GoldColor : MutedColor);
            Button cancel = CreateButton(
                "CancelBoardDraft",
                panel,
                "편집 취소",
                new Vector2(135f, 48f),
                new Vector2(-210f, -268f),
                new Color(0.13f, 0.18f, 0.22f, 1f),
                out _);
            cancel.interactable = _isBoardDraftDirty;
            cancel.onClick.AddListener(() => ResetBoardDraft(growth));
            Button clear = CreateButton(
                "ClearBoardDraft",
                panel,
                "초기화",
                new Vector2(135f, 48f),
                new Vector2(-65f, -268f),
                new Color(0.28f, 0.16f, 0.08f, 1f),
                out _);
            clear.interactable = growth.CanEditBoard && _draftLayout.Count > 0;
            clear.onClick.AddListener(ClearBoardDraft);
            Button apply = CreateButton(
                "ApplyBoardDraft",
                panel,
                _confirmBoardApply ? "변경 적용 확정" : "변경 적용",
                new Vector2(260f, 46f),
                new Vector2(155f, -268f),
                _confirmBoardApply
                    ? new Color(0.52f, 0.30f, 0.04f, 1f)
                    : new Color(0.02f, 0.38f, 0.72f, 1f),
                out _);
            apply.interactable = growth.CanEditBoard && _isBoardDraftDirty &&
                                 (growth.IsBoardSeasonLocked ||
                                  !requiresRecovery ||
                                  growth.CanRedesignBoard);
            apply.onClick.AddListener(() => ApplyBoardDraft(growth));
        }

        private void RenderBlockInventory(CareerGrowthView growth)
        {
            RectTransform panel = CreatePanel(
                "BlockInventory",
                string.Empty,
                "블록 보관함",
                new Vector2(800f, 760f),
                new Vector2(500f, -3f));
            List<InventoryStack> stacks = BuildInventoryStacks(growth);
            int totalFree = CountDraftInventoryBlocks(growth);
            CreateText(
                "Summary",
                panel,
                $"보유 {stacks.Count}종 / 총 {totalFree}개    장착 {_draftLayout.Count}개    신규 {growth.LastPulledBlocks.Length}개",
                12,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                new Vector2(390f, 24f),
                new Vector2(175f, 300f),
                SecondaryTextColor);

            CreateFramedSection(
                "InventoryFilters",
                panel,
                new Vector2(752f, 84f),
                new Vector2(0f, 233f),
                PanelDarkColor);
            RenderInventoryCategoryFilters(panel, growth);
            RenderInventoryRarityFilters(panel);
            Button newOnly = CreateButton(
                "NewOnlyFilter",
                panel,
                "신규",
                new Vector2(78f, 30f),
                new Vector2(112f, 214f),
                _inventoryNewOnly
                    ? new Color(0.10f, 0.39f, 0.22f, 1f)
                    : PanelDarkColor,
                out Text newOnlyLabel);
            newOnlyLabel.fontSize = 11;
            newOnly.onClick.AddListener(() =>
            {
                _inventoryNewOnly = !_inventoryNewOnly;
                _inventoryPage = 0;
                Render();
            });
            Button placeable = CreateButton(
                "PlaceableOnlyFilter",
                panel,
                "현재 보드 배치 가능만",
                new Vector2(185f, 30f),
                new Vector2(282f, 214f),
                _inventoryPlaceableOnly
                    ? new Color(0.04f, 0.38f, 0.31f, 1f)
                    : PanelDarkColor,
                out Text placeableLabel);
            placeableLabel.fontSize = 11;
            placeable.onClick.AddListener(() =>
            {
                _inventoryPlaceableOnly = !_inventoryPlaceableOnly;
                _inventoryPage = 0;
                Render();
            });

            int pageCount = Math.Max(1, (stacks.Count + InventoryPageSize - 1) / InventoryPageSize);
            if (_inventoryPage >= pageCount)
                _inventoryPage = pageCount - 1;
            int pageStart = _inventoryPage * InventoryPageSize;
            int visibleCount = Math.Min(InventoryPageSize, stacks.Count - pageStart);
            for (int index = 0; index < visibleCount; index++)
                RenderInventoryStackCard(panel, stacks[pageStart + index], index);

            Button previous = CreateButton(
                "InventoryPreviousPage",
                panel,
                "‹",
                new Vector2(34f, 28f),
                new Vector2(260f, -102f),
                PanelDarkColor,
                out _);
            previous.interactable = _inventoryPage > 0;
            previous.onClick.AddListener(ShowNewerInventoryPage);
            CreateText(
                "Page",
                panel,
                $"{_inventoryPage + 1} / {pageCount}",
                11,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(65f, 28f),
                new Vector2(306f, -102f),
                SecondaryTextColor);
            Button next = CreateButton(
                "InventoryNextPage",
                panel,
                "›",
                new Vector2(34f, 28f),
                new Vector2(352f, -102f),
                PanelDarkColor,
                out _);
            next.interactable = _inventoryPage < pageCount - 1;
            next.onClick.AddListener(ShowOlderInventoryPage);
            RenderWorkspaceSelectedBlock(panel, growth);
        }

        private void RenderInventoryCategoryFilters(RectTransform panel, CareerGrowthView growth)
        {
            SkillBlockCategory[] categories = GetWorkspaceCategories(growth.PlayerType);
            Button all = CreateButton(
                "InventoryCategoryAll",
                panel,
                "전체",
                new Vector2(70f, 30f),
                new Vector2(-344f, 252f),
                !_inventoryCategory.HasValue ? new Color(0.02f, 0.36f, 0.68f, 1f) : PanelDarkColor,
                out Text allLabel);
            allLabel.fontSize = 11;
            all.onClick.AddListener(() => SetInventoryCategory(null));
            for (int index = 0; index < categories.Length; index++)
            {
                SkillBlockCategory category = categories[index];
                Button filter = CreateButton(
                    "InventoryCategory_" + category,
                    panel,
                    GetCategoryShortLabel(category),
                    new Vector2(78f, 30f),
                    new Vector2(-264f + index * 84f, 252f),
                    _inventoryCategory == category
                        ? Color.Lerp(PanelDarkColor, GetCategoryColor(category), 0.7f)
                        : PanelDarkColor,
                    out Text label);
                label.fontSize = 11;
                filter.onClick.AddListener(() => SetInventoryCategory(category));
            }
        }

        private void RenderInventoryRarityFilters(RectTransform panel)
        {
            SkillBlockRarity[] rarities = GetRarities();
            for (int index = 0; index < rarities.Length; index++)
            {
                SkillBlockRarity rarity = rarities[index];
                Button filter = CreateButton(
                    "InventoryRarity_" + rarity,
                    panel,
                    GetRarityCode(rarity),
                    new Vector2(44f, 30f),
                    new Vector2(-344f + index * 50f, 214f),
                    _inventoryRarity == rarity
                        ? Color.Lerp(PanelDarkColor, GetRarityFrameColor(rarity), 0.62f)
                        : PanelDarkColor,
                    out Text label);
                label.fontSize = 12;
                label.color = GetRarityFrameColor(rarity);
                filter.onClick.AddListener(() => SetInventoryRarity(rarity));
            }
        }

        private void RenderInventoryStackCard(RectTransform panel, InventoryStack stack, int index)
        {
            const float width = 178f;
            const float height = 112f;
            int column = index % 4;
            int row = index / 4;
            float x = -285f + column * 190f;
            float y = 138f - row * 112f;
            GrowthSkillBlockView block = stack.Block;
            bool selected = block.InstanceId == _selectedOwnedBlockId;
            Color frame = GetRarityFrameColor(block.Rarity);
            Button card = CreateButton(
                "InventoryBlock_" + block.InstanceId,
                panel,
                string.Empty,
                new Vector2(width, height),
                new Vector2(x, y),
                selected ? Color.Lerp(PanelDarkColor, frame, 0.34f) : PanelDarkColor,
                out _);
            int instanceId = block.InstanceId;
            card.onClick.AddListener(() => SelectWorkspaceOwnedBlock(instanceId));
            RenderTetromino(
                card.transform,
                block.ShapeCells,
                0,
                GetCategoryColor(block.Category),
                new Vector2(-16f, 8f),
                new Vector2(76f, 64f),
                19f,
                "InventoryShape");
            CreateText(
                "Badge",
                card.transform,
                GetRarityCode(block.Rarity),
                12,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(28f, 24f),
                new Vector2(-42f, 39f),
                frame);
            CreateText(
                "Count",
                card.transform,
                stack.Count > 1 ? $"×{stack.Count}" : string.Empty,
                11,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                new Vector2(45f, 20f),
                new Vector2(60f, 39f),
                PrimaryTextColor);
            CreateText(
                "Bonus",
                card.transform,
                FormatAbilityChanges(block.AbilityBonuses),
                11,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(72f, 34f),
                new Vector2(50f, 9f),
                GetCategoryColor(block.Category));
            string state = stack.PlacementCount > 0
                ? $"{block.CellCount}칸 · 배치 {stack.PlacementCount}곳"
                : $"{block.CellCount}칸 · 배치 불가";
            CreateText(
                "State",
                card.transform,
                state,
                9,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(150f, 20f),
                new Vector2(10f, -38f),
                stack.PlacementCount > 0 ? SecondaryTextColor : MutedColor);
            if (block.IsLocked)
            {
                CreateText(
                    "Locked", card.transform, "LOCK", 8, FontStyle.Bold, TextAnchor.MiddleRight,
                    new Vector2(42f, 18f), new Vector2(60f, -39f), GoldColor);
            }
            if (stack.IsNew)
            {
                CreateText(
                    "New", card.transform, "NEW", 8, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(30f, 14f), new Vector2(15f, 39f), GreenColor);
            }
        }

        private void RenderWorkspaceSelectedBlock(RectTransform panel, CareerGrowthView growth)
        {
            RectTransform detail = CreateFramedSection(
                "SelectedBlockDetail",
                panel,
                new Vector2(720f, 144f),
                new Vector2(0f, -214f),
                new Color(0.008f, 0.035f, 0.055f, 1f));
            if (_selectedOwnedBlockId <= 0 && _selectedPlacedBlockId <= 0)
            {
                CreateText(
                    "Empty",
                    detail,
                    "블록을 선택하면 형태·보너스·배치 가능 위치를 확인할 수 있습니다.",
                    13,
                    FontStyle.Normal,
                    TextAnchor.MiddleCenter,
                    new Vector2(650f, 50f),
                    Vector2.zero,
                    SecondaryTextColor);
                return;
            }

            int instanceId = _selectedOwnedBlockId > 0 ? _selectedOwnedBlockId : _selectedPlacedBlockId;
            GrowthSkillBlockView block = FindAnyBlock(growth, instanceId);
            RenderTetromino(
                detail,
                block.ShapeCells,
                _selectedOwnedBlockId > 0 ? _selectedRotation : GetDraftRotation(instanceId),
                GetCategoryColor(block.Category),
                new Vector2(-305f, 0f),
                new Vector2(120f, 116f),
                27f,
                "SelectedShape");
            CreateText(
                "Name",
                detail,
                $"{GetRarityLabel(block.Rarity)} · {GetCategoryLabel(block.Category)} 블록",
                16,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(310f, 28f),
                new Vector2(-65f, 48f),
                GetRarityFrameColor(block.Rarity));
            int placementCount = CountDraftPlacements(growth, block);
            CreateText(
                "Info",
                detail,
                $"{FormatAbilityChanges(block.AbilityBonuses)} / {block.CellCount}칸 / " +
                $"{(block.CanRotate ? "회전 가능" : "회전 불가")}\n" +
                $"현재 보드에 배치 가능한 위치 {placementCount}곳",
                12,
                FontStyle.Normal,
                TextAnchor.MiddleLeft,
                new Vector2(400f, 58f),
                new Vector2(-20f, 6f),
                SecondaryTextColor);
            Button lockButton = CreateButton(
                "ToggleBlockLock",
                detail,
                block.IsLocked ? "잠금 해제" : "잠금",
                new Vector2(105f, 40f),
                new Vector2(15f, -42f),
                block.IsLocked ? new Color(0.42f, 0.31f, 0.07f, 1f) : PanelDarkColor,
                out _);
            lockButton.onClick.AddListener(() => ToggleSelectedBlockLock(block));
            if (_selectedOwnedBlockId > 0)
            {
                Button sell = CreateButton(
                    "SellSelectedBlock",
                    detail,
                    $"판매 {FormatMoney(block.SellValue)}",
                    new Vector2(150f, 40f),
                    new Vector2(145f, -42f),
                    new Color(0.28f, 0.16f, 0.08f, 1f),
                    out _);
                bool isActuallyOwned = IsActuallyOwned(growth, block.InstanceId);
                sell.interactable = !block.IsLocked && isActuallyOwned;
                if (!isActuallyOwned)
                    sell.GetComponentInChildren<Text>().text = "적용 후 판매";
                sell.onClick.AddListener(SellSelectedOwnedBlock);
                Button rotate = CreateButton(
                    "RotateSelectedBlock",
                    detail,
                    $"회전 {_selectedRotation * 90}°",
                    new Vector2(115f, 40f),
                    new Vector2(280f, -42f),
                    new Color(0.03f, 0.24f, 0.42f, 1f),
                    out _);
                rotate.interactable = growth.CanEditBoard && block.CanRotate;
                rotate.onClick.AddListener(RotateSelectedBlock);
            }
            else
            {
                Button recover = CreateButton(
                    "RecoverPlacedBlock",
                    detail,
                    "보관함으로 회수",
                    new Vector2(230f, 40f),
                    new Vector2(230f, -42f),
                    new Color(0.42f, 0.25f, 0.04f, 1f),
                    out _);
                recover.interactable = growth.CanEditBoard;
                recover.onClick.AddListener(() => StageRecoverBlock(instanceId));
            }
        }

        private void RenderOffseasonActionWorkspace(
            CareerDashboardView dashboard,
            CareerGrowthView growth)
        {
            RenderCompactPlayerSummary(dashboard, growth);
            RectTransform panel = CreatePanel(
                "OffseasonActionWorkspace",
                string.Empty,
                "오프시즌 액션",
                new Vector2(1480f, 760f),
                new Vector2(180f, -3f));
            string phase = growth.IsOffseason
                ? $"남은 기간 {growth.RemainingWeeks}주 · 현재 {growth.CurrentWeek}주차"
                : "정규 시즌 중 · 다음 오프시즌 계획을 미리 확인할 수 있습니다.";
            const float summaryRowY = 280f;
            CreateText(
                "Phase",
                panel,
                phase,
                14,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(620f, 28f),
                new Vector2(-380f, summaryRowY),
                growth.IsOffseason ? GoldColor : WarningColor);
            string milestoneStatus = growth.MasterFocusAbility.HasValue
                ? $"집중 {GetAbilityLabel(growth.MasterFocusAbility.Value)} · " +
                  $"반복 면제 {growth.RepetitionPenaltyWaivers}회 · " +
                  $"다음 시즌 부상 위험 -{growth.NextSeasonInjuryRiskReduction:P0}"
                : growth.RepetitionPenaltyWaivers > 0
                    ? $"반복 페널티 면제 {growth.RepetitionPenaltyWaivers}회 · " +
                      $"다음 시즌 부상 위험 -{growth.NextSeasonInjuryRiskReduction:P0}"
                    : $"다음 시즌 부상 위험 -{growth.NextSeasonInjuryRiskReduction:P0}";
            CreateText(
                "EconomyGuide",
                panel,
                $"현재 역할 {GetExpectedRoleLabel(growth.CurrentRole)} · " +
                $"경쟁 격차 {growth.RoleScore - growth.CompetitorRoleScore:+0.0;-0.0;0.0} · " +
                milestoneStatus,
                12,
                FontStyle.Normal,
                TextAnchor.MiddleRight,
                new Vector2(430f, 28f),
                new Vector2(245f, summaryRowY),
                SecondaryTextColor);
            if (growth.MasterFocusAbility.HasValue)
            {
                Button focus = CreateButton(
                    "MasterFocus",
                    panel,
                    $"집중 능력 변경  {GetAbilityLabel(growth.MasterFocusAbility.Value)}",
                    new Vector2(250f, 34f),
                    new Vector2(590f, summaryRowY),
                    new Color(0.19f, 0.16f, 0.34f, 1f),
                    out Text focusLabel);
                focusLabel.fontSize = 11;
                focus.interactable = growth.IsOffseason && !growth.IsActivityInProgress;
                focus.onClick.AddListener(() => CycleMasterTrainingFocus(growth));
            }

            int programPageCount = GetProgramPageCount(growth);
            RenderProgramPageNavigation(panel, programPageCount);
            GrowthProgramView[] programs = GetFeaturedPrograms(growth, _programPage);
            for (int index = 0; index < programs.Length; index++)
                RenderWorkspaceProgramCard(
                    panel,
                    growth,
                    programs[index],
                    index,
                    programs.Length);

            GrowthProgramView selected = FindSelectedProgram(growth);
            RectTransform plan = CreateSection(
                "ActionPlan",
                panel,
                new Vector2(1400f, 155f),
                new Vector2(0f, -215f),
                PanelDarkColor);
            string preview = growth.PlannedActivities.Length > 0
                ? $"계획 {growth.PlannedActivities.Length}개 · {growth.PlannedWeeks}주 · " +
                  $"{FormatMoney(growth.PlannedCost)} · 예상 컨디션 {growth.ProjectedConditionAfterPlan}"
                : selected.ProgramId == null
                    ? "액션 카드를 선택하면 기간·비용·예상 변화가 표시됩니다."
                    : BuildProgramPreview(selected);
            CreateText(
                "Preview",
                plan,
                preview,
                13,
                FontStyle.Normal,
                TextAnchor.MiddleLeft,
                new Vector2(850f, 75f),
                new Vector2(-245f, 20f),
                SecondaryTextColor);
            RenderWorkspaceActionButtons(plan, growth, selected);
            if (!string.IsNullOrEmpty(_manager.LastError))
            {
                CreateText(
                    "Error",
                    panel,
                    _manager.LastError,
                    12,
                    FontStyle.Normal,
                    TextAnchor.MiddleCenter,
                    new Vector2(1300f, 25f),
                    new Vector2(0f, -310f),
                    ErrorColor);
            }
        }

        private void RenderWorkspaceProgramCard(
            RectTransform panel,
            CareerGrowthView growth,
            GrowthProgramView program,
            int index,
            int programCount)
        {
            float pitch = 1340f / Math.Max(1, programCount);
            float cardWidth = Math.Min(255f, pitch - 16f);
            float contentWidth = Math.Max(160f, cardWidth - 35f);
            float x = (index - (programCount - 1) * 0.5f) * pitch;
            bool selected = string.Equals(growth.SelectedProgramId, program.ProgramId, StringComparison.Ordinal);
            Button card = CreateButton(
                "WorkspaceProgram_" + program.ProgramId,
                panel,
                string.Empty,
                new Vector2(cardWidth, 360f),
                new Vector2(x, 60f),
                selected
                    ? Color.Lerp(GetProgramColor(program.ActivityType), Color.white, 0.20f)
                    : GetProgramColor(program.ActivityType),
                out _);
            string programId = program.ProgramId;
            card.interactable = growth.IsOffseason;
            card.onClick.AddListener(() => OpenActivityConfirmation(programId));
            CreateText(
                "Name", card.transform, GetProgramLabel(program.ProgramId), 17,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(contentWidth, 55f),
                new Vector2(0f, 128f), PrimaryTextColor);
            CreateText(
                "Cost", card.transform,
                $"{program.DurationWeeks}주\n{FormatMoney(program.MoneyCost)}",
                15, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(contentWidth, 60f),
                new Vector2(0f, 35f), GoldColor);
            CreateText(
                "Growth", card.transform, FormatProgramAbilities(program), 14,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(contentWidth, 70f),
                new Vector2(0f, -45f), GreenColor);
            CreateText(
                "Condition", card.transform,
                $"컨디션 {program.ConditionBefore} → {program.ConditionAfter}",
                11, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(contentWidth, 26f),
                new Vector2(0f, -110f), SecondaryTextColor);
            CreateText(
                "Fit", card.transform, "적합도 " + GetFitLabel(program.Fit), 12,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(contentWidth, 26f),
                new Vector2(0f, -145f), GetFitColor(program.Fit));
            string availability = program.CanSelect
                ? $"역할 점수 최대 +{program.EstimatedRoleScoreGain:0.0}"
                : program.UnavailableReason;
            CreateText(
                "Availability", card.transform, availability, 10,
                FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(contentWidth, 28f),
                new Vector2(0f, -168f), program.CanSelect ? AccentColor : WarningColor);
        }

        private void RenderProgramPageNavigation(RectTransform panel, int pageCount)
        {
            Button previous = CreateButton(
                "PreviousProgramPage",
                panel,
                "‹",
                new Vector2(42f, 30f),
                new Vector2(-75f, 263f),
                PanelDarkColor,
                out Text previousLabel);
            previousLabel.fontSize = 20;
            previous.interactable = _programPage > 0;
            previous.onClick.AddListener(() =>
            {
                _programPage--;
                Render();
            });
            CreateText(
                "ProgramPage",
                panel,
                $"훈련 메뉴 {_programPage + 1} / {pageCount}",
                11,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(100f, 30f),
                new Vector2(0f, 263f),
                SecondaryTextColor);
            Button next = CreateButton(
                "NextProgramPage",
                panel,
                "›",
                new Vector2(42f, 30f),
                new Vector2(75f, 263f),
                PanelDarkColor,
                out Text nextLabel);
            nextLabel.fontSize = 20;
            next.interactable = _programPage + 1 < pageCount;
            next.onClick.AddListener(() =>
            {
                _programPage++;
                Render();
            });
        }

        private void RenderWorkspaceActionButtons(
            RectTransform parent,
            CareerGrowthView growth,
            GrowthProgramView selected)
        {
            string executeLabel;
            bool canExecute;
            if (!growth.IsOffseason)
            {
                executeLabel = "오프시즌 전용";
                canExecute = false;
            }
            else if (growth.RemainingWeeks == 0 && !growth.IsActivityInProgress)
            {
                executeLabel = "남은 주차 없음";
                canExecute = false;
            }
            else if (growth.PlannedActivities.Length > 0)
            {
                executeLabel = $"성장 계획 {growth.PlannedActivities.Length}개 실행";
                canExecute = true;
            }
            else
            {
                executeLabel = selected.ProgramId == null
                    ? "진행할 액션을 선택하세요"
                    : $"{GetProgramLabel(selected.ProgramId)} 실행";
                canExecute = growth.IsActivityInProgress || selected.ProgramId != null && selected.CanSelect;
            }
            Button execute = CreateButton(
                "ExecuteWorkspaceActivity",
                parent,
                executeLabel,
                new Vector2(260f, 52f),
                new Vector2(335f, -40f),
                new Color(0.02f, 0.38f, 0.72f, 1f),
                out Text executeText);
            executeText.fontSize = 14;
            execute.interactable = canExecute;
            execute.onClick.AddListener(() =>
            {
                if (growth.PlannedActivities.Length > 0)
                    _manager.ExecuteGrowthPlan();
                else
                    _manager.ExecuteSelectedGrowthProgram();
            });

            string completeLabel = growth.RemainingWeeks > 0
                ? $"{growth.RemainingWeeks}주 포기 · 스프링캠프"
                : "스프링캠프 진행";
            Button complete = CreateButton(
                "CompleteWorkspaceOffseason",
                parent,
                completeLabel,
                new Vector2(200f, 52f),
                new Vector2(570f, -40f),
                new Color(0.42f, 0.25f, 0.04f, 1f),
                out Text completeText);
            completeText.fontSize = 12;
            complete.interactable = growth.CanCompleteOffseason;
            complete.onClick.AddListener(CompleteOffseason);
        }

        private void RenderGachaOverlay(CareerDashboardView dashboard, CareerGrowthView growth)
        {
            RectTransform blocker = CreateImage(
                "GachaModalBlocker",
                _content,
                new Color(0f, 0.01f, 0.02f, 0.86f),
                Vector2.zero,
                Vector2.zero,
                stretch: true);
            Button dismiss = blocker.gameObject.AddComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(CloseGachaOverlay);
            RectTransform panel = CreatePanel(
                "GrowthGachaOverlay",
                string.Empty,
                "블록 뽑기",
                new Vector2(1240f, 790f),
                Vector2.zero);
            Button close = CreateButton(
                "CloseGachaOverlay",
                panel,
                "×",
                new Vector2(46f, 40f),
                new Vector2(580f, 354f),
                PanelDarkColor,
                out Text closeLabel);
            closeLabel.fontSize = 24;
            close.onClick.AddListener(CloseGachaOverlay);
            CreateText(
                "OwnedBlocks",
                panel,
                $"보유 블록  {CountOwnedBlockKinds(growth)}종 / " +
                $"{growth.OwnedBlocks.Length + growth.PlacedBlocks.Length}개",
                12,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                new Vector2(270f, 28f),
                new Vector2(120f, 354f),
                SecondaryTextColor);
            CreateText(
                "OwnedMoney",
                panel,
                $"보유 금액  {FormatMoney(dashboard.AvailableMoney)}",
                14,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                new Vector2(280f, 28f),
                new Vector2(410f, 354f),
                GoldColor);

            SkillGachaPurchaseTier[] tiers = GetGachaTiers();
            for (int index = 0; index < tiers.Length; index++)
                RenderGachaTierCard(panel, growth, dashboard.AvailableMoney, tiers[index], index);
            GrowthGachaOfferView offer = FindGachaOffer(growth, _selectedGachaTier);

            RectTransform detail = CreateSection(
                "GachaSelection",
                panel,
                new Vector2(1160f, 250f),
                new Vector2(0f, -30f),
                PanelDarkColor);
            CreateText(
                "Selection",
                detail,
                $"선택 등급: {GetGachaTierLabel(offer.Tier)}",
                18,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(300f, 32f),
                new Vector2(-410f, 92f),
                GetRarityFrameColor(offer.MinimumRarity));
            string guarantee = offer.IsUnlocked
                ? $"{GetRarityLabel(offer.MinimumRarity)} 이상 100% 보장 · 상위 등급 획득 가능"
                : offer.UnavailableReason;
            CreateText(
                "Guarantee",
                detail,
                guarantee + "\n기간 소모 없이 자금만 사용합니다.",
                13,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(590f, 55f),
                new Vector2(-265f, 48f),
                offer.IsUnlocked ? SecondaryTextColor : WarningColor);
            RenderGachaCategoryFilters(detail, growth);
            RenderGachaPoolPreview(detail, growth, offer);
            Button probability = CreateButton(
                "ToggleGachaProbability",
                detail,
                _isProbabilityOpen ? "확률 닫기" : "상세 확률 보기",
                new Vector2(150f, 34f),
                new Vector2(475f, 92f),
                PanelDarkColor,
                out Text probabilityLabel);
            probabilityLabel.fontSize = 11;
            probability.onClick.AddListener(() =>
            {
                _isProbabilityOpen = !_isProbabilityOpen;
                Render();
            });
            if (_isProbabilityOpen)
            {
                CreateText(
                    "ProbabilityTable",
                    detail,
                    FormatGachaProbability(offer),
                    11,
                    FontStyle.Bold,
                    TextAnchor.MiddleRight,
                    new Vector2(520f, 28f),
                    new Vector2(270f, 48f),
                    SecondaryTextColor);
            }

            RectTransform payment = CreateSection(
                "GachaPayment",
                panel,
                new Vector2(1160f, 150f),
                new Vector2(0f, -250f),
                new Color(0.008f, 0.035f, 0.055f, 1f));
            long afterOne = Math.Max(0L, dashboard.AvailableMoney - offer.Price);
            int affordableCount = offer.Price <= 0L
                ? 0
                : (int)Math.Min(int.MaxValue, dashboard.AvailableMoney / offer.Price);
            if (offer.MaxPurchasesPerOffseason > 0)
                affordableCount = Math.Min(affordableCount, offer.RemainingPurchases);
            CreateText(
                "PaymentInfo",
                payment,
                $"보유 금액      {FormatMoney(dashboard.AvailableMoney)}\n" +
                $"1회 결제     -{FormatMoney(offer.Price)}\n" +
                $"결제 후        {FormatMoney(afterOne)}",
                13,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(330f, 100f),
                new Vector2(-390f, 0f),
                SecondaryTextColor);
            string availability = offer.IsUnlocked
                ? offer.MaxPurchasesPerOffseason > 0
                    ? $"오프시즌 잔여 {offer.RemainingPurchases}회 · 현재 자금으로 {affordableCount}회"
                    : $"현재 자금으로 구매 가능 {affordableCount}회"
                : offer.UnavailableReason;
            CreateText(
                "Availability",
                payment,
                availability,
                12,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(390f, 50f),
                new Vector2(0f, 0f),
                offer.IsUnlocked ? GoldColor : WarningColor);
            Button one = CreateButton(
                "GachaBuyOne",
                payment,
                $"1회 뽑기\n{FormatMoney(offer.Price)}",
                new Vector2(180f, 70f),
                new Vector2(325f, 0f),
                new Color(0.02f, 0.38f, 0.72f, 1f),
                out Text oneLabel);
            oneLabel.fontSize = 14;
            one.interactable = offer.CanPurchaseOne;
            one.onClick.AddListener(() => PurchaseFromGacha(1));
            Button five = CreateButton(
                "GachaBuyFive",
                payment,
                offer.MaxPurchasesPerOffseason > 0
                    ? "5회 뽑기\n구매 제한 상품"
                    : $"5회 뽑기  {offer.FivePullDiscountRate:P0} 할인\n{FormatMoney(offer.FivePullPrice)}",
                new Vector2(155f, 70f),
                new Vector2(497f, 0f),
                new Color(0.22f, 0.18f, 0.42f, 1f),
                out Text fiveLabel);
            fiveLabel.fontSize = 11;
            five.interactable = offer.CanPurchaseFive;
            five.onClick.AddListener(() => PurchaseFromGacha(5));
        }

        private void RenderGachaTierCard(
            RectTransform panel,
            CareerGrowthView growth,
            long money,
            SkillGachaPurchaseTier tier,
            int index)
        {
            GrowthGachaOfferView offer = FindGachaOffer(growth, tier);
            float x = -464f + index * 232f;
            bool selected = tier == _selectedGachaTier;
            Color frame = GetRarityFrameColor(offer.MinimumRarity);
            Button card = CreateButton(
                "GachaTier_" + tier,
                panel,
                string.Empty,
                new Vector2(212f, 175f),
                new Vector2(x, 205f),
                selected ? Color.Lerp(PanelDarkColor, frame, 0.34f) : PanelDarkColor,
                out _);
            card.onClick.AddListener(() =>
            {
                _selectedGachaTier = tier;
                _isProbabilityOpen = false;
                Render();
            });
            CreateText(
                "Name",
                card.transform,
                GetGachaTierLabel(tier),
                15,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(180f, 30f),
                new Vector2(0f, 52f),
                PrimaryTextColor);
            CreateText(
                "Price",
                card.transform,
                FormatMoney(offer.Price),
                16,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(180f, 32f),
                new Vector2(0f, 8f),
                GoldColor);
            string status;
            Color statusColor;
            if (!offer.IsUnlocked)
            {
                status = "잠금";
                statusColor = WarningColor;
            }
            else if (offer.RemainingPurchases != int.MaxValue && offer.RemainingPurchases <= 0)
            {
                status = "구매 완료";
                statusColor = MutedColor;
            }
            else if (money < offer.Price)
            {
                status = $"{FormatMoney(offer.Price - money)} 부족";
                statusColor = ErrorColor;
            }
            else
            {
                int count = (int)Math.Min(99L, money / offer.Price);
                if (offer.RemainingPurchases != int.MaxValue)
                    count = Math.Min(count, offer.RemainingPurchases);
                status = $"구매 가능 {count}회";
                statusColor = GreenColor;
            }
            CreateText(
                "Status",
                card.transform,
                status,
                11,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(180f, 24f),
                new Vector2(0f, -38f),
                statusColor);
            string limit = offer.MaxPurchasesPerOffseason > 0
                ? $"오프시즌 {offer.MaxPurchasesPerOffseason}회 제한"
                : $"{GetRarityLabel(offer.MinimumRarity)} 이상 확정";
            CreateText(
                "Limit",
                card.transform,
                limit,
                10,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(190f, 22f),
                new Vector2(0f, -67f),
                SecondaryTextColor);
        }

        private void RenderGachaCategoryFilters(RectTransform detail, CareerGrowthView growth)
        {
            SkillBlockCategory[] categories = GetWorkspaceCategories(growth.PlayerType);
            CreateText(
                "CategoryLabel", detail, "카테고리", 11, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(90f, 22f), new Vector2(-500f, -8f), SecondaryTextColor);
            Button all = CreateButton(
                "GachaCategoryAll",
                detail,
                "전체",
                new Vector2(80f, 34f),
                new Vector2(-390f, -8f),
                !_selectedGachaCategory.HasValue
                    ? new Color(0.02f, 0.36f, 0.68f, 1f)
                    : CardColor,
                out Text allLabel);
            allLabel.fontSize = 11;
            all.onClick.AddListener(() =>
            {
                _selectedGachaCategory = null;
                Render();
            });
            for (int index = 0; index < categories.Length; index++)
            {
                SkillBlockCategory category = categories[index];
                Button filter = CreateButton(
                    "GachaCategory_" + category,
                    detail,
                    GetCategoryLabel(category),
                    new Vector2(105f, 34f),
                    new Vector2(-290f + index * 115f, -8f),
                    _selectedGachaCategory == category
                        ? Color.Lerp(CardColor, GetCategoryColor(category), 0.72f)
                        : CardColor,
                    out Text label);
                label.fontSize = 11;
                filter.onClick.AddListener(() =>
                {
                    _selectedGachaCategory = category;
                    Render();
                });
            }
        }

        private void RenderGachaPoolPreview(
            RectTransform detail,
            CareerGrowthView growth,
            GrowthGachaOfferView offer)
        {
            int shown = 0;
            for (int index = 0; index < growth.GachaPool.Length && shown < GachaPoolPreviewCount; index++)
            {
                GrowthGachaPoolItemView item = growth.GachaPool[index];
                if (item.Rarity != offer.MinimumRarity ||
                    _selectedGachaCategory.HasValue && item.Category != _selectedGachaCategory.Value)
                {
                    continue;
                }
                float x = GachaPoolPreviewFirstX + shown * GachaPoolPreviewStepX;
                RectTransform card = CreateImage(
                    "GachaPoolPreview_" + shown,
                    detail,
                    CardColor,
                    new Vector2(132f, 82f),
                    new Vector2(x, -76f));
                RenderTetromino(
                    card,
                    item.ShapeCells,
                    0,
                    GetCategoryColor(item.Category),
                    new Vector2(-31f, 5f),
                    new Vector2(62f, 58f),
                    15f,
                    "PoolShape");
                CreateText(
                    "Category",
                    card,
                    GetCategoryShortLabel(item.Category),
                    10,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    new Vector2(60f, 24f),
                    new Vector2(31f, 16f),
                    GetCategoryColor(item.Category));
                CreateText(
                    "Bonus",
                    card,
                    FormatAbilityChanges(item.AbilityBonuses),
                    9,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    new Vector2(65f, 30f),
                    new Vector2(30f, -17f),
                    PrimaryTextColor);
                shown++;
            }
        }

        private void PurchaseFromGacha(int count)
        {
            if (!_manager.PurchaseSkillBlocks(_selectedGachaCategory, _selectedGachaTier, count))
                return;
            CareerGrowthView growth = _manager.GrowthDashboard;
            if (growth.LastPulledBlocks.Length > 0)
            {
                GrowthSkillBlockView selected = growth.LastPulledBlocks[growth.LastPulledBlocks.Length - 1];
                _selectedOwnedBlockId = selected.InstanceId;
                _selectedPlacedBlockId = 0;
                _selectedRotation = 0;
            }
            _inventoryCategory = null;
            _inventoryRarity = null;
            _inventoryPlaceableOnly = false;
            _inventoryNewOnly = true;
            _inventoryPage = 0;
            _growthSection = GrowthSection.Board;
            _isGachaOpen = false;
            Render();
        }

        private void CloseGachaOverlay()
        {
            _isGachaOpen = false;
            _isProbabilityOpen = false;
            Render();
        }

        private void SelectWorkspaceOwnedBlock(int instanceId)
        {
            _selectedOwnedBlockId = instanceId;
            _selectedPlacedBlockId = 0;
            _selectedRotation = 0;
            _confirmBoardApply = false;
            Render();
        }

        private void SelectDraftPlacedBlock(int instanceId)
        {
            _selectedOwnedBlockId = 0;
            _selectedPlacedBlockId = instanceId;
            _selectedRotation = GetDraftRotation(instanceId);
            _confirmBoardApply = false;
            Render();
        }

        private void StageSelectedBlock(int x, int y, CareerGrowthView growth)
        {
            if (_selectedOwnedBlockId <= 0)
                return;
            GrowthBlockPlacementPreviewView preview = GetDraftPlacementPreview(
                growth,
                _selectedOwnedBlockId,
                x,
                y,
                _selectedRotation);
            if (!preview.CanPlace)
                return;
            _draftLayout.Add(new GrowthBoardLayoutPlacement(
                _selectedOwnedBlockId,
                x,
                y,
                _selectedRotation));
            _selectedPlacedBlockId = _selectedOwnedBlockId;
            _selectedOwnedBlockId = 0;
            RefreshBoardDraftDirty(growth);
            _confirmBoardApply = false;
            Render();
        }

        private void StageRecoverBlock(int instanceId)
        {
            for (int index = 0; index < _draftLayout.Count; index++)
            {
                if (_draftLayout[index].InstanceId != instanceId)
                    continue;
                _draftLayout.RemoveAt(index);
                _selectedOwnedBlockId = instanceId;
                _selectedPlacedBlockId = 0;
                _selectedRotation = 0;
                RefreshBoardDraftDirty(_manager.GrowthDashboard);
                _confirmBoardApply = false;
                Render();
                return;
            }
        }

        private void ClearBoardDraft()
        {
            _draftLayout.Clear();
            _selectedOwnedBlockId = 0;
            _selectedPlacedBlockId = 0;
            RefreshBoardDraftDirty(_manager.GrowthDashboard);
            _confirmBoardApply = false;
            Render();
        }

        private void ResetBoardDraft(CareerGrowthView growth)
        {
            _draftLayout.Clear();
            if (growth.AppliedLayout != null)
                _draftLayout.AddRange(growth.AppliedLayout);
            _selectedOwnedBlockId = 0;
            _selectedPlacedBlockId = 0;
            _selectedRotation = 0;
            _isBoardDraftDirty = false;
            _confirmBoardApply = false;
            Render();
        }

        private void ApplyBoardDraft(CareerGrowthView growth)
        {
            if (!_confirmBoardApply)
            {
                _confirmBoardApply = true;
                Render();
                return;
            }
            GrowthBoardLayoutPlacement[] layout = _draftLayout.ToArray();
            if (!_manager.ApplySkillBoardLayout(layout))
            {
                _confirmBoardApply = false;
                Render();
                return;
            }
            _isBoardDraftInitialized = false;
            _isBoardDraftDirty = false;
            _confirmBoardApply = false;
            _selectedOwnedBlockId = 0;
            _selectedPlacedBlockId = 0;
            Render();
        }

        private void ToggleSelectedBlockLock(GrowthSkillBlockView block)
        {
            _manager.SetSkillBlockLocked(block.InstanceId, !block.IsLocked);
        }

        private void SetInventoryCategory(SkillBlockCategory? category)
        {
            _inventoryCategory = category;
            _inventoryPage = 0;
            Render();
        }

        private void SetInventoryRarity(SkillBlockRarity rarity)
        {
            _inventoryRarity = _inventoryRarity == rarity ? null : rarity;
            _inventoryPage = 0;
            Render();
        }

        private List<InventoryStack> BuildInventoryStacks(CareerGrowthView growth)
        {
            var result = new List<InventoryStack>();
            AddInventoryBlocks(result, growth, growth.OwnedBlocks);
            for (int index = 0; index < growth.PlacedBlocks.Length; index++)
            {
                if (!IsDraftPlaced(growth.PlacedBlocks[index].InstanceId))
                    AddInventoryBlock(result, growth, growth.PlacedBlocks[index]);
            }
            result.Sort((left, right) => right.Block.InstanceId.CompareTo(left.Block.InstanceId));
            return result;
        }

        private void AddInventoryBlocks(
            List<InventoryStack> destination,
            CareerGrowthView growth,
            GrowthSkillBlockView[] blocks)
        {
            for (int index = 0; index < blocks.Length; index++)
            {
                if (!IsDraftPlaced(blocks[index].InstanceId))
                    AddInventoryBlock(destination, growth, blocks[index]);
            }
        }

        private void AddInventoryBlock(
            List<InventoryStack> destination,
            CareerGrowthView growth,
            GrowthSkillBlockView block)
        {
            bool isNew = IsLastPulled(growth, block.InstanceId);
            if (_inventoryNewOnly && !isNew)
                return;
            if (_inventoryCategory.HasValue && block.Category != _inventoryCategory.Value)
                return;
            if (_inventoryRarity.HasValue && block.Rarity != _inventoryRarity.Value)
                return;
            int placementCount = CountDraftPlacements(growth, block);
            if (_inventoryPlaceableOnly && placementCount == 0)
                return;
            for (int index = 0; index < destination.Count; index++)
            {
                if (!string.Equals(destination[index].Block.DefinitionId, block.DefinitionId, StringComparison.Ordinal) ||
                    destination[index].Block.IsLocked != block.IsLocked)
                {
                    continue;
                }
                destination[index].Count++;
                destination[index].IsNew |= isNew;
                if (block.InstanceId > destination[index].Block.InstanceId)
                    destination[index].Block = block;
                return;
            }
            destination.Add(new InventoryStack
            {
                Block = block,
                Count = 1,
                IsNew = isNew,
                PlacementCount = placementCount
            });
        }

        private int CountDraftInventoryBlocks(CareerGrowthView growth)
        {
            int count = growth.OwnedBlocks.Length;
            for (int index = 0; index < growth.PlacedBlocks.Length; index++)
            {
                if (!IsDraftPlaced(growth.PlacedBlocks[index].InstanceId))
                    count++;
            }
            for (int index = 0; index < growth.OwnedBlocks.Length; index++)
            {
                if (IsDraftPlaced(growth.OwnedBlocks[index].InstanceId))
                    count--;
            }
            return count;
        }

        private int CountDraftPlacements(CareerGrowthView growth, GrowthSkillBlockView block)
        {
            int count = 0;
            int rotations = block.CanRotate ? 4 : 1;
            for (int y = 0; y < growth.BoardHeight; y++)
            {
                for (int x = 0; x < growth.BoardWidth; x++)
                {
                    bool fits = false;
                    for (int rotation = 0; rotation < rotations; rotation++)
                    {
                        if (GetDraftPlacementPreview(
                                growth,
                                block.InstanceId,
                                x,
                                y,
                                rotation).CanPlace)
                        {
                            fits = true;
                            break;
                        }
                    }
                    if (fits)
                        count++;
                }
            }
            return count;
        }

        private GrowthBlockPlacementPreviewView GetDraftPlacementPreview(
            CareerGrowthView growth,
            int instanceId,
            int originX,
            int originY,
            int rotation)
        {
            GrowthSkillBlockView block = FindAnyBlock(growth, instanceId);
            if (block.InstanceId == 0 || IsDraftPlaced(instanceId) ||
                rotation < 0 || rotation > 3 || !block.CanRotate && rotation != 0)
            {
                return new GrowthBlockPlacementPreviewView(Array.Empty<BoardCell>(), false);
            }
            BoardCell[] cells = BuildOccupiedCells(block.ShapeCells, originX, originY, rotation);
            bool canPlace = true;
            for (int index = 0; index < cells.Length; index++)
            {
                if (cells[index].X < 0 || cells[index].X >= growth.BoardWidth ||
                    cells[index].Y < 0 || cells[index].Y >= growth.BoardHeight ||
                    FindDraftInstanceAt(growth, cells[index].X, cells[index].Y) > 0)
                {
                    canPlace = false;
                    break;
                }
            }
            return new GrowthBlockPlacementPreviewView(cells, canPlace);
        }

        /// <summary>
        /// 마우스가 올라간 칸이 블록의 회전된 모양 중 어느 칸이든 걸치기만 하면
        /// 배치 가능하도록, 실제로 유효한 원점(originX, originY)을 역산한다.
        /// 바운딩 박스 좌상단(originX, originY)이 S/Z 등 일부 회전에서는
        /// 블록이 실제로 차지하는 칸이 아니라서, 원점 칸만 클릭 가능하게 두면
        /// 빈 칸을 가리키고 있어도 "다른 블록이 있다"고 잘못 판정되는 것처럼 보인다.
        /// </summary>
        private bool TryResolvePlacementOrigin(
            CareerGrowthView growth,
            int instanceId,
            int rotation,
            int hoverX,
            int hoverY,
            out int originX,
            out int originY)
        {
            if (instanceId > 0)
            {
                GrowthSkillBlockView block = FindAnyBlock(growth, instanceId);
                if (block.InstanceId > 0)
                {
                    BoardCell[] localCells = BuildOccupiedCells(block.ShapeCells, 0, 0, rotation);
                    for (int index = 0; index < localCells.Length; index++)
                    {
                        int candidateX = hoverX - localCells[index].X;
                        int candidateY = hoverY - localCells[index].Y;
                        if (GetDraftPlacementPreview(
                                growth, instanceId, candidateX, candidateY, rotation).CanPlace)
                        {
                            originX = candidateX;
                            originY = candidateY;
                            return true;
                        }
                    }
                }
            }
            originX = hoverX;
            originY = hoverY;
            return false;
        }

        private void ShowDraftPlacementPreview(
            RectTransform board,
            CareerGrowthView growth,
            int hoverX,
            int hoverY,
            float boardSpan,
            float cellSize,
            float gap)
        {
            ClearPlacementPreview();
            if (_selectedOwnedBlockId <= 0)
                return;
            if (!TryResolvePlacementOrigin(
                    growth, _selectedOwnedBlockId, _selectedRotation, hoverX, hoverY,
                    out int originX, out int originY))
            {
                originX = hoverX;
                originY = hoverY;
            }
            GrowthBlockPlacementPreviewView preview = GetDraftPlacementPreview(
                growth,
                _selectedOwnedBlockId,
                originX,
                originY,
                _selectedRotation);
            GrowthSkillBlockView block = FindAnyBlock(growth, _selectedOwnedBlockId);
            Color color = preview.CanPlace
                ? Color.Lerp(GetCategoryColor(block.Category), GreenColor, 0.2f)
                : new Color(0.94f, 0.20f, 0.18f, 0.54f);
            color.a = preview.CanPlace ? 0.72f : 0.62f;
            for (int index = 0; index < preview.Cells.Length; index++)
            {
                BoardCell cell = preview.Cells[index];
                if (cell.X < 0 || cell.X >= growth.BoardWidth ||
                    cell.Y < 0 || cell.Y >= growth.BoardHeight)
                {
                    continue;
                }
                Image image = _placementPreviewImages[cell.Y * growth.BoardWidth + cell.X];
                if (image == null)
                    continue;
                image.color = new Color(color.r, color.g, color.b, 0.18f);
                image.enabled = true;
            }

            BoardCell[] occupiedCells = BuildOccupiedCells(
                block.ShapeCells,
                originX,
                originY,
                _selectedRotation);
            GetCellBounds(
                occupiedCells,
                out int minimumX,
                out int minimumY,
                out int maximumX,
                out int maximumY);
            float cellPitch = cellSize + gap;
            float centerX = -boardSpan * 0.5f + cellSize * 0.5f +
                            (minimumX + maximumX) * 0.5f * cellPitch;
            float centerY = boardSpan * 0.5f - cellSize * 0.5f -
                            (minimumY + maximumY) * 0.5f * cellPitch;
            int widthInCells = maximumX - minimumX + 1;
            int heightInCells = maximumY - minimumY + 1;
            if (_draftPlacementPreviewVisual == null)
            {
                _draftPlacementPreviewVisual = RenderTetromino(
                    board,
                    block.ShapeCells,
                    _selectedRotation,
                    color,
                    new Vector2(centerX, centerY),
                    new Vector2(widthInCells * cellPitch, heightInCells * cellPitch),
                    cellPitch,
                    "DraftPlacementPreview");
            }
            else
            {
                _draftPlacementPreviewVisual.anchoredPosition = new Vector2(centerX, centerY);
                _draftPlacementPreviewVisual.gameObject.SetActive(true);
            }
            Image previewGraphic = _draftPlacementPreviewVisual != null
                ? _draftPlacementPreviewVisual.GetComponent<Image>()
                : null;
            if (previewGraphic == null)
                return;
            previewGraphic.color = color;
            Outline outline = previewGraphic.GetComponent<Outline>();
            if (outline == null)
                outline = previewGraphic.gameObject.AddComponent<Outline>();
            outline.effectColor = preview.CanPlace ? GreenColor : ErrorColor;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = true;
        }

        private static BoardCell[] BuildOccupiedCells(
            BoardCell[] source,
            int originX,
            int originY,
            int rotation)
        {
            var result = new BoardCell[source.Length];
            int minimumX = int.MaxValue;
            int minimumY = int.MaxValue;
            for (int index = 0; index < source.Length; index++)
            {
                GetRotatedCoordinates(source[index], rotation, out int x, out int y);
                result[index] = new BoardCell(x, y);
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
            }
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = new BoardCell(
                    originX + result[index].X - minimumX,
                    originY + result[index].Y - minimumY);
            }
            return result;
        }

        private int FindDraftInstanceAt(CareerGrowthView growth, int x, int y)
        {
            for (int index = 0; index < _draftLayout.Count; index++)
            {
                GrowthSkillBlockView block = FindAnyBlock(growth, _draftLayout[index].InstanceId);
                BoardCell[] cells = BuildOccupiedCells(
                    block.ShapeCells,
                    _draftLayout[index].OriginX,
                    _draftLayout[index].OriginY,
                    _draftLayout[index].RotationQuarterTurns);
                for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                {
                    if (cells[cellIndex].X == x && cells[cellIndex].Y == y)
                        return block.InstanceId;
                }
            }
            return 0;
        }

        private void DrawDraftBlockEdges(
            Transform cell,
            CareerGrowthView growth,
            int instanceId,
            int x,
            int y,
            float cellSize)
        {
            GrowthSkillBlockView block = FindAnyBlock(growth, instanceId);
            Color color = GetRarityFrameColor(block.Rarity);
            const float thickness = 3f;
            if (FindDraftInstanceAt(growth, x, y - 1) != instanceId)
                CreateImage("TopEdge", cell, color, new Vector2(cellSize, thickness), new Vector2(0f, cellSize * 0.5f - thickness * 0.5f));
            if (FindDraftInstanceAt(growth, x, y + 1) != instanceId)
                CreateImage("BottomEdge", cell, color, new Vector2(cellSize, thickness), new Vector2(0f, -cellSize * 0.5f + thickness * 0.5f));
            if (FindDraftInstanceAt(growth, x - 1, y) != instanceId)
                CreateImage("LeftEdge", cell, color, new Vector2(thickness, cellSize), new Vector2(-cellSize * 0.5f + thickness * 0.5f, 0f));
            if (FindDraftInstanceAt(growth, x + 1, y) != instanceId)
                CreateImage("RightEdge", cell, color, new Vector2(thickness, cellSize), new Vector2(cellSize * 0.5f - thickness * 0.5f, 0f));
        }

        private void RenderDraftBlockVisuals(
            RectTransform board,
            CareerGrowthView growth,
            float boardSpan,
            float cellSize,
            float gap)
        {
            float cellPitch = cellSize + gap;
            for (int index = 0; index < _draftLayout.Count; index++)
            {
                GrowthBoardLayoutPlacement placement = _draftLayout[index];
                GrowthSkillBlockView block = FindAnyBlock(growth, placement.InstanceId);
                BoardCell[] occupiedCells = BuildOccupiedCells(
                    block.ShapeCells,
                    placement.OriginX,
                    placement.OriginY,
                    placement.RotationQuarterTurns);
                GetCellBounds(
                    occupiedCells,
                    out int minimumX,
                    out int minimumY,
                    out int maximumX,
                    out int maximumY);
                float centerX = -boardSpan * 0.5f + cellSize * 0.5f +
                                (minimumX + maximumX) * 0.5f * cellPitch;
                float centerY = boardSpan * 0.5f - cellSize * 0.5f -
                                (minimumY + maximumY) * 0.5f * cellPitch;
                int widthInCells = maximumX - minimumX + 1;
                int heightInCells = maximumY - minimumY + 1;
                Color tint = GetCategoryColor(block.Category);
                if (block.InstanceId == _selectedPlacedBlockId)
                    tint = Color.Lerp(tint, Color.white, 0.24f);

                RectTransform visual = RenderTetromino(
                    board,
                    block.ShapeCells,
                    placement.RotationQuarterTurns,
                    tint,
                    new Vector2(centerX, centerY),
                    new Vector2(widthInCells * cellPitch, heightInCells * cellPitch),
                    cellPitch,
                    "DraftBlock_" + block.InstanceId);
                if (visual != null)
                    visual.SetSiblingIndex(Mathf.Min(index + 1, board.childCount - 1));
            }
        }

        private static void GetCellBounds(
            BoardCell[] cells,
            out int minimumX,
            out int minimumY,
            out int maximumX,
            out int maximumY)
        {
            minimumX = int.MaxValue;
            minimumY = int.MaxValue;
            maximumX = int.MinValue;
            maximumY = int.MinValue;
            for (int index = 0; index < cells.Length; index++)
            {
                minimumX = Math.Min(minimumX, cells[index].X);
                minimumY = Math.Min(minimumY, cells[index].Y);
                maximumX = Math.Max(maximumX, cells[index].X);
                maximumY = Math.Max(maximumY, cells[index].Y);
            }
        }

        private int[] BuildDraftBonuses(CareerGrowthView growth)
        {
            var result = new int[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < _draftLayout.Count; index++)
            {
                GrowthSkillBlockView block = FindAnyBlock(growth, _draftLayout[index].InstanceId);
                for (int bonusIndex = 0; bonusIndex < block.AbilityBonuses.Length; bonusIndex++)
                {
                    AbilityChange bonus = block.AbilityBonuses[bonusIndex];
                    result[(int)bonus.Ability] += bonus.Amount;
                }
            }
            return result;
        }

        private bool DraftRequiresSafeRecovery(CareerGrowthView growth)
        {
            if (growth.AppliedLayout == null)
                return false;
            for (int index = 0; index < growth.AppliedLayout.Length; index++)
            {
                bool found = false;
                for (int draftIndex = 0; draftIndex < _draftLayout.Count; draftIndex++)
                {
                    if (HasSameLayout(growth.AppliedLayout[index], _draftLayout[draftIndex]))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return true;
            }
            return false;
        }

        private void RefreshBoardDraftDirty(CareerGrowthView growth)
        {
            if (growth?.AppliedLayout == null || growth.AppliedLayout.Length != _draftLayout.Count)
            {
                _isBoardDraftDirty = true;
                return;
            }
            for (int index = 0; index < growth.AppliedLayout.Length; index++)
            {
                bool found = false;
                for (int draftIndex = 0; draftIndex < _draftLayout.Count; draftIndex++)
                {
                    if (!HasSameLayout(growth.AppliedLayout[index], _draftLayout[draftIndex]))
                        continue;
                    found = true;
                    break;
                }
                if (!found)
                {
                    _isBoardDraftDirty = true;
                    return;
                }
            }
            _isBoardDraftDirty = false;
        }

        private static bool HasSameLayout(
            GrowthBoardLayoutPlacement left,
            GrowthBoardLayoutPlacement right)
        {
            return left.InstanceId == right.InstanceId &&
                   left.OriginX == right.OriginX &&
                   left.OriginY == right.OriginY &&
                   left.RotationQuarterTurns == right.RotationQuarterTurns;
        }

        private bool IsDraftPlaced(int instanceId)
        {
            for (int index = 0; index < _draftLayout.Count; index++)
            {
                if (_draftLayout[index].InstanceId == instanceId)
                    return true;
            }
            return false;
        }

        private bool IsDraftOrigin(int instanceId, int x, int y)
        {
            for (int index = 0; index < _draftLayout.Count; index++)
            {
                if (_draftLayout[index].InstanceId == instanceId &&
                    _draftLayout[index].OriginX == x &&
                    _draftLayout[index].OriginY == y)
                {
                    return true;
                }
            }
            return false;
        }

        private int GetDraftRotation(int instanceId)
        {
            for (int index = 0; index < _draftLayout.Count; index++)
            {
                if (_draftLayout[index].InstanceId == instanceId)
                    return _draftLayout[index].RotationQuarterTurns;
            }
            return 0;
        }

        private static bool IsLastPulled(CareerGrowthView growth, int instanceId)
        {
            for (int index = 0; index < growth.LastPulledBlocks.Length; index++)
            {
                if (growth.LastPulledBlocks[index].InstanceId == instanceId)
                    return true;
            }
            return false;
        }

        private static bool IsActuallyOwned(CareerGrowthView growth, int instanceId)
        {
            for (int index = 0; index < growth.OwnedBlocks.Length; index++)
            {
                if (growth.OwnedBlocks[index].InstanceId == instanceId)
                    return true;
            }
            return false;
        }

        private static int CountOwnedBlockKinds(CareerGrowthView growth)
        {
            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < growth.OwnedBlocks.Length; index++)
                definitionIds.Add(growth.OwnedBlocks[index].DefinitionId);
            for (int index = 0; index < growth.PlacedBlocks.Length; index++)
                definitionIds.Add(growth.PlacedBlocks[index].DefinitionId);
            return definitionIds.Count;
        }

        private static GrowthSkillBlockView FindAnyBlock(CareerGrowthView growth, int instanceId)
        {
            for (int index = 0; index < growth.OwnedBlocks.Length; index++)
            {
                if (growth.OwnedBlocks[index].InstanceId == instanceId)
                    return growth.OwnedBlocks[index];
            }
            for (int index = 0; index < growth.PlacedBlocks.Length; index++)
            {
                if (growth.PlacedBlocks[index].InstanceId == instanceId)
                    return growth.PlacedBlocks[index];
            }
            return default;
        }

        private static SkillBlockCategory[] GetWorkspaceCategories(PlayerType playerType)
        {
            return playerType == PlayerType.Batter
                ? new[]
                {
                    SkillBlockCategory.Contact,
                    SkillBlockCategory.Power,
                    SkillBlockCategory.Baserunning,
                SkillBlockCategory.Arm,
                    SkillBlockCategory.Defense,
                    SkillBlockCategory.BatterMental
                }
                : new[]
                {
                    SkillBlockCategory.Velocity,
                    SkillBlockCategory.Control,
                    SkillBlockCategory.Breaking,
                    SkillBlockCategory.PitcherPhysical,
                    SkillBlockCategory.Stuff,
                    SkillBlockCategory.PitcherMental
                };
        }

        private static SkillBlockRarity[] GetRarities()
        {
            return new[]
            {
                SkillBlockRarity.Normal,
                SkillBlockRarity.Rare,
                SkillBlockRarity.Elite,
                SkillBlockRarity.Unique,
                SkillBlockRarity.Legendary
            };
        }

        private static SkillGachaPurchaseTier[] GetGachaTiers()
        {
            return new[]
            {
                SkillGachaPurchaseTier.Normal,
                SkillGachaPurchaseTier.Rare,
                SkillGachaPurchaseTier.Elite,
                SkillGachaPurchaseTier.Unique,
                SkillGachaPurchaseTier.Legendary
            };
        }

        private static string GetGachaTierLabel(SkillGachaPurchaseTier tier)
        {
            return tier switch
            {
                SkillGachaPurchaseTier.Normal => "Normal 뽑기",
                SkillGachaPurchaseTier.Rare => "Rare 뽑기",
                SkillGachaPurchaseTier.Elite => "Elite 뽑기",
                SkillGachaPurchaseTier.Unique => "Unique 뽑기",
                SkillGachaPurchaseTier.Legendary => "Legendary 뽑기",
                _ => tier.ToString()
            };
        }

        private static Color GetRarityFrameColor(SkillBlockRarity rarity)
        {
            return rarity switch
            {
                SkillBlockRarity.Normal => new Color(0.55f, 0.62f, 0.69f, 1f),
                SkillBlockRarity.Rare => new Color(0.18f, 0.65f, 1f, 1f),
                SkillBlockRarity.Elite => new Color(0.77f, 0.38f, 0.95f, 1f),
                SkillBlockRarity.Unique => new Color(1f, 0.72f, 0.13f, 1f),
                SkillBlockRarity.Legendary => new Color(1f, 0.26f, 0.26f, 1f),
                _ => SecondaryTextColor
            };
        }

        private static string FormatBoardBonuses(int[] bonuses, PlayerType playerType)
        {
            PlayerAbility[] abilities = GetVisibleAbilities(playerType);
            var parts = new List<string>();
            for (int index = 0; index < abilities.Length; index++)
            {
                int value = bonuses[(int)abilities[index]];
                if (value > 0)
                    parts.Add($"{GetAbilityLabel(abilities[index])} +{value}");
            }
            return parts.Count == 0 ? "장착 보너스 없음" : string.Join(" · ", parts);
        }

        private static string FormatBoardBonusDifference(
            int[] current,
            int[] draft,
            PlayerType playerType)
        {
            PlayerAbility[] abilities = GetVisibleAbilities(playerType);
            var parts = new List<string>();
            for (int index = 0; index < abilities.Length; index++)
            {
                int value = draft[(int)abilities[index]];
                int delta = value - current[(int)abilities[index]];
                if (value <= 0 && delta == 0)
                    continue;
                string change = delta > 0 ? $" ▲{delta}" : delta < 0 ? $" ▼{-delta}" : string.Empty;
                parts.Add($"{GetAbilityLabel(abilities[index])} +{value}{change}");
            }
            return parts.Count == 0 ? "장착 보너스 없음" : string.Join(" · ", parts);
        }
    }
}
