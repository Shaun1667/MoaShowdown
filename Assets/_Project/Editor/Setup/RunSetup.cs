using System.Collections.Generic;
using Moa.Showdown.Game.Data;
using UnityEditor;
using UnityEngine;

namespace Moa.Showdown.Editor.Setup
{
    /// <summary>판 진행 수치 에셋. 이미 있으면 값을 덮어쓰지 않는다. 아래 기본값 = BALANCE 5 · 9.</summary>
    public static class RunSetup
    {
        public const string RunBalancePath = "Assets/_Project/Data/RunBalance.asset";

        [MenuItem("MoaShowdown/Setup/Run Balance")]
        public static void CreateRunBalance()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RunBalance>(RunBalancePath);
            if (existing != null)
            {
                // 새로 생긴 PvE · 캐러셀 칸만 비어 있으면 채운다 (다른 값은 그대로)
                if (FillPveCarouselDefaults(existing))
                {
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[Setup] {RunBalancePath} PvE · 캐러셀 기본값 채움 (다른 값은 그대로)");
                }
                else Debug.Log($"[Setup] {RunBalancePath} 이미 있음 — 값은 그대로 둠");
                return;
            }

            var r = ScriptableObject.CreateInstance<RunBalance>();
            r.winsToChampion = 10;
            r.startLives = 10;
            // 1~4라운드 1 · 5~9라운드 2 · 10라운드~ 3 (임시)
            r.lifeLossByRound = new[] { 1, 1, 1, 1, 2, 2, 2, 2, 2, 3 };
            r.startTrainerId = "debug_trainer";
            // 시작 팀 없음 — 1라운드 골드로 상점에서 산다 (2-2)
            // 상점 · 골드 (BALANCE 10): 수입 30 → 35 · 40 · 45 · 50 … (+5씩), 가격은 원작 값 (사용자 2026-11-06), 판매 75%는 추정
            r.startGold = 30;
            r.firstIncome = 35;
            r.incomeStep = 5;
            r.shopSlots = 5;
            r.rerollCost = 3;
            r.priceByRarity = new[] { 10, 20, 30, 40, 50 };
            r.sellPercent = 75;
            r.mergeCountByLevel = new[] { 3, 2 }; // Lv1 × 3 → Lv2, Lv2 × 2 → Lv3 (사용자)
            r.maxLevel = 3; // 합체로는 Lv3까지 (사용자)
            // 로비 (BALANCE 13)
            r.playerCount = 8;
            r.recentOpponentMemory = 3;
            r.prepSeconds = 30; // 준비 시간 BALANCE 15
            r.poolSizeByRarity = new[] { 60, 22, 18, 12, 10 }; // 공유 풀 BALANCE 14 — 일반은 몬스터가 6종뿐이라 임시 60 (TFT 29, 종류가 늘면 되돌림)
            FillPveCarouselDefaults(r);
            AssetDatabase.CreateAsset(r, RunBalancePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] {RunBalancePath} 생성");
        }

        /// <summary>
        /// PvE · 캐러셀 기본값 (BALANCE 16). 시기 · 패배 · 보상 종류는 사용자 결정 (2026-11-06), PvE 팀 구성 · 골드 액수는 임시 (Claude).
        /// 이미 PvE 팀이 있으면 아무것도 안 함 (값 보존). 옛 에셋에 새 필드를 채울 때도 이것을 부른다.
        /// </summary>
        public static bool FillPveCarouselDefaults(RunBalance r)
        {
            if (r.pveTeams != null && r.pveTeams.Count > 0) return false;
            r.pveOpeningRounds = 2;  // 1 · 2라운드
            r.pveRepeatStart = 8;    // 8 · 14 · 20 …
            r.pveRepeatEvery = 6;
            r.pveLifeLoss = 1;
            r.pveTeams = new List<PveTeamRow>
            {
                Pve("야생 1 (1R)", 5, 2, ("sprout", 0, 1), ("drip", 1, 1)),
                Pve("야생 2 (2R)", 5, 2, ("sprout", 0, 1), ("drip", 1, 1), ("gust", 3, 1)),
                Pve("야생 3 (8R)", 10, 5, ("ember", 0, 2), ("pebble", 1, 2), ("drip", 2, 2), ("gust", 3, 2), ("zap", 4, 2), ("sprout", 5, 2)),
                Pve("야생 4 (14R)", 10, 5, ("ember", 0, 3), ("pebble", 1, 3), ("drip", 2, 3), ("gust", 3, 2), ("zap", 4, 2), ("sprout", 5, 2)),
                Pve("야생 5 (20R~)", 10, 5, ("ember", 0, 3), ("pebble", 1, 3), ("drip", 2, 3), ("gust", 3, 3), ("zap", 4, 3), ("sprout", 5, 3)),
            };
            r.carouselOpeningRounds = 1; // 1라운드 (판 시작)
            r.carouselRepeatStart = 5;   // 5 · 11 · 17 … (PvE 사이 중간)
            r.carouselRepeatEvery = 6;
            r.carouselPickSeconds = 10;
            return true;
        }

        private static PveTeamRow Pve(string label, int win, int lose, params (string id, int slot, int level)[] monsters)
        {
            var row = new PveTeamRow { label = label, winGold = win, loseGold = lose };
            foreach (var (id, slot, level) in monsters)
                row.monsters.Add(new StartSlot { slot = slot, speciesId = id, level = level });
            return row;
        }
    }
}
