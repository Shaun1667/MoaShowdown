using System;
using System.Collections.Generic;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Random;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Snapshot;

namespace Moa.Showdown.Core.Shop
{
    public enum ShopError
    {
        None = 0,
        NotEnoughGold = 1,
        Empty = 2,
        BenchFull = 3,
        Invalid = 4,
    }

    public enum Area
    {
        Board = 0,
        Bench = 1,
    }

    /// <summary>내 몬스터 자리: 보드(0~5, 칸 번호 = 줄 × 3 + 열) 또는 대기석(0~3).</summary>
    public readonly struct Location : IEquatable<Location>
    {
        public readonly Area Area;
        public readonly int Index;

        public Location(Area area, int index)
        {
            Area = area;
            Index = index;
        }

        public static Location Board(int index) => new Location(Area.Board, index);
        public static Location Bench(int index) => new Location(Area.Bench, index);

        public bool Equals(Location other) => Area == other.Area && Index == other.Index;
        public override bool Equals(object obj) => obj is Location l && Equals(l);
        public override int GetHashCode() => ((int)Area * 16) + Index;
        public override string ToString() => $"{Area}{Index}";
    }

    /// <summary>
    /// 상점 · 골드 · 배치 한 곳. 판 상태(<see cref="RunState"/>)의 골드 · 보드 · 대기석 · 상점만 바꾼다.
    /// 라운드 시작(<see cref="BeginRound"/>): 수입 + 상점 진열 (라운드당 한 번).
    /// 사기: 대기석 빈칸으로. 대기석이 가득 차도 이번에 사면 합체가 되는 경우는 살 수 있다.
    /// 합체: 같은 몬스터 · 같은 레벨이 MergeCountFor(레벨)만큼(Lv1 3 · Lv2 2) 모이면 보드 → 대기석 순으로 첫 자리에 하나로 (레벨 +1, 버프는 합침), 연쇄 합체.
    /// 팔기: 들인 골드(가격 × CopiesAt(레벨): Lv1 1 · Lv2 3 · Lv3 6) × SellPercent%, 반올림.
    /// 상점 난수 = SeedMix(판 시드, 상점, 라운드) ⊕ 새로고침 횟수 — 같은 판 · 같은 행동 = 같은 상점.
    /// </summary>
    public sealed class ShopController
    {
        private readonly ShopRules _rules;
        private readonly SpeciesCatalog _catalog;

        private readonly UnitStatRules _statRules;

        public ShopController(ShopRules rules, SpeciesCatalog catalog, UnitStatRules statRules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _statRules = statRules ?? throw new ArgumentNullException(nameof(statRules));
            _rules.Validate();
            _statRules.Validate();
            if (_catalog.Count == 0) throw new ArgumentException("몬스터 표가 비어 있음");
        }

        public ShopRules Rules => _rules;

        /// <summary>
        /// 라운드 시작: 이 라운드 수입을 받고 상점을 새로 진열, 보드의 "라운드 시작" 능력 발동 (보드 칸 순서).
        /// 같은 라운드에 다시 불러도 아무 일 없음.
        /// </summary>
        public void BeginRound(RunState s)
        {
            if (s.ShopRound == s.Round) return;
            s.Gold += _rules.IncomeFor(s.Round);
            s.ShopRound = s.Round;
            s.ShopRollIndex = 0;
            Roll(s);
            for (int i = 0; i < BattleTeam.SlotCount; i++)
            {
                MonsterSnapshot m = s.Team[i];
                if (m == null) continue;
                AbilityDef a = Species(m.SpeciesId).Ability;
                if (a.Trigger == AbilityTrigger.ShopRoundStart) ApplyShopAbility(s, a, m, Location.Board(i));
            }
        }

