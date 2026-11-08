using System.Linq;
using Moa.Showdown.Game.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Moa.Showdown.Tests
{
    /// <summary>G-1 몬스터 3D 모델 표: 몬스터마다 한 줄 · 연결된 프리팹은 보이는 메시가 있음 · 모델이 없으면 캡슐(null).</summary>
    public class MonsterVisualTests
    {
        private static MonsterVisualTable Table() => AssetDatabase.LoadAssetAtPath<MonsterVisualTable>("Assets/_Project/Data/MonsterVisuals.asset");

        [Test]
        public void Table_HasRowForEverySpecies()
        {
            MonsterVisualTable table = Table();
            Assert.That(table, Is.Not.Null, "Setup/Monster Visuals를 한 번 돌려야 함");
            var species = AssetDatabase.LoadAssetAtPath<SpeciesTable>("Assets/_Project/Data/SpeciesTable.asset");
            foreach (SpeciesRow s in species.rows)
                Assert.That(table.rows.Count(r => r.speciesId == s.id), Is.EqualTo(1), $"{s.id} 줄이 정확히 하나");
            Assert.That(table.targetHeight, Is.GreaterThan(0f));
        }

        [Test]
        public void LinkedPrefabs_HaveRenderers_AndSaneCorrections()
        {
            foreach (MonsterVisualRow r in Table().rows.Where(r => r.prefab != null))
            {
                Assert.That(r.prefab.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(0), $"{r.speciesId} 프리팹에 보이는 메시");
                Assert.That(r.prefab.GetComponentsInChildren<Collider>().Length, Is.EqualTo(0), $"{r.speciesId} 충돌체 없음");
                Assert.That(r.scale, Is.GreaterThan(0f), $"{r.speciesId} 크기 보정 > 0");
            }
        }

        [Test]
        public void Find_ReturnsNullWithoutPrefab()
        {
            var t = ScriptableObject.CreateInstance<MonsterVisualTable>();
            t.rows.Add(new MonsterVisualRow { speciesId = "a" });
            Assert.That(t.Find("a"), Is.Null, "프리팹이 없으면 캡슐");
            Assert.That(t.Find("없음"), Is.Null);
            Assert.That(t.Find(null), Is.Null);
            Object.DestroyImmediate(t);
        }
    }
}
