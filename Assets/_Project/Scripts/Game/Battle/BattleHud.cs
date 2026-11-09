using System;
using System.Collections.Generic;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Game.Ui;
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

        [SerializeField] private Color panelColor = new Color(0.05f, 0.06f, 0.08f, 0.92f);

        public event Action<int> SpeedClicked;
        public event Action ReplayClicked;
        public event Action ContinueClicked;
        public event Action DebugLevelUpClicked;

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
        private Text _runInfo;
        private RectTransform _resultPanel;
        private Text _resultDetail;
        private Text _continueLabel;
        private GameObject _debugButton;
        private readonly Text[] _sideLabels = new Text[2];
        private readonly Text[] _status2 = new Text[2];

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

            _runInfo = NewText("RunInfo", _canvasRect, new Vector2(0.5f, 1f), new Vector2(0f, -178f), 26, TextAnchor.MiddleCenter);

            // HP 바 아래 상태 줄 (방어막 · 화상 · 독)
            _status2[0] = NewText("StatusA", _canvasRect, new Vector2(0f, 1f), new Vector2(680f, -118f), 24, TextAnchor.MiddleLeft);
            _status2[1] = NewText("StatusB", _canvasRect, new Vector2(1f, 1f), new Vector2(-680f, -118f), 24, TextAnchor.MiddleRight);
            _status2[0].rectTransform.sizeDelta = new Vector2(420f, 40f);
            _status2[1].rectTransform.sizeDelta = new Vector2(420f, 40f);
            _status2[0].color = _status2[1].color = new Color(0.75f, 0.9f, 1f);

            // 라운드 결과 패널 (화면 아래 가운데): 내용 + 계속 버튼
            _resultPanel = NewRect("ResultPanel", _canvasRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(760f, 200f));
            _resultPanel.gameObject.AddComponent<Image>().color = panelColor;
            _resultDetail = NewText("Detail", _resultPanel, new Vector2(0.5f, 1f), new Vector2(0f, -55f), 28, TextAnchor.MiddleCenter);
            _resultDetail.rectTransform.sizeDelta = new Vector2(720f, 90f);
            _continueLabel = NewButton("Continue", "", _resultPanel, new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(260f, 56f), () => ContinueClicked?.Invoke())
                .GetComponentInChildren<Text>();
            _resultPanel.gameObject.SetActive(false);

            _debugButton = NewButton("DebugLevelUp", HudText.DebugLevelUp, _canvasRect, new Vector2(0f, 0f), new Vector2(150f, 40f), new Vector2(260f, 44f), () => DebugLevelUpClicked?.Invoke()).gameObject;
            _debugButton.SetActive(false);
        }

        public void ShowRunInfo(string text) => _runInfo.text = text;

        /// <summary>상점 단계: 라운드 · 안내 글, HP 바는 이번 라운드 체력으로 가득, 전투 이름표 · 숫자 · 배너는 지운다.</summary>
        public void ShowShopHeader(int round, string text, int roundHp)
        {
            ShowBattle(round, "", "", roundHp);
            _teams.text = text;
        }

        public void ShowRoundResult(string detail, string continueLabel)
        {
            _resultDetail.text = detail;
            _continueLabel.text = continueLabel;
            _resultPanel.gameObject.SetActive(true);
        }

        public void HideRoundResult() => _resultPanel.gameObject.SetActive(false);

        public bool IsRoundResultVisible => _resultPanel.gameObject.activeSelf;

        public void SetDebugVisible(bool visible) => _debugButton.SetActive(visible);

        /// <summary>HP 바 아래 "나" · "상대" 이름표 — 마우스를 올리면 정보 창 (원작의 트레이너 초상화 자리).</summary>
        public void AddSideInfo(TooltipView tooltip, Func<(string, string)?> mine, Func<(string, string)?> theirs)
        {
            _sideLabels[0] = SideLabel("MyInfo", new Vector2(0f, 1f), new Vector2(40f, -118f), TextAnchor.MiddleLeft, tooltip, mine);
            _sideLabels[1] = SideLabel("OpponentInfo", new Vector2(1f, 1f), new Vector2(-40f, -118f), TextAnchor.MiddleRight, tooltip, theirs);
            SetSideLabels(HudText.MySideLabel, "");
        }

        /// <summary>HP 바 아래 상태 줄: 방어막 · 화상 · 독 (0이면 안 보임). 원작 화면의 방패 · 보라 아이콘 자리.</summary>
        public void SetStatus(BattleSide side, int shield, int burn, int poison)
        {
            Text t = _status2[(int)side];
            if (t == null) return;
            var parts = new List<string>();
            if (shield > 0) parts.Add(string.Format(HudText.StatusShieldFormat, shield));
            if (burn > 0) parts.Add(string.Format(HudText.StatusBurnFormat, burn));
            if (poison > 0) parts.Add(string.Format(HudText.StatusPoisonFormat, poison));
            t.text = string.Join("   ", parts);
        }

        /// <summary>떠오르는 글 (회복 · 지속 피해 · 능력 발동).</summary>
        public void ShowPopup(Vector3 world, string text, Color color)
        {
            Text t = NewText("Popup", _labelLayer, Vector2.zero, Vector2.zero, 28, TextAnchor.MiddleCenter);
            t.text = text;
            t.color = color;
            _popups.Add(new Popup { Text = t, World = world, Time = 0f });
        }

        public void SetSideLabels(string mine, string theirs)
        {
            if (_sideLabels[0] == null) return;
            _sideLabels[0].text = mine;
            _sideLabels[1].text = theirs;
            _sideLabels[1].transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(theirs));
        }

        private Text SideLabel(string name, Vector2 anchor, Vector2 pos, TextAnchor align, TooltipView tooltip, Func<(string, string)?> provider)
        {
            var rt = NewRect(name, _canvasRect, anchor, anchor, pos, new Vector2(420f, 40f));
            rt.pivot = anchor;
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.06f);
            var hover = rt.gameObject.AddComponent<HoverTarget>();
            hover.Tooltip = tooltip;
            hover.Provider = provider;
            Text t = NewText("Label", rt, new Vector2(0.5f, 0.5f), Vector2.zero, 24, align);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.sizeDelta = new Vector2(-16f, 0f);
            return t;
        }

        public void ShowBattle(int round, string teamA, string teamB, int startHp)
        {
            _round.text = string.Format(HudText.RoundFormat, round);
            _teams.text = string.Format(HudText.TeamVsFormat, teamA, teamB);
            _banner.text = "";
            SetHp(BattleSide.A, startHp, startHp);
            SetHp(BattleSide.B, startHp, startHp);
            SetStatus(BattleSide.A, 0, 0, 0);
            SetStatus(BattleSide.B, 0, 0, 0);
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

        private Image NewButton(string name, string label, Vector2 pos, Vector2 size, Action onClick) =>
            NewButton(name, label, _canvasRect, new Vector2(0.5f, 1f), pos, size, onClick);

        private Image NewButton(string name, string label, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick)
        {
            var rt = NewRect(name, parent, anchor, anchor, pos, size);
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
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f); // 밝은 하늘 배경에서도 읽히게 (G-2)
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
