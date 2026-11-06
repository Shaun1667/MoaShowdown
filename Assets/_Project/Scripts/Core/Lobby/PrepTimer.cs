using System;
using System.Collections.Generic;

namespace Moa.Showdown.Core.Lobby
{
    /// <summary>
    /// 준비(상점) 시간 판정 한 곳 (L-3). 방장 · 오프라인 모두 이걸로 "언제 전투를 시작하나"를 정한다.
    /// 시작 조건: 시간이 0이 되거나, 살아 있는 사람이 모두 준비 완료 (봇 · 탈락자는 처음부터 준비 완료).
    /// 시간 끝에 보드는 그대로 (대기석 자동 배치 없음, 결정 2026-11-06).
    /// 실제 시간이라 결정론 대상이 아니다 — 전투 결과에는 영향 없음 (언제 시작하는지만 정함).
    /// </summary>
    public sealed class PrepTimer
    {
        private bool[] _ready = Array.Empty<bool>();

        public double DurationSeconds { get; private set; }
        public double RemainingSeconds { get; private set; }
        public bool IsRunning { get; private set; }

        /// <summary>새 준비 시간 시작. autoReady = 처음부터 준비 완료인 플레이어(봇 · 탈락자).</summary>
        public void Start(int playerCount, double durationSeconds, IEnumerable<int> autoReady)
        {
            if (playerCount < 1) throw new ArgumentOutOfRangeException(nameof(playerCount));
            if (durationSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            _ready = new bool[playerCount];
            foreach (int i in autoReady)
                if (i >= 0 && i < playerCount) _ready[i] = true;
            DurationSeconds = durationSeconds;
            RemainingSeconds = durationSeconds;
            IsRunning = true;
        }

        public void Stop() => IsRunning = false;

        public void SetReady(int player)
        {
            if (player >= 0 && player < _ready.Length) _ready[player] = true;
        }

        public bool IsReady(int player) => player >= 0 && player < _ready.Length && _ready[player];

        public bool AllReady
        {
            get
            {
                foreach (bool r in _ready) if (!r) return false;
                return _ready.Length > 0;
            }
        }

        /// <summary>시간을 흘린다 (음수는 무시).</summary>
        public void Advance(double seconds)
        {
            if (!IsRunning || seconds <= 0) return;
            RemainingSeconds = Math.Max(0, RemainingSeconds - seconds);
        }

        /// <summary>지금 전투를 시작해야 하나.</summary>
        public bool ShouldStart => IsRunning && (RemainingSeconds <= 0 || AllReady);

        /// <summary>화면에 보일 남은 초 (올림).</summary>
        public int DisplaySeconds => (int)Math.Ceiling(RemainingSeconds);
    }
}
