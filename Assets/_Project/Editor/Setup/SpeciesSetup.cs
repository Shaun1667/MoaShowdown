using Moa.Showdown.Game.Data;
using UnityEditor;
using UnityEngine;

namespace Moa.Showdown.Editor.Setup
{
    /// <summary>
    /// 몬스터 표 에셋. 이미 있으면 값을 덮어쓰지 않는다. 아래 기본 줄 = BALANCE 7 (시험용 6종, 이름 · 수치 임시).
    /// ID는 스냅샷에 저장되므로 바꾸지 않는다.
    /// </summary>
    public static class SpeciesSetup
    {
        public const string SpeciesTablePath = "Assets/_Project/Data/SpeciesTable.asset";

        [MenuItem("MoaShowdown/Setup/Species Table")]
        public static void CreateSpeciesTable()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SpeciesTable>(SpeciesTablePath);
            if (existing != null)
            {
                Debug.Log($"[Setup] {SpeciesTablePath} 이미 있음 — 값은 그대로 둠");
                return;
            }

            var t = ScriptableObject.CreateInstance<SpeciesTable>();
            // (ID, 이름, 공격력, 공격 간격 틱) — BALANCE 7
            Row(t, "sprout", "새싹이", 8, 8);
            Row(t, "ember", "불씨", 14, 12);
            Row(t, "pebble", "자갈이", 20, 18);
            Row(t, "zap", "찌릿", 5, 4);
            Row(t, "drip", "물방울", 10, 10);
            Row(t, "gust", "바람개비", 7, 6);
            AssetDatabase.CreateAsset(t, SpeciesTablePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] {SpeciesTablePath} 생성");
        }

        private static void Row(SpeciesTable t, string id, string name, int attack, int interval)
        {
            t.rows.Add(new SpeciesRow { id = id, displayName = name, attack = attack, attackIntervalTicks = interval });
        }
    }
}