        /// <summary>
        /// 상점 단계 능력 효과. PermAttackSelf = 자기 공격력 영구 +, PermAttackWeakestAlly = 자기 말고 가장 약한(실제 공격력, 같으면 보드 → 대기석 앞 칸) 몬스터 +.
        /// self가 null이면 (대기석이 가득해 바로 합쳐진 경우) 자기 대상 효과는 keeper에게.
        /// </summary>
        private void ApplyShopAbility(RunState s, AbilityDef a, MonsterSnapshot self, Location? selfAt)
        {
            int amount = a.AmountAt(self.Level);
            switch (a.Effect)
            {
                case AbilityEffect.PermAttackSelf:
                    self.Buffs.Add(new StatBuff(StatIds.Attack, amount));
                    break;
                case AbilityEffect.PermAttackWeakestAlly:
                    MonsterSnapshot weakest = null;
                    int weakestAttack = int.MaxValue;
                    foreach (Location at in AllLocations())
                    {
                        MonsterSnapshot m = Get(s, at);
                        if (m == null || ReferenceEquals(m, self) || (selfAt.HasValue && at.Equals(selfAt.Value))) continue;
                        int attack = TeamFactory.BuildUnit(m, _catalog, _statRules).Attack;
                        if (attack < weakestAttack)
                        {
                            weakestAttack = attack;
                            weakest = m;
                        }
                    }
                    weakest?.Buffs.Add(new StatBuff(StatIds.Attack, amount));
                    break;
            }
        }

        public ShopError Reroll(RunState s)
        {
            if (s.Gold < _rules.RerollCost) return ShopError.NotEnoughGold;
            s.Gold -= _rules.RerollCost;
            s.ShopRollIndex++;
            Roll(s);
            return ShopError.None;
        }

        public int PriceOf(string speciesId) => _rules.PriceOf(Species(speciesId).Rarity);

        public ShopError Buy(RunState s, int shopIndex)
        {
            if (shopIndex < 0 || shopIndex >= s.Shop.Count) return ShopError.Invalid;
            string id = s.Shop[shopIndex];
            if (id == null) return ShopError.Empty;
            int price = PriceOf(id);
            if (s.Gold < price) return ShopError.NotEnoughGold;

            int bench = FirstEmptyBench(s);
            if (bench < 0)
            {
                // 대기석이 가득: 이번 구매로 합체가 되면 산 몬스터를 자리 없이 바로 합친다
                List<Location> copies = FindAll(s, id, 1);
                int need = _rules.MergeCountFor(1);
                if (need == 0 || copies.Count < need - 1) return ShopError.BenchFull;
                s.Gold -= price;
                s.Shop[shopIndex] = null;
                // 산 몬스터는 자리 없이 합쳐진다: "살 때" 능력은 합쳐질 자리(keeper)를 제외하고 먼저 발동, 그 버프는 keeper가 물려받음
                var bought = new MonsterSnapshot { SpeciesId = id, Level = 1 };
                AbilityDef ab = Species(id).Ability;
                if (ab.Trigger == AbilityTrigger.OnBuy) ApplyShopAbility(s, ab, bought, copies[0]);
                MonsterSnapshot keeper = Get(s, copies[0]);
                for (int i = 1; i < need - 1; i++) Absorb(s, keeper, copies[i]);
                foreach (StatBuff b in bought.Buffs) keeper.Buffs.Add(b);
                keeper.Level++;
                ResolveMerges(s);
                return ShopError.None;
            }

            s.Gold -= price;
            s.Shop[shopIndex] = null;
            var placed = new MonsterSnapshot { SpeciesId = id, Level = 1 };
            s.Bench[bench] = placed;
            AbilityDef onBuy = Species(id).Ability;
            if (onBuy.Trigger == AbilityTrigger.OnBuy) ApplyShopAbility(s, onBuy, placed, Location.Bench(bench));
            ResolveMerges(s);
            return ShopError.None;
        }

        public int SellValue(MonsterSnapshot m)
        {
            long copies = _rules.CopiesAt(m.Level);
            long invested = PriceOf(m.SpeciesId) * copies;
            long value = (invested * _rules.SellPercent + 50) / 100;
            return (int)Math.Min(value, int.MaxValue);
        }

