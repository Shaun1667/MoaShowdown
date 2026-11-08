using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Moa.Showdown.Game.Ui
{
    /// <summary>
    /// 온라인 대기실 화면 (O-1, uGUI 코드 생성, 따로 캔버스): 방 밖 = 이름 · 방 만들기 · 빠른 참가 · 코드 입력 + 참가,
    /// 방 안 = 참가 코드 · 참가자 목록 · 시험 신호 · 기록 · 나가기. 규칙도 네트워크도 모른다 — 클릭 이벤트만 낸다.
    /// </summary>
    public sealed class OnlineView : MonoBehaviour
    {
        [SerializeField] private Color panelColor = new Color(0.1f, 0.11f, 0.15f, 0.92f);
        [SerializeField] private Color buttonColor = new Color(0.25f, 0.45f, 0.8f);
        [SerializeField] private Color leaveColor = new Color(0.85f, 0.35f, 0.5f);
        [SerializeField] private Color fieldColor = new Color(1f, 1f, 1f, 0.1f);

        public const int MaxLogLines = 8;

        /// <summary>(이름)</summary>
        public event Action<string> CreateClicked;
        /// <summary>(이름)</summary>
        public event Action<string> QuickJoinClicked;
        /// <summary>(이름, 코드)</summary>
        public event Action<string, string> JoinClicked;
        public event Action PingClicked;
        public event Action LeaveClicked;

        private Font _font;
        private RectTransform _outside;
        private RectTransform _inside;
        private InputField _name;
        private InputField _code;
        private Text _status;
        private Text _roomCode;
        private Text _members;
        private Text _log;
        private readonly List<Button> _buttons = new List<Button>();
        private readonly Queue<string> _logLines = new Queue<string>();

        public bool IsInsideVisible => _inside != null && _inside.gameObject.activeSelf;
        public string NameText => _name != null ? _name.text : "";
        public string StatusText => _status != null ? _status.text : "";

        public void Build(string defaultName)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("OnlineCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;
            Vector2 c = new Vector2(0.5f, 0.5f);

            var panel = NewRect("OnlinePanel", root, c, c, Vector2.zero, new Vector2(900f, 760f));
            panel.gameObject.AddComponent<Image>().color = panelColor;
            NewText("Title", panel, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(860f, 60f), 38, TextAnchor.MiddleCenter).text = HudText.OnlineTitle;
            _status = NewText("Status", panel, new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(860f, 50f), 22, TextAnchor.MiddleCenter);
            _status.color = new Color(1f, 0.85f, 0.4f);

            // 방 밖
            _outside = NewRect("Outside", panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            NewText("NameLabel", _outside, c, new Vector2(-250f, 200f), new Vector2(260f, 50f), 26, TextAnchor.MiddleRight).text = HudText.OnlineNameLabel;
            _name = NewField("NameField", _outside, new Vector2(110f, 200f), new Vector2(420f, 56f), HudText.OnlineNamePlaceholder);
            _name.text = defaultName;
            NewButton("Create", _outside, new Vector2(-170f, 90f), new Vector2(320f, 80f), HudText.OnlineCreate, buttonColor, () => CreateClicked?.Invoke(_name.text));
            NewButton("QuickJoin", _outside, new Vector2(170f, 90f), new Vector2(320f, 80f), HudText.OnlineQuickJoin, buttonColor, () => QuickJoinClicked?.Invoke(_name.text));
            NewText("CodeLabel", _outside, c, new Vector2(-250f, -40f), new Vector2(260f, 50f), 26, TextAnchor.MiddleRight).text = HudText.OnlineCodeLabel;
            _code = NewField("CodeField", _outside, new Vector2(30f, -40f), new Vector2(260f, 56f), HudText.OnlineCodePlaceholder);
            NewButton("Join", _outside, new Vector2(270f, -40f), new Vector2(180f, 56f), HudText.OnlineJoin, buttonColor, () => JoinClicked?.Invoke(_name.text, _code.text));

            // 방 안
            _inside = NewRect("Inside", panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _roomCode = NewText("RoomCode", _inside, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(860f, 50f), 30, TextAnchor.MiddleCenter);
            _members = NewText("Members", _inside, new Vector2(0f, 1f), new Vector2(240f, -330f), new Vector2(400f, 360f), 24, TextAnchor.UpperLeft);
            _log = NewText("Log", _inside, new Vector2(1f, 1f), new Vector2(-230f, -330f), new Vector2(400f, 360f), 20, TextAnchor.UpperLeft);
            NewButton("Ping", _inside, new Vector2(-170f, -280f), new Vector2(320f, 70f), HudText.OnlinePing, buttonColor, () => PingClicked?.Invoke());
            NewButton("Leave", _inside, new Vector2(170f, -280f), new Vector2(320f, 70f), HudText.OnlineLeave, leaveColor, () => LeaveClicked?.Invoke());

            ShowOutside("");
        }

        public void ShowOutside(string status)
        {
            _outside.gameObject.SetActive(true);
            _inside.gameObject.SetActive(false);
            _status.text = status;
        }

        /// <summary>방 안 화면: 코드 · 참가자 줄(그릴 글은 GameRoot가 만든다).</summary>
        public void ShowRoom(string codeLine, IReadOnlyList<string> memberLines, string status)
        {
            _outside.gameObject.SetActive(false);
            _inside.gameObject.SetActive(true);
            _roomCode.text = codeLine;
            _members.text = string.Join("\n", memberLines);
            _status.text = status;
        }

        public void SetStatus(string status) => _status.text = status;

        /// <summary>연결 중에는 버튼을 막는다.</summary>
        public void SetBusy(bool busy)
        {
            foreach (Button b in _buttons) b.interactable = !busy;
        }

        public void AppendLog(string line)
        {
            _logLines.Enqueue(line);
            while (_logLines.Count > MaxLogLines) _logLines.Dequeue();
            _log.text = string.Join("\n", _logLines);
        }

        public void ClearLog()
        {
            _logLines.Clear();
            if (_log != null) _log.text = "";
        }

        // ───────── 만들기 도우미 ─────────

        private void NewButton(string name, RectTransform parent, Vector2 pos, Vector2 size, string label, Color color, Action onClick)
        {
            var rt = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            NewText("Label", rt, new Vector2(0.5f, 0.5f), Vector2.zero, size, 26, TextAnchor.MiddleCenter).text = label;
            _buttons.Add(b);
        }

        private InputField NewField(string name, RectTransform parent, Vector2 pos, Vector2 size, string placeholder)
        {
            var rt = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = fieldColor;
            var field = rt.gameObject.AddComponent<InputField>();
            Text text = NewText("Text", rt, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(20f, 8f), 26, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.raycastTarget = true;
            Text ph = NewText("Placeholder", rt, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(20f, 8f), 24, TextAnchor.MiddleLeft);
            ph.text = placeholder;
            ph.color = new Color(1f, 1f, 1f, 0.35f);
            field.textComponent = text;
            field.placeholder = ph;
            field.targetGraphic = img;
            return field;
        }

        private Text NewText(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
        {
            var rt = NewRect(name, parent, anchor, anchor, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            return t;
        }

        private static RectTransform NewRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
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
