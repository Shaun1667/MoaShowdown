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

        // 판 진행 (2-1)
        public const string MyTeam = "내 팀";
        public const string RunInfoFormat = "승 {0}/{1}   ·   목숨 {2}/{3}";
        public const string NextRound = "다음 라운드";
        public const string NewRun = "새 판";
        public const string Champion = "챔피언!";
        public const string Eliminated = "탈락";
        public const string RoundWinFormat = "승리!  승 {0}/{1}";
        public const string RoundLoseFormat = "패배 — 목숨 {0}개 잃음  (남은 목숨 {1})";
        public const string RoundDraw = "무승부 — 승수 · 목숨 변화 없음";
        public const string RoundBye = "상대가 없어 부전승";
        public const string ChampionFormat = "{0}라운드 만에 {1}승 달성!";
        public const string EliminatedFormat = "{0}라운드에서 목숨을 모두 잃었습니다  ({1}승)";
        public const string DebugLevelUp = "레벨업 (디버그)";
        public const string DebugLevelUpDoneFormat = "디버그: 팀 레벨 +1 → 레벨 {0}";
    }
}
