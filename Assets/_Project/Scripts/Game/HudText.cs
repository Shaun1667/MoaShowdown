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

        // 상점 (2-2)
        public const string ShopHeader = "상점 — 몬스터를 사서 보드에 배치하세요";
        public const string Bench = "대기석";
        public const string StartBattle = "전투 시작";
        public const string GoldFormat = "골드 {0}";
        public const string RerollFormat = "새로고침 ({0})";
        public const string SellFormat = "판매 (+{0})";
        public const string SoldOut = "—";
        public const string ShopCardFormat = "{0}\n{1}\n{2}골드";
        public const string UnitLabelFormat = "{0}\nLv{1}";
        public const string NotEnoughGold = "골드가 부족합니다";
        public const string BenchFull = "대기석이 가득 찼습니다";
        public const string BoughtFormat = "{0} 구매";
        public const string MergedFormat = "{0} 합체! → Lv{1}";
        public const string SoldFormat = "{0} 판매 (+{1})";
        public const string EmptyBoardWarning = "보드에 몬스터가 없습니다 — 그래도 전투를 시작하려면 한 번 더 누르세요";
        public static readonly string[] RarityNames = { "일반", "고급", "희귀", "영웅", "전설" };

        // 정보 창 (2-3)
        public const string InfoMonsterTitleFormat = "{0}  Lv{1}{2}";
        public const string InfoEnemyTag = "  (상대)";
        public const string InfoRarityFormat = "등급: {0}";
        public const string InfoAttackFormat = "공격력: {0}   (기본 {1} × {2}^(Lv−1) + 버프 {3})";
        public const string InfoIntervalFormat = "공격 간격: {0}초";
        public const string InfoDpsFormat = "초당 피해: {0}";
        public const string InfoPriceFormat = "가격: {0}골드";
        public const string InfoSellFormat = "판매가: {0}골드";
        public const string InfoMergeFormat = "합체: 같은 Lv{0} {1}/{2}마리 보유";
        public const string InfoMaxLevel = "최대 레벨";
        public const string InfoBot = "봇 (개발자가 만든 팀)";
        public const string InfoPlayer = "다른 플레이어의 팀";
        public const string InfoRunIdFormat = "판 ID: {0}";
        public const string InfoTrainerFormat = "트레이너: {0}";
        public const string InfoWinsLivesLostFormat = "그때 승수 {0} · 잃은 목숨 {1}";
        public const string InfoMonsterCountFormat = "몬스터 {0}마리";
        public const string InfoSourceFormat = "출처: {0}";
        public const string InfoRoundFormat = "라운드 {0}";
        public const string MySideLabel = "나 (정보)";
        public const string OpponentSideLabel = "상대 (정보)";
        public const string OpponentTitleFormat = "상대: {0}";
        public const string MyTitle = "나";

        // 로비 (L-1)
        public const string MyName = "나";
        public const string BotNameFormat = "봇 {0}";
        public const string LobbyInfoFormat = "목숨 {0}/{1}   ·   생존 {2}/{3}";
        public const string LobbyWinFormat = "승리!  ({0})";
        public const string LobbyEliminatedFormat = "{0} 탈락 — {1}등";
        public const string LobbyFirst = "1등!";
        public const string LobbyFirstFormat = "{0}라운드 — 마지막까지 살아남았습니다!";
        public const string PlacementFormat = "{0}등";
        public const string LobbyOutFormat = "{0}라운드에서 목숨을 모두 잃었습니다 — {1}등";
        public const string GhostFormat = "유령: {0}";
        public const string StandingsTitle = "순위";
        public const string StandingsAliveFormat = "{0}   목숨 {1}{2}";
        public const string StandingsOutFormat = "{0}등  {1}{2}";
        public const string MeMark = "  (나)";
        public const string InfoBotPlayer = "봇 플레이어";
        public const string InfoGhost = "유령 — 그 플레이어 팀의 복사본 (주인은 영향 없음)";
        public const string InfoLivesFormat = "목숨 {0}/{1}";
        public const string LobbyAliveFormat = "생존 {0}/{1}";
        public const string InfoPoolFormat = "풀에 남은 수: {0}";

        // 능력 · 상태 (3-1)
        public const string StatusShieldFormat = "방어막 {0}";
        public const string StatusBurnFormat = "화상 {0}";
        public const string StatusPoisonFormat = "독 {0}";
        public const string HealPopupFormat = "+{0}";
        public const string InfoTypeFormat = "속성: {0}";
        public const string InfoAbilityFormat = "능력: {0} {1}";
        public static readonly string[] TypeNames = { "없음", "풀", "불", "물", "전기", "바위", "비행", "독" };
        /// <summary>AbilityTrigger 번호 순.</summary>
        public static readonly string[] TriggerNames = { "", "전투 시작 시", "공격할 때", "{0}번째 공격마다", "라운드 시작마다", "살 때" };
        /// <summary>AbilityEffect 번호 순. {0} = 수치.</summary>
        public static readonly string[] EffectFormats =
        {
            "",
            "피해 +{0}",
            "상대에게 화상 {0}",
            "상대에게 독 {0}",
            "우리 팀 방어막 {0}",
            "우리 팀 체력 {0} 회복",
            "피해 ×{0}",
            "자기 공격력 영구 +{0}",
            "가장 약한 아군 공격력 영구 +{0}",
        };
        /// <summary>전투 중 능력 발동 때 떠오르는 짧은 글. AbilityEffect 번호 순.</summary>
        public static readonly string[] EffectPopupFormats = { "", "+{0}", "화상+{0}", "독+{0}", "방어막+{0}", "", "×{0}!", "", "" };
    }
}
