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
                Debug.Log($"[Setup] {RunBalancePath} 이미 있음 — 값은 그대로 둠");
                return;
            }

            var r = ScriptableObject.CreateInstance<RunBalance>();
            r.winsToChampion = 10;
            r.startLives = 10;
            // 1~4라운드 1 · 5~9라운드 2 · 10라운드~ 3 (임시)
            r.lifeLossByRound = new[] { 1, 1, 1, 1, 2, 2, 2, 2, 2, 3 };
            r.startTrainerId = "debug_trainer";
            r.startTeam.Add(new StartSlot { slot = 0, speciesId = "sprout", level = 1 });
            r.startTeam.Add(new StartSlot { slot = 3, speciesId = "ember", level = 1 });
            AssetDatabase.CreateAsset(r, RunBalancePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] {RunBalancePath} 생성");
        }
    }
}
