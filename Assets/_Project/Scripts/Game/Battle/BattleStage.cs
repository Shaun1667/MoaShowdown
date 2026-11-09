using System.Collections.Generic;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Random;
using Moa.Showdown.Core.Snapshot;
using Moa.Showdown.Game.Data;
using UnityEngine;

namespace Moa.Showdown.Game.Battle
{
    /// <summary>
    /// 3D 전투 무대. 몬스터 = 3D 모델(<see cref="MonsterVisualTable"/>, 없으면 캡슐) + 팀 색 발판, 옆 세로 막대 = 다음 공격까지 게이지.
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

        [Header("모델 (Phase G)")]
        [Tooltip("몬스터 ID → 3D 모델. 비었거나 모델이 없는 몬스터는 캡슐")]
        [SerializeField] private MonsterVisualTable visuals;
        [Tooltip("왼쪽(A) 몬스터가 보는 방향 (도). 90 = 오른쪽, 180 = 카메라 쪽. B는 좌우 반대")]
        [SerializeField] private float facingYawA = 125f;
        [SerializeField] private float idleBobAmount = 0.04f;
        [SerializeField] private float idleBobSpeed = 1.6f;
        [SerializeField] private float hitSquashSeconds = 0.2f;
        [SerializeField] private float hitSquashAmount = 0.18f;
        [Tooltip("레벨별 크기 배율 ([0] = Lv1). 표보다 높은 레벨은 마지막 값 (결정 2026-11-09 사용자: Lv2 ×1.15 · Lv3 ×1.3)")]
        [SerializeField] private float[] levelScale = { 1f, 1.15f, 1.3f };

        [Header("무대: 언덕 위 풀밭 경기장 (G-2)")]
        [SerializeField] private Color skyColor = new Color(0.62f, 0.82f, 0.95f);
        [SerializeField] private Color hillColor = new Color(0.36f, 0.58f, 0.30f);
        [SerializeField] private Color grassColor = new Color(0.50f, 0.74f, 0.38f);
        [SerializeField] private Color lineColor = new Color(0.92f, 0.95f, 0.85f);
        [SerializeField] private Color stoneColor = new Color(0.78f, 0.74f, 0.66f);
        [SerializeField] private Color bushColor = new Color(0.28f, 0.52f, 0.26f);
        [SerializeField] private Color rockColor = new Color(0.58f, 0.56f, 0.54f);
        [SerializeField] private Color lightColor = new Color(1f, 0.95f, 0.85f);

        private sealed class UnitView
        {
            public Transform Body;
            /// <summary>숨쉬기 · 맞으면 찌그러지기를 적용할 모양 (모델이면 모델, 아니면 캡슐).</summary>
            public Transform Shape;
            public Vector3 ShapeScale;
            public float Phase;
            public float HitTime;
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
            public UnitView Target;
        }

        private readonly UnitView[,] _units = new UnitView[2, BattleTeam.SlotCount];
        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private Transform _root;
        private DeterministicRandom _fxRng;

        private Transform _arena;

        /// <param name="boardA">칸별 몬스터 (ID → 모델, 레벨 → 크기. 그리는 데만 씀, null이면 모두 캡슐 · 기본 크기).</param>
        public void Build(BattleTeam teamA, BattleTeam teamB, ulong fxSeed, IReadOnlyList<MonsterSnapshot> boardA = null, IReadOnlyList<MonsterSnapshot> boardB = null)
        {
            Clear();
            BuildArena();
            _fxRng = new DeterministicRandom(fxSeed); // 연출 전용 난수 (전투 난수와 분리, CLAUDE 핵심 규칙 4)
            _root = new GameObject("StageRoot").transform;
            _root.SetParent(transform, false);

            BuildSide(BattleSide.A, teamA, colorA, boardA);
            BuildSide(BattleSide.B, teamB, colorB, boardB);
        }

