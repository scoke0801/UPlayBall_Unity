using System;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단 시설 Route의 구장 조감도와 구장 외형 선택 팝업을 구성한다.</summary>
    public sealed partial class UI_Scene_OwnerClubOperations
    {
        private static readonly string[] StadiumNames =
        {
            "청람 야구장",
            "클래식 파크",
            "리버사이드 구장",
            "스카이 돔"
        };

        private static readonly string[] StadiumDescriptions =
        {
            "도심 접근성이 좋은 개방형 구장입니다. 파란 지붕과 균형 잡힌 관람 동선이 특징입니다.",
            "붉은 벽돌 외관을 살린 전통형 구장입니다. 오래된 야구장의 밀도 높은 분위기를 제공합니다.",
            "강변 녹지와 연결된 친환경 구장입니다. 열린 외야와 넓은 산책 동선이 특징입니다.",
            "전천후 관람을 고려한 현대형 구장입니다. 밝은 외관과 대형 관람석을 갖췄습니다."
        };

        private static readonly Sprite[] StadiumChoiceSprites = new Sprite[4];

        private readonly Button[] _stadiumChoiceButtons = new Button[4];
        private RectTransform _facilityNavigationRoot;
        private RectTransform _stadiumSceneRoot;
        private RectTransform _stadiumPopupRoot;
        private Button _stadiumTabButton;
        private Button _additionalFacilityTabButton;
        private Button _stadiumSceneUpgradeButton;
        private Text _stadiumSceneNameText;
        private Text _stadiumSceneStatusText;
        private Text _stadiumSceneAttendanceText;
        private Text _stadiumPopupTitleText;
        private Text _stadiumPopupDescriptionText;
        private Text _stadiumPopupSpecificationText;
        private Image _stadiumPopupPreview;
        private OwnerClubOperationPresentationModel _stadiumModel;
        private bool _isStadiumSectionSelected = true;
        private int _selectedStadiumIndex;
        private int _pendingStadiumIndex;

        public int SelectedStadiumIndex => _selectedStadiumIndex;

        private void BuildStadiumWorkspace(RectTransform root)
        {
            BuildStadiumScene(root);
            BuildFacilityNavigation(root);
            BuildStadiumSelectionPopup(root);
        }

        private void BuildStadiumScene(RectTransform root)
        {
            Image scene = OwnerRuntimeUiFactory.CreateImage("StadiumScene", root, Color.white);
            _stadiumSceneRoot = scene.rectTransform;
            scene.sprite = LoadGeneratedSprite("UI/Generated/owner_stadium_city_v1");
            scene.preserveAspect = false;
            OwnerRuntimeUiFactory.SetAnchors(
                _stadiumSceneRoot,
                Vector2.zero,
                new Vector2(1f, 0.93f),
                new Vector2(8f, 8f),
                new Vector2(-8f, -2f));

            Image titleBar = OwnerRuntimeUiFactory.CreateImage(
                "TitleBar", scene.transform, new Color32(20, 24, 29, 238));
            OwnerRuntimeUiFactory.SetAnchors(
                titleBar.rectTransform,
                new Vector2(0f, 0.88f),
                new Vector2(0.46f, 0.98f),
                Vector2.zero,
                Vector2.zero);
            Text title = OwnerRuntimeUiFactory.CreateText(
                "Title", titleBar.transform, "구장 관리", 22, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white);
            OwnerRuntimeUiFactory.Stretch(title.rectTransform, new Vector2(22f, 0f), new Vector2(-10f, 0f));

            Image informationBar = OwnerRuntimeUiFactory.CreateImage(
                "InformationBar", scene.transform, new Color32(13, 17, 21, 242));
            informationBar.raycastTarget = true;
            OwnerRuntimeUiFactory.SetAnchors(
                informationBar.rectTransform,
                new Vector2(0.06f, 0.025f),
                new Vector2(0.94f, 0.20f),
                Vector2.zero,
                Vector2.zero);
            AddOutline(informationBar.gameObject, new Color32(188, 196, 205, 255), 2f);
            UIOwnerFrontOfficePanel.Apply(informationBar.rectTransform, "CompactStrip");

            _stadiumSceneNameText = OwnerRuntimeUiFactory.CreateText(
                "StadiumName", informationBar.transform, StadiumNames[0], 23, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white);
            OwnerRuntimeUiFactory.SetAnchors(
                _stadiumSceneNameText.rectTransform,
                new Vector2(0.02f, 0.48f),
                new Vector2(0.35f, 0.95f),
                Vector2.zero,
                Vector2.zero);
            _stadiumSceneStatusText = OwnerRuntimeUiFactory.CreateText(
                "StadiumStatus", informationBar.transform, string.Empty, 14, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Color32(214, 220, 226, 255));
            OwnerRuntimeUiFactory.SetAnchors(
                _stadiumSceneStatusText.rectTransform,
                new Vector2(0.02f, 0.08f),
                new Vector2(0.42f, 0.50f),
                Vector2.zero,
                Vector2.zero);

            Button select = OwnerRuntimeUiFactory.CreateButton(
                "OpenStadiumSelection", informationBar.transform, "구장 선택", Color.black, 18);
            OwnerRuntimeUiFactory.SetAnchors(
                select.GetComponent<RectTransform>(),
                new Vector2(0.43f, 0.18f),
                new Vector2(0.61f, 0.84f),
                Vector2.zero,
                Vector2.zero);
            StyleDarkButton(select, true);
            select.onClick.AddListener(OpenStadiumSelection);

            _stadiumSceneUpgradeButton = OwnerRuntimeUiFactory.CreateButton(
                "StadiumUpgrade", informationBar.transform, "구장 증축", Color.black, 16);
            OwnerRuntimeUiFactory.SetAnchors(
                _stadiumSceneUpgradeButton.GetComponent<RectTransform>(),
                new Vector2(0.625f, 0.18f),
                new Vector2(0.76f, 0.84f),
                Vector2.zero,
                Vector2.zero);
            StyleDarkButton(_stadiumSceneUpgradeButton, false);
            _stadiumSceneUpgradeButton.onClick.AddListener(() => StadiumUpgradeRequested?.Invoke());

            _stadiumSceneAttendanceText = OwnerRuntimeUiFactory.CreateText(
                "Attendance", informationBar.transform, string.Empty, 14, FontStyle.Bold,
                TextAnchor.MiddleRight, new Color32(236, 214, 119, 255));
            OwnerRuntimeUiFactory.SetAnchors(
                _stadiumSceneAttendanceText.rectTransform,
                new Vector2(0.77f, 0.10f),
                new Vector2(0.98f, 0.90f),
                Vector2.zero,
                Vector2.zero);
        }

        private void BuildFacilityNavigation(RectTransform root)
        {
            Image navigation = OwnerRuntimeUiFactory.CreateImage(
                "FacilityNavigation", root, OwnerDashboardStyle.TableHeader);
            navigation.raycastTarget = true;
            _facilityNavigationRoot = navigation.rectTransform;
            OwnerRuntimeUiFactory.SetAnchors(
                _facilityNavigationRoot,
                new Vector2(0f, 0.93f),
                Vector2.one,
                new Vector2(8f, 2f),
                new Vector2(-8f, -6f));
            OwnerDashboardStyle.SetDataSurface(navigation, OwnerDashboardStyle.TableHeader);

            Text category = OwnerRuntimeUiFactory.CreateText(
                "Category", navigation.transform, "시설", 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, OwnerDashboardStyle.Ivory);
            OwnerDashboardStyle.SetDataText(category, true);
            OwnerRuntimeUiFactory.SetAnchors(
                category.rectTransform, Vector2.zero, new Vector2(0.12f, 1f), Vector2.zero, Vector2.zero);

            _stadiumTabButton = CreateFacilityTab(
                navigation.transform, "StadiumTab", "구장 건설", 0.12f, 0.34f, true);
            _stadiumTabButton.onClick.AddListener(() => SelectFacilitySection(true));
            _additionalFacilityTabButton = CreateFacilityTab(
                navigation.transform, "AdditionalFacilityTab", "부가 시설", 0.34f, 0.56f, false);
            _additionalFacilityTabButton.onClick.AddListener(() => SelectFacilitySection(false));

            Text instruction = OwnerRuntimeUiFactory.CreateText(
                "Instruction", navigation.transform,
                "구장 외형 선택 · 수용 인원 증축 · 운영 시설 투자",
                13, FontStyle.Normal, TextAnchor.MiddleRight, new Color32(82, 90, 101, 255));
            OwnerRuntimeUiFactory.SetAnchors(
                instruction.rectTransform,
                new Vector2(0.58f, 0f),
                new Vector2(0.98f, 1f),
                Vector2.zero,
                Vector2.zero);
        }

        private Button CreateFacilityTab(
            Transform parent,
            string name,
            string label,
            float minX,
            float maxX,
            bool selected)
        {
            Button button = OwnerRuntimeUiFactory.CreateButton(
                name, parent, label,
                selected ? new Color32(25, 67, 113, 255) : new Color32(251, 251, 250, 255), 15);
            OwnerRuntimeUiFactory.SetAnchors(
                button.GetComponent<RectTransform>(),
                new Vector2(minX, 0.08f),
                new Vector2(maxX, 0.92f),
                new Vector2(2f, 0f),
                new Vector2(-2f, 0f));
            StyleFacilityTab(button, selected);
            return button;
        }

        private void BuildStadiumSelectionPopup(RectTransform root)
        {
            Image shade = OwnerRuntimeUiFactory.CreateImage(
                "StadiumSelectionPopup", root, new Color(0f, 0f, 0f, 0.74f));
            shade.raycastTarget = true;
            _stadiumPopupRoot = shade.rectTransform;
            OwnerRuntimeUiFactory.Stretch(_stadiumPopupRoot);

            Image panel = OwnerRuntimeUiFactory.CreateImage(
                "Panel", shade.transform, new Color32(247, 247, 245, 255));
            panel.raycastTarget = true;
            OwnerRuntimeUiFactory.SetAnchors(
                panel.rectTransform,
                new Vector2(0.10f, 0.10f),
                new Vector2(0.90f, 0.90f),
                Vector2.zero,
                Vector2.zero);
            AddOutline(panel.gameObject, new Color32(54, 61, 70, 255), 2f);
            UIOwnerFrontOfficePanel.Apply(panel.rectTransform, "ManagerReport");

            Image header = OwnerRuntimeUiFactory.CreateImage(
                "Header", panel.transform, new Color32(22, 68, 119, 255));
            header.enabled = false;
            OwnerRuntimeUiFactory.SetAnchors(
                header.rectTransform,
                new Vector2(0f, 0.91f),
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            Text headerTitle = OwnerRuntimeUiFactory.CreateText(
                "Title", header.transform, "구장 선택", 21, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white);
            OwnerRuntimeUiFactory.Stretch(headerTitle.rectTransform, new Vector2(20f, 0f), new Vector2(-64f, 0f));
            Button close = OwnerRuntimeUiFactory.CreateButton(
                "Close", header.transform, "×", new Color32(238, 240, 242, 255), 22);
            OwnerRuntimeUiFactory.SetAnchors(
                close.GetComponent<RectTransform>(),
                new Vector2(0.94f, 0.14f),
                new Vector2(0.988f, 0.86f),
                Vector2.zero,
                Vector2.zero);
            close.onClick.AddListener(CloseStadiumSelection);

            Image choiceArea = OwnerRuntimeUiFactory.CreateImage(
                "ChoiceArea", panel.transform, new Color32(235, 238, 240, 255));
            OwnerRuntimeUiFactory.SetAnchors(
                choiceArea.rectTransform,
                new Vector2(0.018f, 0.12f),
                new Vector2(0.63f, 0.89f),
                Vector2.zero,
                Vector2.zero);
            Text sectionTitle = OwnerRuntimeUiFactory.CreateText(
                "SectionTitle", choiceArea.transform, "일반 구장", 16, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Color32(39, 44, 51, 255));
            OwnerRuntimeUiFactory.SetAnchors(
                sectionTitle.rectTransform,
                new Vector2(0.02f, 0.91f),
                new Vector2(0.98f, 0.99f),
                Vector2.zero,
                Vector2.zero);

            for (int index = 0; index < _stadiumChoiceButtons.Length; index++)
                CreateStadiumChoice(choiceArea.transform, index);

            Image detail = OwnerRuntimeUiFactory.CreateImage(
                "Detail", panel.transform, Color.white);
            OwnerRuntimeUiFactory.SetAnchors(
                detail.rectTransform,
                new Vector2(0.645f, 0.12f),
                new Vector2(0.982f, 0.89f),
                Vector2.zero,
                Vector2.zero);
            AddOutline(detail.gameObject, new Color32(145, 153, 161, 255), 1f);

            Text selectedLabel = OwnerRuntimeUiFactory.CreateText(
                "SelectedLabel", detail.transform, "선택한 시설", 14, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Color32(135, 74, 28, 255));
            OwnerRuntimeUiFactory.SetAnchors(
                selectedLabel.rectTransform,
                new Vector2(0.04f, 0.92f),
                new Vector2(0.96f, 0.99f),
                Vector2.zero,
                Vector2.zero);

            _stadiumPopupPreview = OwnerRuntimeUiFactory.CreateImage("Preview", detail.transform, Color.white);
            _stadiumPopupPreview.preserveAspect = false;
            OwnerRuntimeUiFactory.SetAnchors(
                _stadiumPopupPreview.rectTransform,
                new Vector2(0.05f, 0.58f),
                new Vector2(0.95f, 0.91f),
                Vector2.zero,
                Vector2.zero);
            AddOutline(_stadiumPopupPreview.gameObject, new Color32(96, 105, 115, 255), 1f);

            _stadiumPopupTitleText = OwnerRuntimeUiFactory.CreateText(
                "StadiumName", detail.transform, string.Empty, 20, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Color32(34, 39, 46, 255));
            OwnerRuntimeUiFactory.SetAnchors(
                _stadiumPopupTitleText.rectTransform,
                new Vector2(0.05f, 0.48f),
                new Vector2(0.95f, 0.58f),
                Vector2.zero,
                Vector2.zero);
            _stadiumPopupSpecificationText = OwnerRuntimeUiFactory.CreateText(
                "Specification", detail.transform, string.Empty, 15, FontStyle.Bold,
                TextAnchor.UpperLeft, new Color32(73, 80, 89, 255));
            OwnerRuntimeUiFactory.SetAnchors(
                _stadiumPopupSpecificationText.rectTransform,
                new Vector2(0.05f, 0.30f),
                new Vector2(0.95f, 0.48f),
                Vector2.zero,
                Vector2.zero);
            _stadiumPopupDescriptionText = OwnerRuntimeUiFactory.CreateText(
                "Description", detail.transform, string.Empty, 14, FontStyle.Normal,
                TextAnchor.UpperLeft, new Color32(75, 82, 91, 255));
            _stadiumPopupDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _stadiumPopupDescriptionText.verticalOverflow = VerticalWrapMode.Truncate;
            OwnerRuntimeUiFactory.SetAnchors(
                _stadiumPopupDescriptionText.rectTransform,
                new Vector2(0.05f, 0.05f),
                new Vector2(0.95f, 0.29f),
                Vector2.zero,
                Vector2.zero);

            Button confirm = OwnerRuntimeUiFactory.CreateButton(
                "Confirm", panel.transform, "지정", new Color32(245, 246, 247, 255), 16);
            OwnerRuntimeUiFactory.SetAnchors(
                confirm.GetComponent<RectTransform>(),
                new Vector2(0.36f, 0.025f),
                new Vector2(0.50f, 0.095f),
                Vector2.zero,
                Vector2.zero);
            AddOutline(confirm.gameObject, new Color32(92, 100, 109, 255), 1f);
            confirm.onClick.AddListener(ConfirmStadiumSelection);
            Button cancel = OwnerRuntimeUiFactory.CreateButton(
                "Cancel", panel.transform, "취소", new Color32(245, 246, 247, 255), 16);
            OwnerRuntimeUiFactory.SetAnchors(
                cancel.GetComponent<RectTransform>(),
                new Vector2(0.51f, 0.025f),
                new Vector2(0.65f, 0.095f),
                Vector2.zero,
                Vector2.zero);
            AddOutline(cancel.gameObject, new Color32(92, 100, 109, 255), 1f);
            cancel.onClick.AddListener(CloseStadiumSelection);

            _stadiumPopupRoot.gameObject.SetActive(false);
        }

        private void CreateStadiumChoice(Transform parent, int index)
        {
            int column = index % 2;
            int row = index / 2;
            float minX = column == 0 ? 0.02f : 0.51f;
            float maxX = column == 0 ? 0.49f : 0.98f;
            float maxY = row == 0 ? 0.89f : 0.48f;
            float minY = row == 0 ? 0.50f : 0.09f;
            Button choice = OwnerRuntimeUiFactory.CreateButton(
                string.Concat("StadiumChoice", index), parent, string.Empty, Color.white, 13);
            OwnerRuntimeUiFactory.SetAnchors(
                choice.GetComponent<RectTransform>(),
                new Vector2(minX, minY),
                new Vector2(maxX, maxY),
                Vector2.zero,
                Vector2.zero);
            AddOutline(choice.gameObject, new Color32(118, 128, 138, 255), 1f);

            Image artwork = OwnerRuntimeUiFactory.CreateImage("Artwork", choice.transform, Color.white);
            artwork.sprite = LoadStadiumChoiceSprite(index);
            artwork.preserveAspect = false;
            OwnerRuntimeUiFactory.SetAnchors(
                artwork.rectTransform,
                new Vector2(0.03f, 0.31f),
                new Vector2(0.97f, 0.97f),
                Vector2.zero,
                Vector2.zero);
            Text name = OwnerRuntimeUiFactory.CreateText(
                "Name", choice.transform, StadiumNames[index], 15, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Color32(38, 43, 49, 255));
            OwnerRuntimeUiFactory.SetAnchors(
                name.rectTransform,
                new Vector2(0.05f, 0.13f),
                new Vector2(0.70f, 0.31f),
                Vector2.zero,
                Vector2.zero);
            Text capacity = OwnerRuntimeUiFactory.CreateText(
                "Capacity", choice.transform, "구장 외형", 12, FontStyle.Normal,
                TextAnchor.MiddleRight, new Color32(87, 95, 104, 255));
            OwnerRuntimeUiFactory.SetAnchors(
                capacity.rectTransform,
                new Vector2(0.58f, 0.13f),
                new Vector2(0.95f, 0.31f),
                Vector2.zero,
                Vector2.zero);
            int capturedIndex = index;
            choice.onClick.AddListener(() => SelectStadiumChoice(capturedIndex));
            _stadiumChoiceButtons[index] = choice;
        }

        private void BindStadiumWorkspace(OwnerClubOperationPresentationModel model)
        {
            _stadiumModel = model;
            if (_stadiumSceneNameText == null) return;
            _stadiumSceneNameText.text = string.Concat(StadiumNames[_selectedStadiumIndex], "  /  ", model.StadiumText);
            _stadiumSceneStatusText.text = string.Concat(model.FanBaseText, "     ", model.PopularityText);
            _stadiumSceneAttendanceText.text = string.Concat(
                model.ExpectedAttendanceText, "\n", model.RecentAttendanceText);
            _stadiumSceneUpgradeButton.interactable = model.Snapshot.CanUpgradeStadium;
            _stadiumSceneUpgradeButton.transform.Find("Label").GetComponent<Text>().text =
                model.Snapshot.CanUpgradeStadium ? "구장 증축" : "증축 불가";
            RefreshStadiumChoice();
        }

        private void SelectFacilitySection(bool showStadium)
        {
            _isStadiumSectionSelected = showStadium;
            CloseStadiumSelection();
            RefreshFacilitySection();
        }

        private void RefreshFacilitySection()
        {
            if (_stadiumSceneRoot == null || _facilityRoot == null) return;
            _stadiumSceneRoot.gameObject.SetActive(_isStadiumSectionSelected);
            _facilityRoot.gameObject.SetActive(!_isStadiumSectionSelected);
            if (_readabilityCanvas != null)
            {
                float alpha = _isStadiumSectionSelected ? 0f : 1f;
                _readabilityCanvas.color = new Color(
                    CareerUiTheme.ReferenceCanvas.r,
                    CareerUiTheme.ReferenceCanvas.g,
                    CareerUiTheme.ReferenceCanvas.b,
                    alpha);
            }
            StyleFacilityTab(_stadiumTabButton, _isStadiumSectionSelected);
            StyleFacilityTab(_additionalFacilityTabButton, !_isStadiumSectionSelected);
        }

        private void OpenStadiumSelection()
        {
            _pendingStadiumIndex = _selectedStadiumIndex;
            RefreshStadiumChoice();
            _stadiumPopupRoot.gameObject.SetActive(true);
            _stadiumPopupRoot.SetAsLastSibling();
        }

        private void CloseStadiumSelection()
        {
            if (_stadiumPopupRoot != null) _stadiumPopupRoot.gameObject.SetActive(false);
        }

        private void SelectStadiumChoice(int index)
        {
            if (index < 0 || index >= StadiumNames.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            _pendingStadiumIndex = index;
            RefreshStadiumChoice();
        }

        private void ConfirmStadiumSelection()
        {
            _selectedStadiumIndex = _pendingStadiumIndex;
            if (_stadiumSceneNameText != null)
            {
                string stadiumState = _stadiumModel == null ? string.Empty : string.Concat("  /  ", _stadiumModel.StadiumText);
                _stadiumSceneNameText.text = string.Concat(StadiumNames[_selectedStadiumIndex], stadiumState);
            }
            SetFeedback(
                string.Concat(StadiumNames[_selectedStadiumIndex], " 외형을 지정했습니다. 수용 인원과 운영 효과는 현재 구장 상태를 따릅니다."),
                false);
            CloseStadiumSelection();
        }

        private void RefreshStadiumChoice()
        {
            if (_stadiumPopupPreview == null) return;
            for (int index = 0; index < _stadiumChoiceButtons.Length; index++)
            {
                Button choice = _stadiumChoiceButtons[index];
                if (choice == null) continue;
                choice.GetComponent<Image>().color = index == _pendingStadiumIndex
                    ? new Color32(220, 234, 248, 255)
                    : Color.white;
                Outline outline = choice.GetComponent<Outline>();
                if (outline != null)
                    outline.effectColor = index == _pendingStadiumIndex
                        ? new Color32(22, 74, 132, 255)
                        : new Color32(118, 128, 138, 255);
            }
            _stadiumPopupPreview.sprite = LoadStadiumChoiceSprite(_pendingStadiumIndex);
            _stadiumPopupTitleText.text = StadiumNames[_pendingStadiumIndex];
            _stadiumPopupDescriptionText.text = StadiumDescriptions[_pendingStadiumIndex];
            string capacity = _stadiumModel == null
                ? "수용 인원 -"
                : string.Concat("수용 인원 ", _stadiumModel.Snapshot.StadiumCapacity.ToString("N0"), "명");
            _stadiumPopupSpecificationText.text = string.Concat(
                capacity,
                "\n외형 변경 비용 0원\n경기·재정 효과 변화 없음");
        }

        private static Sprite LoadStadiumChoiceSprite(int index)
        {
            if (index < 0 || index >= StadiumChoiceSprites.Length) return null;
            if (StadiumChoiceSprites[index] != null) return StadiumChoiceSprites[index];
            Texture2D texture = Resources.Load<Texture2D>("UI/Generated/owner_stadium_choices_v1");
            if (texture == null) return null;
            float width = texture.width * 0.5f;
            float height = texture.height * 0.5f;
            float x = index % 2 * width;
            float y = (1 - index / 2) * height;
            StadiumChoiceSprites[index] = Sprite.Create(
                texture,
                new Rect(x, y, width, height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            return StadiumChoiceSprites[index];
        }

        private static Sprite LoadGeneratedSprite(string resourcePath)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites != null && sprites.Length > 0) return sprites[0];
            return Resources.Load<Sprite>(resourcePath);
        }

        private static void StyleFacilityTab(Button button, bool selected)
        {
            OwnerUiButtonSkin.Apply(button, OwnerButtonRole.Tab);
            OwnerUiButtonSkin.SetSelected(button, selected);
        }

        private static void StyleDarkButton(Button button, bool primary)
        {
            button.GetComponent<Image>().color = primary
                ? new Color32(4, 7, 10, 255)
                : new Color32(49, 56, 64, 255);
            button.transform.Find("Label").GetComponent<Text>().color = Color.white;
            AddOutline(button.gameObject, new Color32(208, 214, 220, 255), 2f);
        }

        private static void AddOutline(GameObject target, Color color, float distance)
        {
            Outline outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = false;
        }
    }
}
