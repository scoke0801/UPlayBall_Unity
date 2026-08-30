using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// 선수 생성부터 구단 계약과 Rookie League 시작까지를 한 화면 마법사로 표시한다.
    /// </summary>
    internal sealed class UI_Scene_NewGame_Legacy : UISceneBase
    {
        private static readonly Color BackgroundColor = new(0.018f, 0.027f, 0.045f, 1f);
        private static readonly Color PanelColor = new(0.035f, 0.055f, 0.085f, 0.98f);
        private static readonly Color CardColor = new(0.06f, 0.085f, 0.12f, 1f);
        private static readonly Color AccentColor = new(0.18f, 0.68f, 0.88f, 1f);
        private static readonly Color SelectedColor = new(0.12f, 0.34f, 0.48f, 1f);
        private static readonly Color PrimaryTextColor = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.62f, 0.71f, 0.8f, 1f);
        private static readonly Color WarningColor = new(1f, 0.74f, 0.3f, 1f);
        private static readonly Color ErrorColor = new(1f, 0.42f, 0.42f, 1f);

        private readonly int[] _attributeDraft = new int[CareerCreationRules.MaximumAttributeCount];
        private NewGameManager _manager;
        private RectTransform _body;
        private Text _title;
        private Text _progress;
        private Text _error;
        private Button _backButton;
        private Button _nextButton;
        private Text _nextLabel;
        private InputField _nameInput;
        private InputField _nationalityInput;
        private bool _hasAttributeDraft;

        public override bool BlocksLowerInput => true;

        /// <summary>
        /// 프리팹이 없는 프로토타입 환경에서 같은 화면을 런타임 생성한다.
        /// </summary>
        public static UI_Scene_NewGame_Legacy CreateRuntime(Transform parent)
        {
            var screenObject = new GameObject(
                nameof(UI_Scene_NewGame_Legacy),
                typeof(RectTransform),
                typeof(CanvasGroup));
            screenObject.transform.SetParent(parent, false);
            UI_Scene_NewGame_Legacy screen = screenObject.AddComponent<UI_Scene_NewGame_Legacy>();
            Stretch(screenObject.GetComponent<RectTransform>());
            return screen;
        }

        protected override void OnInitialize()
        {
            _manager = GameManager.EnsureExists().EnsureManager<NewGameManager>("NewGameManager");
            _manager.FlowChanged += Render;
            BuildHierarchy();
            Render();
        }

        protected override void OnShow()
        {
            Render();
        }

        protected override void OnDestroy()
        {
            if (_manager != null)
                _manager.FlowChanged -= Render;
            base.OnDestroy();
        }

        private void BuildHierarchy()
        {
            RectTransform root = (RectTransform)transform;
            Stretch(root);
            CreateImage("Background", root, BackgroundColor, Vector2.zero, Vector2.zero, stretch: true);
            RectTransform panel = CreateImage(
                "NewGamePanel", root, PanelColor, new Vector2(1360f, 920f), Vector2.zero);

            _title = CreateText(
                "Title", panel, "새 선수 커리어", 34, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(760f, 64f), new Vector2(-250f, 400f), PrimaryTextColor);
            _progress = CreateText(
                "Progress", panel, string.Empty, 18, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(420f, 48f), new Vector2(410f, 400f), SecondaryTextColor);
            _body = CreateRect("Body", panel, new Vector2(1240f, 690f), new Vector2(0f, 15f));
            _error = CreateText(
                "Error", panel, string.Empty, 17, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(900f, 40f), new Vector2(0f, -350f), ErrorColor);
            _backButton = CreateButton(
                "Back", panel, "이전", new Vector2(160f, 52f), new Vector2(-510f, -405f),
                CardColor, out _);
            _nextButton = CreateButton(
                "Next", panel, "다음", new Vector2(240f, 52f), new Vector2(470f, -405f),
                AccentColor, out _nextLabel);
        }

        private void Render()
        {
            if (_body == null || _manager == null)
                return;

            ClearChildren(_body);
            _progress.text = GetProgressText(_manager.CurrentStep);
            _error.text = _manager.LastError;
            ConfigureBackButton();

            switch (_manager.CurrentStep)
            {
                case NewGameStep.Identity:
                    RenderIdentity();
                    break;
                case NewGameStep.PlayerType:
                    RenderPlayerType();
                    break;
                case NewGameStep.Position:
                    RenderPosition();
                    break;
                case NewGameStep.Handedness:
                    RenderHandedness();
                    break;
                case NewGameStep.AttributeAllocation:
                    RenderAttributes();
                    break;
                case NewGameStep.PlayerCard:
                    RenderPlayerCard();
                    break;
                case NewGameStep.ContractOffers:
                    RenderOffers();
                    break;
                case NewGameStep.ContractComplete:
                    RenderContractComplete();
                    break;
                case NewGameStep.Completed:
                    RenderSeasonStarted();
                    break;
            }
        }

        private void RenderIdentity()
        {
            SetTitle("선수 기본 정보", "이름과 국적은 커리어 기록에 남습니다.");
            _nameInput = CreateInputField(
                "PlayerName", _body, "선수 이름", _manager.PlayerName,
                new Vector2(520f, 60f), new Vector2(0f, 90f));
            _nationalityInput = CreateInputField(
                "Nationality", _body, "국적",
                string.IsNullOrWhiteSpace(_manager.Nationality) ? "대한민국" : _manager.Nationality,
                new Vector2(520f, 60f), new Vector2(0f, 5f));
            CreateText(
                "Seed", _body, $"WORLD SEED  {_manager.RandomSeed}", 15, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(600f, 34f), new Vector2(0f, -80f), SecondaryTextColor);
            SetNext("선수 유형 선택", () => _manager.SubmitIdentity(_nameInput.text, _nationalityInput.text));
        }

        private void RenderPlayerType()
        {
            _hasAttributeDraft = false;
            SetTitle("투수 / 타자 선택", "경기에서 성장시킬 역할을 먼저 정합니다.");
            CreateChoice("Batter", "BATTER\n타자 커리어", new Vector2(-285f, 20f),
                () => _manager.SelectPlayerType(PlayerType.Batter));
            CreateChoice("Pitcher", "PITCHER\n투수 커리어", new Vector2(285f, 20f),
                () => _manager.SelectPlayerType(PlayerType.Pitcher));
            HideNext();
        }

        private void RenderPosition()
        {
            SetTitle("포지션 선택", "추천과 다른 능력치 배분도 허용됩니다.");
            PlayerPosition[] positions = _manager.PlayerType == PlayerType.Pitcher
                ? new[] { PlayerPosition.StartingPitcher, PlayerPosition.ReliefPitcher }
                : new[]
                {
                    PlayerPosition.Catcher, PlayerPosition.FirstBase, PlayerPosition.SecondBase,
                    PlayerPosition.ThirdBase, PlayerPosition.Shortstop, PlayerPosition.LeftField,
                    PlayerPosition.CenterField, PlayerPosition.RightField, PlayerPosition.DesignatedHitter
                };
            int columns = positions.Length <= 2 ? 2 : 3;
            for (int index = 0; index < positions.Length; index++)
            {
                PlayerPosition position = positions[index];
                int row = index / columns;
                int column = index % columns;
                float x = (column - (columns - 1) * 0.5f) * 330f;
                float y = 170f - row * 115f;
                Button button = CreateButton(
                    "Position_" + position, _body, GetPositionLabel(position),
                    new Vector2(285f, 82f), new Vector2(x, y), CardColor, out _);
                button.onClick.AddListener(() => _manager.SelectPosition(position));
            }
            HideNext();
        }

        private void RenderHandedness()
        {
            SetTitle("투타 선택", "투구 손은 좌·우, 타격은 스위치까지 선택할 수 있습니다.");
            var choices = new[]
            {
                ("우투우타", Handedness.Right, Handedness.Right),
                ("우투좌타", Handedness.Left, Handedness.Right),
                ("우투양타", Handedness.Switch, Handedness.Right),
                ("좌투우타", Handedness.Right, Handedness.Left),
                ("좌투좌타", Handedness.Left, Handedness.Left),
                ("좌투양타", Handedness.Switch, Handedness.Left)
            };
            for (int index = 0; index < choices.Length; index++)
            {
                var choice = choices[index];
                float x = (index % 3 - 1) * 330f;
                float y = 120f - index / 3 * 120f;
                Button button = CreateButton(
                    "Hands_" + index, _body, choice.Item1,
                    new Vector2(285f, 82f), new Vector2(x, y), CardColor, out _);
                button.onClick.AddListener(() => _manager.SelectHandedness(choice.Item2, choice.Item3));
            }
            HideNext();
        }

        private void RenderAttributes()
        {
            SetTitle("초기 능력치 배분", "프리셋으로 빠르게 배분한 뒤 원하는 능력치만 조정하세요.");
            EnsureAttributeDraft();
            CareerAttributeAllocationRule rule = _manager.CurrentCreationAttributeRule;
            string[] names = _manager.PlayerType == PlayerType.Pitcher
                ? new[] { "Stuff", "Control", "Breaking", "Stamina" }
                : new[] { "Contact", "Power", "Eye", "Speed", "Defense", "Arm" };
            int remaining = rule.BonusPoints - GetSpentPoints(rule);
            CreateText(
                "Remaining", _body,
                $"남은 포인트  {remaining} / {rule.BonusPoints}    ·    상한 {rule.MaxValue}",
                20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(700f, 44f), new Vector2(0f, 260f), remaining == 0 ? AccentColor : PrimaryTextColor);

            RenderAttributePresets();

            for (int index = 0; index < names.Length; index++)
            {
                int captured = index;
                float y = 135f - index * 62f;
                CreateText(
                    "Name_" + index, _body, names[index], 19, FontStyle.Bold, TextAnchor.MiddleLeft,
                    new Vector2(260f, 48f), new Vector2(-320f, y), PrimaryTextColor);
                Button minusFive = CreateButton(
                    "MinusFive_" + index, _body, "−5", new Vector2(58f, 44f), new Vector2(10f, y),
                    CardColor, out _);
                minusFive.interactable = _attributeDraft[index] > rule.BaseValue;
                minusFive.onClick.AddListener(() => ChangeAttribute(captured, -5));
                Button minus = CreateButton(
                    "Minus_" + index, _body, "−", new Vector2(52f, 44f), new Vector2(72f, y),
                    CardColor, out _);
                minus.interactable = _attributeDraft[index] > rule.BaseValue;
                minus.onClick.AddListener(() => ChangeAttribute(captured, -1));
                CreateText(
                    "Value_" + index, _body, _attributeDraft[index].ToString(), 22, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(70f, 48f), new Vector2(135f, y), AccentColor);
                Button plus = CreateButton(
                    "Plus_" + index, _body, "+", new Vector2(52f, 44f), new Vector2(198f, y),
                    CardColor, out _);
                plus.interactable = _attributeDraft[index] < rule.MaxValue && remaining > 0;
                plus.onClick.AddListener(() => ChangeAttribute(captured, 1));
                Button plusFive = CreateButton(
                    "PlusFive_" + index, _body, "+5", new Vector2(58f, 44f), new Vector2(260f, y),
                    CardColor, out _);
                plusFive.interactable = _attributeDraft[index] < rule.MaxValue && remaining > 0;
                plusFive.onClick.AddListener(() => ChangeAttribute(captured, 5));
            }
            SetNext("선수 카드 생성", () => _manager.SubmitAttributes(_attributeDraft));
        }

        private void RenderAttributePresets()
        {
            IReadOnlyList<AttributeAllocationPresetView> presets = _manager.AttributeAllocationPresets;
            const float buttonSpacing = 168f;
            float startX = -(presets.Count * buttonSpacing) * 0.5f + buttonSpacing * 0.5f;
            for (int index = 0; index < presets.Count; index++)
            {
                AttributeAllocationPresetView preset = presets[index];
                bool isSelected = IsPresetSelected(preset);
                string label = preset.IsRecommended ? "추천 · " + preset.Label : preset.Label;
                Button button = CreateButton(
                    "Preset_" + index, _body, label, new Vector2(156f, 46f),
                    new Vector2(startX + index * buttonSpacing, 205f),
                    isSelected ? SelectedColor : CardColor, out Text text);
                text.fontSize = preset.IsRecommended ? 15 : 17;
                button.onClick.AddListener(() => ApplyPreset(preset));
            }

            Button reset = CreateButton(
                "ResetAttributes", _body, "초기화", new Vector2(92f, 38f), new Vector2(485f, 260f),
                CardColor, out Text resetText);
            resetText.fontSize = 15;
            reset.interactable = GetSpentPoints(_manager.CurrentCreationAttributeRule) > 0;
            reset.onClick.AddListener(ResetAttributes);
        }

        private void RenderPlayerCard()
        {
            SetTitle("무소속 선수 카드", "이 능력치와 포지션으로 Rookie League 구단의 평가를 받습니다.");
            RectTransform card = CreateImage(
                "PlayerCard", _body, CardColor, new Vector2(760f, 430f), new Vector2(0f, 15f));
            CreateText(
                "CardName", card, _manager.PlayerName, 32, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(650f, 60f), new Vector2(0f, 150f), PrimaryTextColor);
            CreateText(
                "CardMeta", card,
                $"{_manager.Nationality}  ·  {GetPositionLabel(_manager.PrimaryPosition)}  ·  " +
                $"{GetThrowLabel(_manager.ThrowingHand)}{GetBatLabel(_manager.BattingHand)}  ·  무소속",
                19, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(650f, 42f), new Vector2(0f, 95f), SecondaryTextColor);
            CreateText(
                "CardAttributes", card, GetAttributeSummary(), 20, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(650f, 160f), new Vector2(0f, -5f), PrimaryTextColor);
            if (!string.IsNullOrWhiteSpace(_manager.BuildWarning))
            {
                CreateText(
                    "Warning", card, _manager.BuildWarning, 17, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(650f, 70f), new Vector2(0f, -140f), WarningColor);
            }
            SetNext("구단 오퍼 확인", () => _manager.GenerateOffers());
        }

        private void RenderOffers()
        {
            SetTitle("계약 오퍼 비교", "금액뿐 아니라 육성 환경과 실제 출장 기회를 함께 비교하세요.");
            IReadOnlyList<ContractOfferView> offers = _manager.Offers;
            bool hasSelection = false;
            for (int index = 0; index < offers.Count; index++)
            {
                ContractOfferView offer = offers[index];
                hasSelection |= offer.IsSelected;
                float y = 220f - index * 122f;
                Button button = CreateButton(
                    "Offer_" + offer.TeamId, _body, string.Empty,
                    new Vector2(1110f, 105f), new Vector2(0f, y),
                    offer.IsSelected ? SelectedColor : CardColor, out Text text);
                text.alignment = TextAnchor.MiddleLeft;
                text.rectTransform.offsetMin = new Vector2(28f, 8f);
                text.rectTransform.offsetMax = new Vector2(-24f, -8f);
                text.text =
                    $"{(offer.IsSelected ? "✓  " : string.Empty)}{offer.TeamName}  ·  {GetArchetypeLabel(offer.Archetype)}\n" +
                    $"계약금 {FormatMoney(offer.SigningBonus)}  |  연봉 {FormatMoney(offer.AnnualSalary)}  |  " +
                    $"{offer.ContractYears}년  |  육성 {GetGrade(offer.DevelopmentRating)}  |  " +
                    $"{GetRoleLabel(offer.ExpectedRole)}\n" +
                    $"{offer.EvaluationOpportunitySummary}  |  " +
                    $"포지션 필요도 {offer.PositionNeed}  ·  경쟁자 {offer.CompetitorSummary}";
                int teamId = offer.TeamId;
                button.onClick.AddListener(() => _manager.SelectOffer(teamId));
                RectTransform stripe = CreateImage(
                    "TeamColor", button.transform, ToColor(offer.PrimaryColor),
                    new Vector2(8f, 105f), new Vector2(-551f, 0f));
                stripe.GetComponent<Image>().raycastTarget = false;
            }
            SetNext("이 구단과 계약", () => _manager.SignSelectedOffer(), hasSelection);
        }

        private void RenderContractComplete()
        {
            SetTitle("계약 완료", "당신을 필요로 한 구단에서 첫 시즌을 시작합니다.");
            CareerSummaryView summary = _manager.CareerSummary.Value;
            RectTransform card = CreateImage(
                "ContractCard", _body, CardColor, new Vector2(820f, 410f), new Vector2(0f, 15f));
            CreateText(
                "Signed", card, "SIGNED", 18, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(300f, 35f), new Vector2(0f, 165f), AccentColor);
            CreateText(
                "Team", card, summary.TeamName, 34, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(700f, 64f), new Vector2(0f, 115f), PrimaryTextColor);
            CreateText(
                "Contract", card,
                $"{summary.PlayerName}  ·  {GetPositionLabel(summary.Position)}\n\n" +
                $"예상 역할  {GetRoleLabel(summary.ExpectedRole)}\n" +
                $"연봉  {FormatMoney(summary.AnnualSalary)}\n" +
                $"보유 자금  {FormatMoney(summary.AvailableMoney)}",
                21, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(700f, 220f), new Vector2(0f, -35f), PrimaryTextColor);
            _backButton.gameObject.SetActive(false);
            SetNext("Rookie League 시작", () => _manager.StartRookieSeason());
        }

        private void RenderSeasonStarted()
        {
            SetTitle("선수 커리어", "첫 시즌이 시작되었습니다.");
            CareerSummaryView summary = _manager.CareerSummary.Value;
            RectTransform card = CreateImage(
                "Dashboard", _body, CardColor, new Vector2(980f, 470f), new Vector2(0f, 10f));
            CreateText(
                "League", card, $"{summary.SeasonYear} ROOKIE LEAGUE", 18, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(700f, 40f), new Vector2(0f, 185f), AccentColor);
            CreateText(
                "Player", card, summary.PlayerName, 38, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(700f, 70f), new Vector2(0f, 110f), PrimaryTextColor);
            CreateText(
                "Summary", card,
                $"{summary.TeamName}  ·  {GetPositionLabel(summary.Position)}  ·  {GetRoleLabel(summary.ExpectedRole)}\n\n" +
                $"정규 시즌 준비 완료\n보유 자금 {FormatMoney(summary.AvailableMoney)}",
                22, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(760f, 220f), new Vector2(0f, -40f), PrimaryTextColor);
            _backButton.gameObject.SetActive(false);
            HideNext();
        }

        private void ConfigureBackButton()
        {
            _backButton.onClick.RemoveAllListeners();
            bool canGoBack = _manager.CurrentStep is not (
                NewGameStep.Identity or NewGameStep.ContractComplete or NewGameStep.Completed);
            _backButton.gameObject.SetActive(canGoBack);
            if (canGoBack)
                _backButton.onClick.AddListener(() => _manager.GoBack());
        }

        private void SetNext(string label, Action action, bool interactable = true)
        {
            _nextButton.gameObject.SetActive(true);
            _nextButton.interactable = interactable;
            _nextButton.onClick.RemoveAllListeners();
            _nextLabel.text = label;
            _nextButton.onClick.AddListener(() => action());
        }

        private void HideNext()
        {
            _nextButton.onClick.RemoveAllListeners();
            _nextButton.gameObject.SetActive(false);
        }

        private void SetTitle(string title, string subtitle)
        {
            _title.text = title;
            CreateText(
                "Subtitle", _body, subtitle, 18, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(1000f, 44f), new Vector2(0f, 315f), SecondaryTextColor);
        }

        private void CreateChoice(string name, string label, Vector2 position, Action action)
        {
            Button button = CreateButton(
                name, _body, label, new Vector2(460f, 250f), position, CardColor, out Text text);
            text.fontSize = 26;
            button.onClick.AddListener(() => action());
        }

        private void EnsureAttributeDraft()
        {
            if (_hasAttributeDraft)
                return;
            CareerAttributeAllocationRule rule = _manager.CurrentCreationAttributeRule;
            for (int index = 0; index < _attributeDraft.Length; index++)
                _attributeDraft[index] = rule.BaseValue;
            if (_manager.BatterAttributes.HasValue)
            {
                BatterAttributes value = _manager.BatterAttributes.Value;
                CopyValues(value.Contact, value.Power, value.Speed, value.Arm, value.Defense, value.Mental);
            }
            else if (_manager.PitcherAttributes.HasValue)
            {
                PitcherAttributes value = _manager.PitcherAttributes.Value;
                CopyValues(value.Stuff, value.Control, value.Breaking, value.Stamina, rule.BaseValue, rule.BaseValue);
            }
            _hasAttributeDraft = true;
        }

        private void CopyValues(int first, int second, int third, int fourth, int fifth, int sixth)
        {
            _attributeDraft[0] = first;
            _attributeDraft[1] = second;
            _attributeDraft[2] = third;
            _attributeDraft[3] = fourth;
            _attributeDraft[4] = fifth;
            _attributeDraft[5] = sixth;
        }

        private void ChangeAttribute(int index, int delta)
        {
            CareerAttributeAllocationRule rule = _manager.CurrentCreationAttributeRule;
            int adjustedDelta = delta;
            if (delta > 0)
            {
                int remaining = rule.BonusPoints - GetSpentPoints(rule);
                adjustedDelta = Math.Min(delta, Math.Min(rule.MaxValue - _attributeDraft[index], remaining));
            }
            else if (delta < 0)
            {
                adjustedDelta = Math.Max(delta, rule.BaseValue - _attributeDraft[index]);
            }

            if (adjustedDelta == 0)
                return;
            _attributeDraft[index] += adjustedDelta;
            Render();
        }

        private void ApplyPreset(AttributeAllocationPresetView preset)
        {
            for (int index = 0; index < _manager.CurrentCreationAttributeRule.AttributeCount; index++)
                _attributeDraft[index] = preset.GetValue(index);
            Render();
        }

        private void ResetAttributes()
        {
            int baseValue = _manager.CurrentCreationAttributeRule.BaseValue;
            for (int index = 0; index < _manager.CurrentCreationAttributeRule.AttributeCount; index++)
                _attributeDraft[index] = baseValue;
            Render();
        }

        private bool IsPresetSelected(AttributeAllocationPresetView preset)
        {
            for (int index = 0; index < _attributeDraft.Length; index++)
            {
                if (_attributeDraft[index] != preset.GetValue(index))
                    return false;
            }

            return true;
        }

        private int GetSpentPoints(CareerAttributeAllocationRule rule)
        {
            int spent = 0;
            for (int index = 0; index < rule.AttributeCount; index++)
                spent += _attributeDraft[index] - rule.BaseValue;
            return spent;
        }

        private string GetAttributeSummary()
        {
            if (_manager.BatterAttributes.HasValue)
            {
                BatterAttributes value = _manager.BatterAttributes.Value;
                return $"Contact {value.Contact}     Power {value.Power}     Speed {value.Speed}\n\n" +
                       $"Arm {value.Arm}     Fielding {value.Defense}     Mental {value.Mental}";
            }
            PitcherAttributes pitcher = _manager.PitcherAttributes.Value;
            return $"Stamina {pitcher.Stamina}     Velocity {pitcher.Velocity}     Stuff {pitcher.Stuff}\n\n" +
                   $"Breaking {pitcher.Breaking}     Control {pitcher.Control}     Mental {pitcher.Mental}";
        }

        private static string GetProgressText(NewGameStep step)
        {
            int current = step switch
            {
                NewGameStep.Identity => 1,
                NewGameStep.PlayerType => 2,
                NewGameStep.Position => 3,
                NewGameStep.Handedness => 4,
                NewGameStep.AttributeAllocation => 5,
                NewGameStep.PlayerCard => 6,
                NewGameStep.ContractOffers => 7,
                NewGameStep.ContractComplete => 8,
                _ => 9
            };
            return current < 9 ? $"NEW CAREER  {current} / 8" : "ROOKIE SEASON";
        }

        private static string GetPositionLabel(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "C  포수",
                PlayerPosition.FirstBase => "1B  1루수",
                PlayerPosition.SecondBase => "2B  2루수",
                PlayerPosition.ThirdBase => "3B  3루수",
                PlayerPosition.Shortstop => "SS  유격수",
                PlayerPosition.LeftField => "LF  좌익수",
                PlayerPosition.CenterField => "CF  중견수",
                PlayerPosition.RightField => "RF  우익수",
                PlayerPosition.DesignatedHitter => "DH  지명타자",
                PlayerPosition.StartingPitcher => "SP  선발투수",
                PlayerPosition.ReliefPitcher => "RP  구원투수",
                _ => "미정"
            };
        }

        private static string GetArchetypeLabel(TeamArchetype archetype)
        {
            return archetype switch
            {
                TeamArchetype.Development => "육성형",
                TeamArchetype.Contender => "강팀형",
                TeamArchetype.OffenseFocused => "타격 육성형",
                TeamArchetype.PitchingFocused => "투수 육성형",
                TeamArchetype.SmallMarket => "도전자형",
                _ => archetype.ToString()
            };
        }

        private static string GetRoleLabel(ExpectedRole role)
        {
            return role switch
            {
                ExpectedRole.StartingCompetition => "주전 경쟁",
                ExpectedRole.RosterCompetition => "로스터 경쟁",
                _ => "벤치 경쟁"
            };
        }

        private static string GetGrade(int rating)
        {
            if (rating >= 85) return "S";
            if (rating >= 70) return "A";
            if (rating >= 55) return "B";
            if (rating >= 40) return "C";
            return "D";
        }

        private static string GetThrowLabel(Handedness hand) => hand == Handedness.Left ? "좌투" : "우투";

        private static string GetBatLabel(Handedness hand)
        {
            return hand switch
            {
                Handedness.Left => "좌타",
                Handedness.Switch => "양타",
                _ => "우타"
            };
        }

        private static string FormatMoney(long amount)
        {
            return amount >= 100_000_000L
                ? $"{amount / 100_000_000d:0.##}억원"
                : $"{amount / 10_000d:N0}만원";
        }

        private static Color ToColor(TeamColor color) => new Color32(color.Red, color.Green, color.Blue, byte.MaxValue);

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static RectTransform CreateImage(
            string name, Transform parent, Color color, Vector2 size, Vector2 position, bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch) Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static Text CreateText(
            string name, Transform parent, string value, int fontSize, FontStyle style, TextAnchor alignment,
            Vector2 size, Vector2 position, Color color)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = GetRuntimeFont();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(
            string name, Transform parent, string label, Vector2 size, Vector2 position, Color color, out Text text)
        {
            RectTransform rect = CreateImage(name, parent, color, size, position);
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.35f);
            button.colors = colors;
            text = CreateText(
                "Label", rect, label, 18, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, PrimaryTextColor);
            Stretch(text.rectTransform);
            return button;
        }

        private static InputField CreateInputField(
            string name, Transform parent, string placeholder, string value, Vector2 size, Vector2 position)
        {
            RectTransform rect = CreateImage(name, parent, CardColor, size, position);
            InputField input = rect.gameObject.AddComponent<InputField>();
            Text text = CreateText(
                "Text", rect, value, 20, FontStyle.Normal, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.zero, PrimaryTextColor);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(18f, 4f);
            text.rectTransform.offsetMax = new Vector2(-18f, -4f);
            Text hint = CreateText(
                "Placeholder", rect, placeholder, 20, FontStyle.Italic, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.zero, SecondaryTextColor);
            Stretch(hint.rectTransform);
            hint.rectTransform.offsetMin = new Vector2(18f, 4f);
            hint.rectTransform.offsetMax = new Vector2(-18f, -4f);
            input.textComponent = text;
            input.placeholder = hint;
            input.text = value;
            input.characterLimit = 20;
            return input;
        }

        private static Font GetRuntimeFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child);
                else
#endif
                    Destroy(child);
            }
        }
    }
}
