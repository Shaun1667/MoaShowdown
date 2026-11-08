using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Game.Data;
using UnityEditor;
using UnityEngine;

namespace Moa.Showdown.Editor.Setup
{
    /// <summary>
    /// 몬스터 표 에셋. 아래 기본 줄 = BALANCE 7 · 12 (이름 · 수치 임시).
    /// 없으면 만들고, 있으면 **없는 ID의 줄만 끝에 추가**한다 (있는 줄의 값은 그대로 — 손으로 고친 값 보존).
    /// ID는 스냅샷에 저장되므로 바꾸지 않는다. 표 순서는 상점 · 풀 순서이므로 새 줄은 끝에만 붙인다.
    /// </summary>
    public static class SpeciesSetup
    {
        public const string SpeciesTablePath = "Assets/_Project/Data/SpeciesTable.asset";

        private readonly struct Def
        {
            public readonly string Id, Name;
            public readonly int Attack, Interval;
            public readonly Rarity Rarity;
            public readonly ElementType Type;
            public readonly AbilityTrigger Trigger;
            public readonly AbilityEffect Effect;
            public readonly int Amount, EveryN;
            public readonly bool PerLevel;

            public Def(string id, string name, int attack, int interval, Rarity rarity, ElementType type,
                AbilityTrigger trigger, AbilityEffect effect, int amount, bool perLevel = false, int everyN = 0)
            {
                Id = id; Name = name; Attack = attack; Interval = interval; Rarity = rarity; Type = type;
                Trigger = trigger; Effect = effect; Amount = amount; PerLevel = perLevel; EveryN = everyN;
            }
        }

        // (ID, 이름, 공격력, 간격 틱, 등급, 속성, 언제, 무엇, 수치, ×레벨, N) — BALANCE 7 · 12
        private static readonly Def[] Defaults =
        {
            // 일반 — 시험용 6종 (1-4 · 3-1)
            new Def("sprout", "새싹이", 8, 8, Rarity.Common, ElementType.Grass, AbilityTrigger.ShopRoundStart, AbilityEffect.PermAttackSelf, 1),
            new Def("ember", "불씨", 14, 12, Rarity.Common, ElementType.Fire, AbilityTrigger.OnAttack, AbilityEffect.Burn, 1),
            new Def("pebble", "자갈이", 20, 18, Rarity.Common, ElementType.Rock, AbilityTrigger.BattleStart, AbilityEffect.Shield, 20, perLevel: true),
            new Def("zap", "찌릿", 5, 4, Rarity.Common, ElementType.Electric, AbilityTrigger.EveryNthAttack, AbilityEffect.DamageMultiplier, 2, everyN: 3),
            new Def("drip", "물방울", 10, 10, Rarity.Common, ElementType.Water, AbilityTrigger.OnAttack, AbilityEffect.Heal, 3, perLevel: true),
            new Def("gust", "바람개비", 7, 6, Rarity.Common, ElementType.Flying, AbilityTrigger.OnBuy, AbilityEffect.PermAttackWeakestAlly, 2),
            // 3-2 (사용자 "이대로 진행", 2026-11-08)
            new Def("spore", "포자송이", 9, 10, Rarity.Common, ElementType.Toxic, AbilityTrigger.OnAttack, AbilityEffect.Poison, 1),
            new Def("chirp", "짹짹이", 6, 5, Rarity.Common, ElementType.Flying, AbilityTrigger.ShopRoundStart, AbilityEffect.GainGold, 1),
            new Def("thornet", "가시벌", 12, 6, Rarity.Uncommon, ElementType.Bug, AbilityTrigger.EveryNthAttack, AbilityEffect.Poison, 2, everyN: 3),
            new Def("cinder", "잿불여우", 18, 9, Rarity.Uncommon, ElementType.Fire, AbilityTrigger.OnAttack, AbilityEffect.Burn, 2),
            new Def("shellby", "단단껍질", 22, 14, Rarity.Uncommon, ElementType.Steel, AbilityTrigger.BattleStart, AbilityEffect.Shield, 40, perLevel: true),
            new Def("bubble", "거품고래", 14, 10, Rarity.Uncommon, ElementType.Water, AbilityTrigger.OnAttack, AbilityEffect.Heal, 6, perLevel: true),
            new Def("buzzbolt", "윙윙전기", 8, 5, Rarity.Uncommon, ElementType.Electric, AbilityTrigger.BattleStart, AbilityEffect.AdjacentHaste, 1),
            new Def("drum", "둥둥두더지", 16, 12, Rarity.Uncommon, ElementType.Rock, AbilityTrigger.BattleStart, AbilityEffect.AdjacentAttackBonus, 3, perLevel: true),
            new Def("wisp", "도깨비불", 20, 10, Rarity.Rare, ElementType.Ghost, AbilityTrigger.OnAttack, AbilityEffect.AttackGrowth, 2),
            new Def("toxifrog", "독개굴", 18, 10, Rarity.Rare, ElementType.Toxic, AbilityTrigger.OnAttack, AbilityEffect.Poison, 2),
            new Def("grovekeeper", "숲지기", 26, 14, Rarity.Rare, ElementType.Grass, AbilityTrigger.BattleStart, AbilityEffect.AdjacentAttackBonus, 6, perLevel: true),
            new Def("stormwing", "폭풍매", 14, 4, Rarity.Epic, ElementType.Flying, AbilityTrigger.EveryNthAttack, AbilityEffect.DamageMultiplier, 3, everyN: 4),
            new Def("magmaw", "용암거북", 40, 15, Rarity.Epic, ElementType.Fire, AbilityTrigger.OnAttack, AbilityEffect.Burn, 4),
            new Def("dracon", "새벽용", 45, 12, Rarity.Legendary, ElementType.Dragon, AbilityTrigger.OnAttack, AbilityEffect.AttackGrowth, 5),
        };

