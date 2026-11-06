using System.Collections.Generic;
using System.Linq;

namespace Moa.Showdown.Core.Lobby
{
    public enum OpponentKind
    {
        None = 0,
        Player = 1,
        /// <summary>유령전 (OpponentIndex = 복사한 플레이어).</summary>
        Ghost = 2,
        Pve = 3,
    }

    /// <summary>로비 목록 한 줄 (L-5).</summary>
    public sealed class StandingRow
    {
        public int Index;
        public string Name;
        public bool IsBot;
        public int Lives;
        /// <summary>최종 순위 (0 = 살아 있음).</summary>
        public int Placement;
        public OpponentKind Opponent;
        /// <summary>상대 플레이어 번호 (Player) · 복사한 플레이어 번호 (Ghost) · 그 외 -1.</summary>
        public int OpponentIndex = -1;

        public bool IsAlive => Placement == 0;
    }

    /// <summary>
    /// 로비 목록 (L-5) 한 곳: 순서 = 살아 있는 사람(목숨 많은 순, 같으면 번호 순) → 탈락한 사람(순위 순).
    /// report를 주면 그 라운드에 각자 누구와 싸웠는지 (A ↔ B, 유령전, PvE)를 채운다.
    /// </summary>
    public static class LobbyStandings
    {
        /// <param name="beforeResults">true면 report 라운드의 결과를 되돌려 보여 준다 (전투 재생 중 — 목숨 · 탈락을 미리 알려 주지 않게).</param>
        public static List<StandingRow> Build(LobbyState lobby, LobbyRoundReport report = null, bool beforeResults = false)
        {
            var rows = lobby.Players.Select(p => new StandingRow
            {
                Index = p.Index,
                Name = p.Name,
                IsBot = p.IsBot,
                Lives = p.Run.Lives,
                Placement = p.Placement,
            }).ToList();

            if (report != null && beforeResults)
            {
                foreach (LobbyMatch m in report.Matches)
                {
                    rows[m.PlayerA].Lives += m.LivesLostA;
                    if (m.PlayerB >= 0) rows[m.PlayerB].Lives += m.LivesLostB;
                }
                foreach (int i in report.Eliminated) rows[i].Placement = 0;
                if (report.FinishedAfter)
                    foreach (StandingRow r in rows)
                        if (r.Placement == 1) r.Placement = 0; // 우승도 아직
            }

            if (report != null)
                foreach (LobbyMatch m in report.Matches)
                {
                    StandingRow a = rows[m.PlayerA];
                    if (m.IsPve) a.Opponent = OpponentKind.Pve;
                    else if (m.IsGhost)
                    {
                        a.Opponent = OpponentKind.Ghost;
                        a.OpponentIndex = m.GhostOf;
                    }
                    else
                    {
                        a.Opponent = OpponentKind.Player;
                        a.OpponentIndex = m.PlayerB;
                        StandingRow b = rows[m.PlayerB];
                        b.Opponent = OpponentKind.Player;
                        b.OpponentIndex = m.PlayerA;
                    }
                }

            return rows.Where(r => r.IsAlive).OrderByDescending(r => r.Lives).ThenBy(r => r.Index)
                .Concat(rows.Where(r => !r.IsAlive).OrderBy(r => r.Placement))
                .ToList();
        }
    }
}
