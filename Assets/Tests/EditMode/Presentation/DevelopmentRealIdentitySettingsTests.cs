using System;
using System.Reflection;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.Career;
using Baseball.Game.Manager;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>개발용 실제 Identity 설정과 공식 구단 엠블렘 로딩을 검증한다.</summary>
    public sealed class DevelopmentRealIdentitySettingsTests
    {
        private bool _wasEnabled;

        [SetUp]
        public void SetUp()
        {
            DevelopmentRealIdentitySettings.Initialize();
            _wasEnabled = DevelopmentRealIdentitySettings.IsEnabled;
            DevelopmentRealIdentitySettings.SetEnabled(true);
        }

        [TearDown]
        public void TearDown()
        {
            DevelopmentRealIdentitySettings.SetEnabled(_wasEnabled);
            if (GameManager.HasInstance)
                UnityEngine.Object.DestroyImmediate(GameManager.Instance.gameObject);
        }

        [TestCase("KIA 타이거즈", "KiaTigers")]
        [TestCase("KT 위즈", "KtWiz")]
        [TestCase("LG 트윈스", "LgTwins")]
        [TestCase("NC 다이노스", "NcDinos")]
        [TestCase("SSG 랜더스", "SsgLanders")]
        [TestCase("두산 베어스", "DoosanBears")]
        [TestCase("롯데 자이언츠", "LotteGiants")]
        [TestCase("삼성 라이온즈", "SamsungLions")]
        [TestCase("키움 히어로즈", "KiwoomHeroes")]
        [TestCase("한화 이글스", "HanwhaEagles")]
        public void Editor_CatalogLoadsEveryCurrentKboTeamEmblem(string teamName, string fileName)
        {
            Assert.That(DevelopmentRealIdentitySettings.IsAvailable, Is.True);
            Assert.That(DevelopmentRealIdentitySettings.TryGetEmblemResource(
                teamName, out string resourcePath), Is.True);
            Assert.That(resourcePath, Is.EqualTo($"DevelopmentKboIdentities/Emblems/{fileName}"));
            Assert.That(Resources.Load<Sprite>(resourcePath), Is.Not.Null);
        }

        [TestCase("2003 SK 와이번스", "SkWyverns")]
        [TestCase("1991년 빙그레 이글스", "BinggraeEagles")]
        [TestCase("1989 MBC 청룡", "MbcChungyong")]
        [TestCase("1986 청보 핀토스", "ChungboPintos")]
        [TestCase("2009 히어로즈", "HeroesWordmark")]
        [TestCase("2009년 히어로즈", "HeroesWordmark")]
        [TestCase("히어로즈", "HeroesWordmark")]
        [TestCase("키움", "KiwoomHeroes")]
        public void Editor_CatalogLoadsHistoricalEmblemAfterSeasonYearPrefix(
            string teamName,
            string fileName)
        {
            Assert.That(DevelopmentRealIdentitySettings.TryGetEmblemResource(
                teamName, out string resourcePath), Is.True);
            Assert.That(resourcePath, Is.EqualTo($"DevelopmentKboIdentities/Emblems/{fileName}"));
            Assert.That(Resources.Load<Sprite>(resourcePath), Is.Not.Null);
        }

        [TestCase(
            "FRANCHISE_1b36b987034cef53c24a_2003",
            "FRANCHISE_1b36b987034cef53c24a",
            "SK 와이번스")]
        [TestCase(
            "FRANCHISE_1b36b987034cef53c24a_2021",
            "FRANCHISE_1b36b987034cef53c24a",
            "SSG 랜더스")]
        [TestCase(
            "FRANCHISE_8d4c4aa7cff1444ab4f5_2003",
            "FRANCHISE_8d4c4aa7cff1444ab4f5",
            "현대 유니콘스")]
        [TestCase(
            "FRANCHISE_c66296716a6d841d9cec_1991",
            "FRANCHISE_c66296716a6d841d9cec",
            "쌍방울 레이더스")]
        public void TeamSeason_UsesHistoricalBrandInsteadOfCurrentOrVirtualFranchiseName(
            string teamSeasonKey,
            string franchiseId,
            string expected)
        {
            var registry = new WorldIdentityRegistry(
                "test-v1",
                9UL,
                new WorldPlayerIdentity[0],
                new[] { new WorldFranchiseIdentity(franchiseId, "수원 파이어오니어스") });

            Assert.That(DevelopmentRealIdentitySettings.ResolveTeamSeasonName(
                registry, teamSeasonKey, franchiseId), Is.EqualTo(expected));
            Assert.That(registry.GetFranchiseDisplayName(franchiseId),
                Is.EqualTo("수원 파이어오니어스"));
        }

        [Test]
        public void DisplayTab_TogglesRealIdentitySetting()
        {
            UI_Popup_CareerSettings popup = UI_Popup_CareerSettings.ShowRuntime();
            popup.transform.Find("Content/SettingsPanel/Tab_2").GetComponent<Button>().onClick.Invoke();
            Transform body = popup.transform.Find("Content/SettingsPanel/Body");
            Button toggle = body.Find("RealIdentityToggle").GetComponent<Button>();

            Assert.That(toggle, Is.Not.Null);
            Assert.That(toggle.GetComponentInChildren<Text>().text, Does.StartWith("ON"));
            toggle.onClick.Invoke();

            Assert.That(DevelopmentRealIdentitySettings.IsEnabled, Is.False);
        }

        [Test]
        public void TeamEmblemSprites_UsesOfficialEmblemWhenRealIdentityIsEnabled()
        {
            Type spriteType = Assembly.Load("Baseball.Presentation")
                .GetType("Baseball.Presentation.UI.TeamEmblemSprites", true);
            MethodInfo apply = spriteType.GetMethod(
                "TryApply",
                new[] { typeof(Image), typeof(int), typeof(string) });
            var root = new GameObject("Emblem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                Image image = root.GetComponent<Image>();
                Assert.That(apply.Invoke(null, new object[] { image, 1, "KIA 타이거즈" }), Is.True);
                Assert.That(image.sprite, Is.Not.Null);
                Assert.That(image.sprite.name, Is.EqualTo("KiaTigers"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TeamEmblemSprites_UsesHistoricalEmblemEvenWhenVirtualSlotIsMissing()
        {
            Type spriteType = Assembly.Load("Baseball.Presentation")
                .GetType("Baseball.Presentation.UI.TeamEmblemSprites", true);
            MethodInfo canResolve = spriteType.GetMethod("CanResolve");
            MethodInfo apply = spriteType.GetMethod(
                "TryApply",
                new[] { typeof(Image), typeof(int), typeof(string) });
            var root = new GameObject("Emblem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                Image image = root.GetComponent<Image>();
                Assert.That(canResolve.Invoke(null, new object[] { "2003 SK 와이번스", 0 }), Is.True);
                Assert.That(apply.Invoke(null, new object[] { image, 0, "2003 SK 와이번스" }), Is.True);
                Assert.That(image.sprite, Is.Not.Null);
                Assert.That(image.sprite.name, Is.EqualTo("SkWyverns"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SourceBackedPlayer_UsesRealNameWithoutChangingStoredIdentity()
        {
            var registry = new WorldIdentityRegistry(
                "test-v1",
                9UL,
                new[] { new WorldPlayerIdentity("PERSON_08a17db05a3902825cd1", "박도현") },
                new WorldFranchiseIdentity[0]);

            Assert.That(DevelopmentRealIdentitySettings.ResolvePlayerName(
                registry, "PERSON_08a17db05a3902825cd1"), Is.EqualTo("류현진"));
            Assert.That(registry.GetPlayerDisplayName("PERSON_08a17db05a3902825cd1"), Is.EqualTo("박도현"));
            Assert.That(registry.PlayerIdentities[0].DisplayName, Is.EqualTo("박도현"));

            DevelopmentRealIdentitySettings.SetEnabled(false);

            Assert.That(DevelopmentRealIdentitySettings.ResolvePlayerName(
                registry, "PERSON_08a17db05a3902825cd1"), Is.EqualTo("박도현"));
        }
    }
}