        [MenuItem("MoaShowdown/Setup/Species Table")]
        public static void CreateSpeciesTable()
        {
            var t = AssetDatabase.LoadAssetAtPath<SpeciesTable>(SpeciesTablePath);
            bool created = false;
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<SpeciesTable>();
                created = true;
            }
            int added = 0;
            foreach (Def d in Defaults)
            {
                if (t.rows.Exists(r => r.id == d.Id)) continue;
                t.rows.Add(ToRow(d));
                added++;
            }
            if (created)
            {
                AssetDatabase.CreateAsset(t, SpeciesTablePath);
                Debug.Log($"[Setup] {SpeciesTablePath} 생성 ({added}줄)");
            }
            else
            {
                if (added > 0) EditorUtility.SetDirty(t);
                Debug.Log($"[Setup] {SpeciesTablePath} 이미 있음 — 없는 몬스터 {added}줄 추가, 있는 줄은 그대로");
            }
            AssetDatabase.SaveAssets();
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
            int changed = 0;
            foreach (Def d in Defaults)
            {
                SpeciesRow row = t.rows.Find(r => r.id == d.Id);
                if (row == null || row.ability.trigger != AbilityTrigger.None) continue;
                row.type = d.Type;
                row.ability = AbilityOf(d);
                changed++;
            }
            EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] 몬스터 능력 채움: {changed}줄");
        }

        private static SpeciesRow ToRow(Def d) => new SpeciesRow
        {
            id = d.Id,
            displayName = d.Name,
            attack = d.Attack,
            attackIntervalTicks = d.Interval,
            rarity = d.Rarity,
            type = d.Type,
            ability = AbilityOf(d),
        };

        private static AbilityRow AbilityOf(Def d) => new AbilityRow
        {
            trigger = d.Trigger,
            effect = d.Effect,
            amount = d.Amount,
            perLevel = d.PerLevel,
            everyN = d.EveryN,
        };
    }
}
