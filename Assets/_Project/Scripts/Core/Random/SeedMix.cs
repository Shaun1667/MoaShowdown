namespace Moa.Showdown.Core.Random
{
    /// <summary>
    /// 판 시드 하나에서 용도 · 라운드별 시드를 뽑는다 (CLAUDE 핵심 규칙 4: 용도별 분리).
    /// 같은 (판 시드, 용도, 라운드) → 같은 시드. 용도 번호는 바꾸지 않는다 (바꾸면 옛 판의 결과가 달라짐).
    /// </summary>
    public static class SeedMix
    {
        public const ulong PurposeBattle = 1;
        public const ulong PurposeOpponent = 2;
        public const ulong PurposeShop = 3;
        public const ulong PurposePairing = 4;
        public const ulong PurposeLobbyPlayer = 5;

        public static ulong Derive(ulong runSeed, ulong purpose, int round)
        {
            ulong mixed = runSeed ^ (purpose * 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)round * 0xD1B54A32D192ED03UL);
            return new DeterministicRandom(mixed).NextULong();
        }
    }
}
