using System.Collections.Generic;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Random;
using UnityEngine;

namespace Moa.Showdown.Game.Battle
{
    /// <summary>
    /// 3D 전투 무대 (그레이박스). 몬스터 = 캡슐, 옆 세로 막대 = 다음 공격까지 게이지.
    /// 배치: 왼쪽 = A(내 팀), 오른쪽 = B. 줄 0 = 위 줄(화면 안쪽), 줄 1 = 아래 줄. 열 0 = 가운데 쪽.
    /// 규칙은 모른다 — <see cref="BattleTeam"/>(모양)과 이벤트만 받아 그린다.
    /// </summary>
    public sealed class BattleStage : MonoBehaviour
    {
        [Header("배치 (표현 전용)")]
        [SerializeField] private float centerGap = 1.4f;
        [SerializeField] private float columnSpacing = 1.7f;
        [SerializeField] private float rowDepth = 1.0f;
        [SerializeField] private float lowerRowShift = 0.5f;

        [Header("연출 (초, 1x 기준)")]
        [SerializeField] private float lungeSeconds = 0.18f;
        [SerializeField] private float lungeDistance = 0.35f;
        [SerializeField] private float projectileSeconds = 0.25f;

        [Header("색")]
        [SerializeField] private Color colorA = new Color(0.30f, 0.55f, 0.95f);
        [SerializeField] private Color colorB = new Color(0.95f, 0.40f, 0.35f);
        [SerializeField] private Color gaugeColor = new Color(1f, 0.95f, 0.6f);
        [SerializeField] private Color groundColor = new Color(0.22f, 0.26f, 0.24f);

        private sealed class UnitView
        {
            public Transform Body;
            public Vector3 Home;
            public Transform GaugePivot;
            public int Interval;
            public float LungeTime;
            public float Sign;
        }

        private sealed class Projectile
        {
            public Transform Tr;
            public Vector3 From;
            public Vector3 To;
            public float Time;
        }

        private readonly UnitView[,] _units = new UnitView[2, BattleTeam.SlotCount];
        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private Transform _root;
        private DeterministicRandom _fxRng;

        public void Build(BattleTeam teamA, BattleTeam teamB, ulong fxSeed)
        {
            Clear();
            _fxRng = new DeterministicRandom(fxSeed); // 연출 전용 난수 (전투 난수와 분리, CLAUDE 핵심 규칙 4)
            _root = new GameObject("StageRoot").transform;
            _root.SetParent(transform, false);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(_root, false);
            ground.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            ground.transform.localScale = new Vector3(16f, 0.1f, 6f);
            SetColor(ground, groundColor);

            BuildSide(BattleSide.A, teamA, colorA);
            BuildSide(BattleSide.B, teamB, colorB);
        }

        private void BuildSide(BattleSide side, BattleTeam team, Color color)
        {
            float sign = side == BattleSide.A ? -1f : 1f;
            for (int slot = 0; slot < BattleTeam.SlotCount; slot++)
            {
                Vector3 pos = SlotPosition(side, slot);

                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"Slot_{side}_{slot}";
                marker.transform.SetParent(_root, false);
                marker.transform.position = new Vector3(pos.x, pos.y - 0.48f, pos.z);
                marker.transform.localScale = new Vector3(0.9f, 0.02f, 0.9f);
                SetColor(marker, Color.Lerp(groundColor, color, 0.25f));
                Destroy(marker.GetComponent<Collider>());

                BattleUnit unit = team[slot];
                if (unit.IsEmpty) continue;

                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = $"Unit_{side}_{slot}";
                body.transform.SetParent(_root, false);
                body.transform.position = pos;
                body.transform.localScale = new Vector3(0.7f, 0.45f, 0.7f);
                SetColor(body, color);
                Destroy(body.GetComponent<Collider>());

                // 게이지: 몬스터 바깥쪽 옆, 아래를 기준으로 위로 자란다
                var pivot = new GameObject($"Gauge_{side}_{slot}").transform;
                pivot.SetParent(_root, false);
                pivot.position = pos + new Vector3(sign * 0.5f, -0.45f, 0f);
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.transform.SetParent(pivot, false);
                bar.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                bar.transform.localScale = new Vector3(0.08f, 1f, 0.08f);
                SetColor(bar, gaugeColor);
                Destroy(bar.GetComponent<Collider>());
                pivot.localScale = new Vector3(1f, 0.001f, 1f);

                _units[(int)side, slot] = new UnitView
                {
                    Body = body.transform,
                    Home = pos,
                    GaugePivot = pivot,
                    Interval = unit.AttackIntervalTicks,
                    Sign = sign,
                };
            }
        }

