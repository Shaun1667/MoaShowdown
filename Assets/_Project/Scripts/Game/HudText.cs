namespace Moa.Showdown.Game
{
    /// <summary>화면에 나오는 한글 문구는 모두 여기 (수정 · 번역 한 곳).</summary>
    public static class HudText
    {
        public const string Hp = "HP";
        public const string RoundFormat = "라운드 {0}";
        public const string SuddenDeath = "서든데스!";
        public const string Win = "승리!";
        public const string Lose = "패배";
        public const string Draw = "무승부";
        public const string Replay = "다시 보기";
        public const string SpeedFormat = "{0}x";
        public const string TeamVsFormat = "{0}  vs  {1}";
        public const string SameResult = "같은 시드 → 같은 결과 (확인됨)";
        public const string DifferentResult = "결과가 다름! (결정론 깨짐)";
        public const string OpponentFormat = "{0} ({1}승)";
        public const string OpponentBotFormat = "봇 {0}";
        public const string OpponentNotFound = "상대를 못 찾아 디버그 팀으로 대신함";
    }
}
