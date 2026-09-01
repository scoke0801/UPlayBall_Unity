using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_NewGame
    {
        private void ResetLocalDraft()
        {
            _nameDraft = string.Empty;
            _selectedPlayerType = null;
            _lastAnimatedPlayerType = null;
            _selectedBattingHand = Handedness.Right;
            _selectedThrowingHand = Handedness.Right;
            _selectedPosition = PlayerPosition.Unknown;
            _selectedPitcherRole = PitcherRole.Starter;
            _attributeDraft = Array.Empty<int>();
            _attributeDraftType = null;
            _selectedBatterStyle = BatterStyle.Balanced;
            _selectedPitches.Clear();
            _primaryPitch = PitchType.Slider;
            _selectedBattingApproach = BattingApproach.Balanced;
            _selectedPitchingApproach = PitchingApproach.Balanced;
            _selectedProgressMode = MatchProgressMode.InterveneOnPlayer;
            _selectedAutomaticProgressMode = MatchProgressMode.InterveneOnPlayer;
            _selectedGameSpeed = 2;
            _autoSlowOnPlayerEvent = true;
            _showStartConfirmation = false;
        }

        private bool CanSubmitBasicInformation()
        {
            string value = _nameDraft?.Trim() ?? string.Empty;
            return value.Length is >= 2 and <= 12 && _selectedPlayerType.HasValue;
        }

        private void UpdateBasicNextInteractable()
        {
            if (_nextButton != null)
                _nextButton.interactable = CanSubmitBasicInformation();
        }

        private string GetPlayerTypeGuide()
        {
            return _selectedPlayerType switch
            {
                PlayerType.Batter => "정교한 타격과 장타, 수비 능력을 성장시켜 리그를 대표하는 타자로 커리어를 이어갑니다.",
                PlayerType.Pitcher => "구위와 제구, 다양한 구종을 성장시켜 선발 또는 불펜 투수로 커리어를 이어갑니다.",
                _ => "두 유형 중 하나를 선택하면 선수 프리뷰와 이후 생성 항목이 바뀝니다."
            };
        }

        private void CreateToggleChoice(
            Transform parent,
            string name,
            string label,
            bool selected,
            Vector2 position,
            Action action,
            Vector2? size = null)
        {
            Button button = CreateButton(name, parent, label, size ?? new Vector2(220f, 52f), position,
                selected ? SelectedColor : CardColor, out _);
            button.onClick.AddListener(() => action());
        }

        private void EnsureCreationAttributeDraft()
        {
            PlayerType playerType = _manager.PlayerType ?? PlayerType.Batter;
            CareerAttributeAllocationRule rule = _manager.CurrentCreationAttributeRule;
            if (_attributeDraftType == playerType && _attributeDraft.Length == rule.AttributeCount)
                return;

            int[] saved = _manager.Draft?.InitialAttributes;
            if (saved != null && saved.Length == rule.AttributeCount)
            {
                _attributeDraft = saved;
            }
            else
            {
                _attributeDraft = new int[rule.AttributeCount];
                for (int index = 0; index < _attributeDraft.Length; index++)
                    _attributeDraft[index] = rule.BaseValue;
            }
            _attributeDraftType = playerType;
        }

        private int GetRemainingCreationPoints(CareerAttributeAllocationRule rule)
        {
            int spent = 0;
            for (int index = 0; index < _attributeDraft.Length; index++)
                spent += _attributeDraft[index] - rule.BaseValue;
            return rule.BonusPoints - spent;
        }

        private void ChangeCreationAttribute(int index, int delta)
        {
            CareerAttributeAllocationRule rule = _manager.CurrentCreationAttributeRule;
            if (delta > 0 && GetRemainingCreationPoints(rule) <= 0)
                return;
            int value = Mathf.Clamp(_attributeDraft[index] + delta, rule.BaseValue, rule.MaxValue);
            if (value == _attributeDraft[index])
                return;
            _attributeDraft[index] = value;
            Render();
        }

        private void ResetCreationAttributes()
        {
            CareerAttributeAllocationRule rule = _manager.CurrentCreationAttributeRule;
            for (int index = 0; index < _attributeDraft.Length; index++)
                _attributeDraft[index] = rule.BaseValue;
            Render();
        }

        private void CreateAttributeBar(Transform parent, string name, int value, int maximum, Vector2 position)
        {
            RectTransform track = CreateImage(name, parent, new Color(0.07f, 0.12f, 0.16f, 1f),
                new Vector2(300f, 14f), position);
            float ratio = maximum <= 0 ? 0f : Mathf.Clamp01(value / (float)maximum);
            RectTransform fill = CreateImage("Fill", track, AccentColor,
                new Vector2(300f * ratio, 10f), Vector2.zero);
            fill.anchorMin = new Vector2(0f, 0.5f);
            fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            CareerUiSkin.ApplyProgressBar(track.GetComponent<Image>(), fill.GetComponent<Image>(), ratio);
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

        private void ApplyCreationPreset(AttributeAllocationPresetView preset)
        {
            for (int index = 0; index < _attributeDraft.Length; index++)
                _attributeDraft[index] = preset.GetValue(index);
            Render();
        }

        private string GetExpectedBuildLabel()
        {
            if (_attributeDraft.Length == 0)
                return "미정";
            int highest = 0;
            for (int index = 1; index < _attributeDraft.Length; index++)
            {
                if (_attributeDraft[index] > _attributeDraft[highest])
                    highest = index;
            }
            if (_manager.PlayerType == PlayerType.Pitcher)
                return new[] { "파워 피처", "제구형", "변화구형", "이닝이터" }[highest];
            return new[] { "교타자", "장타자", "선구안형", "호타준족", "수비형", "강한 어깨" }[highest];
        }

        private string GetRoleSuitabilityLabel()
        {
            if (_manager.PlayerType != PlayerType.Pitcher || _attributeDraft.Length < 4)
                return GetPositionLabel(_manager.PrimaryPosition) + "  " + ToSuitabilityGrade(Average(_attributeDraft));
            int starter = (_attributeDraft[1] + _attributeDraft[2] + _attributeDraft[3] * 2) / 4;
            int bullpen = (_attributeDraft[0] * 2 + _attributeDraft[1] + _attributeDraft[2]) / 4;
            return $"선발  {ToSuitabilityGrade(starter)}    불펜  {ToSuitabilityGrade(bullpen)}";
        }

        private static int Average(int[] values)
        {
            if (values == null || values.Length == 0)
                return 0;
            int sum = 0;
            for (int index = 0; index < values.Length; index++)
                sum += values[index];
            return sum / values.Length;
        }

        private static string ToSuitabilityGrade(int value)
        {
            if (value >= 52) return "A";
            if (value >= 48) return "B+";
            if (value >= 45) return "B";
            if (value >= 41) return "C+";
            return "C";
        }

        private void EnsureDefaultPitchSelection()
        {
            if (_selectedPitches.Count > 0)
                return;
            PitchRepertoireEntry[] saved = _manager.Draft?.PitchRepertoire;
            if (saved != null && saved.Length == 3)
            {
                for (int index = 0; index < saved.Length; index++)
                {
                    _selectedPitches.Add(saved[index].PitchType);
                    if (saved[index].IsPrimary)
                        _primaryPitch = saved[index].PitchType;
                }
                return;
            }
            _selectedPitches.Add(PitchType.FourSeamFastball);
            _selectedPitches.Add(PitchType.Slider);
            _selectedPitches.Add(PitchType.Changeup);
            _primaryPitch = PitchType.Slider;
        }

        private void TogglePitch(PitchType pitch)
        {
            if (pitch == PitchType.FourSeamFastball)
                return;
            if (_selectedPitches.Contains(pitch))
            {
                _selectedPitches.Remove(pitch);
                if (_primaryPitch == pitch)
                    _primaryPitch = PitchType.FourSeamFastball;
            }
            else if (_selectedPitches.Count < 3)
            {
                _selectedPitches.Add(pitch);
            }
            Render();
        }

        private PitchType[] CopySelectedPitches()
        {
            var values = new PitchType[_selectedPitches.Count];
            int resultIndex = 0;
            PitchType[] all = (PitchType[])Enum.GetValues(typeof(PitchType));
            for (int index = 0; index < all.Length; index++)
            {
                if (_selectedPitches.Contains(all[index]))
                    values[resultIndex++] = all[index];
            }
            return values;
        }

        private string GetCreationAttributeSummary(int[] values)
        {
            if (_manager.PlayerType == PlayerType.Pitcher)
                return $"구위 {values[0]}     제구 {values[1]}     변화 {values[2]}     체력 {values[3]}";
            return $"컨택 {values[0]}     파워 {values[1]}     선구안 {values[2]}\n" +
                   $"주루 {values[3]}     수비 {values[4]}     송구 {values[5]}";
        }

        private string GetCreationDetailSummary(CareerCreationDraft draft)
        {
            if (_manager.PlayerType != PlayerType.Pitcher)
                return "타격 스타일  " + GetBatterStyleLabel(draft.BatterStyle);
            PitchRepertoireEntry[] entries = draft.PitchRepertoire;
            string result = "구종  ";
            for (int index = 0; index < entries.Length; index++)
            {
                if (index > 0) result += "  ·  ";
                result += GetPitchLabel(entries[index].PitchType) + " " + entries[index].Proficiency;
                if (entries[index].IsPrimary) result += " ★";
            }
            return result;
        }

        private string GetSelectedApproachLabel()
        {
            CareerGameSettings settings = _manager.Draft.GameSettings;
            return _manager.PlayerType == PlayerType.Pitcher
                ? GetPitchingApproachLabel(settings.PitchingApproach)
                : GetBattingApproachLabel(settings.BattingApproach);
        }

        private static int GetGuidedStepIndex(NewGameStep step)
        {
            return step switch
            {
                NewGameStep.Identity => 1,
                NewGameStep.Position => 2,
                NewGameStep.AttributeAllocation => 3,
                NewGameStep.PlayerDetails => 4,
                NewGameStep.MatchSettings or NewGameStep.FinalConfirmation => 5,
                _ => 6
            };
        }

        private static bool IsBatterPosition(PlayerPosition position)
        {
            return position is >= PlayerPosition.Catcher and <= PlayerPosition.DesignatedHitter;
        }

        private static string GetPlayerTypeLabel(PlayerType? playerType) =>
            playerType == PlayerType.Pitcher ? "투수" : "타자";

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
                PlayerPosition.ReliefPitcher => "RP  불펜투수",
                _ => "미정"
            };
        }

        private static string GetPitcherRoleLabel(PitcherRole role)
        {
            return role switch
            {
                PitcherRole.Starter => "선발 투수",
                PitcherRole.LongRelief => "롱 릴리프",
                PitcherRole.MiddleRelief => "중간계투",
                PitcherRole.Setup => "셋업맨",
                PitcherRole.Closer => "마무리 투수",
                _ => "불펜 투수"
            };
        }

        private static string GetPitcherRoleGuide(PitcherRole role)
        {
            return role switch
            {
                PitcherRole.Starter => "체력 · 제구 · 다양한 구종",
                PitcherRole.LongRelief => "체력 · 제구 · 2~4이닝",
                PitcherRole.MiddleRelief => "구위 · 제구 · 변화",
                PitcherRole.Setup => "구위 · 변화 · 제구",
                PitcherRole.Closer => "구위 · 제구 · 주무기",
                _ => "팀 상황에 따른 불펜 임무"
            };
        }

        private static string GetBatterStyleLabel(BatterStyle style)
        {
            return style switch
            {
                BatterStyle.Contact => "교타형",
                BatterStyle.Power => "장타형",
                BatterStyle.Patient => "선구안형",
                BatterStyle.Aggressive => "적극형",
                _ => "균형형"
            };
        }

        private static string GetBatterStyleGuide(BatterStyle style)
        {
            return style switch
            {
                BatterStyle.Contact => "정확한 타격과 인플레이를 중시합니다.",
                BatterStyle.Power => "파워를 활용해 장타를 노립니다.",
                BatterStyle.Patient => "볼을 골라 유리한 카운트를 만듭니다.",
                BatterStyle.Aggressive => "빠른 카운트부터 과감하게 타격합니다.",
                _ => "상황에 따라 치우치지 않게 대응합니다."
            };
        }

        private static BattingApproach GetRecommendedBattingApproach(BatterStyle style)
        {
            return style switch
            {
                BatterStyle.Contact => BattingApproach.Contact,
                BatterStyle.Power => BattingApproach.Power,
                BatterStyle.Patient => BattingApproach.Patient,
                BatterStyle.Aggressive => BattingApproach.Aggressive,
                _ => BattingApproach.Balanced
            };
        }

        private static string GetPitchLabel(PitchType pitch)
        {
            return pitch switch
            {
                PitchType.FourSeamFastball => "포심 패스트볼",
                PitchType.TwoSeamFastball => "투심 패스트볼",
                PitchType.Cutter => "커터",
                PitchType.Slider => "슬라이더",
                PitchType.Curveball => "커브",
                PitchType.Changeup => "체인지업",
                PitchType.Splitter => "스플리터",
                PitchType.Sinker => "싱커",
                _ => pitch.ToString()
            };
        }

        private static string GetPitchTrait(PitchType pitch)
        {
            return pitch switch
            {
                PitchType.FourSeamFastball => "빠른 구속 · 탈삼진",
                PitchType.TwoSeamFastball => "약한 타구 · 땅볼",
                PitchType.Cutter => "빗맞은 타구",
                PitchType.Slider => "횡 변화 · 헛스윙",
                PitchType.Curveball => "큰 낙차 · 카운트",
                PitchType.Changeup => "타이밍 교란",
                PitchType.Splitter => "낙차 · 탈삼진",
                PitchType.Sinker => "땅볼 · 장타 억제",
                _ => string.Empty
            };
        }

        private static string GetBattingApproachLabel(BattingApproach approach)
        {
            return approach switch
            {
                BattingApproach.Power => "강하게 타격",
                BattingApproach.Contact => "정확하게 타격",
                BattingApproach.Patient => "신중한 타격",
                BattingApproach.Aggressive => "적극적인 타격",
                _ => "균형 타격"
            };
        }

        private static string GetBattingApproachGuide(BattingApproach approach)
        {
            return approach switch
            {
                BattingApproach.Power => "장타↑ · 삼진 위험↑",
                BattingApproach.Contact => "컨택 · 인플레이 중시",
                BattingApproach.Patient => "볼넷 · 유리한 카운트",
                BattingApproach.Aggressive => "초구부터 빠른 승부",
                _ => "특정 결과에 치우치지 않음"
            };
        }

        private static string GetPitchingApproachLabel(PitchingApproach approach)
        {
            return approach switch
            {
                PitchingApproach.AttackZone => "제구 우선",
                PitchingApproach.Nibble => "유인구 승부",
                PitchingApproach.Strikeout => "전력 투구",
                PitchingApproach.GroundBall => "빠른 승부",
                _ => "균형 투구"
            };
        }

        private static string GetPitchingApproachGuide(PitchingApproach approach)
        {
            return approach switch
            {
                PitchingApproach.AttackZone => "볼넷 억제 · 존 적극 활용",
                PitchingApproach.Nibble => "존 바깥 · 헛스윙 유도",
                PitchingApproach.Strikeout => "구위 · 탈삼진 · 체력 소모",
                PitchingApproach.GroundBall => "투구 수 절약 · 인플레이",
                _ => "체력과 투구 수를 균형 관리"
            };
        }

        private static string GetProgressModeLabel(MatchProgressMode mode)
        {
            return mode switch
            {
                MatchProgressMode.FullGameWatch => "전체 경기 관전",
                MatchProgressMode.InterveneOnPlayer => "내 선수 때만 개입",
                MatchProgressMode.PlayerFocusAutomatic => "내 선수 중심 자동",
                MatchProgressMode.InstantResult => "즉시 결과",
                MatchProgressMode.MiniGame => "직접 참여",
                _ => mode.ToString()
            };
        }

        private static string GetProgressModeGuide(MatchProgressMode mode)
        {
            return mode switch
            {
                MatchProgressMode.FullGameWatch => "모든 플레이 연출 · 자동 진행",
                MatchProgressMode.InterveneOnPlayer => "내 선수 시작 직전 자동 정지",
                MatchProgressMode.PlayerFocusAutomatic => "내 선수 장면만 일반 연출",
                MatchProgressMode.InstantResult => "연출 없이 경기 결과로 이동",
                MatchProgressMode.MiniGame => "내 선수의 투구·타격을 직접 진행",
                _ => string.Empty
            };
        }

        private static string GetThrowLabel(Handedness hand) => hand == Handedness.Left ? "좌투" : "우투";

        private static string GetBatLabel(Handedness hand)
        {
            return hand switch
            {
                Handedness.Left => "좌타",
                Handedness.Switch => "스위치",
                _ => "우타"
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

        private static string FormatMoney(long amount)
        {
            return amount >= 100_000_000L
                ? $"{amount / 100_000_000d:0.##}억원"
                : $"{amount / 10_000d:N0}만원";
        }

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
            string name, Transform parent, Color color, Vector2 size, Vector2 position)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static RectTransform CreatePortrait(
            string name, Transform parent, PlayerPosition position, Vector2 size, Vector2 location)
        {
            RectTransform rect = CreateRect(name, parent, size, location);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = PlayerPortraitSprites.GetDefault(position);
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            rect.gameObject.AddComponent<CanvasGroup>();
            return rect;
        }

        private static Text CreateText(
            string name, Transform parent, string value, int fontSize, FontStyle style, TextAnchor alignment,
            Vector2 size, Vector2 position, Color color)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
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
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.32f);
            button.colors = colors;
            text = CreateText("Label", rect, label, 18, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, PrimaryTextColor);
            Stretch(text.rectTransform);
            return button;
        }

        private static void ApplyFramedButtonSkin(Button button)
        {
            CareerUiVisualElement visual = button.GetComponent<CareerUiVisualElement>();
            if (visual == null)
                visual = button.gameObject.AddComponent<CareerUiVisualElement>();
            visual.Initialize(CareerUiVisualRole.FramedControl);
            CareerUiSkin.ApplyButton(button);
        }

        /// <summary>클릭할 수 없는 카드에 Button 카드와 같은 두께의 공통 프레임을 입힌다.</summary>
        private static void ApplyFramedCardSkin(RectTransform card)
        {
            CareerUiVisualElement visual = card.GetComponent<CareerUiVisualElement>();
            if (visual == null)
                visual = card.gameObject.AddComponent<CareerUiVisualElement>();
            visual.Initialize(CareerUiVisualRole.FramedCard);
            CareerUiSkin.ApplyVisualElement(card.GetComponent<Image>());
        }

        private static InputField CreateInputField(
            string name, Transform parent, string placeholder, string value, Vector2 size, Vector2 position)
        {
            RectTransform rect = CreateImage(name, parent, CardColor, size, position);
            InputField input = rect.gameObject.AddComponent<InputField>();
            Text text = CreateText("Text", rect, value, 19, FontStyle.Normal, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.zero, PrimaryTextColor);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16f, 4f);
            text.rectTransform.offsetMax = new Vector2(-16f, -4f);
            Text hint = CreateText("Placeholder", rect, placeholder, 17, FontStyle.Italic, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.zero, SecondaryTextColor);
            Stretch(hint.rectTransform);
            hint.rectTransform.offsetMin = new Vector2(16f, 4f);
            hint.rectTransform.offsetMax = new Vector2(-16f, -4f);
            input.textComponent = text;
            input.placeholder = hint;
            input.text = value;
            return input;
        }

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
