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
        private int _rosterPage;
        private OwnerGrowthRosterFilter _rosterFilter;
        private string _rosterQuery = string.Empty;
        private InputField _rosterSearchInput;
        private ScrollRect _skillInventoryScroll;
        private float _skillInventoryPosition = 1f;
        private int _displayedSkillRarity = -1;
        private bool _isDisplayedSkillPitcher;
        private Button[] _boardButtons = Array.Empty<Button>();
        private Text _rotationLabel;
        private UI_Popup_OwnerOffseason _offseasonPopup;
        private RectTransform _popupHost;
        public void SetPopupHost(RectTransform host) => _popupHost = host;
        private Baseball.Game.Historical.OwnerModeManager _developmentManager;
        private UI_Popup_OwnerDevelopment _developmentPopup;
        private UI_Popup_OwnerTraitTraining _traitPopup;
        public void SetDevelopmentManager(Baseball.Game.Historical.OwnerModeManager manager) => _developmentManager = manager;
        public event Action<int> OffseasonWeekAdvanceRequested;
        private readonly List<RectTransform> _rotationTiles = new List<RectTransform>();

        public event Action<string, string> StudyRequested;
        public event Action<string, int, int, int, int> SkillPlacementRequested;
        public event Action<string, int> SkillRemovalRequested;
        public event Action ShopRequested;

        /// <summary>공용 Shell의 업무 영역 아래에 성장 화면을 생성한다.</summary>
        public static UI_Scene_OwnerGrowth CreateRuntime(RectTransform host)
        {
            var view = new GameObject(nameof(UI_Scene_OwnerGrowth)).AddComponent<UI_Scene_OwnerGrowth>();
            view._root = OwnerWorkspaceUiFactory.CreateRoot(host, "OwnerGrowthWorkspace", false);
            UIOwnerFrontOfficePanel.ApplyWorkspace(view._root);
            view._sheet = OwnerRuntimeUiFactory.CreateRect("ReferenceGrowthSheet", view._root);
            view._sheet.anchorMin = view._sheet.anchorMax = new Vector2(.5f, .5f);
            view._sheet.sizeDelta = new Vector2(1100, 560);
            view._feedback = Label(view._sheet, "Feedback", "선수를 선택하세요.", 12, 20, 522, 1060, 28);
            view.Resize();
            return view;
        }

        /// <summary>실제 카드·인벤토리·유학 상태를 갱신하고 현재 선택을 유지한다.</summary>
        public void Bind(OwnerGrowthSnapshot snapshot, string routeId = null)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (routeId != null) _isStudy = routeId == OwnerNavigationRoutes.PowerUpStudy;
            _pendingStudy = string.Empty;
            EnsureSelection();
            Render();
            if (_offseasonPopup != null && _offseasonPopup.IsVisible) _offseasonPopup.Bind(snapshot.Offseason);
        }

        /// <summary>스킬 배치 또는 유학 메뉴를 선택한다.</summary>
        public void ShowRoute(string routeId)
        {
            bool isStudy = routeId == OwnerNavigationRoutes.PowerUpStudy;
            bool needsRender = _content == null || _isStudy != isStudy ||
                _isChoosingStudyPlayer || !string.IsNullOrEmpty(_pendingStudy) ||
                isStudy && !string.IsNullOrEmpty(_programId);
            _isStudy = isStudy;
            if (needsRender) _rosterPage = 0;
            _isChoosingStudyPlayer = false;
            if (isStudy) _programId = string.Empty;
            _pendingStudy = string.Empty;
            SetFeedback(_isStudy ? "과정과 선수를 선택한 뒤 비용·성장 결과를 확인하세요."
                : "블록 선택 → 회전 → 초록색 성장판 칸을 눌러 바로 배치", false);
            if (needsRender)
                Render();
        }

        /// <summary>성장 업무 영역의 표시 여부를 변경한다.</summary>
        public void SetVisible(bool visible)
        {
            if (!visible && _offseasonPopup != null) _offseasonPopup.Hide();
            if (!visible && _developmentPopup != null) _developmentPopup.Close();
            if (!visible && _traitPopup != null) _traitPopup.Close();
            _root.gameObject.SetActive(visible);
        }

        /// <summary>정산 실패 시 이전 일정에서 다시 시도할 수 있도록 팝업 입력을 복원한다.</summary>
        public void ShowOffseasonError() => _offseasonPopup?.ShowError();

        /// <summary>선수 선택, 유학 확인, 스킬 블록 선택 중 가장 안쪽 작업만 취소한다.</summary>
        public bool TryHandleCancel()
        {
            if (_traitPopup != null && _traitPopup.IsVisible) return _traitPopup.TryHandleCancel();
            if (_developmentPopup != null) return _developmentPopup.TryHandleCancel();
            if (_offseasonPopup != null && _offseasonPopup.TryHandleCancel()) return true;
            if (_isChoosingStudyPlayer)
            {
                foreach (Dropdown dropdown in _studyPickerSafe.GetComponentsInChildren<Dropdown>())
                    if (dropdown.transform.Find("Dropdown List") != null)
                    { dropdown.Hide(); dropdown.Select(); return true; }
                CloseStudyPicker(false);
                return true;
            }
            if (!string.IsNullOrEmpty(_pendingStudy))
            {
                _pendingStudy = string.Empty;
                Render();
                return true;
            }
            if (_isStudy && !string.IsNullOrEmpty(_programId))
            {
                _programId = string.Empty;
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
            _feedback.color = isError ? CareerUiTheme.Error : OwnerDashboardStyle.Ivory;
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
            List<OwnerGrowthCardSnapshot> candidates = OwnerGrowthRosterPresentationBuilder.Build(
                _snapshot,
                _isPitcher,
                OwnerGrowthRosterFilter.All, sortByCost: !_isStudy);
            if (candidates.Count > 0)
                _cardId = candidates[0].Card.CardId;
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
            string previousFocus = EventSystem.current?.currentSelectedGameObject != null && _content != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_content)
                ? EventSystem.current.currentSelectedGameObject.name : null;
            if (_skillInventoryScroll != null)
                _skillInventoryPosition = _skillInventoryScroll.verticalNormalizedPosition;
            if (_displayedSkillRarity != _rarity || _isDisplayedSkillPitcher != _isPitcher)
                _skillInventoryPosition = 1f;
            _skillInventoryScroll = null;
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
            Tab(_content, "OffseasonCalendar", "훈련 일정", ShowOffseason, false, 924, 42, 152, 28);
            Tab(_content, "DevelopmentOffice", "성장 관리", () =>
            {
                if (_developmentPopup != null) return;
                _developmentPopup = UI_Popup_OwnerDevelopment.Show(_popupHost != null ? _popupHost : _root, _developmentManager);
            }, false, 756, 42, 152, 28);
            Label(_content, "SchedulePermission", _snapshot.SeasonPhase == Baseball.Game.Historical.OwnerSeasonPhase.Offseason
                ? _snapshot.OffseasonCompletedWeeks == Baseball.Core.Historical.OwnerOffseasonState.DurationWeeks
                    ? "오프시즌 · 훈련 종료 · 스킬 편성 가능"
                    : $"오프시즌 · {Baseball.Core.Historical.OwnerOffseasonState.DurationWeeks - _snapshot.OffseasonCompletedWeeks}주 남음"
                : "시즌 중 · 특성훈련 잠금", 13, 246, 42, 315, 28);
            Tab(_content, "TraitTraining", "특성훈련", () => {
                if (_traitPopup != null && _traitPopup.IsVisible) return;
                _traitPopup = UI_Popup_OwnerTraitTraining.Show(_popupHost != null ? _popupHost : _root,
                    _developmentManager, _cardId, ShowOffseason);
            }, false, 588, 42, 152, 28);
            OwnerDashboardStyle.SetDataSurface(Surface(_content, "BlueRule", 20, 72, 1060, 1, OwnerDashboardStyle.Line), OwnerDashboardStyle.Line);
            if (_isStudy) RenderStudy(); else RenderSkills();
            _feedback.transform.SetAsLastSibling();
            if (!_isStudy && previousFocus != null) FocusRosterControl(previousFocus);
            Resize();
        }

        private void SelectType(bool pitcher)
        {
            _isPitcher = pitcher;
            _studyPosition = 0;
            _rosterPage = 0;
            _rosterFilter = OwnerGrowthRosterFilter.All;
            _rosterQuery = string.Empty;
            _cardId = _programId = _pendingStudy = string.Empty;
            _instanceId = _rotation = 0;
            EnsureSelection();
            Render();
        }

        private void RenderRoster(Transform parent, float x, float y, float width, float height, int columns,
            bool applyFilters = false, float cardHeight = 92)
        {
            // 현재 보이는 두 행만 생성해 보유 카드 수가 화면 생성 비용을 늘리지 않게 한다.
            int pageSize = columns * 2;
            List<OwnerGrowthCardSnapshot> candidates = OwnerGrowthRosterPresentationBuilder.Build(
                _snapshot,
                _isPitcher,
                applyFilters ? _rosterFilter : OwnerGrowthRosterFilter.All,
                applyFilters ? _rosterQuery : null, sortByCost: !_isStudy);
            int pageCount = Math.Max(1, (candidates.Count + pageSize - 1) / pageSize);
            _rosterPage = Math.Max(0, Math.Min(_rosterPage, pageCount - 1));
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalGridScroll("PlayerInventory", parent,
                columns, new Vector2((width - 22 - (columns - 1) * 5) / columns, cardHeight), 5,
                out RectTransform cards);
            Place(scroll.GetComponent<RectTransform>(), x, y, width, height - 28);
            cards.GetComponent<GridLayoutGroup>().padding = new RectOffset(5, 5, 5, 5);
            AddScrollbar(scroll);
            int start = _rosterPage * pageSize;
            int end = Math.Min(candidates.Count, start + pageSize);
            for (int index = start; index < end; index++)
            {
                OwnerGrowthCardSnapshot candidate = candidates[index];
                OwnerGrowthCardSnapshot selected = candidate;
                PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(cards, "Card_" + candidate.Card.CardId);
                card.UseLineupSlotLayout();
                card.UseRosterPresentation();
                card.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(candidate.Card, candidate.Card.CardId == _cardId));
                card.SetPortrait(PlayerPortraitSprites.GetDefault(candidate.Card.Position));
                card.Selected += _ =>
                {
                    _cardId = selected.Card.CardId;
                    _pendingStudy = string.Empty;
                    _instanceId = _rotation = 0;
                    Render();
                    FocusRosterControl("Card_" + _cardId);
                };
                card.DetailRequested += _ => UI_Popup_OwnerPlayerCard.Show(_root, new[] { selected.DetailCard }, 0);
            }
            if (candidates.Count == 0) Label(parent, "NoPlayers", applyFilters
                ? "조건에 맞는 선수가 없습니다.\n검색어나 카드 필터를 초기화하세요."
                : "해당 유형의 보유 선수가 없습니다.", 13, x + 8, y + 20, width - 16, 48);
            Button previous = Tab(parent, "PreviousRosterPage", "이전", () => ChangeRosterPage(-1),
                false, x, y + height - 26, 60, 24);
            previous.interactable = _rosterPage > 0;
            Label(parent, "RosterPage", $"{_rosterPage + 1}/{pageCount} · {candidates.Count}명", 12,
                x + 65, y + height - 26, width - 130, 24).alignment = TextAnchor.MiddleCenter;
            Button next = Tab(parent, "NextRosterPage", "다음", () => ChangeRosterPage(1),
                false, x + width - 60, y + height - 26, 60, 24);
            next.interactable = _rosterPage + 1 < pageCount;
        }

        private void RenderRoster(Transform parent, float x, float y, float width, float height, int columns,
            float cardHeight) => RenderRoster(parent, x, y, width, height, columns, false, cardHeight);

        private void ChangeRosterPage(int delta)
        {
            _rosterPage += delta;
            Render();
            // 페이지를 만든 뒤에도 키보드·게임패드로 다음 페이지 또는 되돌아가기를 이어 간다.
            if (EventSystem.current == null) return;
            string preferred = delta > 0 ? "NextRosterPage" : "PreviousRosterPage";
            Button button = _content.Find(preferred).GetComponent<Button>();
            if (!button.interactable)
                button = _content.Find(delta > 0 ? "PreviousRosterPage" : "NextRosterPage").GetComponent<Button>();
            if (button.interactable) button.Select();
        }

        private void RenderSkills()
        {
            Frame(_content, "RosterFrame", 20, 86, 330, 416);
            Label(_content, "RosterHeading", "보유 선수  ·  코스트 높은 순", 14, 28, 91, 310, 24);
            RenderRosterSearch();
            RenderRosterFilters();
            RenderRoster(_content, 25, 174, 320, 318, 5, true);
            Frame(_content, "SelectedFrame", 360, 86, 234, 416);
            OwnerGrowthCardSnapshot card = SelectedCard();
            Label(_content, "SelectedName", card?.Card.DisplayName ?? "선수를 선택하세요", 16, 370, 91, 214, 26);
            if (card != null)
            {
                PlayerMiniCardView preview = PlayerMiniCardView.CreateRuntime(_content, "SelectedPlayerCard");
                preview.UseLineupSlotLayout();
                preview.UseRosterPresentation();
                Place(preview.GetComponent<RectTransform>(), 371, 123, 85, 117);
                preview.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(card.DetailCard, false));
                preview.SetPortrait(PlayerPortraitSprites.GetDefault(card.Card.Position));
                string detail = OwnerCollectionPresentationBuilder.FormatPlayerRole(
                        card.Card.Position,
                        card.Card.PitcherRole, card.Card.IsPositionEvidenceMissing) + "\n" +
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
                    FocusRosterControl("Block_" + instanceId);
                }, instanceId == _instanceId, 0, 0, 141, 89);
                OwnerDashboardStyle.SetDataRow(button, instanceId == _instanceId, OwnerDashboardStyle.TableSurface);
                button.GetComponent<LayoutElement>().preferredHeight = 89;
                button.interactable = string.IsNullOrEmpty(equipped) || equipped == _cardId;
                RenderSkillIcon(button.transform, definition);
                Label(button.transform, "BlockName", DescribeBlock(definition), 11, 3, 64, 135, 21).alignment = TextAnchor.MiddleCenter;
                Label(button.transform, "BlockStatus", string.IsNullOrEmpty(equipped) ? RarityLabel(definition.Rarity)
                    : equipped == _cardId ? "장착 중" : "다른 선수 사용 중", 10, 4, 0, 133, 19);
                count++;
            }
            if (count == 0) Label(_content, "NoBlocks", CountAvailableBlocks() == 0
                ? "보유 블록이 없습니다.\n상점에서 스킬 블록을 획득하세요."
                : "선택한 희귀도의 블록이 없습니다.\n전체 탭에서 보유 블록을 확인하세요.", 14, 630, 207, 410, 90);
            RenderSkillActions(card);
            _skillInventoryScroll = scroll;
            _displayedSkillRarity = _rarity;
            _isDisplayedSkillPitcher = _isPitcher;
            // 새 목록의 높이가 확정된 뒤 복원해야 다음 Layout 단계에서 맨 위로 보정되지 않는다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = _skillInventoryPosition;
        }

        private void RenderRosterSearch()
        {
            Image surface = Surface(_content, "RosterSearch", 28, 118, 176, 24, OwnerDashboardStyle.TableSurface);
            surface.raycastTarget = true;
            var outline = surface.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);
            var input = surface.gameObject.AddComponent<InputField>();
            Text value = Label(surface.transform, "Text", string.Empty, 11, 7, 0, 162, 24);
            Text placeholder = Label(surface.transform, "Placeholder", "이름·포지션·연도", 11, 7, 0, 162, 24);
            placeholder.color = new Color32(122, 132, 145, 255);
            input.textComponent = value;
            input.placeholder = placeholder;
            input.targetGraphic = surface;
            OwnerDashboardStyle.SetDataInput(input);
            input.text = _rosterQuery;
            input.lineType = InputField.LineType.SingleLine;
            input.onEndEdit.AddListener(value =>
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    ApplyRosterSearch(value);
            });
            _rosterSearchInput = input;

            Tab(_content, "ApplyRosterSearch", "찾기", () => ApplyRosterSearch(_rosterSearchInput.text),
                false, 209, 118, 58, 24);
            Button reset = Tab(_content, "ResetRosterSearch", "초기화", ResetRosterSearch,
                false, 272, 118, 70, 24);
            reset.interactable = _rosterQuery.Length > 0 || _rosterFilter != OwnerGrowthRosterFilter.All;
        }

        private void RenderRosterFilters()
        {
            int all = OwnerGrowthRosterPresentationBuilder.Count(
                _snapshot, _isPitcher, OwnerGrowthRosterFilter.All);
            int equipped = OwnerGrowthRosterPresentationBuilder.Count(
                _snapshot, _isPitcher, OwnerGrowthRosterFilter.ActiveRoster);
            int reserve = all - equipped;
            RenderRosterFilter("RosterAll", $"전체 {all}", OwnerGrowthRosterFilter.All, 28);
            RenderRosterFilter("RosterEquipped", $"장착 {equipped}", OwnerGrowthRosterFilter.ActiveRoster, 132);
            RenderRosterFilter("RosterReserve", $"보관 {reserve}", OwnerGrowthRosterFilter.Reserve, 236);
        }

        private void RenderRosterFilter(string name, string label, OwnerGrowthRosterFilter filter, float x)
        {
            Tab(_content, name, label, () => ApplyRosterFilter(filter), _rosterFilter == filter,
                x, 146, 99, 24);
        }

        private void ApplyRosterFilter(OwnerGrowthRosterFilter filter)
        {
            _rosterFilter = filter;
            _rosterPage = 0;
            SelectFirstVisibleRosterCard();
            Render();
            FocusRosterControl(filter switch
            {
                OwnerGrowthRosterFilter.ActiveRoster => "RosterEquipped",
                OwnerGrowthRosterFilter.Reserve => "RosterReserve",
                _ => "RosterAll"
            });
        }

        private void ApplyRosterSearch(string query)
        {
            _rosterQuery = query?.Trim() ?? string.Empty;
            _rosterPage = 0;
            SelectFirstVisibleRosterCard();
            Render();
            FocusRosterControl("RosterSearch");
        }

        private void ResetRosterSearch()
        {
            _rosterFilter = OwnerGrowthRosterFilter.All;
            _rosterQuery = string.Empty;
            _rosterPage = 0;
            SelectFirstVisibleRosterCard();
            Render();
            FocusRosterControl("RosterAll");
        }

        private void SelectFirstVisibleRosterCard()
        {
            List<OwnerGrowthCardSnapshot> cards = OwnerGrowthRosterPresentationBuilder.Build(
                _snapshot,
                _isPitcher,
                _rosterFilter,
                _rosterQuery, sortByCost: !_isStudy);
            _cardId = cards.Count > 0 ? cards[0].Card.CardId : string.Empty;
            _pendingStudy = string.Empty;
            _instanceId = _rotation = 0;
        }

        private void FocusRosterControl(string objectName)
        {
            if (EventSystem.current == null || _content == null) return;
            foreach (Selectable selectable in _content.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable.name != objectName || !selectable.IsInteractable()) continue;
                selectable.Select();
                return;
            }
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

        private void RenderBoard(OwnerGrowthCardSnapshot card, bool reuseCells = false)
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
            if (!reuseCells)
            {
                _boardCellImages = new Image[boardCellCount];
                _boardCellColors = new Color[boardCellCount];
                _boardButtons = new Button[boardCellCount];
            }
            for (int y = 0; y < _snapshot.Board.Height; y++)
            for (int x = 0; x < _snapshot.Board.Width; x++)
            {
                int column = x, row = y, equippedInstance = 0;
                Color color = OwnerDashboardStyle.TableAlternate;
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
                int cellIndex = row * _snapshot.Board.Width + column;
                Button button = reuseCells ? _boardButtons[cellIndex] :
                    Tab(_content, "BoardCell_" + x + "_" + y, "", () => { }, false,
                        384 + x * cellSize, 278 + y * cellSize, cellSize - 2, cellSize - 2);
                _boardButtons[cellIndex] = button;
                button.GetComponentInChildren<Text>().text = "";
                button.onClick.RemoveAllListeners();
                EventTrigger trigger = button.GetComponent<EventTrigger>();
                if (trigger != null) trigger.triggers.Clear();
                button.onClick.AddListener(() =>
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
                });
                // 상태색은 즉시 반영해 회전마다 비활성 전환의 페이드가 번쩍이지 않게 한다.
                button.colors = CreateBoardColors(button.colors);
                button.interactable = occupiedId > 0 || canPlace && _snapshot.SkillPermission.IsAllowed;
                Image cellImage = button.GetComponent<Image>();
                OwnerDashboardStyle.SetDataSurface(cellImage, color, true);
                _boardCellImages[cellIndex] = cellImage;
                _boardCellColors[cellIndex] = color;
                OwnerDashboardStyle.ConfigureDataControl(button);
                if (canPlace)
                {
                    button.GetComponentInChildren<Text>().text = "＋";
                    BoardCell[] previewCells = placementPreview.Cells;
                    AddPointerListener(button.gameObject, EventTriggerType.PointerEnter,
                        () => ShowBoardPlacementPreview(previewCells));
                    AddPointerListener(button.gameObject, EventTriggerType.PointerExit,
                        ClearBoardPlacementPreview);
                    AddPointerListener(button.gameObject, EventTriggerType.Select,
                        () => ShowBoardPlacementPreview(previewCells));
                    AddPointerListener(button.gameObject, EventTriggerType.Deselect,
                        ClearBoardPlacementPreview);
                }
                if (!reuseCells && equippedInstance > 0)
                    CreateSkillTile(button.transform,
                        FindDefinition(FindInventoryBlock(equippedInstance).DefinitionId).Rarity, 0, 0, cellSize - 2);
            }
            if (!reuseCells) Label(_content, "BoardHint", selectedLocalCells == null
                ? "블록을 고르면 배치 가능한 칸이 표시됩니다."
                : "초록색 칸을 누르면 바로 장착됩니다.", 11, 371, 465, 214, 28);
        }

        private void RenderSkillActions(OwnerGrowthCardSnapshot card)
        {
            SkillBlockDefinition selected = null;
            foreach (SkillBlockInstance block in _snapshot.Inventory)
                if (block.InstanceId == _instanceId) selected = FindDefinition(block.DefinitionId);
            string detail = selected == null ? "블록을 선택하면 능력치 효과가 표시됩니다." : DescribeBlock(selected) + "  ·  " + DescribeBonuses(selected);
            Label(_content, "SkillDescription", detail, 12, 724, 401, 340, 42);
            _rotationLabel = Label(_content, "RotationState", "", 11, 724, 438, 106, 16);
            _rotationTiles.Clear();
            if (selected != null)
                foreach (BoardCell cell in selected.ShapeCells)
                {
                    CreateSkillTile(_content, selected.Rarity, 619, 399, 12);
                    _rotationTiles.Add((RectTransform)_content.GetChild(_content.childCount - 1));
                }
            UpdateRotationPreview();
            Button rotate = Tab(_content, "Rotate", "90° 회전", RotateSelectedBlock, false, 619, 458, 100, 28);
            rotate.interactable = selected != null && selected.CanRotate && string.IsNullOrEmpty(_snapshot.GetEquippedCardId(_instanceId));
            Label(_content, "PlacementActionHint", "칸 선택 시 장착", 11, 724, 458, 103, 28).alignment = TextAnchor.MiddleCenter;
            Button remove = Tab(_content, "RemovePlacement", "블록 해제", () => SkillRemovalRequested?.Invoke(_cardId, _instanceId), false, 832, 450, 108, 32);
            remove.interactable = _snapshot.SkillPermission.IsAllowed && card != null && _instanceId > 0 && _snapshot.GetEquippedCardId(_instanceId) == _cardId;
            Tab(_content, "SkillShop", "스킬 상점", () => ShopRequested?.Invoke(), false, 945, 450, 120, 32);
            if (!_snapshot.SkillPermission.IsAllowed) SetFeedback(_snapshot.SkillPermission.Reason, false);
        }

        private static ColorBlock CreateBoardColors(ColorBlock colors)
        {
            colors.fadeDuration = 0;
            return colors;
        }

        private void RotateSelectedBlock()
        {
            SkillBlockDefinition definition = FindDefinition(FindInventoryBlock(_instanceId).DefinitionId);
            if (definition == null || !definition.CanRotate ||
                !string.IsNullOrEmpty(_snapshot.GetEquippedCardId(_instanceId))) return;
            _rotation = (_rotation + 1) % 4;
            RenderBoard(SelectedCard(), true);
            UpdateRotationPreview();
        }

        private void UpdateRotationPreview()
        {
            SkillBlockInstance instance = FindInventoryBlock(_instanceId);
            if (instance.InstanceId == 0)
            {
                _rotationLabel.text = "블록 선택 필요";
                return;
            }
            int rotation = _rotation;
            OwnerGrowthCardSnapshot card = SelectedCard();
            if (card != null)
                foreach (PlacedSkillBlock placement in card.Placements)
                    if (placement.Instance.InstanceId == _instanceId) rotation = placement.RotationQuarterTurns;
            var service = new SkillBoardService(_snapshot.Board, _snapshot.Definitions);
            BoardCell[] cells = service.GetOccupiedCells(new PlacedSkillBlock(instance, 0, 0, rotation));
            int width = 1, height = 1;
            foreach (BoardCell cell in cells)
            {
                width = Math.Max(width, cell.X + 1);
                height = Math.Max(height, cell.Y + 1);
            }
            float size = Mathf.Min(16, 54f / height);
            for (int index = 0; index < cells.Length; index++)
                Place(_rotationTiles[index], 619 + (100 - width * size) * .5f + cells[index].X * size,
                    399 + (54 - height * size) * .5f + cells[index].Y * size, size, size);
            SkillBlockDefinition definition = FindDefinition(instance.DefinitionId);
            _rotationLabel.text = !string.IsNullOrEmpty(_snapshot.GetEquippedCardId(_instanceId))
                ? $"장착 방향 {rotation * 90}°" : definition.CanRotate ? $"현재 방향 {rotation * 90}°" : "회전 불가";
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
                CreateSkillTile(parent, definition.Rarity,
                    (141 - width * size) * .5f + cell.X * size,
                    19 + (48 - height * size) * .5f + cell.Y * size, size);
        }

        private void CreateSkillTile(Transform parent, SkillBlockRarity rarity, float x, float y, float size)
        {
            var tile = new GameObject("SkillTile", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            tile.transform.SetParent(parent, false);
            Place(tile.rectTransform, x, y, size, size);
            SkillBlockVisual.ApplyTile(tile, rarity);
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
            if (_offseasonPopup != null)
            {
                if (Application.isPlaying) Destroy(_offseasonPopup.gameObject);
                else DestroyImmediate(_offseasonPopup.gameObject);
            }
            OffseasonWeekAdvanceRequested = null;
            if (_developmentPopup != null) _developmentPopup.Close();
            StudyRequested = null;
            SkillPlacementRequested = null;
            SkillRemovalRequested = null;
            ShopRequested = null;
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_root);
        }

        private void ShowOffseason()
        {
            if (_offseasonPopup == null)
            {
                _offseasonPopup = UI_Popup_OwnerOffseason.CreateRuntime(_popupHost != null ? _popupHost : _root);
                _offseasonPopup.WeekAdvanceRequested += week => OffseasonWeekAdvanceRequested?.Invoke(week);
                _offseasonPopup.Closed += () => FocusRosterControl("OffseasonCalendar");
            }
            _offseasonPopup.Show(_snapshot.Offseason);
        }
    }
}
