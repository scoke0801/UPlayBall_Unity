using Baseball.Game.Career;
using Baseball.Game.Career.News;
using Baseball.Game.Manager;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>고정 8탭을 늘리지 않고 홈 뉴스 카드에서 여는 전체 뉴스 피드다.</summary>
    public sealed class UI_Popup_CareerNews : UIPopupBase
    {
        private const int ArticlesPerPage = 6;

        private static readonly Color OverlayColor = new(0.002f, 0.009f, 0.016f, 0.94f);
        private static readonly Color BackgroundColor = new(0.007f, 0.025f, 0.043f, 1f);
        private static readonly Color PanelColor = new(0.014f, 0.052f, 0.087f, 1f);
        private static readonly Color CardColor = new(0.02f, 0.075f, 0.12f, 1f);
        private static readonly Color SelectedColor = new(0.025f, 0.20f, 0.36f, 1f);
        private static readonly Color BorderColor = new(0.13f, 0.34f, 0.51f, 1f);
        private static readonly Color AccentColor = new(0.12f, 0.60f, 1f, 1f);
        private static readonly Color GoldColor = new(0.95f, 0.70f, 0.20f, 1f);
        private static readonly Color PrimaryTextColor = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.64f, 0.73f, 0.82f, 1f);
        private static readonly Color MutedTextColor = new(0.36f, 0.45f, 0.54f, 1f);

        private static readonly NewsFeedCategory[] Categories =
        {
            NewsFeedCategory.Latest,
            NewsFeedCategory.MyCareer,
            NewsFeedCategory.Club,
            NewsFeedCategory.League,
            NewsFeedCategory.TransferContract,
            NewsFeedCategory.RecordsAwards,
            NewsFeedCategory.CareerTimeline
        };

        private static readonly string[] CategoryLabels =
            { "최신", "내 커리어", "구단", "리그", "이적·계약", "기록·수상", "커리어 연표" };

        private CareerManager _manager;
        private RectTransform _content;
        private NewsFeedCategory _category = NewsFeedCategory.Latest;
        private string _selectedArticleId = string.Empty;
        private int _pageIndex;

        /// <summary>프리팹이 없는 프로토타입에서 전체 뉴스 팝업을 런타임 생성한다.</summary>
        public static UI_Popup_CareerNews CreateRuntime(Transform parent)
        {
            var gameObject = new GameObject(
                nameof(UI_Popup_CareerNews),
                typeof(RectTransform),
                typeof(CanvasGroup));
            gameObject.transform.SetParent(parent, false);
            UI_Popup_CareerNews popup = gameObject.AddComponent<UI_Popup_CareerNews>();
            Stretch(gameObject.GetComponent<RectTransform>());
            return popup;
        }

        protected override void OnInitialize()
        {
            _manager = GameManager.EnsureExists().EnsureManager<CareerManager>("CareerManager");
            _manager.CareerChanged += HandleCareerChanged;
            RectTransform root = (RectTransform)transform;
            Stretch(root);
            CreateImage("Overlay", root, OverlayColor, Vector2.zero, Vector2.zero, stretch: true);
            _content = CreateImage(
                "Content",
                root,
                BackgroundColor,
                new Vector2(1720f, 900f),
                Vector2.zero);
        }

        protected override void OnShow()
        {
            EnsureSelection(markRead: true);
            Render();
        }

        protected override void OnDestroy()
        {
            if (_manager != null)
                _manager.CareerChanged -= HandleCareerChanged;
            base.OnDestroy();
        }

        private void HandleCareerChanged()
        {
            if (_manager == null || !_manager.HasActiveCareer)
            {
                Hide();
                return;
            }
            if (IsVisible)
                Render();
        }

        private void EnsureSelection(bool markRead)
        {
            CareerNewsFeedView feed = _manager.BuildNewsFeed(_category, 100);
            if (!Contains(feed, _selectedArticleId))
                _selectedArticleId = feed.Articles.Length > 0 ? feed.Articles[0].ArticleId : string.Empty;
            if (!markRead || string.IsNullOrEmpty(_selectedArticleId))
                return;
            NewsArticleView? selected = Find(feed, _selectedArticleId);
            if (selected.HasValue && !selected.Value.IsRead)
                _manager.MarkNewsArticleRead(_selectedArticleId);
        }

        private void Render()
        {
            if (_content == null || _manager == null || !_manager.HasActiveCareer)
                return;
            ClearChildren(_content);
            CareerNewsFeedView feed = _manager.BuildNewsFeed(_category, 100);
            RenderHeader(feed);
            RenderCategories(feed);
            RenderFeed(feed);
            RenderDetail(feed);
        }

        private void RenderHeader(CareerNewsFeedView feed)
        {
            CreateImage("HeaderLine", _content, BorderColor, new Vector2(1680f, 2f), new Vector2(0f, 382f));
            CreateText(
                "Title",
                _content,
                "커리어 뉴스",
                32,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(500f, 54f),
                new Vector2(-570f, 414f),
                PrimaryTextColor);
            CreateText(
                "Unread",
                _content,
                feed.UnreadCount > 0 ? $"읽지 않음 {feed.UnreadCount}" : "모두 읽음",
                14,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                new Vector2(220f, 38f),
                new Vector2(580f, 414f),
                feed.UnreadCount > 0 ? AccentColor : MutedTextColor);
            Button close = CreateButton(
                "Close",
                _content,
                "닫기  ESC",
                new Vector2(130f, 42f),
                new Vector2(770f, 414f),
                PanelColor,
                out Text closeLabel);
            closeLabel.fontSize = 14;
            close.onClick.AddListener(Close);
        }

        private void RenderCategories(CareerNewsFeedView feed)
        {
            RectTransform panel = CreateImage(
                "Categories",
                _content,
                PanelColor,
                new Vector2(240f, 724f),
                new Vector2(-720f, -3f));
            CreateText(
                "Label",
                panel,
                "카테고리",
                15,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(190f, 32f),
                new Vector2(0f, 326f),
                SecondaryTextColor);
            for (int index = 0; index < Categories.Length; index++)
            {
                NewsFeedCategory category = Categories[index];
                bool isSelected = category == _category;
                Button button = CreateButton(
                    "Category_" + category,
                    panel,
                    CategoryLabels[index],
                    new Vector2(204f, 58f),
                    new Vector2(0f, 270f - index * 70f),
                    isSelected ? SelectedColor : BackgroundColor,
                    out Text label);
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(20f, 0f);
                label.fontSize = 16;
                label.color = isSelected ? PrimaryTextColor : SecondaryTextColor;
                button.onClick.AddListener(() =>
                {
                    _category = category;
                    _pageIndex = 0;
                    _selectedArticleId = string.Empty;
                    EnsureSelection(markRead: true);
                    Render();
                });
            }
        }

        private void RenderFeed(CareerNewsFeedView feed)
        {
            RectTransform panel = CreateImage(
                "Feed",
                _content,
                PanelColor,
                new Vector2(650f, 724f),
                new Vector2(-255f, -3f));
            if (feed.Articles.Length == 0)
            {
                CreateText(
                    "Empty",
                    panel,
                    "이 카테고리에 발행된 기사가 없습니다.",
                    17,
                    FontStyle.Normal,
                    TextAnchor.MiddleCenter,
                    new Vector2(560f, 80f),
                    Vector2.zero,
                    SecondaryTextColor);
                return;
            }

            int pageCount = Mathf.CeilToInt(feed.Articles.Length / (float)ArticlesPerPage);
            _pageIndex = Mathf.Clamp(_pageIndex, 0, Mathf.Max(0, pageCount - 1));
            int startIndex = _pageIndex * ArticlesPerPage;
            int visibleCount = Mathf.Min(ArticlesPerPage, feed.Articles.Length - startIndex);
            for (int index = 0; index < visibleCount; index++)
            {
                NewsArticleView article = feed.Articles[startIndex + index];
                bool isSelected = article.ArticleId == _selectedArticleId;
                RectTransform card = CreateImage(
                    "Article_" + index,
                    panel,
                    isSelected ? SelectedColor : CardColor,
                    new Vector2(606f, 88f),
                    new Vector2(0f, 294f - index * 96f));
                if (!article.IsRead)
                    CreateImage("Unread", card, AccentColor, new Vector2(4f, 74f), new Vector2(-300f, 0f));
                CreateText(
                    "Meta",
                    card,
                    $"{GetCategoryLabel(article.Category)}  ·  {article.PublishedAt:M월 d일}",
                    11,
                    FontStyle.Bold,
                    TextAnchor.MiddleLeft,
                    new Vector2(520f, 20f),
                    new Vector2(10f, 25f),
                    article.Importance is NewsImportance.S or NewsImportance.A ? GoldColor : AccentColor);
                CreateText(
                    "Headline",
                    card,
                    article.Headline,
                    16,
                    article.IsRead ? FontStyle.Normal : FontStyle.Bold,
                    TextAnchor.MiddleLeft,
                    new Vector2(540f, 46f),
                    new Vector2(10f, -10f),
                    PrimaryTextColor);
                Button button = card.gameObject.AddComponent<Button>();
                card.GetComponent<Image>().raycastTarget = true;
                button.onClick.AddListener(() =>
                {
                    _selectedArticleId = article.ArticleId;
                    if (!article.IsRead)
                        _manager.MarkNewsArticleRead(article.ArticleId);
                    Render();
                });
            }

            if (pageCount > 1)
                RenderPageControls(panel, feed, pageCount);
        }

        private void RenderPageControls(
            RectTransform panel,
            CareerNewsFeedView feed,
            int pageCount)
        {
            Button previous = CreateButton(
                "PreviousPage",
                panel,
                "이전",
                new Vector2(90f, 34f),
                new Vector2(-128f, -331f),
                BackgroundColor,
                out Text previousLabel);
            previousLabel.fontSize = 12;
            previous.interactable = _pageIndex > 0;
            previous.onClick.AddListener(() => SelectPage(feed, _pageIndex - 1));

            CreateText(
                "Page",
                panel,
                $"{_pageIndex + 1} / {pageCount}",
                12,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(100f, 34f),
                new Vector2(0f, -331f),
                SecondaryTextColor);

            Button next = CreateButton(
                "NextPage",
                panel,
                "다음",
                new Vector2(90f, 34f),
                new Vector2(128f, -331f),
                BackgroundColor,
                out Text nextLabel);
            nextLabel.fontSize = 12;
            next.interactable = _pageIndex < pageCount - 1;
            next.onClick.AddListener(() => SelectPage(feed, _pageIndex + 1));
        }

        private void SelectPage(CareerNewsFeedView feed, int pageIndex)
        {
            _pageIndex = pageIndex;
            int firstArticleIndex = _pageIndex * ArticlesPerPage;
            if (firstArticleIndex < feed.Articles.Length)
            {
                NewsArticleView article = feed.Articles[firstArticleIndex];
                _selectedArticleId = article.ArticleId;
                if (!article.IsRead)
                    _manager.MarkNewsArticleRead(article.ArticleId);
            }
            Render();
        }

        private void RenderDetail(CareerNewsFeedView feed)
        {
            RectTransform panel = CreateImage(
                "Detail",
                _content,
                PanelColor,
                new Vector2(710f, 724f),
                new Vector2(440f, -3f));
            NewsArticleView? selected = Find(feed, _selectedArticleId);
            if (!selected.HasValue)
            {
                CreateText(
                    "Empty",
                    panel,
                    "왼쪽에서 기사를 선택하세요.",
                    17,
                    FontStyle.Normal,
                    TextAnchor.MiddleCenter,
                    new Vector2(560f, 80f),
                    Vector2.zero,
                    SecondaryTextColor);
                return;
            }

            NewsArticleView article = selected.Value;
            Sprite illustration = CareerPresentationAssetLibrary.GetIllustration(article.Illustration);
            bool hasIllustration = illustration != null;
            string publicationState = article.Category == NewsCategory.League
                ? "  ·  오늘 경기 종료 · 순위 반영 완료"
                : string.Empty;
            CreateText(
                "Source",
                panel,
                $"{GetSourceLabel(article.SourceType)}  ·  {article.PublishedAt:yyyy년 M월 d일}{publicationState}",
                13,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(620f, 26f),
                new Vector2(0f, 320f),
                AccentColor);
            CreateText(
                "Headline",
                panel,
                article.Headline,
                27,
                FontStyle.Bold,
                TextAnchor.UpperLeft,
                hasIllustration ? new Vector2(410f, 110f) : new Vector2(620f, 110f),
                hasIllustration ? new Vector2(-105f, 246f) : new Vector2(0f, 246f),
                PrimaryTextColor);
            CreateImage("Divider", panel, BorderColor, new Vector2(620f, 1f), new Vector2(0f, 174f));
            if (hasIllustration)
            {
                RectTransform frame = CreateImage(
                    "CareerIllustrationFrame",
                    panel,
                    GoldColor,
                    new Vector2(184f, 246f),
                    new Vector2(215f, 36f));
                RectTransform imageRoot = CreateImage(
                    "CareerIllustration",
                    frame,
                    Color.white,
                    new Vector2(176f, 238f),
                    Vector2.zero);
                Image image = imageRoot.GetComponent<Image>();
                image.sprite = illustration;
                image.preserveAspect = true;
            }
            CreateText(
                "Lead",
                panel,
                article.Lead,
                18,
                FontStyle.Bold,
                TextAnchor.UpperLeft,
                hasIllustration ? new Vector2(400f, 92f) : new Vector2(620f, 92f),
                hasIllustration ? new Vector2(-110f, 112f) : new Vector2(0f, 112f),
                SecondaryTextColor);
            CreateText(
                "Body",
                panel,
                article.Body,
                17,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                hasIllustration ? new Vector2(400f, 300f) : new Vector2(620f, 300f),
                hasIllustration ? new Vector2(-110f, -105f) : new Vector2(0f, -105f),
                PrimaryTextColor);
            if (article.IsCareerArchive)
            {
                CreateText(
                    "Archive",
                    panel,
                    "● 커리어 연표에 보관됨",
                    13,
                    FontStyle.Bold,
                    TextAnchor.MiddleLeft,
                    new Vector2(300f, 30f),
                    new Vector2(-160f, -322f),
                    GoldColor);
            }
        }

        private static NewsArticleView? Find(CareerNewsFeedView feed, string articleId)
        {
            for (int index = 0; index < feed.Articles.Length; index++)
            {
                if (feed.Articles[index].ArticleId == articleId)
                    return feed.Articles[index];
            }
            return null;
        }

        private static bool Contains(CareerNewsFeedView feed, string articleId)
        {
            return Find(feed, articleId).HasValue;
        }

        private static string GetCategoryLabel(NewsCategory category)
        {
            return category switch
            {
                NewsCategory.Game => "경기",
                NewsCategory.MyPlayer => "내 선수",
                NewsCategory.Club => "구단",
                NewsCategory.League => "리그",
                NewsCategory.Injury => "부상",
                NewsCategory.TransferContract => "이적·계약",
                NewsCategory.Postseason => "포스트시즌",
                NewsCategory.RecordsAwards => "기록·수상",
                _ => "오프시즌"
            };
        }

        private static string GetSourceLabel(NewsSourceType source)
        {
            return source switch
            {
                NewsSourceType.LeagueOfficial => "리그 공식",
                NewsSourceType.LeagueSportsMedia => "리그 스포츠 미디어",
                NewsSourceType.RegionalSports => "지역 스포츠",
                NewsSourceType.NationalSports => "전국 스포츠",
                _ => "구단 소식"
            };
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

        private static RectTransform CreateImage(
            string name,
            Transform parent,
            Color color,
            Vector2 size,
            Vector2 position,
            bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch)
                Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Vector2 size,
            Vector2 position,
            Color color)
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
            string name,
            Transform parent,
            string label,
            Vector2 size,
            Vector2 position,
            Color color,
            out Text text)
        {
            RectTransform rect = CreateImage(name, parent, color, size, position);
            Image image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            button.colors = colors;
            text = CreateText(
                "Label",
                rect,
                label,
                16,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.zero,
                PrimaryTextColor);
            Stretch(text.rectTransform);
            return button;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(parent.GetChild(index).gameObject);
                else
#endif
                    Destroy(parent.GetChild(index).gameObject);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
