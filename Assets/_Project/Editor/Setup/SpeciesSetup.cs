using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Game.Data;
using UnityEditor;
using UnityEngine;

namespace Moa.Showdown.Editor.Setup
{
    /// <summary>
    /// 몬스터 표 에셋. 이미 있으면 값을 덮어쓰지 않는다. 아래 기본 줄 = BALANCE 7 · 12 (시험용 6종, 이름 · 수치 임시).
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
            FillAbilities(t);
            AssetDatabase.CreateAsset(t, SpeciesTablePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] {SpeciesTablePath} 생성");
        }

        /// <summary>속성 · 능력이 비어 있는 줄에만 기본값을 채운다 (3-1). 몇 번 실행해도 같고, 직접 고친 줄은 그대로.</summary>
        [MenuItem("MoaShowdown/Setup/Species Abilities")]
        public static void FillSpeciesAbilities()
        {
            var t = AssetDatabase.LoadAssetAtPath<SpeciesTable>(SpeciesTablePath);
            if (t == null)
            {
                CreateSpeciesTable();
                return;
            }
            int changed = FillAbilities(t);
            EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] 몬스터 능력 채움: {changed}줄");
        }

        // BALANCE 12: (ID, 속성, 언제, 무엇, 수치, ×레벨, N)
        private static int FillAbilities(SpeciesTable t)
        {
            int n = 0;
            n += Ability(t, "sprout", ElementType.Grass, AbilityTrigger.ShopRoundStart, AbilityEffect.PermAttackSelf, 1, false, 0);
            n += Ability(t, "ember", ElementType.Fire, AbilityTrigger.OnAttack, AbilityEffect.Burn, 1, false, 0);
            n += Ability(t, "pebble", ElementType.Rock, AbilityTrigger.BattleStart, AbilityEffect.Shield, 20, true, 0);
            n += Ability(t, "zap", ElementType.Electric, AbilityTrigger.EveryNthAttack, AbilityEffect.DamageMultiplier, 2, false, 3);
            n += Ability(t, "drip", ElementType.Water, AbilityTrigger.OnAttack, AbilityEffect.Heal, 3, true, 0);
            n += Ability(t, "gust", ElementType.Flying, AbilityTrigger.OnBuy, AbilityEffect.PermAttackWeakestAlly, 2, false, 0);
            return n;
        }

        private static int Ability(SpeciesTable t, string id, ElementType type, AbilityTrigger trigger, AbilityEffect effect, int amount, bool perLevel, int everyN)
        {
            SpeciesRow row = t.rows.Find(r => r.id == id);
            if (row == null || row.ability.trigger != AbilityTrigger.None) return 0;
            row.type = type;
            row.ability = new AbilityRow { trigger = trigger, effect = effect, amount = amount, perLevel = perLevel, everyN = everyN };
            return 1;
        }

        private static void Row(SpeciesTable t, string id, string name, int attack, int interval)
        {
            t.rows.Add(new SpeciesRow { id = id, displayName = name, attack = attack, attackIntervalTicks = interval });
        }
    }
}
