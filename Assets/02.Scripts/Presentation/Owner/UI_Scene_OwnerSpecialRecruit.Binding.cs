using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerSpecialRecruit
    {
        private OwnerModeManager _manager;
        private RectTransform _content;
        private Dropdown _targets;
        private readonly Dropdown[] _materials = new Dropdown[8];
        private readonly List<string>[] _materialIds = new List<string>[8];
        private IReadOnlyList<PlayerCardDefinition> _targetCards;
        private readonly string[] _selectedMaterials = new string[8];
        private PlayerCardDefinition _selectedTarget;
        private Text _cardLabel;
        private Image _portrait;
        private Text _status;
        private Button _confirm;
        private bool _isConfirming;
        private string _transactionId;

        /// <summary>발급된 대상·레시피·보유 카드의 실제 상태를 영입 화면에 표시한다.</summary>
        public void Bind(OwnerModeManager manager)
        {
            _manager = manager;
            if (_manager == null || !_manager.HasActiveRuntime) return;
            EnsureContent();
            _legend.gameObject.SetActive(false);
            _careerHigh.gameObject.SetActive(false);
            _content.gameObject.SetActive(true);
            var availability = _root.Find("Availability").GetComponent<Text>();
            availability.text = "재료 카드는 최종 확인 후 소모됩니다. 잠금·기용·위시·유학 중인 카드는 보호됩니다.";
            _targetCards = _manager.Runtime.GetSpecialRecruitTargets(
                _isCareerHigh ? PlayerCardEdition.CareerHigh : PlayerCardEdition.Legend);
            var labels = new List<string>();
            foreach (var card in _targetCards)
                labels.Add(Describe(card.CardId) + (_manager.Runtime.TryGetOwnedCard(card.CardId, out _) ? " · 보유" : ""));
            _targets.ClearOptions();
            _targets.AddOptions(labels);
            _targets.SetValueWithoutNotify(0);
            SelectTarget(0);
        }

        private void EnsureContent()
        {
            if (_content != null) return;
            _content = Surface("IssuedRecruitContent", _root, new Color32(230, 228, 217, 255), .018f, .11f, .982f, .912f);
            SectionTitle(_content, "영입 대상", .01f, .91f, .35f, .99f);
            SectionTitle(_content, "영입 필요 선수카드", .37f, .91f, .99f, .99f);
            _targets = CreateSelector("Target", _content, .01f, .82f, .35f, .90f);
            _targets.onValueChanged.AddListener(SelectTarget);
            var preview = Surface("TargetPreview", _content, new Color32(32, 30, 29, 255), .025f, .16f, .335f, .80f);
            Frame(preview, _isCareerHigh ? "CareerHigh" : "Legend", false);
            _cardLabel = preview.Find("EmptyCard").GetComponent<Text>();
            _portrait = OwnerRuntimeUiFactory.CreateImage("PlayerPortrait", preview, Color.white);
            Place(_portrait.rectTransform, .14f, .29f, .86f, .79f);
            _portrait.preserveAspect = true;
            Place(_cardLabel.rectTransform, .08f, .08f, .92f, .28f);
            for (int index = 0; index < 8; index++)
            {
                int slot = index;
                float y = .78f - index * .078f;
                Label("MaterialLabel" + index, _content, "재료 " + (index + 1), 13, Ink, .38f, y, .46f, y + .065f);
                _materials[index] = CreateSelector("Material" + index, _content, .47f, y, .975f, y + .065f);
                _materials[index].onValueChanged.AddListener(value => SelectMaterial(slot, value));
            }
            _status = Label("SelectionStatus", _content, "", 14, Ink, .38f, .09f, .98f, .18f);
            Button("AutoSelect", _content, "자동 배치", .38f, .015f, .60f, .08f, AutoSelect);
            _confirm = Button("ConfirmRecruit", _content, "선수 영입", .64f, .015f, .98f, .08f, ConfirmRecruit);
        }

        private Dropdown CreateSelector(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            GameObject obj = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            obj.name = name;
            obj.transform.SetParent(parent, false);
            Place((RectTransform)obj.transform, x0, y0, x1, y1);
            foreach (var text in obj.GetComponentsInChildren<Text>(true))
            {
                text.font = _helpText.font;
                text.fontSize = 14;
                text.color = Ink;
            }
            var dropdown = obj.GetComponent<Dropdown>();
            dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 280);
            return dropdown;
        }

        private string Describe(string cardId)
        {
            var runtime = _manager.Runtime;
            var card = runtime.WorldCardCatalog.GetRequiredCard(cardId);
            var season = runtime.WorldCardCatalog.GetPlayerSeason(card);
            return season.OriginYear + " " + runtime.IdentityRegistry.GetPlayerDisplayName(season.PlayerPersonId) +
                " · " + runtime.IdentityRegistry.GetFranchiseDisplayName(season.OriginFranchiseId) +
                " · Cost " + season.Cost;
        }

        private void SelectTarget(int index)
        {
            _selectedTarget = index >= 0 && index < _targetCards.Count ? _targetCards[index] : null;
            _isConfirming = false;
            _transactionId = null;
            Array.Clear(_selectedMaterials, 0, _selectedMaterials.Length);
            if (_selectedTarget == null)
            {
                _cardLabel.text = "영입 대상 없음";
                _confirm.interactable = false;
                return;
            }
            _cardLabel.text = Describe(_selectedTarget.CardId).Replace(" · ", "\n");
            _portrait.sprite = PlayerPortraitSprites.GetAssigned(_selectedTarget.PlayerSeasonId);
            _portrait.enabled = _portrait.sprite != null;
            var image = _content.Find("TargetPreview/CardFrame").GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>("UI/PlayerCards/PlayerCard_Full_" + _selectedTarget.Edition + "_v2");
            var recipe = _manager.Runtime.WorldCardCatalog.SpecialCards.GetRequiredRecipe(_selectedTarget.CardId);
            for (int slot = 0; slot < 8; slot++)
            {
                _materialIds[slot] = new List<string> { null };
                var options = new List<string> { "재료 선택" };
                foreach (string id in recipe.MaterialGroups[slot].CandidateCardIds)
                {
                    _materialIds[slot].Add(id);
                    string state = _manager.Runtime.CanUseSpecialRecruitMaterial(id) ? "사용 가능" :
                        _manager.Runtime.TryGetOwnedCard(id, out _) ? "보호 중" : "미보유";
                    options.Add(Describe(id) + " · " + state);
                }
                _materials[slot].ClearOptions();
                _materials[slot].AddOptions(options);
                _materials[slot].SetValueWithoutNotify(0);
            }
            RefreshSelection();
        }

        private void SelectMaterial(int slot, int value)
        {
            _isConfirming = false;
            _transactionId = null;
            _selectedMaterials[slot] = value < _materialIds[slot].Count ? _materialIds[slot][value] : null;
            RefreshSelection();
        }

        private bool CanSelect(string id, HashSet<string> cards, HashSet<int> years)
        {
            if (id == null || !_manager.Runtime.CanUseSpecialRecruitMaterial(id) || cards.Contains(id)) return false;
            var catalog = _manager.Runtime.WorldCardCatalog;
            int year = catalog.GetPlayerSeason(catalog.GetRequiredCard(id)).OriginYear;
            return !_isCareerHigh || !years.Contains(year);
        }

        private void AutoSelect()
        {
            if (_selectedTarget == null) return;
            var cards = new HashSet<string>(StringComparer.Ordinal);
            var years = new HashSet<int>();
            for (int slot = 0; slot < 8; slot++)
            {
                _selectedMaterials[slot] = null;
                _materials[slot].SetValueWithoutNotify(0);
                for (int index = 1; index < _materialIds[slot].Count; index++)
                {
                    string id = _materialIds[slot][index];
                    if (!CanSelect(id, cards, years)) continue;
                    _selectedMaterials[slot] = id;
                    _materials[slot].SetValueWithoutNotify(index);
                    cards.Add(id);
                    var catalog = _manager.Runtime.WorldCardCatalog;
                    years.Add(catalog.GetPlayerSeason(catalog.GetRequiredCard(id)).OriginYear);
                    break;
                }
            }
            _isConfirming = false;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            int count = 0;
            var cards = new HashSet<string>(StringComparer.Ordinal);
            var years = new HashSet<int>();
            foreach (string id in _selectedMaterials)
            {
                if (!CanSelect(id, cards, years)) continue;
                cards.Add(id);
                var catalog = _manager.Runtime.WorldCardCatalog;
                years.Add(catalog.GetPlayerSeason(catalog.GetRequiredCard(id)).OriginYear);
                count++;
            }
            bool owned = _selectedTarget != null && _manager.Runtime.TryGetOwnedCard(_selectedTarget.CardId, out _);
            _status.text = owned ? "이미 보유한 특수 카드입니다." : "사용 가능한 재료 " + count + " / 8";
            _confirm.interactable = count == 8 && !owned;
            _confirm.GetComponentInChildren<Text>().text = "선수 영입";
        }

        private void ConfirmRecruit()
        {
            if (!_isConfirming)
            {
                _isConfirming = true;
                _transactionId = Guid.NewGuid().ToString("N");
                _status.text = "선택한 8장이 소모됩니다. 마지막 보유 장을 쓰면 해당 카드의 성장 상태도 사라집니다.";
                _confirm.GetComponentInChildren<Text>().text = "8장 소모하고 영입 확정";
                return;
            }
            try
            {
                string name = Describe(_selectedTarget.CardId);
                _manager.RecruitSpecialCard(_transactionId, _selectedTarget.CardId, (string[])_selectedMaterials.Clone());
                _status.text = name + " 영입 완료";
                _confirm.interactable = false;
            }
            catch (Exception exception)
            {
                _isConfirming = false;
                _status.text = exception.Message;
            }
        }
    }
}
