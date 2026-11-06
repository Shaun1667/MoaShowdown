using System;
using Moa.Showdown.Core.Content;

namespace Moa.Showdown.Core.Shop
{
    /// <summary>상점 · 골드 수치 (BALANCE 10). 값은 데이터 에셋(Game의 RunBalance)에서.</summary>
    public sealed class ShopRules
    {
        /// <summary>1라운드에 받는 골드 (판 시작 지급).</summary>
        public int StartGold;

        /// <summary>2라운드에 받는 수입.</summary>
        public int FirstIncome;

        /// <summary>3라운드부터 라운드마다 수입이 늘어나는 양.</summary>
        public int IncomeStep;

        /// <summary>상점 진열 칸 수.</summary>
        public int ShopSlots;

        public int RerollCost;

        /// <summary>등급(Rarity 번호 순)별 가격.</summary>
        public int[] PriceByRarity;

        /// <summary>판매가 = 들인 골드 × 이 % (반올림).</summary>
        public int SellPercent;

        /// <summary>
        /// 레벨별 합체에 필요한 마리 수: [0] = Lv1 몇 마리 → Lv2, [1] = Lv2 몇 마리 → Lv3 … (같은 몬스터 · 같은 레벨).
        /// ~~모든 레벨 3마리~~ → Lv1 × 3 → Lv2, Lv2 × 2 → Lv3 (2026-11-06 사용자).
        /// </summary>
        public int[] MergeCountByLevel;

        /// <summary>합체로 오를 수 있는 최대 레벨.</summary>
        public int MaxLevel;

        /// <summary>이 레벨 몬스터를 다음 레벨로 합치는 데 필요한 마리 수. 최대 레벨이면 0 (합칠 수 없음).</summary>
        public int MergeCountFor(int level)
        {
            if (level < 1 || level >= MaxLevel) return 0;
            return MergeCountByLevel[level - 1];
        }

        /// <summary>이 레벨 몬스터 하나에 들어간 Lv1 마리 수 (판매가 계산용). Lv1 = 1, Lv2 = 3, Lv3 = 6 …</summary>
        public long CopiesAt(int level)
        {
            long copies = 1;
            for (int lv = 1; lv < level && lv - 1 < MergeCountByLevel.Length && copies < int.MaxValue; lv++)
                copies *= MergeCountByLevel[lv - 1];
            return copies;
        }

        /// <summary>라운드 시작 때 받는 골드. 1라운드 = 시작 지급, 그 뒤 = 첫 수입 + 증가량 × (라운드 − 2).</summary>
        public int IncomeFor(int round)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            return round == 1 ? StartGold : FirstIncome + IncomeStep * (round - 2);
        }

        public int PriceOf(Rarity rarity)
        {
            int i = (int)rarity;
            if (i < 0 || i >= PriceByRarity.Length) throw new ArgumentOutOfRangeException(nameof(rarity));
            return PriceByRarity[i];
        }

        public void Validate()
        {
            if (StartGold < 0 || FirstIncome < 0 || IncomeStep < 0) throw new InvalidOperationException("골드 · 수입 음수");
            if (ShopSlots < 1) throw new InvalidOperationException("ShopSlots < 1");
            if (RerollCost < 0) throw new InvalidOperationException("RerollCost < 0");
            if (PriceByRarity == null || PriceByRarity.Length < Enum.GetValues(typeof(Rarity)).Length)
                throw new InvalidOperationException("PriceByRarity가 등급 수보다 짧음");
            foreach (int p in PriceByRarity)
                if (p < 0) throw new InvalidOperationException("가격 음수");
            if (SellPercent < 0 || SellPercent > 100) throw new InvalidOperationException("SellPercent 범위 밖");
            if (MaxLevel < 1) throw new InvalidOperationException("MaxLevel < 1");
            if (MergeCountByLevel == null || MergeCountByLevel.Length < MaxLevel - 1)
                throw new InvalidOperationException("MergeCountByLevel이 최대 레벨까지 안 됨");
            foreach (int n in MergeCountByLevel)
                if (n < 2) throw new InvalidOperationException("합체 마리 수 < 2");
        }
    }
}
