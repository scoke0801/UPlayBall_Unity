using System;
using Baseball.Presentation.SharedScreens;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>
    /// 읽기 전용 선수단 View의 단일 로스터 크기 계약을 검증한다.
    /// </summary>
    public sealed class ReadOnlyRosterListViewTests
    {
        private GameObject _root;
        private ReadOnlyRosterListView _view;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ReadOnlyRosterListViewTests_Root", typeof(RectTransform));
            _view = ReadOnlyRosterListView.CreateRuntime(_root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void Bind_최대선수수를넘으면거부한다()
        {
            ReadOnlyRosterModel roster = CreateRoster(ReadOnlyRosterListView.MaxPlayerRows + 1);

            Assert.Throws<ArgumentException>(() => _view.Bind(roster));
        }

        private static ReadOnlyRosterModel CreateRoster(int playerCount)
        {
            var players = new ReadOnlyRosterPlayerModel[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                players[i] = new ReadOnlyRosterPlayerModel(
                    $"player-{i}", $"선수 {i}", "SS", "백업", "70", "100", ".250");
            }
            return new ReadOnlyRosterModel(
                "team-1", "서울", "2028", $"{playerCount}명",
                new[] { new ReadOnlyRosterGroupModel("Backup", "백업", players) });
        }
    }
}
