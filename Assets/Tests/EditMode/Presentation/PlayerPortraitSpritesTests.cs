using System;
using System.Reflection;
using System.Collections.Generic;
using Baseball.Core.Players;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>발급 초상의 Resources 등록과 동일 인물의 시즌·등급 간 얼굴 유지를 검증한다.</summary>
    public sealed class PlayerPortraitSpritesTests
    {
        [Serializable]
        private sealed class Catalog
        {
            public int portraitCount;
            public Assignment[] persons;
            public Alias[] aliases;
        }

        [Serializable]
        private sealed class Assignment { public string id; public int appearance; }

        [Serializable]
        private sealed class Alias { public string id; public string person; }

        [Serializable]
        private sealed class UniformCatalog { public Lineage[] lineages; public Season[] seasons; }

        [Serializable]
        private sealed class Lineage { public string id; public string uniform; public string[] franchises; }

        [Serializable]
        private sealed class Season { public string id; public string franchise; }

        private static Type Resolver => Type.GetType("Baseball.Presentation.UI.PlayerPortraitSprites, Baseball.Presentation", true);

        [Test]
        public void IssuedPortraits_전체인물에게모든초상을균등발급한다()
        {
            TextAsset asset = Resources.Load<TextAsset>("UI/Portraits/player_portrait_assignments");
            Assert.That(asset, Is.Not.Null);
            Catalog catalog = JsonUtility.FromJson<Catalog>(asset.text);
            var counts = new int[catalog.portraitCount];
            foreach (Assignment person in catalog.persons)
            {
                Assert.That(GetAssigned(person.id), Is.Not.Null, person.id);
                counts[person.appearance - 1]++;
            }
            Assert.That(catalog.portraitCount, Is.EqualTo(576));
            Assert.That(catalog.persons.Length, Is.EqualTo(3565));
            foreach (int count in counts) Assert.That(count, Is.InRange(6, 7));
        }

        [Test]
        public void AssignedPortrait_전체시즌과카드등급이인물의얼굴을유지한다()
        {
            Catalog catalog = JsonUtility.FromJson<Catalog>(
                Resources.Load<TextAsset>("UI/Portraits/player_portrait_assignments").text);
            UniformCatalog uniforms = JsonUtility.FromJson<UniformCatalog>(
                Resources.Load<TextAsset>("UI/Portraits/player_uniform_assignments").text);
            var franchiseUniforms = new Dictionary<string, string>();
            foreach (Lineage lineage in uniforms.lineages)
                foreach (string franchise in lineage.franchises) franchiseUniforms.Add(franchise, lineage.uniform);
            var seasonUniforms = new Dictionary<string, string>();
            foreach (Season season in uniforms.seasons) seasonUniforms.Add(season.id, franchiseUniforms[season.franchise]);
            var appearances = new Dictionary<string, int>();
            foreach (Assignment person in catalog.persons) appearances.Add(person.id, person.appearance);
            Assert.That(seasonUniforms.Count, Is.EqualTo(catalog.aliases.Length));
            foreach (Alias alias in catalog.aliases)
            {
                Sprite expected = Resources.Load<Sprite>(
                    $"UI/Portraits/Uniforms/{seasonUniforms[alias.id]}/face-{appearances[alias.person]:D4}");
                Assert.That(expected, Is.Not.Null, alias.id);
                Assert.That(GetAssigned(alias.id), Is.SameAs(expected), alias.id);
                Assert.That(GetAssigned(alias.id + ":Normal"), Is.SameAs(expected));
                Assert.That(GetAssigned(alias.id + ":Rare"), Is.SameAs(expected));
                Assert.That(GetAssigned(alias.id + ":Legend"), Is.SameAs(expected));
                Assert.That(expected.name, Is.EqualTo(GetAssigned(alias.person).name), "이적해도 얼굴 ID 유지");
            }
        }

        [Test]
        public void UniformLineages_역사구단을서로다른열벌로묶는다()
        {
            UniformCatalog catalog = JsonUtility.FromJson<UniformCatalog>(
                Resources.Load<TextAsset>("UI/Portraits/player_uniform_assignments").text);
            var uniforms = new HashSet<string>();
            var franchises = new Dictionary<string, string>();
            foreach (Lineage lineage in catalog.lineages)
            {
                Assert.That(uniforms.Add(lineage.uniform), Is.True, lineage.id);
                foreach (string franchise in lineage.franchises) franchises.Add(franchise, lineage.uniform);
            }
            Assert.That(uniforms.Count, Is.EqualTo(10));
            Assert.That(franchises.Count, Is.EqualTo(12));
            Assert.That(franchises["FRANCHISE_c66296716a6d841d9cec"],
                Is.EqualTo(franchises["FRANCHISE_1b36b987034cef53c24a"]), "쌍방울·SK·SSG");
            Assert.That(franchises["FRANCHISE_8d4c4aa7cff1444ab4f5"],
                Is.EqualTo(franchises["FRANCHISE_9c3e6defcfcca3d554fb"]), "현대 이전 역사·키움");
            Assert.That(franchises["FRANCHISE_35294c0c8039e3d5d238"], Is.EqualTo("uniform-06"), "MBC·LG");
        }

        [Test]
        public void GeneratedPlayer_포지션이변경되어도같은얼굴을유지한다()
        {
            MethodInfo method = Resolver.GetMethod("GetForPlayer", new[] { typeof(int), typeof(PlayerPosition) });
            Sprite first = (Sprite)method.Invoke(null, new object[] { 12345, PlayerPosition.Shortstop });
            Sprite second = (Sprite)method.Invoke(null, new object[] { 12345, PlayerPosition.StartingPitcher });
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.SameAs(first));
            Assert.That(GetAssigned("12345"), Is.SameAs(first));
        }

        private static Sprite GetAssigned(string id)
        {
            return (Sprite)Resolver.GetMethod("GetAssigned").Invoke(null, new object[] { id });
        }
    }
}
