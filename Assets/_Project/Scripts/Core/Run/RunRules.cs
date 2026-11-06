using System;

namespace Moa.Showdown.Core.Run
{
    /// <summary>판 진행 수치 (BALANCE 5). 값은 데이터 에셋(Game의 RunBalance)에서.</summary>
    public sealed class RunRules
    {
        /// <summary>이만큼 이기면 챔피언.</summary>
        public int WinsToChampion;

        /// <summary>시작 목숨. 0이 되면 탈락.</summary>
        public int StartLives;

        /// <summary>라운드 1부터 패배 시 잃는 목숨. 표보다 뒤 라운드는 마지막 값.</summary>
        public int[] LifeLossByRound;

        public int GetLifeLoss(int round)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            int i = Math.Min(round - 1, LifeLossByRound.Length - 1);
            return LifeLossByRound[i];
        }

        public void Validate()
        {
            if (WinsToChampion < 1) throw new InvalidOperationException("WinsToChampion < 1");
            if (StartLives < 1) throw new InvalidOperationException("StartLives < 1");
            if (LifeLossByRound == null || LifeLossByRound.Length == 0) throw new InvalidOperationException("LifeLossByRound가 비어 있음");
            foreach (int v in LifeLossByRound)
                if (v < 1) throw new InvalidOperationException("LifeLossByRound에 1 미만 값");
        }
    }
}