        public ShopError Sell(RunState s, Location at)
        {
            if (!IsValid(at)) return ShopError.Invalid;
            MonsterSnapshot m = Get(s, at);
            if (m == null) return ShopError.Empty;
            s.Gold += SellValue(m);
            Set(s, at, null);
            return ShopError.None;
        }

        /// <summary>두 자리를 맞바꾼다 (빈칸으로 옮기기 포함).</summary>
        public ShopError Move(RunState s, Location from, Location to)
        {
            if (!IsValid(from) || !IsValid(to)) return ShopError.Invalid;
            MonsterSnapshot a = Get(s, from);
            if (a == null) return ShopError.Empty;
            MonsterSnapshot b = Get(s, to);
            Set(s, to, a);
            Set(s, from, b);
            return ShopError.None;
        }

        public static MonsterSnapshot Get(RunState s, Location at) =>
            at.Area == Area.Board ? s.Team[at.Index] : s.Bench[at.Index];

        // ───────── 내부 ─────────

        private void Roll(RunState s)
        {
            ulong baseSeed = SeedMix.Derive(unchecked((ulong)s.Seed), SeedMix.PurposeShop, s.Round);
            var rng = new DeterministicRandom(baseSeed ^ ((ulong)(uint)s.ShopRollIndex * 0x9E3779B97F4A7C15UL));
            IReadOnlyList<SpeciesDef> all = _catalog.All;
            s.Shop.Clear();
            for (int i = 0; i < _rules.ShopSlots; i++) s.Shop.Add(all[rng.NextInt(all.Count)].Id);
        }

        /// <summary>합칠 수 있는 묶음이 없어질 때까지 합체 (낮은 레벨부터, 보드 → 대기석 순서).</summary>
        private void ResolveMerges(RunState s)
        {
            bool merged;
            do
            {
                merged = false;
                foreach (Location at in AllLocations())
                {
                    MonsterSnapshot m = Get(s, at);
                    int need = m == null ? 0 : _rules.MergeCountFor(m.Level);
                    if (need == 0) continue;
                    List<Location> same = FindAll(s, m.SpeciesId, m.Level);
                    if (same.Count < need) continue;
                    MonsterSnapshot keeper = Get(s, same[0]);
                    for (int i = 1; i < need; i++) Absorb(s, keeper, same[i]);
                    keeper.Level++;
                    merged = true;
                    break;
                }
            } while (merged);
        }

        private static void Absorb(RunState s, MonsterSnapshot keeper, Location other)
        {
            MonsterSnapshot o = Get(s, other);
            foreach (StatBuff b in o.Buffs) keeper.Buffs.Add(new StatBuff(b.Stat, b.Value));
            Set(s, other, null);
        }

        private static List<Location> FindAll(RunState s, string speciesId, int level)
        {
            var list = new List<Location>();
            foreach (Location at in AllLocations())
            {
                MonsterSnapshot m = Get(s, at);
                if (m != null && m.SpeciesId == speciesId && m.Level == level) list.Add(at);
            }
            return list;
        }

        private static IEnumerable<Location> AllLocations()
        {
            for (int i = 0; i < BattleTeam.SlotCount; i++) yield return Location.Board(i);
            for (int i = 0; i < RunState.BenchSlotCount; i++) yield return Location.Bench(i);
        }

        private static int FirstEmptyBench(RunState s)
        {
            for (int i = 0; i < s.Bench.Length; i++)
                if (s.Bench[i] == null) return i;
            return -1;
        }

        private static bool IsValid(Location at) =>
            at.Index >= 0 && at.Index < (at.Area == Area.Board ? BattleTeam.SlotCount : RunState.BenchSlotCount);

        private static void Set(RunState s, Location at, MonsterSnapshot m)
        {
            if (at.Area == Area.Board) s.Team[at.Index] = m;
            else s.Bench[at.Index] = m;
        }

        private SpeciesDef Species(string id)
        {
            if (!_catalog.TryGet(id, out SpeciesDef d)) throw new SnapshotContentException($"모르는 몬스터 '{id}'");
            return d;
        }
    }
}
