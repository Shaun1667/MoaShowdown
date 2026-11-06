using System.Collections.Generic;
using System.Threading.Tasks;
using Moa.Showdown.Core.Snapshot;

namespace Moa.Showdown.Core.Online
{
    /// <summary>
    /// 상대 공급자 (CLAUDE 핵심 규칙 7). 로컬 파일 ↔ UGS 교체는 이 인터페이스의 구현 교체만.
    /// 서버를 생각해 비동기(Task). 로컬 구현은 바로 끝난 Task를 돌려준다.
    /// </summary>
    public interface IOpponentSource
    {
        /// <summary>조건에 맞는 상대 하나. 플레이어 팀이 없으면 기본(봇) 팀, 그것도 없으면 Snapshot = null.</summary>
        Task<OpponentResult> FindOpponentAsync(OpponentQuery query);

        /// <summary>라운드가 끝난 내 팀을 올린다 (다른 플레이어가 상대로 받게).</summary>
        Task SubmitAsync(TeamSnapshot mine);
    }

    public sealed class OpponentQuery
    {
        public int Round;
        public int Wins;
        public int RulesVersion;
        /// <summary>내 판 ID — 내 판의 스냅샷은 상대로 고르지 않는다.</summary>
        public string OwnRunId;
        /// <summary>이미 만난 판 ID (같은 상대 반복 방지).</summary>
        public HashSet<string> ExcludedRunIds = new HashSet<string>();
        /// <summary>고르기 난수 시드 (같은 시드 + 같은 후보 = 같은 상대).</summary>
        public ulong Seed;
    }

    public sealed class OpponentResult
    {
        public TeamSnapshot Snapshot;
        public bool IsBot;
        /// <summary>어디서 왔는지 (파일 이름 등, 디버그 표시용).</summary>
        public string Source;
    }
}
