using UnityEngine;

namespace Baseball.Presentation.Career
{
    /// <summary>타이틀과 신규 선수 생성 화면이 공유하는 이미지 정의다.</summary>
    public sealed class CareerCreationPresentationData : ScriptableObject
    {
        private const string ResourcePath = "NewGame/CareerCreationPresentationData";

        [SerializeField] private Sprite _titleImage;

        public Sprite TitleImage => _titleImage;

        public static CareerCreationPresentationData Load()
        {
            return Resources.Load<CareerCreationPresentationData>(ResourcePath);
        }
    }
}
