using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_NewGame
    {
        private void RenderBasicInformation()
        {
            SetTitle("1단계 · 기본 정보", "선수 이름을 입력하고 플레이할 유형을 선택하세요.");
            if (string.IsNullOrEmpty(_nameDraft) && !string.IsNullOrEmpty(_manager.PlayerName))
                _nameDraft = _manager.PlayerName;
            _selectedPlayerType ??= _manager.PlayerType;

            RectTransform form = CreateImage(
                "BasicForm", _body, new Color(0.016f, 0.04f, 0.068f, 1f),
                new Vector2(570f, 610f), new Vector2(-490f, -5f));
            CreateText("NameLabel", form, "선수 이름", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(470f, 26f), new Vector2(0f, 245f), SecondaryTextColor);
            _nameInput = CreateInputField(
                "PlayerName", form, "2~12자 · 한글 / 영문 / 숫자", _nameDraft,
                new Vector2(470f, 58f), new Vector2(0f, 202f));
            _nameInput.characterLimit = 12;
            _nameInput.onValueChanged.AddListener(value =>
            {
                _nameDraft = value;
                UpdateBasicNextInteractable();
            });

            CreateText("ThrowLabel", form, "투구 손", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(470f, 26f), new Vector2(0f, 125f), SecondaryTextColor);
            CreateToggleChoice(form, "ThrowRight", "우투", _selectedThrowingHand == Handedness.Right,
                new Vector2(-120f, 80f), () => { _selectedThrowingHand = Handedness.Right; Render(); });
            CreateToggleChoice(form, "ThrowLeft", "좌투", _selectedThrowingHand == Handedness.Left,
                new Vector2(120f, 80f), () => { _selectedThrowingHand = Handedness.Left; Render(); });

            CreateText("BatLabel", form, "타격 손", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(470f, 26f), new Vector2(0f, 12f), SecondaryTextColor);
            Handedness[] batHands = { Handedness.Right, Handedness.Left, Handedness.Switch };
            string[] batLabels = { "우타", "좌타", "스위치" };
            for (int index = 0; index < batHands.Length; index++)
            {
                Handedness hand = batHands[index];
                CreateToggleChoice(form, "Bat_" + hand, batLabels[index], _selectedBattingHand == hand,
                    new Vector2(-155f + index * 155f, -34f),
                    () => { _selectedBattingHand = hand; Render(); }, new Vector2(140f, 50f));
            }
            CreateText("NameRule", form,
                "이름 중복은 허용됩니다. 앞뒤 공백은 자동으로 제거됩니다.\n투수 커리어도 타격 손을 기록합니다.",
                14, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(470f, 80f), new Vector2(0f, -145f), MutedTextColor);

            RectTransform preview = CreateImage(
                "PlayerPreview", _body, new Color(0.012f, 0.03f, 0.052f, 1f),
                new Vector2(930f, 610f), new Vector2(315f, -5f));
            _hitterPreview = CreatePortrait(
                "HitterPreview", preview, PlayerPosition.DesignatedHitter,
                new Vector2(330f, 410f), new Vector2(-220f, 65f));
            _pitcherPreview = CreatePortrait(
                "PitcherPreview", preview, PlayerPosition.StartingPitcher,
                new Vector2(330f, 410f), new Vector2(220f, 65f));
            ApplyPreviewState(_hitterPreview, PlayerType.Batter);
            ApplyPreviewState(_pitcherPreview, PlayerType.Pitcher);

            CreateTypeCard(preview, PlayerType.Batter, new Vector2(-220f, -220f));
            CreateTypeCard(preview, PlayerType.Pitcher, new Vector2(220f, -220f));
            CreateText("TypeGuide", preview, GetPlayerTypeGuide(), 14, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(790f, 48f), new Vector2(0f, -285f), SecondaryTextColor);

            SetNext("다음 · 포지션", () => _manager.SubmitBasicInformation(
                _nameDraft,
                _selectedPlayerType ?? PlayerType.Batter,
                _selectedBattingHand,
                _selectedThrowingHand),
                CanSubmitBasicInformation());
            PlayPlayerTypeTransitionIfNeeded();
        }

        private void CreateTypeCard(Transform parent, PlayerType playerType, Vector2 position)
        {
            bool selected = _selectedPlayerType == playerType;
            string label = playerType == PlayerType.Batter ? "타자" : "투수";
            Button button = CreateButton(
                "Type_" + playerType, parent, selected ? "✓  " + label : label,
                new Vector2(330f, 64f), position, selected ? SelectedColor : CardColor, out _);
            button.onClick.AddListener(() =>
            {
                if (_selectedPlayerType == playerType)
                    return;
                _selectedPlayerType = playerType;
                Render();
            });
        }

        private void ApplyPreviewState(RectTransform preview, PlayerType playerType)
        {
            bool hasSelection = _selectedPlayerType.HasValue;
            bool selected = _selectedPlayerType == playerType;
            preview.localScale = Vector3.one * (!hasSelection ? 0.92f : selected ? 1.02f : 0.88f);
            preview.GetComponent<CanvasGroup>().alpha = !hasSelection ? 0.65f : selected ? 1f : 0.35f;
        }

        private void PlayPlayerTypeTransitionIfNeeded()
        {
            if (!_selectedPlayerType.HasValue || _lastAnimatedPlayerType == _selectedPlayerType)
                return;
            _lastAnimatedPlayerType = _selectedPlayerType;
            RectTransform selected = _selectedPlayerType == PlayerType.Batter ? _hitterPreview : _pitcherPreview;
            RectTransform unselected = _selectedPlayerType == PlayerType.Batter ? _pitcherPreview : _hitterPreview;
            CanvasGroup selectedGroup = selected.GetComponent<CanvasGroup>();
            CanvasGroup unselectedGroup = unselected.GetComponent<CanvasGroup>();
            float targetY = selected.anchoredPosition.y;
            selected.anchoredPosition = new Vector2(selected.anchoredPosition.x, targetY + 16f);
            selected.localScale = Vector3.one * 0.92f;
            selectedGroup.alpha = 0.65f;
            unselected.localScale = Vector3.one;
            unselectedGroup.alpha = 1f;

            DOTween.Sequence().SetUpdate(true).SetTarget(this)
                .Join(selected.DOScale(1.08f, 0.22f).SetEase(Ease.OutBack))
                .Join(DOTween.To(
                    () => selected.anchoredPosition.y,
                    value => selected.anchoredPosition = new Vector2(selected.anchoredPosition.x, value),
                    targetY,
                    0.22f).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => selectedGroup.alpha, value => selectedGroup.alpha = value, 1f, 0.22f))
                .Append(selected.DOScale(1.02f, 0.10f).SetEase(Ease.OutQuad));
            unselected.DOScale(0.88f, 0.18f).SetUpdate(true).SetTarget(this);
            DOTween.To(() => unselectedGroup.alpha, value => unselectedGroup.alpha = value, 0.35f, 0.18f)
                .SetUpdate(true).SetTarget(this);
        }

        private void RenderPositionAndRole()
        {
            bool isPitcher = _manager.PlayerType == PlayerType.Pitcher;
            SetTitle("2단계 · 포지션 및 희망 보직",
                isPitcher
                    ? "희망 보직은 기용 요청이며, 실제 보직은 능력치와 구단 경쟁에 따라 감독 AI가 결정합니다."
                    : "주 포지션은 팀 내 경쟁·수상·감독 AI의 기용 판단에 사용됩니다.");
            if (isPitcher)
                RenderPitcherRoles();
            else
                RenderBatterPositions();

            bool canAdvance = isPitcher || _selectedPosition != PlayerPosition.Unknown;
            SetNext("다음 · 능력치", () => _manager.SubmitCreationPosition(
                _selectedPosition,
                _selectedPitcherRole), canAdvance);
        }

        private void RenderBatterPositions()
        {
            if (_selectedPosition == PlayerPosition.Unknown && IsBatterPosition(_manager.PrimaryPosition))
                _selectedPosition = _manager.PrimaryPosition;
            PlayerPosition[] positions =
            {
                PlayerPosition.Catcher, PlayerPosition.FirstBase, PlayerPosition.SecondBase,
                PlayerPosition.ThirdBase, PlayerPosition.Shortstop, PlayerPosition.LeftField,
                PlayerPosition.CenterField, PlayerPosition.RightField, PlayerPosition.DesignatedHitter
            };
            for (int index = 0; index < positions.Length; index++)
            {
                PlayerPosition position = positions[index];
                int row = index / 3;
                int column = index % 3;
                bool selected = _selectedPosition == position;
                Button button = CreateButton(
                    "Position_" + position, _body,
                    GetPositionLabel(position) + "\n" + GetPositionKeyAttributes(position),
                    new Vector2(430f, 125f), new Vector2(-460f + column * 460f, 185f - row * 150f),
                    selected ? SelectedColor : CardColor, out Text text);
                text.fontSize = 18;
                button.onClick.AddListener(() => { _selectedPosition = position; Render(); });
            }
            CreateText("PositionNotice", _body,
                "포지션 선택은 능력치 보너스를 주지 않습니다. 잘 키운 방식과 팀 내 경쟁이 실제 기회를 만듭니다.",
                14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(1280f, 38f), new Vector2(0f, -290f), SecondaryTextColor);
        }

        private void RenderPitcherRoles()
        {
            PitcherRole[] roles =
            {
                PitcherRole.Starter, PitcherRole.LongRelief, PitcherRole.MiddleRelief,
                PitcherRole.Setup, PitcherRole.Closer
            };
            for (int index = 0; index < roles.Length; index++)
            {
                PitcherRole role = roles[index];
                bool selected = _selectedPitcherRole == role;
                float x = index < 3 ? -470f + index * 470f : -235f + (index - 3) * 470f;
                float y = index < 3 ? 145f : -55f;
                Button button = CreateButton(
                    "Role_" + role, _body,
                    GetPitcherRoleLabel(role) + "\n" + GetPitcherRoleGuide(role),
                    new Vector2(430f, 160f), new Vector2(x, y),
                    selected ? SelectedColor : CardColor, out Text text);
                text.fontSize = 18;
                button.onClick.AddListener(() => { _selectedPitcherRole = role; Render(); });
            }
            RectTransform notice = CreateImage("RoleNotice", _body,
                new Color(0.02f, 0.075f, 0.10f, 1f), new Vector2(1200f, 86f), new Vector2(0f, -245f));
            CreateText("Text", notice,
                $"희망 보직  {GetPitcherRoleLabel(_selectedPitcherRole)}     ·     현재 예상 역할은 입단 구단의 뎁스 차트에서 결정됩니다.",
                16, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(1120f, 50f), Vector2.zero, PrimaryTextColor);
        }

        private void RenderAttributes()
        {
            SetTitle("3단계 · 초기 능력치 배분", "모든 포인트를 사용해야 다음 단계로 이동할 수 있습니다.");
            EnsureCreationAttributeDraft();
            CareerAttributeAllocationRule rule = _manager.CurrentCreationAttributeRule;
            string[] names = _manager.PlayerType == PlayerType.Pitcher
                ? new[] { "구위", "제구", "변화", "체력" }
                : new[] { "컨택", "파워", "선구안", "주루", "수비", "송구" };
            int remaining = GetRemainingCreationPoints(rule);

            RectTransform player = CreateImage("Player", _body, CardColor,
                new Vector2(340f, 610f), new Vector2(-610f, -5f));
            CreatePortrait("Portrait", player,
                _manager.PlayerType == PlayerType.Pitcher ? PlayerPosition.StartingPitcher : PlayerPosition.DesignatedHitter,
                new Vector2(290f, 385f), new Vector2(0f, 72f));
            CreateText("Name", player, _manager.PlayerName, 26, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(300f, 42f), new Vector2(0f, -145f), PrimaryTextColor);
            CreateText("Meta", player,
                _manager.PlayerType == PlayerType.Pitcher
                    ? GetPitcherRoleLabel(_selectedPitcherRole)
                    : GetPositionLabel(_manager.PrimaryPosition),
                15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(300f, 34f), new Vector2(0f, -185f), AccentColor);

            RectTransform allocation = CreateImage("Allocation", _body,
                new Color(0.014f, 0.036f, 0.06f, 1f), new Vector2(750f, 610f), new Vector2(-40f, -5f));
            RectTransform pointSummary = CreateImage("PointSummary", allocation,
                new Color(0.025f, 0.065f, 0.095f, 1f), new Vector2(690f, 58f), new Vector2(0f, 258f));
            CreateText("PointLabel", pointSummary, remaining == 0 ? "배분 완료" : "남은 포인트",
                15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(120f, 32f), new Vector2(-278f, 0f),
                remaining == 0 ? AccentColor : SecondaryTextColor);
            CreateText("Remaining", pointSummary, $"{remaining} P", 23, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(88f, 36f), new Vector2(-170f, 0f),
                remaining == 0 ? AccentColor : GoldColor);
            CreateText("Rule", pointSummary,
                $"총 {rule.BonusPoints} P  ·  기본 {rule.BaseValue}  ·  상한 {rule.MaxValue}",
                13, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(255f, 32f), new Vector2(10f, 0f), SecondaryTextColor);
            Button reset = CreateButton("Reset", pointSummary, "초기화", new Vector2(92f, 36f),
                new Vector2(288f, 0f), CardColor, out Text resetLabel);
            resetLabel.fontSize = 14;
            reset.interactable = remaining < rule.BonusPoints;
            reset.onClick.AddListener(ResetCreationAttributes);

            for (int index = 0; index < names.Length; index++)
            {
                int captured = index;
                float y = names.Length == 4 ? 158f - index * 96f : 178f - index * 70f;
                CreateImage("Row_" + index, allocation,
                    index % 2 == 0
                        ? new Color(0.018f, 0.048f, 0.073f, 0.95f)
                        : new Color(0.012f, 0.036f, 0.058f, 0.95f),
                    new Vector2(690f, names.Length == 4 ? 66f : 58f), new Vector2(0f, y));
                CreateAttributeBar(allocation, "Bar_" + index, _attributeDraft[index], rule.MaxValue,
                    new Vector2(-80f, y));
                CreateText("Name_" + index, allocation, names[index], 18, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(90f, 40f), new Vector2(-292f, y), PrimaryTextColor);
                CreateText("Value_" + index, allocation, _attributeDraft[index].ToString(), 20, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(60f, 42f), new Vector2(112f, y), AccentColor);
                Button minus = CreateButton("Minus_" + index, allocation, "−", new Vector2(46f, 42f),
                    new Vector2(196f, y), CardColor, out _);
                minus.interactable = _attributeDraft[index] > rule.BaseValue;
                minus.onClick.AddListener(() => ChangeCreationAttribute(captured, -1));
                Button plus = CreateButton("Plus_" + index, allocation, "+", new Vector2(46f, 42f),
                    new Vector2(258f, y), CardColor, out _);
                plus.interactable = remaining > 0 && _attributeDraft[index] < rule.MaxValue;
                plus.onClick.AddListener(() => ChangeCreationAttribute(captured, 1));
            }

            RectTransform analysis = CreateImage("Analysis", _body, CardColor,
                new Vector2(400f, 610f), new Vector2(555f, -5f));
            CreateText("Heading", analysis, "추천 배분", 17, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(330f, 34f), new Vector2(0f, 260f), SecondaryTextColor);
            IReadOnlyList<AttributeAllocationPresetView> presets = _manager.CreationAttributeAllocationPresets;
            for (int index = 0; index < presets.Count; index++)
            {
                AttributeAllocationPresetView preset = presets[index];
                Button presetButton = CreateButton("Preset_" + index, analysis,
                    preset.IsRecommended ? "추천 · " + preset.Label : preset.Label,
                    new Vector2(330f, 48f), new Vector2(0f, 210f - index * 56f),
                    IsPresetSelected(preset) ? SelectedColor : new Color(0.025f, 0.06f, 0.09f, 1f), out Text label);
                label.fontSize = 15;
                presetButton.onClick.AddListener(() => ApplyCreationPreset(preset));
            }
            CreateText("Type", analysis,
                "예상 유형\n" + GetExpectedBuildLabel() + "\n\n희망 보직 적합도\n" + GetRoleSuitabilityLabel(),
                16, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(330f, 150f), new Vector2(0f, -210f), PrimaryTextColor);
            SetNext("다음 · 세부 설정", () => _manager.SubmitCreationAttributes((int[])_attributeDraft.Clone()),
                remaining == 0);
        }

        private void RenderPlayerDetails()
        {
            bool isPitcher = _manager.PlayerType == PlayerType.Pitcher;
            SetTitle("4단계 · " + (isPitcher ? "구종 구성" : "타격 스타일"),
                isPitcher
                    ? "포심을 포함한 3개 구종과 주무기 1개를 선택하세요."
                    : "스타일은 보너스가 아니라 선수 설명과 추천 경기 방침을 결정합니다.");
            if (isPitcher)
                RenderPitchSelection();
            else
                RenderBatterStyleSelection();
        }

        private void RenderBatterStyleSelection()
        {
            BatterStyle[] styles =
            {
                BatterStyle.Balanced, BatterStyle.Contact, BatterStyle.Power,
                BatterStyle.Patient, BatterStyle.Aggressive
            };
            for (int index = 0; index < styles.Length; index++)
            {
                BatterStyle style = styles[index];
                bool selected = _selectedBatterStyle == style;
                float x = index < 3 ? -470f + index * 470f : -235f + (index - 3) * 470f;
                float y = index < 3 ? 140f : -70f;
                Button button = CreateButton("Style_" + style, _body,
                    GetBatterStyleLabel(style) + "\n" + GetBatterStyleGuide(style),
                    new Vector2(430f, 170f), new Vector2(x, y),
                    selected ? SelectedColor : CardColor, out Text text);
                text.fontSize = 18;
                button.onClick.AddListener(() => { _selectedBatterStyle = style; Render(); });
            }
            CreateText("Recommend", _body,
                "추천 경기 방침  ·  " + GetBattingApproachLabel(GetRecommendedBattingApproach(_selectedBatterStyle)),
                16, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(760f, 42f), new Vector2(0f, -280f), AccentColor);
            SetNext("다음 · 경기 설정", () => _manager.SubmitBatterDetails(_selectedBatterStyle));
        }

        private void RenderPitchSelection()
        {
            EnsureDefaultPitchSelection();
            PitchType[] pitches = (PitchType[])Enum.GetValues(typeof(PitchType));
            for (int index = 0; index < pitches.Length; index++)
            {
                PitchType pitch = pitches[index];
                bool selected = _selectedPitches.Contains(pitch);
                int row = index / 4;
                int column = index % 4;
                Button button = CreateButton("Pitch_" + pitch, _body,
                    (selected ? "✓  " : string.Empty) + GetPitchLabel(pitch) +
                    "\n" + GetPitchTrait(pitch),
                    new Vector2(350f, 150f), new Vector2(-555f + column * 370f, 155f - row * 175f),
                    selected ? SelectedColor : CardColor, out Text text);
                text.fontSize = 16;
                button.interactable = pitch != PitchType.FourSeamFastball || selected;
                button.onClick.AddListener(() => TogglePitch(pitch));
            }

            CreateText("SelectedLabel", _body, $"선택 구종  {_selectedPitches.Count} / 3     ·     주무기 지정",
                16, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(700f, 34f), new Vector2(0f, -225f), SecondaryTextColor);
            PitchType[] selectedPitches = CopySelectedPitches();
            for (int selectedIndex = 0; selectedIndex < selectedPitches.Length; selectedIndex++)
            {
                PitchType pitch = selectedPitches[selectedIndex];
                bool primary = _primaryPitch == pitch;
                Button primaryButton = CreateButton("Primary_" + pitch, _body,
                    (primary ? "★ " : string.Empty) + GetPitchLabel(pitch) + (primary ? "  55" : "  45"),
                    new Vector2(250f, 50f), new Vector2(-270f + selectedIndex * 270f, -275f),
                    primary ? new Color(0.42f, 0.30f, 0.08f, 1f) : CardColor, out Text label);
                label.color = primary ? GoldColor : PrimaryTextColor;
                primaryButton.onClick.AddListener(() => { _primaryPitch = pitch; Render(); });
            }
            SetNext("다음 · 경기 설정", () => _manager.SubmitPitcherDetails(
                CopySelectedPitches(), _primaryPitch), _selectedPitches.Count == 3);
        }

        private void RenderMatchSettings()
        {
            bool isPitcher = _manager.PlayerType == PlayerType.Pitcher;
            SetTitle("5단계 · 경기 운영 설정", "관전·자동 진행·내 선수 개입 방식을 선택합니다. 실제 훈련은 커리어 성장 메뉴에서 진행합니다.");
            RectTransform approachPanel = CreateImage("Approach", _body, CardColor,
                new Vector2(510f, 610f), new Vector2(-530f, -5f));
            CreateText("Heading", approachPanel, isPitcher ? "투구 방침" : "타격 방침", 18,
                FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(430f, 34f), new Vector2(0f, 260f), AccentColor);
            if (isPitcher)
                RenderPitchingApproaches(approachPanel);
            else
                RenderBattingApproaches(approachPanel);

            RectTransform modePanel = CreateImage("ProgressMode", _body,
                new Color(0.014f, 0.038f, 0.064f, 1f), new Vector2(610f, 610f), new Vector2(50f, -5f));
            CreateText("Heading", modePanel, "경기 진행 방식", 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(530f, 34f), new Vector2(0f, 260f), AccentColor);
            MatchProgressMode[] modes =
            {
                MatchProgressMode.FullGameWatch, MatchProgressMode.InterveneOnPlayer,
                MatchProgressMode.PlayerFocusAutomatic, MatchProgressMode.InstantResult
            };
            for (int index = 0; index < modes.Length; index++)
            {
                MatchProgressMode mode = modes[index];
                bool selected = _selectedProgressMode == mode;
                Button button = CreateButton("Mode_" + mode, modePanel,
                    GetProgressModeLabel(mode) + "\n" + GetProgressModeGuide(mode),
                    new Vector2(520f, 105f), new Vector2(0f, 185f - index * 120f),
                    selected ? SelectedColor : CardColor, out Text text);
                text.fontSize = 15;
                button.onClick.AddListener(() => { _selectedProgressMode = mode; Render(); });
            }

            RectTransform speedPanel = CreateImage("Speed", _body, CardColor,
                new Vector2(400f, 610f), new Vector2(570f, -5f));
            CreateText("Heading", speedPanel, "게임 속도", 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(330f, 34f), new Vector2(0f, 260f), AccentColor);
            int[] speeds = { 1, 2, 3, 5 };
            bool speedEnabled = _selectedProgressMode != MatchProgressMode.InstantResult;
            for (int index = 0; index < speeds.Length; index++)
            {
                int speed = speeds[index];
                Button button = CreateButton("Speed_" + speed, speedPanel, speed + "×",
                    new Vector2(145f, 58f), new Vector2(-82f + index % 2 * 164f, 170f - index / 2 * 72f),
                    _selectedGameSpeed == speed ? SelectedColor : CardColor, out _);
                button.interactable = speedEnabled;
                button.onClick.AddListener(() => { _selectedGameSpeed = speed; Render(); });
            }
            Button autoSlow = CreateButton("AutoSlow", speedPanel,
                (_autoSlowOnPlayerEvent ? "ON" : "OFF") + "  내 선수 장면 1× 자동 전환",
                new Vector2(330f, 70f), new Vector2(0f, -5f),
                _autoSlowOnPlayerEvent ? SelectedColor : CardColor, out Text slowLabel);
            slowLabel.fontSize = 15;
            autoSlow.interactable = speedEnabled;
            autoSlow.onClick.AddListener(() => { _autoSlowOnPlayerEvent = !_autoSlowOnPlayerEvent; Render(); });
            CreateText("Notice", speedPanel,
                speedEnabled
                    ? "기본 권장 속도는 2×입니다.\n경기 중 설정에서 다시 바꿀 수 있습니다."
                    : "즉시 결과에서는 경기 장면이 없어\n속도 설정을 사용하지 않습니다.",
                14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(330f, 80f), new Vector2(0f, -155f), SecondaryTextColor);

            SetNext("최종 확인", () => _manager.SubmitMatchSettings(
                _selectedBattingApproach,
                _selectedPitchingApproach,
                _selectedProgressMode,
                _selectedGameSpeed,
                _autoSlowOnPlayerEvent));
        }

        private void RenderBattingApproaches(Transform parent)
        {
            BattingApproach[] approaches =
            {
                BattingApproach.Balanced, BattingApproach.Power, BattingApproach.Contact,
                BattingApproach.Patient, BattingApproach.Aggressive
            };
            for (int index = 0; index < approaches.Length; index++)
            {
                BattingApproach approach = approaches[index];
                CreateApproachButton(parent, "Batting_" + approach,
                    GetBattingApproachLabel(approach), GetBattingApproachGuide(approach),
                    _selectedBattingApproach == approach, index,
                    () => { _selectedBattingApproach = approach; Render(); });
            }
        }

        private void RenderPitchingApproaches(Transform parent)
        {
            PitchingApproach[] approaches =
            {
                PitchingApproach.Balanced, PitchingApproach.FullPower, PitchingApproach.ControlFirst,
                PitchingApproach.InduceChase, PitchingApproach.QuickAttack
            };
            for (int index = 0; index < approaches.Length; index++)
            {
                PitchingApproach approach = approaches[index];
                CreateApproachButton(parent, "Pitching_" + index,
                    GetPitchingApproachLabel(approach), GetPitchingApproachGuide(approach),
                    _selectedPitchingApproach == approach, index,
                    () => { _selectedPitchingApproach = approach; Render(); });
            }
        }

        private void CreateApproachButton(
            Transform parent, string name, string label, string guide, bool selected, int index, Action action)
        {
            Button button = CreateButton(name, parent, label + "\n" + guide,
                new Vector2(430f, 88f), new Vector2(0f, 190f - index * 100f),
                selected ? SelectedColor : new Color(0.02f, 0.055f, 0.085f, 1f), out Text text);
            text.fontSize = 15;
            button.onClick.AddListener(() => action());
        }

        private void RenderFinalConfirmation()
        {
            SetTitle("최종 확인", "커리어 시작 전 모든 선택을 확인하세요.");
            CareerCreationDraft draft = _manager.Draft;
            RectTransform card = CreateImage("SummaryCard", _body, CardColor,
                new Vector2(1250f, 610f), new Vector2(0f, -5f));
            CreatePortrait("Portrait", card, _manager.PrimaryPosition,
                new Vector2(340f, 500f), new Vector2(-410f, 0f));
            CreateText("Identity", card,
                $"{_manager.PlayerName}\n{GetPlayerTypeLabel(_manager.PlayerType)}  ·  " +
                $"{GetThrowLabel(_manager.ThrowingHand)}{GetBatLabel(_manager.BattingHand)}",
                25, FontStyle.Bold, TextAnchor.UpperLeft,
                new Vector2(700f, 90f), new Vector2(185f, 220f), PrimaryTextColor);
            CreateText("Role", card,
                _manager.PlayerType == PlayerType.Pitcher
                    ? "희망 보직  " + GetPitcherRoleLabel(draft.PreferredPitcherRole)
                    : "수비 포지션  " + GetPositionLabel(_manager.PrimaryPosition),
                17, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(700f, 38f), new Vector2(185f, 132f), AccentColor);
            CreateText("Attributes", card, GetCreationAttributeSummary(draft.InitialAttributes),
                17, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(700f, 110f), new Vector2(185f, 58f), PrimaryTextColor);
            CreateText("Details", card, GetCreationDetailSummary(draft),
                16, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(700f, 105f), new Vector2(185f, -60f), PrimaryTextColor);
            CreateText("Settings", card,
                $"경기 방침  {GetSelectedApproachLabel()}\n" +
                $"경기 진행  {GetProgressModeLabel(draft.GameSettings.MatchProgressMode)}\n" +
                $"경기 속도  {(draft.GameSettings.MatchProgressMode == MatchProgressMode.InstantResult ? "사용 안 함" : draft.GameSettings.GameSpeed + "×")}\n" +
                $"내 선수 장면 1×  {(draft.GameSettings.AutoSlowOnPlayerEvent ? "ON" : "OFF")}",
                16, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(700f, 120f), new Vector2(185f, -175f), SecondaryTextColor);
            CreateText("SaveWarning", card,
                "현재 버전은 저장을 지원하지 않습니다. 게임을 종료하거나 타이틀로 돌아가면 진행 내용이 유지되지 않습니다.",
                14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(1080f, 42f), new Vector2(0f, -265f), GoldColor);
            SetNext("커리어 시작", () => { _showStartConfirmation = true; Render(); });
            if (_showStartConfirmation)
                RenderStartConfirmation();
        }

        private void RenderStartConfirmation()
        {
            RectTransform shade = CreateImage("ConfirmationShade", _panel,
                new Color(0f, 0f, 0f, 0.72f), new Vector2(1740f, 990f), Vector2.zero);
            RectTransform popup = CreateImage("StartConfirmation", shade, PanelColor,
                new Vector2(720f, 330f), Vector2.zero);
            CreateText("Title", popup, "이 설정으로 선수 커리어를 시작하시겠습니까?", 24,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(620f, 70f), new Vector2(0f, 78f), PrimaryTextColor);
            CreateText("Warning", popup,
                "저장 기능은 아직 지원하지 않습니다.", 15, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(560f, 36f), new Vector2(0f, 24f), GoldColor);
            Button cancel = CreateButton("Cancel", popup, "선택 수정", new Vector2(220f, 56f),
                new Vector2(-130f, -82f), CardColor, out _);
            cancel.onClick.AddListener(() => { _showStartConfirmation = false; Render(); });
            Button confirm = CreateButton("Confirm", popup, "구단 오퍼 확인", new Vector2(250f, 56f),
                new Vector2(145f, -82f), AccentColor, out _);
            confirm.onClick.AddListener(() =>
            {
                _showStartConfirmation = false;
                _manager.ConfirmCreationAndGenerateOffers();
            });
        }

        private void RenderOffers()
        {
            SetTitle("신인 계약 · 구단 오퍼", "금액뿐 아니라 육성 환경과 실제 출장 기회를 함께 비교하세요.");
            IReadOnlyList<ContractOfferView> offers = _manager.Offers;
            bool hasSelection = false;
            for (int index = 0; index < offers.Count; index++)
            {
                ContractOfferView offer = offers[index];
                hasSelection |= offer.IsSelected;
                float y = 235f - index * 122f;
                Button button = CreateButton("Offer_" + offer.TeamId, _body, string.Empty,
                    new Vector2(1420f, 105f), new Vector2(0f, y),
                    offer.IsSelected ? SelectedColor : CardColor, out Text text);
                text.alignment = TextAnchor.MiddleLeft;
                text.rectTransform.offsetMin = new Vector2(28f, 8f);
                text.rectTransform.offsetMax = new Vector2(-24f, -8f);
                text.text =
                    $"{(offer.IsSelected ? "✓  " : string.Empty)}{offer.TeamName}  ·  {GetArchetypeLabel(offer.Archetype)}\n" +
                    $"계약금 {FormatMoney(offer.SigningBonus)}  |  연봉 {FormatMoney(offer.AnnualSalary)}  |  " +
                    $"{offer.ContractYears}년  |  육성 {GetGrade(offer.DevelopmentRating)}  |  {GetRoleLabel(offer.ExpectedRole)}\n" +
                    $"{offer.EvaluationOpportunitySummary}  |  포지션 필요도 {offer.PositionNeed}  ·  경쟁자 {offer.CompetitorSummary}";
                int teamId = offer.TeamId;
                button.onClick.AddListener(() => _manager.SelectOffer(teamId));
            }
            SetNext("이 구단과 계약", () => _manager.SignSelectedOffer(), hasSelection);
        }

        private void RenderContractComplete()
        {
            SetTitle("계약 완료", "당신을 필요로 한 구단에서 첫 시즌을 시작합니다.");
            CareerSummaryView summary = _manager.CareerSummary.Value;
            RectTransform card = CreateImage("ContractCard", _body, CardColor,
                new Vector2(900f, 470f), new Vector2(0f, 5f));
            CreateText("Signed", card, "SIGNED", 16, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(260f, 30f), new Vector2(0f, 190f), AccentColor);
            CreateText("Team", card, summary.TeamName, 34, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(760f, 62f), new Vector2(0f, 130f), PrimaryTextColor);
            CreateText("Contract", card,
                $"{summary.PlayerName}  ·  {GetPositionLabel(summary.Position)}\n\n" +
                $"예상 역할  {GetRoleLabel(summary.ExpectedRole)}\n" +
                $"연봉  {FormatMoney(summary.AnnualSalary)}\n보유 자금  {FormatMoney(summary.AvailableMoney)}",
                20, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(720f, 240f), new Vector2(0f, -45f), PrimaryTextColor);
            _backButton.gameObject.SetActive(false);
            SetNext("Rookie League 시작", () =>
            {
                if (_manager.StartRookieSeason())
                    OpenCareerHome();
            });
        }

        private void RenderLegacyPlayerType()
        {
            SetTitle("투수 / 타자 선택", "기존 테스트 호환 흐름입니다.");
            CreateToggleChoice(_body, "Batter", "타자", false, new Vector2(-250f, 0f),
                () => _manager.SelectPlayerType(PlayerType.Batter), new Vector2(400f, 180f));
            CreateToggleChoice(_body, "Pitcher", "투수", false, new Vector2(250f, 0f),
                () => _manager.SelectPlayerType(PlayerType.Pitcher), new Vector2(400f, 180f));
            _nextButton.gameObject.SetActive(false);
        }

        private void RenderLegacyHandedness()
        {
            SetTitle("투타 선택", "투구 손과 타격 손을 선택합니다.");
            CreateToggleChoice(_body, "Right", "우투우타", false, new Vector2(-220f, 0f),
                () => _manager.SelectHandedness(Handedness.Right, Handedness.Right), new Vector2(380f, 160f));
            CreateToggleChoice(_body, "Left", "좌투좌타", false, new Vector2(220f, 0f),
                () => _manager.SelectHandedness(Handedness.Left, Handedness.Left), new Vector2(380f, 160f));
            _nextButton.gameObject.SetActive(false);
        }

        private void RenderLegacyPlayerCard()
        {
            SetTitle("무소속 선수 카드", "구단 평가를 받아 계약 오퍼를 확인합니다.");
            CreateText("Summary", _body, _manager.PlayerName + "\n" + GetPositionLabel(_manager.PrimaryPosition),
                30, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(800f, 180f), Vector2.zero, PrimaryTextColor);
            SetNext("구단 오퍼 확인", () => _manager.GenerateOffers());
        }
    }
}