        public Vector3 SlotPosition(BattleSide side, int slot)
        {
            float sign = side == BattleSide.A ? -1f : 1f;
            int row = slot / BattleTeam.Columns;
            int col = slot % BattleTeam.Columns;
            float x = sign * (centerGap + col * columnSpacing + (row == 1 ? lowerRowShift : 0f));
            float z = row == 0 ? rowDepth : -rowDepth;
            return transform.TransformPoint(new Vector3(x, 0f, z));
        }

        /// <summary>몬스터 머리 위 (이름표 · 숫자 위치).</summary>
        public Vector3 LabelAnchor(BattleSide side, int slot) => SlotPosition(side, slot) + Vector3.down * 0.75f;

        public bool HasUnit(BattleSide side, int slot) => _units[(int)side, slot] != null;

        /// <summary>공격 연출. 맞는 위치(피해 숫자를 띄울 곳)를 돌려준다.</summary>
        public Vector3 PlayAttack(BattleSide side, int slot)
        {
            UnitView u = _units[(int)side, slot];
            BattleSide target = side == BattleSide.A ? BattleSide.B : BattleSide.A;
            Vector3 hit = SlotPosition(target, _fxRng.NextInt(BattleTeam.SlotCount)) + Vector3.up * 0.3f;
            if (u == null) return hit;

            u.LungeTime = lungeSeconds;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Projectile";
            ball.transform.SetParent(_root, false);
            ball.transform.localScale = Vector3.one * 0.2f;
            SetColor(ball, gaugeColor);
            Destroy(ball.GetComponent<Collider>());
            _projectiles.Add(new Projectile { Tr = ball.transform, From = u.Home + Vector3.up * 0.3f, To = hit, Time = 0f });
            return hit;
        }

        /// <summary>매 프레임: 게이지 = 지금 틱 기준 다음 공격까지 진행률, 돌진 · 투사체 진행.</summary>
        public void Tick(float currentTick, float scaledDelta, bool finished)
        {
            for (int s = 0; s < 2; s++)
            for (int slot = 0; slot < BattleTeam.SlotCount; slot++)
            {
                UnitView u = _units[s, slot];
                if (u == null) continue;

                // 공격 타이머는 0부터 1씩 올라 간격에서 공격 후 0 → 공격은 간격의 배수 틱. 진행률 = (틱 mod 간격) / 간격
                float fill = finished ? 0f : (currentTick % u.Interval) / u.Interval;
                u.GaugePivot.localScale = new Vector3(1f, Mathf.Max(0.001f, fill * 0.9f), 1f);

                if (u.LungeTime > 0f)
                {
                    u.LungeTime = Mathf.Max(0f, u.LungeTime - scaledDelta);
                    float t = 1f - u.LungeTime / lungeSeconds;
                    float push = Mathf.Sin(t * Mathf.PI) * lungeDistance;
                    u.Body.position = u.Home + new Vector3(-u.Sign * push, 0f, 0f);
                }
            }

            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                Projectile p = _projectiles[i];
                p.Time += scaledDelta;
                float t = Mathf.Clamp01(p.Time / projectileSeconds);
                p.Tr.position = Vector3.Lerp(p.From, p.To, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.6f;
                if (t >= 1f)
                {
                    Destroy(p.Tr.gameObject);
                    _projectiles.RemoveAt(i);
                }
            }
        }

        public void Clear()
        {
            _projectiles.Clear();
            for (int s = 0; s < 2; s++)
            for (int slot = 0; slot < BattleTeam.SlotCount; slot++)
                _units[s, slot] = null;
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
        }

        private static void SetColor(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = c;
        }
    }
}
