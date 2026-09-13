// 콘솔에서는 리소스를 파일로 읽고 성적 입력만 제공한다. 실제 선택 구현은 Assets 소스를 컴파일한다.
namespace UnityEngine
{
    public sealed class TextAsset { public string text; }
    public static class Resources { public static T Load<T>(string path) => throw new System.NotSupportedException(); }
    public static class JsonUtility { public static T FromJson<T>(string text) => throw new System.NotSupportedException(); }
}
namespace Baseball.Presentation.Owner
{
    public sealed class OwnerClubInformationPresentationModel
    {
        public int Wins, Losses, Ties;
        public int Games => Wins + Losses + Ties;
    }
}
