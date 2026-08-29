using System;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_CareerPresentation
    {
        private static readonly Color PrimaryTextColor = new(0.96f, 0.98f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.68f, 0.76f, 0.84f, 1f);
        private static readonly Color GoldColor = new(0.97f, 0.73f, 0.23f, 1f);
        private static readonly Color AccentColor = new(0.22f, 0.68f, 1f, 1f);
        private static readonly Color PositiveColor = new(0.35f, 0.88f, 0.54f, 1f);

        private CanvasGroup _rootCanvasGroup;
        private RectTransform _stage;
        private RectTransform _backgroundRoot;
        private Image _backgroundImage;
        private RectTransform _heroRoot;
        private Image _heroImage;
        private Image _heroBorder;
        private RectTransform _titleRoot;
        private CanvasGroup _categoryCanvasGroup;
        private CanvasGroup _titleCanvasGroup;
        private CanvasGroup _descriptionCanvasGroup;
        private CanvasGroup _statCanvasGroup;
        private CanvasGroup _continueCanvasGroup;
        private Text _categoryText;
        private Text _titleText;
        private Text _playerNameText;
        private Text _descriptionText;
        private Text _weekText;
        private Text _rankText;
        private RectTransform _statContainer;
        private RectTransform _confettiRoot;
        private RectTransform _shineRoot;
        private Image _shineImage;
        private Image _blackFade;
        private Button _continueButton;
        private Text _continueText;
        private AudioSource _audioSource;

        private void BuildHierarchy()
        {
            RectTransform root = (RectTransform)transform;
            Stretch(root);
            _rootCanvasGroup = GetComponent<CanvasGroup>();

            Image blocker = CreateImage("InputBlocker", root, new Color(0f, 0f, 0f, 1f), true);
            blocker.raycastTarget = true;
            _stage = CreateRect("Stage", root, new Vector2(1920f, 1080f), Vector2.zero);

            RectTransform backgroundViewport = CreateRect(
                "BackgroundViewport", _stage, new Vector2(1920f, 1080f), Vector2.zero);
            backgroundViewport.gameObject.AddComponent<RectMask2D>();
            _backgroundRoot = CreateRect(
                "BackgroundRoot", backgroundViewport, new Vector2(1920f, 2560f), Vector2.zero);
            _backgroundImage = _backgroundRoot.gameObject.AddComponent<Image>();
            _backgroundImage.raycastTarget = false;
            _backgroundImage.preserveAspect = false;
            Material blur = Resources.Load<Material>("UI/CareerPresentationBlur");
            if (blur != null)
                _backgroundImage.material = blur;

            CreateImage("BackgroundDim", _stage, new Color(0.002f, 0.009f, 0.016f, 0.54f), true);
            RectTransform leftScrim = CreateRect(
                "LeftScrim", _stage, new Vector2(1160f, 1080f), new Vector2(-380f, 0f));
            leftScrim.gameObject.AddComponent<Image>().color = new Color(0.003f, 0.013f, 0.024f, 0.93f);
            CreateGradientScrim(_stage);

            _heroRoot = CreateRect("HeroImageRoot", _stage, new Vector2(810f, 1020f), new Vector2(500f, 0f));
            _heroBorder = _heroRoot.gameObject.AddComponent<Image>();
            _heroBorder.color = new Color(0.3f, 0.47f, 0.62f, 0.72f);
            _heroBorder.raycastTarget = false;
            RectTransform heroInset = CreateRect("HeroInset", _heroRoot, new Vector2(798f, 1008f), Vector2.zero);
            _heroImage = heroInset.gameObject.AddComponent<Image>();
            _heroImage.preserveAspect = true;
            _heroImage.raycastTarget = false;
            _heroImage.color = Color.white;

            _shineRoot = CreateRect("Shine", heroInset, new Vector2(150f, 1200f), new Vector2(-560f, 0f));
            _shineRoot.localRotation = Quaternion.Euler(0f, 0f, -12f);
            _shineImage = _shineRoot.gameObject.AddComponent<Image>();
            _shineImage.color = new Color(1f, 0.88f, 0.48f, 0f);
            _shineImage.raycastTarget = false;

            _rankText = CreateText(
                "RankWatermark", _stage, string.Empty, 310, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(480f, 430f), new Vector2(-515f, 70f), new Color(1f, 0.75f, 0.22f, 0.11f));

            _titleRoot = CreateRect("TextRoot", _stage, new Vector2(790f, 860f), new Vector2(-500f, 0f));
            _categoryText = CreateText(
                "Category", _titleRoot, string.Empty, 17, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(730f, 32f), new Vector2(0f, 345f), AccentColor);
            _categoryCanvasGroup = _categoryText.gameObject.AddComponent<CanvasGroup>();

            _titleText = CreateText(
                "Title", _titleRoot, string.Empty, 54, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(750f, 92f), new Vector2(0f, 272f), PrimaryTextColor);
            _titleCanvasGroup = _titleText.gameObject.AddComponent<CanvasGroup>();
            AddOutline(_titleText, new Color(0f, 0f, 0f, 0.86f), 2f);

            _playerNameText = CreateText(
                "PlayerName", _titleRoot, string.Empty, 28, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(730f, 48f), new Vector2(0f, 198f), GoldColor);

            RectTransform descriptionRoot = CreateRect(
                "DescriptionRoot", _titleRoot, new Vector2(750f, 155f), new Vector2(0f, 92f));
            _descriptionCanvasGroup = descriptionRoot.gameObject.AddComponent<CanvasGroup>();
            _descriptionText = CreateText(
                "Description", descriptionRoot, string.Empty, 21, FontStyle.Normal, TextAnchor.UpperLeft,
                Vector2.zero, Vector2.zero, SecondaryTextColor, true);

            _weekText = CreateText(
                "WeekProgress", _titleRoot, string.Empty, 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(730f, 34f), new Vector2(0f, -2f), AccentColor);

            _statContainer = CreateRect(
                "StatContainer", _titleRoot, new Vector2(750f, 330f), new Vector2(0f, -205f));
            _statCanvasGroup = _statContainer.gameObject.AddComponent<CanvasGroup>();

            _continueButton = CreateButton(
                "Continue", _titleRoot, new Vector2(460f, 64f), new Vector2(-135f, -397f), out _continueText);
            _continueCanvasGroup = _continueButton.gameObject.AddComponent<CanvasGroup>();
            _continueButton.onClick.AddListener(TryDismiss);

            _confettiRoot = CreateRect("ParticleRoot", _stage, new Vector2(1920f, 1080f), Vector2.zero);
            _confettiRoot.gameObject.AddComponent<CanvasGroup>();

            _blackFade = CreateImage("BlackFade", root, Color.black, true);
            _blackFade.raycastTarget = false;
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.ignoreListenerPause = true;
        }

        private void RenderRequest(CareerPresentationRequest request, CareerPresentationData data)
        {
            Sprite illustration = data?.Illustration;
            _backgroundImage.sprite = illustration;
            _heroImage.sprite = illustration;
            _backgroundImage.enabled = illustration != null;
            _heroImage.enabled = illustration != null;

            Color accent = GetAccent(request.Type);
            _heroBorder.color = new Color(accent.r, accent.g, accent.b, 0.72f);
            _categoryText.color = accent;
            _playerNameText.color = request.Type == CareerPresentationType.Rest ? PositiveColor : GoldColor;
            _categoryText.text = request.Category;
            _titleText.text = request.Title;
            _playerNameText.text = request.PlayerName;
            _descriptionText.text = request.Description;
            _rankText.text = request.Type == CareerPresentationType.RegularSeasonFirst ? "1" : string.Empty;
            _weekText.text = request.HasWeekProgress
                ? $"OFFSEASON WEEK {request.StartWeek}  →  WEEK {request.EndWeek}"
                : string.Empty;
            _weekText.gameObject.SetActive(request.HasWeekProgress);

            ClearChildren(_statContainer);
            int statCount = Math.Min(6, request.Stats.Length);
            float rowHeight = statCount > 4 ? 48f : 58f;
            float startY = (statCount - 1) * rowHeight * 0.5f;
            for (int index = 0; index < statCount; index++)
            {
                PresentationStat stat = request.Stats[index];
                float y = startY - index * rowHeight;
                Image line = CreateImage(
                    "StatRow_" + index,
                    _statContainer,
                    new Color(0.035f, 0.085f, 0.125f, 0.82f),
                    false,
                    new Vector2(720f, rowHeight - 7f),
                    new Vector2(0f, y));
                line.gameObject.AddComponent<CanvasGroup>();
                CreateText(
                    "Label", line.transform, stat.Label, 17, FontStyle.Normal, TextAnchor.MiddleLeft,
                    new Vector2(350f, rowHeight - 8f), new Vector2(-165f, 0f), SecondaryTextColor);
                Text value = CreateText(
                    "Value", line.transform, stat.Value, stat.IsEmphasized ? 25 : 21,
                    FontStyle.Bold, TextAnchor.MiddleRight,
                    new Vector2(300f, rowHeight - 8f), new Vector2(190f, 0f),
                    stat.IsEmphasized ? PositiveColor : PrimaryTextColor);
                value.gameObject.AddComponent<CanvasGroup>();
            }

            _continueText.text = request.Grade == CareerPresentationGrade.Major
                ? "계속   ENTER     ·     건너뛰기   ESC"
                : "결과 확인   ENTER";
            CareerMotionPreset preset = ResolveMotionPreset(request.Type, data);
            BuildConfetti(preset);
            _shineRoot.gameObject.SetActive(preset == CareerMotionPreset.Award);
            if (data?.Stinger != null)
            {
                _audioSource.clip = data.Stinger;
                _audioSource.Play();
            }
        }

        private void BuildConfetti(CareerMotionPreset preset)
        {
            ClearChildren(_confettiRoot);
            bool isCelebration = preset is CareerMotionPreset.RankUp or
                CareerMotionPreset.Championship or CareerMotionPreset.Award;
            _confettiRoot.gameObject.SetActive(isCelebration);
            if (!isCelebration)
                return;
            for (int index = 0; index < 18; index++)
            {
                float x = -900f + index * 103f;
                float y = 590f + (index % 4) * 95f;
                Color color = index % 3 == 0
                    ? Color.white
                    : index % 3 == 1 ? GoldColor : new Color(0.82f, 0.57f, 0.16f, 1f);
                Image piece = CreateImage(
                    "Confetti_" + index,
                    _confettiRoot,
                    color,
                    false,
                    new Vector2(8f + index % 4 * 3f, 18f + index % 3 * 5f),
                    new Vector2(x, y));
                piece.raycastTarget = false;
            }
        }

        private static CareerMotionPreset ResolveMotionPreset(
            CareerPresentationType type,
            CareerPresentationData data)
        {
            if (data != null)
                return data.MotionPreset;
            return type switch
            {
                CareerPresentationType.RegularSeasonFirst => CareerMotionPreset.RankUp,
                CareerPresentationType.PostseasonChampion => CareerMotionPreset.Championship,
                CareerPresentationType.GoldenGlove or CareerPresentationType.RegularSeasonMvp or
                    CareerPresentationType.PostseasonMvp => CareerMotionPreset.Award,
                CareerPresentationType.OverseasTraining => CareerMotionPreset.Travel,
                CareerPresentationType.Rest => CareerMotionPreset.Rest,
                _ => CareerMotionPreset.Training
            };
        }

        private static Color GetAccent(CareerPresentationType type)
        {
            return type switch
            {
                CareerPresentationType.RegularSeasonFirst or CareerPresentationType.PostseasonChampion or
                    CareerPresentationType.GoldenGlove => GoldColor,
                CareerPresentationType.RegularSeasonMvp or CareerPresentationType.PostseasonMvp =>
                    new Color(0.32f, 0.70f, 1f, 1f),
                CareerPresentationType.OverseasTraining => new Color(1f, 0.64f, 0.30f, 1f),
                CareerPresentationType.Rest => PositiveColor,
                _ => AccentColor
            };
        }

        private static void CreateGradientScrim(Transform parent)
        {
            for (int index = 0; index < 8; index++)
            {
                float alpha = 0.84f * (1f - index / 8f);
                Image strip = CreateImage(
                    "Gradient_" + index,
                    parent,
                    new Color(0.003f, 0.013f, 0.024f, alpha),
                    false,
                    new Vector2(95f, 1080f),
                    new Vector2(245f + index * 95f, 0f));
                strip.raycastTarget = false;
            }
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Color color,
            bool stretch,
            Vector2 size = default,
            Vector2 position = default)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch)
                Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Vector2 size,
            Vector2 position,
            Color color,
            bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch)
                Stretch(rect);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position,
            out Text label)
        {
            Image image = CreateImage(
                name,
                parent,
                new Color(0.055f, 0.29f, 0.25f, 0.96f),
                false,
                size,
                position);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.08f, 0.41f, 0.34f, 1f);
            colors.pressedColor = new Color(0.03f, 0.20f, 0.17f, 1f);
            button.colors = colors;
            label = CreateText(
                "Label", image.transform, string.Empty, 18, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, PrimaryTextColor, true);
            return button;
        }

        private static void AddOutline(Text text, Color color, float distance)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
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
