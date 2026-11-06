using System;
using System.Collections.Generic;
using Moa.Showdown.Core.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Moa.Showdown.Game.Battle
{
    /// <summary>
    /// 전투 화면 UI (uGUI, 코드로 생성). 위 양쪽 = 팀 공통 HP 바, 가운데 위 = 라운드 · 속도 · 다시 보기,
    /// 몬스터 아래 = 공격력 숫자, 맞은 곳 = 피해 숫자. 문구는 <see cref="HudText"/>.
    /// </summary>
    public sealed class BattleHud : MonoBehaviour
    {
        [SerializeField] private Color hpFill = new Color(0.35f, 0.85f, 0.35f);
        [SerializeField] private Color hpBack = new Color(0.1f, 0.1f, 0.1f);
        [SerializeField] private Color buttonColor = new Color(0.2f, 0.2f, 0.25f);
        [SerializeField] private Color buttonActiveColor = new Color(0.85f, 0.35f, 0.5f);
        [SerializeField] private float popupSeconds = 0.7f;

        public event Action<int> SpeedClicked;
        public event Action ReplayClicked;

        private sealed class HpBar
        {
            public RectTransform Fill;
            public Text Label;
        }

        private sealed class Popup
        {
            public Text Text;
            public Vector3 World;
            public float Time;
        }

        private sealed class UnitLabel
        {
            public Text Text;
            public Vector3 World;
        }

        private Font _font;
        private Camera _camera;
        private RectTransform _canvasRect;
        private readonly HpBar[] _hp = new HpBar[2];
        private Text _round;
        private Text _teams;
        private Text _banner;
        private Text _status;
        private readonly List<(int speed, Image image)> _speedButtons = new List<(int, Image)>();
        private readonly List<UnitLabel> _labels = new List<UnitLabel>();
        private readonly List<Popup> _popups = new List<Popup>();
        private RectTransform _labelLayer;

        public void Build(Camera cam, IReadOnlyList<int> speeds)
        {
            _camera = cam;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)canvasGo.transform;

            _labelLayer = NewRect("Labels", _canvasRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _hp[0] = BuildHpBar("HpA", new Vector2(0f, 1f), new Vector2(40f, -60f));
            _hp[1] = BuildHpBar("HpB", new Vector2(1f, 1f), new Vector2(-40f, -60f));

            _round = NewText("Round", _canvasRect, new Vector2(0.5f, 1f), new Vector2(0f, -40f), 34, TextAnchor.MiddleCenter);
            _teams = NewText("Teams", _canvasRect, new Vector2(0.5f, 1f), new Vector2(0f, -140f), 24, TextAnchor.MiddleCenter);
            _banner = NewText("Banner", _canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), 72, TextAnchor.MiddleCenter);
            _status = NewText("Status", _canvasRect, new Vector2(0.5f, 0f), new Vector2(0f, 40f), 24, TextAnchor.MiddleCenter);

            float x = -((speeds.Count + 1) * 110f) / 2f + 55f;
            foreach (int speed in speeds)
            {
                int s = speed;
                Image img = NewButton($"Speed{s}", string.Format(HudText.SpeedFormat, s), new Vector2(x, -95f), new Vector2(100f, 46f), () => SpeedClicked?.Invoke(s));
                _speedButtons.Add((s, img));
                x += 110f;
            }
            NewButton("Replay", HudText.Replay, new Vector2(x + 30f, -95f), new Vector2(160f, 46f), () => ReplayClicked?.Invoke());
        }

        public void ShowBattle(int round, string teamA, string teamB, int startHp)
        {
            _round.text = string.Format(HudText.RoundFormat, round);
            _teams.text = string.Format(HudText.TeamVsFormat, teamA, teamB);
            _banner.text = "";
            SetHp(BattleSide.A, startHp, startHp);
            SetHp(BattleSide.B, startHp, startHp);
            foreach (var l in _labels) Destroy(l.Text.gameObject);
            _labels.Clear();
            foreach (var p in _popups) Destroy(p.Text.gameObject);
            _popups.Clear();
        }

        public void AddUnitLabel(Vector3 world, int attack, int intervalTicks, int ticksPerSecond)
        {
            Text t = NewText("UnitLabel", _labelLayer, Vector2.zero, Vector2.zero, 22, TextAnchor.MiddleCenter);
            t.text = $"{attack} / {(float)intervalTicks / ticksPerSecond:0.0}s";
            _labels.Add(new UnitLabel { Text = t, World = world });
        }

        public void SetHp(BattleSide side, int current, int max)
        {
            HpBar bar = _hp[(int)side];
            float f = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            bar.Fill.anchorMin = Vector2.zero; // 양쪽 모두 오른쪽 끝부터 줄어든다 (원작 화면)
            bar.Fill.anchorMax = new Vector2(f, 1f);
            bar.Label.text = $"{HudText.Hp} {current}/{max}";
        }

        public void ShowDamage(Vector3 world, int amount)
        {
            Text t = NewText("Damage", _labelLayer, Vector2.zero, Vector2.zero, 30, TextAnchor.MiddleCenter);
            t.text = amount.ToString();
            t.color = new Color(1f, 0.9f, 0.4f);
            _popups.Add(new Popup { Text = t, World = world, Time = 0f });
        }

        public void ShowBanner(string text) => _banner.text = text;

        public void ShowStatus(string text) => _status.text = text;

        public void SetActiveSpeed(int speed)
        {
            foreach (var (s, image) in _speedButtons)
                image.color = s == speed ? buttonActiveColor : buttonColor;
        }

        public void Tick(float scaledDelta)
        {
            foreach (UnitLabel l in _labels)
                Place(l.Text.rectTransform, l.World);

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                Popup p = _popups[i];
                p.Time += scaledDelta;
                float t = p.Time / popupSeconds;
                Place(p.Text.rectTransform, p.World + Vector3.up * (0.2f + t * 0.8f));
                var c = p.Text.color;
                c.a = 1f - t;
                p.Text.color = c;
                if (t >= 1f)
                {
                    Destroy(p.Text.gameObject);
                    _popups.RemoveAt(i);
                }
            }
        }

        private void Place(RectTransform rt, Vector3 world)
        {
            Vector3 screen = _camera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_labelLayer, screen, null, out Vector2 local);
            rt.anchoredPosition = local + _labelLayer.rect.size * 0.5f;
        }

        private HpBar BuildHpBar(string name, Vector2 anchor, Vector2 pos)
        {
            var back = NewRect(name, _canvasRect, anchor, anchor, pos, new Vector2(600f, 44f));
            back.pivot = anchor;
            back.gameObject.AddComponent<Image>().color = hpBack;
            var fill = NewRect("Fill", back, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            fill.gameObject.AddComponent<Image>().color = hpFill;
            Text label = NewText("Label", back, new Vector2(0.5f, 0.5f), Vector2.zero, 26, TextAnchor.MiddleCenter);
            return new HpBar { Fill = fill, Label = label };
        }

        private Image NewButton(string name, string label, Vector2 pos, Vector2 size, Action onClick)
        {
            var rt = NewRect(name, _canvasRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = buttonColor;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick());
            Text t = NewText("Label", rt, new Vector2(0.5f, 0.5f), Vector2.zero, 26, TextAnchor.MiddleCenter);
            t.text = label;
            return img;
        }

        private Text NewText(string name, RectTransform parent, Vector2 anchor, Vector2 pos, int size, TextAnchor align)
        {
            var rt = NewRect(name, parent, anchor, anchor, pos, new Vector2(800f, size * 1.6f));
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            return t;
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }
    }
}
