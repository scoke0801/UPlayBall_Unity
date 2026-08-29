using Baseball.Game.Sound;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// BGM 곡 선택 규칙이 "같은 곡이 연달아 나오지 않는다", "곡이 하나면 그 곡을 계속 쓴다"를
    /// 지키는지 검증한다. 곡 전환은 눈으로 확인하기 어려워 자동 검증이 필요하다.
    /// </summary>
    public sealed class BgmPlaylistDefinitionTests
    {
        [Test]
        public void GetNextIndex_곡이하나면항상같은곡을반환한다()
        {
            BgmPlaylistDefinition playlist = CreatePlaylist(1, BgmPlaylistDefinition.PlaybackMode.Shuffle);

            Assert.AreEqual(0, playlist.GetNextIndex(-1));
            Assert.AreEqual(0, playlist.GetNextIndex(0));
            Assert.IsTrue(playlist.IsSingleLoopingTrack);
        }

        [Test]
        public void GetNextIndex_셔플은직전곡을연속으로고르지않는다()
        {
            BgmPlaylistDefinition playlist = CreatePlaylist(2, BgmPlaylistDefinition.PlaybackMode.Shuffle);

            int current = playlist.GetNextIndex(-1);
            for (int step = 0; step < 50; step++)
            {
                int next = playlist.GetNextIndex(current);
                Assert.AreNotEqual(current, next, "셔플이 같은 곡을 연달아 골랐다.");
                Assert.IsTrue(next >= 0 && next < playlist.TrackCount);
                current = next;
            }
        }

        [Test]
        public void GetNextIndex_순차재생은목록끝에서처음으로돌아온다()
        {
            BgmPlaylistDefinition playlist = CreatePlaylist(3, BgmPlaylistDefinition.PlaybackMode.Sequential);

            Assert.AreEqual(1, playlist.GetNextIndex(0));
            Assert.AreEqual(2, playlist.GetNextIndex(1));
            Assert.AreEqual(0, playlist.GetNextIndex(2));
        }

        [Test]
        public void GetNextIndex_곡이없으면재생할곡이없음을알린다()
        {
            BgmPlaylistDefinition playlist = CreatePlaylist(0, BgmPlaylistDefinition.PlaybackMode.Sequential);

            Assert.AreEqual(-1, playlist.GetNextIndex(-1));
            Assert.IsNull(playlist.GetTrack(0));
        }

        /// <summary>
        /// AudioClip은 EditMode에서 생성 비용이 크므로 빈 슬롯만 채운다.
        /// 곡 선택 규칙은 클립 내용과 무관하게 인덱스만 다룬다.
        /// </summary>
        private static BgmPlaylistDefinition CreatePlaylist(
            int trackCount,
            BgmPlaylistDefinition.PlaybackMode mode)
        {
            var playlist = ScriptableObject.CreateInstance<BgmPlaylistDefinition>();
            var serialized = new UnityEditor.SerializedObject(playlist);
            serialized.FindProperty("_tracks").arraySize = trackCount;
            serialized.FindProperty("_mode").enumValueIndex = (int)mode;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return playlist;
        }
    }
}
