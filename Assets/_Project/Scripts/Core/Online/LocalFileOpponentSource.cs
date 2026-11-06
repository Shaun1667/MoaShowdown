using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moa.Showdown.Core.Random;
using Moa.Showdown.Core.Snapshot;

namespace Moa.Showdown.Core.Online
{
    /// <summary>
    /// 로컬 파일 상대 공급자 (온라인 전 1단계). 폴더 안 *.json = 스냅샷 JSON 배열 (서버와 같은 형식).
    /// 읽기: 플레이어 폴더들(주어진 순서) + 쓰기 폴더, 폴더 안은 파일 이름 순 → 후보 순서 고정. 봇 폴더는 따로.
    /// 올리기: 쓰기 폴더의 <see cref="SubmittedFileName"/>에 덧붙인다. 매 질의마다 다시 읽는다 (방금 올린 팀도 후보).
    /// </summary>
    public sealed class LocalFileOpponentSource : IOpponentSource
    {
        public const string SubmittedFileName = "my_teams.json";

        private readonly IReadOnlyList<string> _playerDirs;
        private readonly string _botDir;
        private readonly string _writeDir;
        private readonly Func<TeamSnapshot, bool> _isPlayable;

        /// <summary>마지막으로 읽을 때 건너뛴 항목 · 파일 (디버그용).</summary>
        public List<string> LoadErrors { get; } = new List<string>();

        public LocalFileOpponentSource(IReadOnlyList<string> playerDirs, string botDir, string writeDir, Func<TeamSnapshot, bool> isPlayable)
        {
            _playerDirs = playerDirs ?? Array.Empty<string>();
            _botDir = botDir;
            _writeDir = writeDir;
            _isPlayable = isPlayable ?? throw new ArgumentNullException(nameof(isPlayable));
        }

        public Task<OpponentResult> FindOpponentAsync(OpponentQuery query)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            LoadErrors.Clear();
            var rng = new DeterministicRandom(query.Seed);

            var players = new List<TeamSnapshot>();
            var playerSources = new List<string>();
            foreach (string dir in _playerDirs) LoadDir(dir, players, playerSources);
            if (!string.IsNullOrEmpty(_writeDir)) LoadDir(_writeDir, players, playerSources);

            TeamSnapshot pick = OpponentPicker.PickPlayer(players, query, rng, _isPlayable);
            if (pick != null)
                return Task.FromResult(new OpponentResult { Snapshot = pick, IsBot = false, Source = playerSources[players.IndexOf(pick)] });

            var bots = new List<TeamSnapshot>();
            var botSources = new List<string>();
            if (!string.IsNullOrEmpty(_botDir)) LoadDir(_botDir, bots, botSources);
            TeamSnapshot bot = OpponentPicker.PickBot(bots, query, rng, _isPlayable);
            return Task.FromResult(new OpponentResult
            {
                Snapshot = bot,
                IsBot = bot != null,
                Source = bot != null ? botSources[bots.IndexOf(bot)] : null,
            });
        }

        public Task SubmitAsync(TeamSnapshot mine)
        {
            if (mine == null) throw new ArgumentNullException(nameof(mine));
            if (string.IsNullOrEmpty(_writeDir)) throw new InvalidOperationException("쓰기 폴더가 없음");
            TeamSnapshotJson.Validate(mine);

            Directory.CreateDirectory(_writeDir);
            string path = Path.Combine(_writeDir, SubmittedFileName);
            var list = File.Exists(path)
                ? TeamSnapshotJson.DeserializeMany(File.ReadAllText(path), LoadErrors)
                : new List<TeamSnapshot>();
            list.Add(mine);
            File.WriteAllText(path, TeamSnapshotJson.SerializeMany(list));
            return Task.CompletedTask;
        }

        private void LoadDir(string dir, List<TeamSnapshot> into, List<string> sources)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            string[] files = Directory.GetFiles(dir, "*.json");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                try
                {
                    var errors = new List<string>();
                    foreach (TeamSnapshot s in TeamSnapshotJson.DeserializeMany(File.ReadAllText(file), errors))
                    {
                        into.Add(s);
                        sources.Add(name);
                    }
                    foreach (string e in errors) LoadErrors.Add($"{name} {e}");
                }
                catch (Exception e) when (e is SnapshotFormatException || e is IOException)
                {
                    LoadErrors.Add($"{name}: {e.Message}");
                }
            }
        }
    }
}
