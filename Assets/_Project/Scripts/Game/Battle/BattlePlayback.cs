using System;
using Moa.Showdown.Core.Battle;

namespace Moa.Showdown.Game.Battle
{
    /// <summary>
    /// 전투 기록을 시간에 맞춰 내보낸다. 규칙은 이미 끝난 계산 결과(<see cref="BattleResult"/>)이고, 여기서는 "언제 보여 줄지"만 정한다.
    /// </summary>
    public sealed class BattlePlayback
    {
        private readonly BattleResult _result;
        private readonly float _secondsPerTick;
        private float _elapsedTicks;
        private int _nextEvent;

        public BattlePlayback(BattleResult result, int ticksPerSecond)
        {
            _result = result ?? throw new ArgumentNullException(nameof(result));
            if (ticksPerSecond < 1) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            _secondsPerTick = 1f / ticksPerSecond;
        }

        public BattleResult Result => _result;

        /// <summary>지금 화면이 보여 주는 시점(틱, 소수 포함). 끝나면 종료 틱에 멈춘다.</summary>
        public float CurrentTick => Math.Min(_elapsedTicks, _result.EndTick);

        public bool IsFinished => _nextEvent >= _result.Events.Count;

        /// <summary>실제 시간 deltaSeconds × speed만큼 진행하고, 그 사이 일어난 이벤트를 순서대로 onEvent로 보낸다.</summary>
        public void Advance(float deltaSeconds, float speed, Action<BattleEvent> onEvent)
        {
            if (IsFinished) return;
            _elapsedTicks += deltaSeconds * speed / _secondsPerTick;
            var events = _result.Events;
            while (_nextEvent < events.Count && events[_nextEvent].Tick <= _elapsedTicks)
            {
                onEvent(events[_nextEvent]);
                _nextEvent++;
            }
        }
    }
}
