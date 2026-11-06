using Moa.Showdown.Game.Data;
using UnityEditor;
using UnityEngine;

namespace Moa.Showdown.Editor.Setup
{
    /// <summary>
    /// 수치 데이터 에셋을 만든다. 몇 번 실행해도 같은 결과: 에셋이 이미 있으면 값을 덮어쓰지 않는다 (인스펙터에서 고친 값 보존).
    /// 아래 기본값 = BALANCE.md 표. 기본값을 바꾸면 BALANCE와 BalanceTests도 같이 고친다.
    /// </summary>
    public static class BalanceSetup
    {
        public const string BattleBalancePath = "Assets/_Project/Data/BattleBalance.asset";

        [MenuItem("MoaShowdown/Setup/Battle Balance")]
        public static void CreateBattleBalance()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BattleBalance>(BattleBalancePath);
            if (existing != null)
            {
                Debug.Log($"[Setup] {BattleBalancePath} 이미 있음 — 값은 그대로 둠");
                Selection.activeObject = existing;
                return;
            }

            var asset = ScriptableObject.CreateInstance<BattleBalance>();
            ApplyDefaults(asset);
            AssetDatabase.CreateAsset(asset, BattleBalancePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            Debug.Log($"[Setup] {BattleBalancePath} 생성");
        }

        // BALANCE.md 1~4번
        private static void ApplyDefaults(BattleBalance b)
        {
            b.ticksPerSecond = 10;
            b.teamHpByRound = new[]
            {
                300, 500, 800, 1100, 1400,
                1800, 2200, 2600, 3000, 3400,
                3800, 4200, 4600, 5000, 5400,
                5800, 6200, 6600, 7000, 7400,
            };
            b.suddenDeathStartTick = 600;
            b.suddenDeathIntervalTicks = 10;
            b.suddenDeathBaseDamage = 20;
            b.suddenDeathDamageStep = 20;
            b.maxTicks = 3000;
        }
    }
}