        /// <summary>
        /// 경기장 (한 번만 만들고 다시 쓰는 배경): 언덕(납작한 큰 공) 위 타원 풀밭 · 가운데 선 · 둘레 덤불 · 바위, 하늘색 배경, 따뜻한 햇빛.
        /// 모양 · 자리는 고정 (난수 없음). 표현 전용.
        /// </summary>
        private void BuildArena()
        {
            if (_arena != null) return;
            _arena = new GameObject("Arena").transform;
            _arena.SetParent(transform, false);

            Prop(PrimitiveType.Sphere, "Hill", new Vector3(0f, -3.05f, 0.5f), new Vector3(34f, 5f, 18f), hillColor);
            Prop(PrimitiveType.Cylinder, "Grass", new Vector3(0f, -0.56f, 0f), new Vector3(17f, 0.03f, 7.4f), grassColor);
            Prop(PrimitiveType.Cube, "CenterLine", new Vector3(0f, -0.52f, 0f), new Vector3(0.08f, 0.01f, 5.2f), lineColor);

            // 둘레 장식 (좌우 대칭이 아니게 조금씩 다른 자리 · 크기)
            (Vector3 pos, float size, bool bush)[] decor =
            {
                (new Vector3(-8.6f, -0.4f, 2.6f), 0.9f, true), (new Vector3(-7.4f, -0.45f, 3.4f), 0.6f, false),
                (new Vector3(8.3f, -0.4f, 2.9f), 1.0f, true), (new Vector3(6.9f, -0.45f, 3.6f), 0.55f, true),
                (new Vector3(-2.5f, -0.45f, 3.8f), 0.7f, true), (new Vector3(3.1f, -0.5f, 3.9f), 0.5f, false),
                (new Vector3(-9.2f, -0.45f, -1.2f), 0.7f, false), (new Vector3(9.4f, -0.4f, -0.8f), 0.85f, true),
                (new Vector3(0.6f, -0.45f, 4.3f), 0.8f, true),
            };
            foreach (var (pos, size, bush) in decor)
                Prop(PrimitiveType.Sphere, bush ? "Bush" : "Rock", pos, bush ? new Vector3(size * 1.3f, size, size * 1.1f) : new Vector3(size, size * 0.7f, size * 0.9f),
                    bush ? bushColor : rockColor);

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = skyColor;
            }
            Light sun = RenderSettings.sun != null ? RenderSettings.sun : FindAnyObjectByType<Light>();
            if (sun != null) sun.color = lightColor;
        }

        private void Prop(PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(_arena, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            SetColor(go, color);
            Destroy(go.GetComponent<Collider>());
        }

        private float LevelScale(int level)
        {
            if (levelScale == null || levelScale.Length == 0 || level < 1) return 1f;
            return levelScale[Mathf.Min(level, levelScale.Length) - 1];
        }

        private void BuildSide(BattleSide side, BattleTeam team, Color color, IReadOnlyList<MonsterSnapshot> board)
        {
            float sign = side == BattleSide.A ? -1f : 1f;
            for (int slot = 0; slot < BattleTeam.SlotCount; slot++)
            {
                Vector3 pos = SlotPosition(side, slot);

                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"Slot_{side}_{slot}";
                marker.transform.SetParent(_root, false);
                marker.transform.position = new Vector3(pos.x, pos.y - 0.48f, pos.z);
                marker.transform.localScale = new Vector3(0.95f, 0.03f, 0.95f);
                BattleUnit unit = team[slot];
                // 돌판 + 몬스터가 있으면 위에 팀 색 고리 (모델은 팀 색이 없어서 발판으로 구분)
                SetColor(marker, Color.Lerp(stoneColor, color, unit.IsEmpty ? 0.12f : 0.2f));
                Destroy(marker.GetComponent<Collider>());

                if (unit.IsEmpty) continue;

                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = $"Ring_{side}_{slot}";
                ring.transform.SetParent(_root, false);
                ring.transform.position = new Vector3(pos.x, pos.y - 0.455f, pos.z);
                ring.transform.localScale = new Vector3(0.8f, 0.01f, 0.8f);
                SetColor(ring, color);
                Destroy(ring.GetComponent<Collider>());

                MonsterSnapshot mon = board != null && slot < board.Count ? board[slot] : null;
                string id = mon?.SpeciesId;
                float levelSize = LevelScale(mon?.Level ?? 1);
                MonsterVisualRow visual = visuals != null ? visuals.Find(id) : null;
                Transform body, shape;
                if (visual != null)
                {
                    body = new GameObject($"Unit_{side}_{slot}").transform;
                    body.SetParent(_root, false);
                    body.position = pos;
                    // 프리팹은 바닥 = 원점 → 발판 높이에 세운다. 방향 = 팀 기본 방향 + 줄 보정
                    float yaw = (side == BattleSide.A ? facingYawA : -facingYawA) + visual.yaw;
                    GameObject model = Instantiate(visual.prefab, body);
                    model.name = $"Model_{id}";
                    shape = model.transform;
                    shape.localPosition = new Vector3(0f, -0.48f + visual.yOffset, 0f);
                    shape.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    shape.localScale = Vector3.one * (Mathf.Max(0.01f, visual.scale) * levelSize);
                    foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
                }
                else
                {
                    var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    capsule.name = $"Unit_{side}_{slot}";
                    capsule.transform.SetParent(_root, false);
                    capsule.transform.position = pos;
                    capsule.transform.localScale = new Vector3(0.7f, 0.45f, 0.7f) * levelSize;
                    SetColor(capsule, color);
                    Destroy(capsule.GetComponent<Collider>());
                    body = shape = capsule.transform;
                }

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
                    Body = body,
                    Shape = shape,
                    ShapeScale = shape.localScale,
                    Phase = slot * 0.9f + (side == BattleSide.A ? 0f : 0.45f), // 다 같이 출렁이지 않게
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
            int targetSlot = _fxRng.NextInt(BattleTeam.SlotCount);
            Vector3 hit = SlotPosition(target, targetSlot) + Vector3.up * 0.3f;
            if (u == null) return hit;

            u.LungeTime = lungeSeconds;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Projectile";
            ball.transform.SetParent(_root, false);
            ball.transform.localScale = Vector3.one * 0.2f;
            SetColor(ball, gaugeColor);
            Destroy(ball.GetComponent<Collider>());
            _projectiles.Add(new Projectile { Tr = ball.transform, From = u.Home + Vector3.up * 0.3f, To = hit, Time = 0f, Target = _units[(int)target, targetSlot] });
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
                    if (p.Target != null) p.Target.HitTime = hitSquashSeconds; // 맞은 몬스터 찌그러짐
                    Destroy(p.Tr.gameObject);
                    _projectiles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 모양 연출 (표현 전용, 실제 시간): 숨쉬기(위아래로 살짝 늘었다 줄었다) + 맞으면 납작해졌다 돌아오기.
        /// 상점 · 구경 화면에서도 움직이도록 전투 재생(Tick)과 따로 매 프레임 돈다.
        /// </summary>
        private void LateUpdate()
        {
            float now = Time.time;
            float dt = Time.deltaTime;
            for (int s = 0; s < 2; s++)
            for (int slot = 0; slot < BattleTeam.SlotCount; slot++)
            {
                UnitView u = _units[s, slot];
                if (u?.Shape == null) continue;
                float bob = Mathf.Sin((now * idleBobSpeed + u.Phase) * Mathf.PI * 2f) * idleBobAmount;
                float squash = 0f;
                if (u.HitTime > 0f)
                {
                    u.HitTime = Mathf.Max(0f, u.HitTime - dt);
                    squash = Mathf.Sin((1f - u.HitTime / hitSquashSeconds) * Mathf.PI) * hitSquashAmount;
                }
                float y = 1f + bob - squash;
                float xz = 1f - bob * 0.5f + squash * 0.6f;
                u.Shape.localScale = new Vector3(u.ShapeScale.x * xz, u.ShapeScale.y * y, u.ShapeScale.z * xz);
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
