using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using Baseball.Simulation.Growth;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>레퍼런스의 조밀한 선수·성장판·블록 목록과 지도형 유학 화면을 제공한다.</summary>
    public sealed partial class UI_Scene_OwnerGrowth : MonoBehaviour, IUiCancelHandler
    {
        private static readonly Color Ink = new Color32(34, 47, 63, 255);
        private static readonly Color Blue = new Color32(28, 90, 170, 255);
        private static readonly Color Border = new Color32(171, 182, 197, 255);
        private RectTransform _root;
        private RectTransform _sheet;
        private RectTransform _content;
        private Text _feedback;
        private Texture2D _skillTile;
        private Image[] _boardCellImages = Array.Empty<Image>();
        private Color[] _boardCellColors = Array.Empty<Color>();
        private OwnerGrowthSnapshot _snapshot;
        private string _cardId = string.Empty;
        private string _programId = string.Empty;
        private bool _isStudy;
        private bool _isPitcher;
        private int _instanceId;
        private int _rotation;
        private int _rarity = -1;
        private string _pendingStudy = string.Empty;

        public event Action<string, string> StudyRequested;
        public event Action<string, int, int, int, int> SkillPlacementRequested;
        public event Action<string, int> SkillRemovalRequested;
        public event Action ShopRequested;

        /// <summary>공용 Shell의 업무 영역 아래에 성장 화면을 생성한다.</summary>
        public static UI_Scene_OwnerGrowth CreateRuntime(RectTransform host)
        {
            var view = new GameObject(nameof(UI_Scene_OwnerGrowth)).AddComponent<UI_Scene_OwnerGrowth>();
            view._root = OwnerWorkspaceUiFactory.CreateRoot(host, "OwnerGrowthWorkspace", false);
            var paper = view._root.gameObject.AddComponent<RawImage>();
            paper.texture = Resources.Load<Texture2D>("UI/OwnerPowerUp/growth_silver_v1");
            paper.color = Color.white;
            paper.raycastTarget = false;
            view._sheet = OwnerRuntimeUiFactory.CreateRect("ReferenceGrowthSheet", view._root);
            view._sheet.anchorMin = view._sheet.anchorMax = new Vector2(.5f, .5f);
            view._sheet.sizeDelta = new Vector2(1100, 560);
            view._skillTile = Resources.Load<Texture2D>("UI/OwnerPowerUp/skill_stud_tile_v1");
            view._feedback = Label(view._sheet, "Feedback", "선수를 선택하세요.", 12, 20, 522, 1060, 28);
            view.Resize();
            return view;
        }

        /// <summary>실제 카드·인벤토리·유학 상태를 갱신하고 현재 선택을 유지한다.</summary>
        public void Bind(OwnerGrowthSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _pendingStudy = string.Empty;
            EnsureSelection();
            Render();
        }

        /// <summary>스킬 배치 또는 유학 메뉴를 선택한다.</summary>
        public void ShowRoute(string routeId)
        {
            bool isStudy = routeId == OwnerNavigationRoutes.PowerUpStudy;
            bool needsRender = _content == null || _isStudy != isStudy ||
                _isChoosingStudyPlayer || !string.IsNullOrEmpty(_pendingStudy);
            _isStudy = isStudy;
            _isChoosingStudyPlayer = false;
            _pendingStudy = string.Empty;
            SetFeedback(_isStudy ? "과정과 선수를 선택한 뒤 비용·성장 결과를 확인하세요."
                : "블록 선택 → 회전 → 초록색 성장판 칸을 눌러 바로 배치", false);
            if (needsRender)
                Render();
        }

        /// <summary>성장 업무 영역의 표시 여부를 변경한다.</summary>
        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

        /// <summary>선수 선택, 유학 확인, 스킬 블록 선택 중 가장 안쪽 작업만 취소한다.</summary>
        public bool TryHandleCancel()
        {
            if (_isChoosingStudyPlayer)
            {
                _isChoosingStudyPlayer = false;
                _pendingStudy = string.Empty;
                Render();
                return true;
            }
            if (!string.IsNullOrEmpty(_pendingStudy))
            {
                _pendingStudy = string.Empty;
                Render();
                return true;
            }
            if (_instanceId != 0 || _rotation != 0)
            {
                _instanceId = 0;
                _rotation = 0;
                Render();
                return true;
            }
            return false;
        }

        /// <summary>실행 결과 또는 차단 사유를 하단에 표시한다.</summary>
        public void SetFeedback(string text, bool isError)
        {
            _feedback.text = text ?? string.Empty;
            _feedback.color = isError ? new Color32(175, 46, 38, 255) : Ink;
        }

        private void LateUpdate() => Resize();

        private void Resize()
        {
            if (_root == null) return;
            _sheet.localScale = Vector3.one * Mathf.Max(.01f,
                Mathf.Min(_root.rect.width / 1100f, _root.rect.height / 560f));
        }

        private void EnsureSelection()
        {
            if (SelectedCard() != null) return;
            _cardId = string.Empty;
            foreach (OwnerGrowthCardSnapshot card in _snapshot.Cards)
                if (IsPitcher(card.Card) == _isPitcher) { _cardId = card.Card.CardId; break; }
        }

        private OwnerGrowthCardSnapshot SelectedCard()
        {
            if (_snapshot == null) return null;
            foreach (OwnerGrowthCardSnapshot card in _snapshot.Cards)
                if (card.Card.CardId == _cardId && IsPitcher(card.Card) == _isPitcher) return card;
            return null;
        }

        private void Render()
        {
            if (_content != null)
            {
                _content.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(_content.gameObject);
                else DestroyImmediate(_content.gameObject);
            }
            _content = OwnerRuntimeUiFactory.CreateRect("GrowthContent", _sheet);
            OwnerRuntimeUiFactory.Stretch(_content);
            if (_snapshot == null) return;
            Label(_content, "Heading", _isStudy ? "유학  /  선수 성장" : "스킬 블록 배치", 19, 20, 8, 350, 30);
            Label(_content, "Wallet", $"육성 포인트  {_snapshot.DevelopmentPoints:N0}    |    유학  {_snapshot.StudyCount}/{_snapshot.StudyCapacity}",
                13, 690, 8, 385, 30).alignment = TextAnchor.MiddleRight;
            Tab(_content, "BatterTab", "야  수", () => SelectType(false), !_isPitcher, 20, 42, 100, 28);
            Tab(_content, "PitcherTab", "투  수", () => SelectType(true), _isPitcher, 122, 42, 100, 28);
            Surface(_content, "BlueRule", 20, 72, 1060, 2, Blue);
            if (_isStudy) RenderStudy(); else RenderSkills();
            _feedback.transform.SetAsLastSibling();
            Resize();
        }

        private void SelectType(bool pitcher)
        {
            _isPitcher = pitcher;
            _cardId = _programId = _pendingStudy = string.Empty;
            _instanceId = _rotation = 0;
            EnsureSelection();
            Render();
        }

        private void RenderRoster(Transform parent, float x, float y, float width, float height, int columns)
        {
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalGridScroll("PlayerInventory", parent,
                columns, new Vector2((width - 22 - (columns - 1) * 5) / columns, 92), 5,
                out RectTransform cards);
            Place(scroll.GetComponent<RectTransform>(), x, y, width, height);
            cards.GetComponent<GridLayoutGroup>().padding = new RectOffset(5, 5, 5, 5);
            AddScrollbar(scroll);
            int count = 0;
            foreach (OwnerGrowthCardSnapshot candidate in _snapshot.Cards)
            {
                if (IsPitcher(candidate.Card) != _isPitcher) continue;
                count++;
                OwnerGrowthCardSnapshot selected = candidate;
                PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(cards, "Card_" + candidate.Card.CardId);
                card.UseLineupSlotLayout();
                card.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(candidate.Card, candidate.Card.CardId == _cardId));
                card.SetPortrait(PlayerPortraitSprites.GetDefault(candidate.Card.Position));
                FitCompactCardText(card);
                card.Selected += _ =>
                {
                    _cardId = selected.Card.CardId;
                    _pendingStudy = string.Empty;
                    _instanceId = _rotation = 0;
                    Render();
                };
                card.DetailRequested += _ => UI_Popup_OwnerPlayerCard.Show(_root, new[] { selected.Card }, 0);
            }
            if (count == 0) Label(parent, "NoPlayers", "해당 유형의 보유 선수가 없습니다.", 13, x, y + 20, width, 38);
        }

        private void RenderSkills()
        {
            Frame(_content, "RosterFrame", 20, 86, 330, 416);
            Label(_content, "RosterHeading", "보유 선수", 14, 28, 91, 180, 24);
            RenderRoster(_content, 25, 122, 320, 370, 5);
            Frame(_content, "SelectedFrame", 360, 86, 234, 416);
            OwnerGrowthCardSnapshot card = SelectedCard();
            Label(_content, "SelectedName", card?.Card.DisplayName ?? "선수를 선택하세요", 16, 370, 91, 214, 26);
            if (card != null)
            {
                PlayerMiniCardView preview = PlayerMiniCardView.CreateRuntime(_content, "SelectedPlayerCard");
                preview.UseLineupSlotLayout();
                Place(preview.GetComponent<RectTransform>(), 371, 123, 85, 117);
                preview.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(card.Card, false));
                preview.SetPortrait(PlayerPortraitSprites.GetDefault(card.Card.Position));
                FitCompactCardText(preview);
                string detail = OwnerCollectionPresentationBuilder.FormatPosition(card.Card.Position) + "\n" +
                    card.Card.OriginYear + "년\n코스트 " + card.Card.Cost + "\n장착 " + card.Placements.Length + "개";
                Label(_content, "SelectedDetails", detail, 13, 465, 127, 117, 106);
            }
            Label(_content, "BoardTitle", $"성장판  {_snapshot.Board.Width} × {_snapshot.Board.Height}", 14, 373, 247, 208, 24);
            RenderBoard(card);
            Frame(_content, "InventoryFrame", 605, 86, 475, 416);
            Label(_content, "InventoryHeading", $"보유 스킬  {CountAvailableBlocks()}개", 14, 616, 91, 220, 24);
            for (int index = -1; index < 5; index++)
            {
                int rarity = index;
                string[] labels = { "전체", "일반", "레어", "엘리트", "유니크", "전설" };
                Tab(_content, "Rarity" + index, labels[index + 1], () => { _rarity = rarity; Render(); },
                    _rarity == index, 615 + (index + 1) * 76, 122, 72, 25);
            }
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalGridScroll("SkillInventory", _content,
                3, new Vector2(141, 89), 6, out RectTransform blocks);
            Place(scroll.GetComponent<RectTransform>(), 613, 152, 459, 244);
            AddScrollbar(scroll);
            int count = 0;
            foreach (SkillBlockInstance block in _snapshot.Inventory)
            {
                SkillBlockDefinition definition = FindDefinition(block.DefinitionId);
                if (definition == null || !IsAvailableToSelectedPlayerType(definition) ||
                    (_rarity >= 0 && (int)definition.Rarity != _rarity)) continue;
                string equipped = _snapshot.GetEquippedCardId(block.InstanceId);
                int instanceId = block.InstanceId;
                Button button = Tab(blocks, "Block_" + instanceId, "", () =>
                {
                    _instanceId = instanceId;
                    _rotation = 0;
                    Render();
                }, instanceId == _instanceId, 0, 0, 141, 89);
                button.GetComponent<LayoutElement>().preferredHeight = 89;
                button.interactable = string.IsNullOrEmpty(equipped) || equipped == _cardId;
                RenderSkillIcon(button.transform, definition);
                Label(button.transform, "BlockName", DescribeBlock(definition), 11, 3, 64, 135, 21).alignment = TextAnchor.MiddleCenter;
                Label(button.transform, "BlockStatus", string.IsNullOrEmpty(equipped) ? RarityLabel(definition.Rarity)
                    : equipped == _cardId ? "장착 중" : "다른 선수 사용 중", 10, 4, 0, 133, 19);
                count++;
            }
            if (count == 0) Label(_content, "NoBlocks", "보유 블록이 없습니다.\n상점에서 스킬 블록을 획득하세요.", 14, 630, 207, 410, 90);
            RenderSkillActions(card);
        }

        private int CountAvailableBlocks()
        {
            int count = 0;
            foreach (SkillBlockInstance block in _snapshot.Inventory)
            {
                SkillBlockDefinition definition = FindDefinition(block.DefinitionId);
                if (definition != null && IsAvailableToSelectedPlayerType(definition)) count++;
            }
            return count;
        }

        private bool IsAvailableToSelectedPlayerType(SkillBlockDefinition definition) =>
            SkillBlockCategoryCatalog.IsAvailableTo(
                definition.Category,
                _isPitcher ? PlayerType.Pitcher : PlayerType.Batter);

        private void RenderBoard(OwnerGrowthCardSnapshot card)
        {
            float cellSize = Mathf.Min(45f, 190f / Mathf.Max(_snapshot.Board.Width, _snapshot.Board.Height));
            var service = new SkillBoardService(_snapshot.Board, _snapshot.Definitions);
            SkillBoardState boardState = card == null ? null : _snapshot.CreateBoardState(card);
            SkillBlockInstance selectedInstance = FindInventoryBlock(_instanceId);
            BoardCell[] selectedLocalCells = card != null && selectedInstance.InstanceId > 0 &&
                                             string.IsNullOrEmpty(_snapshot.GetEquippedCardId(_instanceId))
                ? service.GetOccupiedCells(new PlacedSkillBlock(selectedInstance, 0, 0, _rotation))
                : null;
            int boardCellCount = _snapshot.Board.Width * _snapshot.Board.Height;
            _boardCellImages = new Image[boardCellCount];
            _boardCellColors = new Color[boardCellCount];
            for (int y = 0; y < _snapshot.Board.Height; y++)
            for (int x = 0; x < _snapshot.Board.Width; x++)
            {
                int column = x, row = y, equippedInstance = 0;
                Color color = new Color32(225, 233, 238, 255);
                if (card != null)
                    foreach (PlacedSkillBlock placement in card.Placements)
                        foreach (BoardCell occupied in service.GetOccupiedCells(placement))
                            if (occupied.X == x && occupied.Y == y)
                            {
                                equippedInstance = placement.Instance.InstanceId;
                                color = BlockColor(FindDefinition(placement.Instance.DefinitionId));
                            }
                int resolvedX = column;
                int resolvedY = row;
                bool canPlace = equippedInstance == 0 && selectedLocalCells != null &&
                    SkillBlockPlacementTargetResolver.TryResolveOrigin(
                        selectedLocalCells,
                        column,
                        row,
                        (candidateX, candidateY) => service.GetPlacementPreview(
                            boardState,
                            _instanceId,
                            candidateX,
                            candidateY,
                            _rotation).CanPlace,
                        out resolvedX,
                        out resolvedY);
                if (canPlace)
                    color = new Color32(184, 224, 199, 255);
                int occupiedId = equippedInstance;
                int placementX = resolvedX;
                int placementY = resolvedY;
                int selectedId = _instanceId;
                int selectedRotation = _rotation;
                string selectedCardId = card?.Card.CardId ?? string.Empty;
                SkillBlockPlacementPreview placementPreview = canPlace
                    ? service.GetPlacementPreview(
                        boardState,
                        selectedId,
                        placementX,
                        placementY,
                        selectedRotation)
                    : default;
                Button button = Tab(_content, "BoardCell_" + x + "_" + y, "", () =>
                {
                    if (occupiedId > 0)
                    {
                        _instanceId = occupiedId;
                        Render();
                        return;
                    }
                    SkillPlacementRequested?.Invoke(
                        selectedCardId,
                        selectedId,
                        placementX,
                        placementY,
                        selectedRotation);
                }, false, 384 + x * cellSize, 278 + y * cellSize, cellSize - 2, cellSize - 2);
                button.interactable = occupiedId > 0 || canPlace;
                Image cellImage = button.GetComponent<Image>();
                cellImage.color = color;
                int cellIndex = row * _snapshot.Board.Width + column;
                _boardCellImages[cellIndex] = cellImage;
                _boardCellColors[cellIndex] = color;
                if (canPlace)
                {
                    BoardCell[] previewCells = placementPreview.Cells;
                    AddPointerListener(button.gameObject, EventTriggerType.PointerEnter,
                        () => ShowBoardPlacementPreview(previewCells));
                    AddPointerListener(button.gameObject, EventTriggerType.PointerExit,
                        ClearBoardPlacementPreview);
                }
                if (equippedInstance > 0) CreateSkillTile(button.transform, color, 0, 0, cellSize - 2);
            }
            Label(_content, "BoardHint", selectedLocalCells == null
                ? "블록을 고르면 배치 가능한 칸이 표시됩니다."
                : "초록색 칸을 누르면 바로 장착됩니다.", 11, 371, 465, 214, 28);
        }

        private void RenderSkillActions(OwnerGrowthCardSnapshot card)
        {
            SkillBlockDefinition selected = null;
            foreach (SkillBlockInstance block in _snapshot.Inventory)
                if (block.InstanceId == _instanceId) selected = FindDefinition(block.DefinitionId);
            string detail = selected == null ? "블록을 선택하면 능력치 효과가 표시됩니다." : DescribeBlock(selected) + "  ·  " + DescribeBonuses(selected);
            Label(_content, "SkillDescription", detail, 12, 619, 401, 447, 42);
            Button rotate = Tab(_content, "Rotate", "회전 ↻", () => { _rotation = (_rotation + 1) % 4; Render(); }, false, 619, 450, 100, 32);
            rotate.interactable = selected != null && selected.CanRotate && string.IsNullOrEmpty(_snapshot.GetEquippedCardId(_instanceId));
            Label(_content, "PlacementActionHint", "칸 클릭 배치", 11, 724, 450, 103, 32).alignment = TextAnchor.MiddleCenter;
            Button remove = Tab(_content, "RemovePlacement", "블록 해제", () => SkillRemovalRequested?.Invoke(_cardId, _instanceId), false, 832, 450, 108, 32);
            remove.interactable = card != null && _instanceId > 0 && _snapshot.GetEquippedCardId(_instanceId) == _cardId;
            Tab(_content, "SkillShop", "스킬 상점", () => ShopRequested?.Invoke(), false, 945, 450, 120, 32);
        }

        private SkillBlockInstance FindInventoryBlock(int instanceId)
        {
            foreach (SkillBlockInstance block in _snapshot.Inventory)
                if (block.InstanceId == instanceId) return block;
            return default;
        }

        private void ShowBoardPlacementPreview(BoardCell[] cells)
        {
            ClearBoardPlacementPreview();
            for (int index = 0; index < cells.Length; index++)
            {
                int cellIndex = cells[index].Y * _snapshot.Board.Width + cells[index].X;
                if (cellIndex < 0 || cellIndex >= _boardCellImages.Length || _boardCellImages[cellIndex] == null)
                    continue;
                _boardCellImages[cellIndex].color = new Color32(82, 190, 121, 255);
            }
        }

        private void ClearBoardPlacementPreview()
        {
            for (int index = 0; index < _boardCellImages.Length; index++)
                if (_boardCellImages[index] != null) _boardCellImages[index].color = _boardCellColors[index];
        }

        private static void AddPointerListener(GameObject target, EventTriggerType type, Action action)
        {
            EventTrigger trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
            trigger.triggers ??= new List<EventTrigger.Entry>();
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private SkillBlockDefinition FindDefinition(string id)
        {
            foreach (SkillBlockDefinition definition in _snapshot.Definitions)
                if (definition.BlockId == id) return definition;
            return null;
        }

        private void RenderSkillIcon(Transform parent, SkillBlockDefinition definition)
        {
            int width = 1, height = 1;
            foreach (BoardCell cell in definition.ShapeCells)
            {
                width = Math.Max(width, cell.X + 1);
                height = Math.Max(height, cell.Y + 1);
            }
            float size = Mathf.Min(20, 48f / height);
            foreach (BoardCell cell in definition.ShapeCells)
                CreateSkillTile(parent, BlockColor(definition),
                    (141 - width * size) * .5f + cell.X * size,
                    19 + (48 - height * size) * .5f + cell.Y * size, size);
        }

        private void CreateSkillTile(Transform parent, Color tint, float x, float y, float size)
        {
            var tile = new GameObject("SkillTile", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            tile.transform.SetParent(parent, false);
            Place(tile.rectTransform, x, y, size, size);
            tile.texture = _skillTile;
            tile.color = tint;
            tile.raycastTarget = false;
        }

        private static string DescribeBlock(SkillBlockDefinition definition) =>
            definition.AbilityBonuses.Length == 0 ? "특성 블록" : CareerSharedSnapshotFormatters.FormatAbility(definition.AbilityBonuses[0].Ability);

        private static string DescribeBonuses(SkillBlockDefinition definition)
        {
            var text = new StringBuilder();
            foreach (AbilityChange bonus in definition.AbilityBonuses)
            {
                if (text.Length > 0) text.Append(" / ");
                text.Append(CareerSharedSnapshotFormatters.FormatAbility(bonus.Ability)).Append(" +").Append(bonus.Amount);
            }
            return text.ToString();
        }

        private static string RarityLabel(SkillBlockRarity rarity) => rarity switch
        {
            SkillBlockRarity.Normal => "일반", SkillBlockRarity.Rare => "레어", SkillBlockRarity.Elite => "엘리트",
            SkillBlockRarity.Unique => "유니크", _ => "전설"
        };

        private static Color BlockColor(SkillBlockDefinition definition) =>
            SkillBlockVisual.GetRarityColor(definition.Rarity);

        private static bool IsPitcher(OwnerCollectionCardSnapshot card) =>
            card.Position == PlayerPosition.StartingPitcher || card.Position == PlayerPosition.ReliefPitcher;

        private static void FitCompactCardText(PlayerMiniCardView card)
        {
            // 목록의 작은 카드도 이름과 코스트가 사라지지 않도록 이 화면의 축소 범위를 적용한다.
            foreach (Text text in card.GetComponentsInChildren<Text>(true))
            {
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 6;
                text.resizeTextMaxSize = 12;
            }
        }

        private void OnDestroy()
        {
            StudyRequested = null;
            SkillPlacementRequested = null;
            SkillRemovalRequested = null;
            ShopRequested = null;
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_root);
        }
    }
}
