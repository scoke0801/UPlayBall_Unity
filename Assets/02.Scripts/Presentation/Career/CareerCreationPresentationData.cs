using UnityEngine;

namespace Baseball.Presentation.Career
{
    /// <summary>타이틀과 신규 선수 생성 화면이 공유하는 이미지 정의다.</summary>
    public sealed class CareerCreationPresentationData : ScriptableObject
    {
        private const string ResourcePath = "NewGame/CareerCreationPresentationData";

        [SerializeField] private Sprite _titleImage;
        [SerializeField] private Sprite _titleLogo;
        [SerializeField] private Vector2 _titleLogoSize = new Vector2(440f, 294f);
        [SerializeField] private Vector2 _titleLogoInset = new Vector2(48f, 0f);
        [SerializeField] private string _gameTitle = "백년구단";
        [SerializeField] private string _gameTitleCaption = "야구 매니저";

        public Sprite TitleImage => _titleImage;
        public Sprite TitleLogo => _titleLogo;
        public Vector2 TitleLogoSize => _titleLogoSize;
        public Vector2 TitleLogoInset => _titleLogoInset;
        public string GameTitle => _gameTitle;
        public string GameTitleCaption => _gameTitleCaption;

        public static CareerCreationPresentationData Load()
        {
            return Resources.Load<CareerCreationPresentationData>(ResourcePath);
        }
    }
}
