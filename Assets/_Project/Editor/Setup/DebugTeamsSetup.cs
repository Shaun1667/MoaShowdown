using Moa.Showdown.Game.Data;
using UnityEditor;
using UnityEngine;

namespace Moa.Showdown.Editor.Setup
{
    /// <summary>
    /// 시험 전투용 팀 에셋. 이미 있으면 값을 덮어쓰지 않는다 (인스펙터에서 고친 팀 보존).
    /// 팀 = (이름, 칸별 (공격력, 공격 간격 틱)). 칸 번호 = 줄 × 3 + 열, 줄 0 = 위 줄, 열 0 = 가운데 쪽.
    /// </summary>
    public static class DebugTeamsSetup
    {
        public const string DebugTeamsPath = "Assets/_Project/Data/DebugTeams.asset";

        [MenuItem("MoaShowdown/Setup/Debug Teams")]
        public static void CreateDebugTeams()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DebugTeamSet>(DebugTeamsPath);
            if (existing != null)
            {
                Debug.Log($"[Setup] {DebugTeamsPath} 이미 있음 — 값은 그대로 둠");
                return;
            }

            var set = ScriptableObject.CreateInstance<DebugTeamSet>();
            // 시험용 (밸런스 아님). 1초 = 10틱
            set.teams.Add(Team("고른 팀", (0, 12, 8), (1, 9, 6), (2, 20, 15), (3, 7, 4), (4, 15, 10)));
            set.teams.Add(Team("빠른 팀", (0, 5, 3), (1, 5, 3), (2, 5, 3), (3, 5, 3), (4, 5, 3), (5, 5, 3)));
            set.teams.Add(Team("한 방 팀", (0, 60, 30), (1, 60, 30), (3, 40, 20)));
            set.teams.Add(Team("빈 팀"));
            AssetDatabase.CreateAsset(set, DebugTeamsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] {DebugTeamsPath} 생성");
        }

        private static DebugTeam Team(string name, params (int slot, int attack, int interval)[] units)
        {
            var t = new DebugTeam { name = name };
            for (int i = 0; i < t.slots.Length; i++) t.slots[i] = new DebugUnit();
            foreach (var u in units)
                t.slots[u.slot] = new DebugUnit { present = true, attack = u.attack, attackIntervalTicks = u.interval };
            return t;
        }
    }
}
