using System;
using System.Collections.Generic;
using Moa.Showdown.Core.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Moa.Showdown.Game.Ui
{
    /// <summary>로비 목록 한 줄에 그릴 내용 (GameRoot가 만든다).</summary>
    public struct LobbyRowView
    {
        public int PlayerIndex;
        public string Name;
        /// <summary>오른쪽 글 (목숨 · 순위 · 이번 상대).</summary>
        public string Right;
        public bool Me;
        public bool Out;
        public bool Scouted;
    }

    /// <summary>
    /// 로비 화면 (L-5, uGUI 코드 생성, 따로 캔버스): 오른쪽 위 8명 목록(줄마다 버튼 = 그 사람 보드 구경) +
    /// 구경 중 아래쪽 막대(제목 · 그 사람 대기석 4칸 · 남은 준비 시간 · 내 보드로). 규칙은 모른다 — 클릭 이벤트만 낸다.
    /// </summary>
    public sealed class LobbyView : MonoBehaviour
    {
        [SerializeField] private Color rowColor = new Color(0.12f, 0.13f, 0.17f, 0.85f);
        [SerializeField] private Color rowMeColor = new Color(0.2f, 0.35f, 0.6f, 0.9f);
        [SerializeField] private Color rowOutColor = new Color(0.08f, 0.08f, 0.09f, 0.7f);
        [SerializeField] private Color rowScoutedColor = new Color(0.85f, 0.65f, 0.2f, 0.9f);
        [SerializeField] private Color slotColor = new Color(1f, 1f, 1f, 0.08f);
        [SerializeField] private Color slotFilledColor = new Color(0.9f, 0.4f, 0.35f, 0.25f);
        [SerializeField] private Color backColor = new Color(0.85f, 0.35f, 0.5f);

        public event Action<int> PlayerClicked;
        public event Action BackClicked;

        private Font _font;
        private RectTransform _list;
        private RectTransform _scout;
        private Text _scoutTitle;
        private Text _scoutTimer;
        private readonly List<(Image image, Text left, Text right)> _rows = new List<(Image, Text, Text)>();
        private readonly List<(Image image, Text text)> _bench = new List<(Image, Text)>();
        private int[] _rowPlayers = Array.Empty<int>();

        public bool IsScoutVisible => _scout != null && _scout.gameObject.activeSelf;

        public void Build(int playerCount)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("LobbyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9; // 상점(10) · 캐러셀(11) 아래
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            // 오른쪽 위 목록 (상대 이름표 아래)
            Vector2 topRight = new Vector2(1f, 1f);
            const float rowH = 34f, rowW = 330f, top = -165f;
            _list = NewRect("LobbyList", root, topRight, topRight, new Vector2(-40f, top), new Vector2(rowW, 30f + playerCount * (rowH + 2f)));
            _list.pivot = new Vector2(1f, 1f);
            Text title = NewText("LobbyListTitle", _list, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(rowW, 28f), 17, TextAnchor.MiddleLeft);
            title.text = HudText.LobbyListTitle;
            for (int i = 0; i < playerCount; i++)
            {
                int row = i;
                var rt = NewRect($"LobbyRow{row}", _list, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f - rowH / 2f - row * (rowH + 2f)), new Vector2(rowW, rowH));
                var img = rt.gameObject.AddComponent<Image>();
                img.color = rowColor;
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = img;
                button.onClick.AddListener(() =>
                {
                    if (row < _rowPlayers.Length) PlayerClicked?.Invoke(_rowPlayers[row]);
                });
                Text left = NewText("Name", rt, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(rowW - 16f, rowH), 18, TextAnchor.MiddleLeft);
                left.rectTransform.pivot = new Vector2(0f, 0.5f);
                Text right = NewText("Right", rt, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(rowW - 16f, rowH), 17, TextAnchor.MiddleRight);
                right.rectTransform.pivot = new Vector2(1f, 0.5f);
                _rows.Add((img, left, right));
            }

            // 구경 막대: 상점의 대기석 · 전투 시작 버튼 자리
            _scout = NewRect("ScoutBar", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Vector2 bottom = new Vector2(0.5f, 0f);
            _scoutTitle = NewText("ScoutTitle", _scout, bottom, new Vector2(-480f, 345f), new Vector2(700f, 40f), 26, TextAnchor.MiddleCenter);
            _scoutTitle.color = new Color(1f, 0.85f, 0.4f);
            for (int b = 0; b < RunState.BenchSlotCount; b++)
            {
                var rt = NewRect($"ScoutBench{b}", _scout, bottom, bottom, new Vector2(-735f + b * 170f, 265f), new Vector2(160f, 90f));
                var img = rt.gameObject.AddComponent<Image>();
                img.color = slotColor;
                Text t = NewText("Label", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 80f), 22, TextAnchor.MiddleCenter);
                _bench.Add((img, t));
            }
            var back = NewRect("ScoutBack", _scout, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-180f, 265f), new Vector2(280f, 80f));
            var backImg = back.gameObject.AddComponent<Image>();
            backImg.color = backColor;
            var backButton = back.gameObject.AddComponent<Button>();
            backButton.targetGraphic = backImg;
            backButton.onClick.AddListener(() => BackClicked?.Invoke());
            NewText("Label", back, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(270f, 70f), 30, TextAnchor.MiddleCenter).text = HudText.ScoutBack;
            _scoutTimer = NewText("ScoutTimer", _scout, new Vector2(1f, 0f), new Vector2(-180f, 340f), new Vector2(200f, 60f), 40, TextAnchor.MiddleCenter);
            HideScout();
            SetListVisible(false);
        }

        public void SetListVisible(bool visible)
        {
            if (_list != null) _list.gameObject.SetActive(visible);
        }

        public void ShowPlayers(IReadOnlyList<LobbyRowView> rows)
        {
            _rowPlayers = new int[rows.Count];
            for (int i = 0; i < _rows.Count; i++)
            {
                var (image, left, right) = _rows[i];
                bool has = i < rows.Count;
                image.gameObject.SetActive(has);
                if (!has) continue;
                LobbyRowView r = rows[i];
                _rowPlayers[i] = r.PlayerIndex;
                left.text = r.Name;
                right.text = r.Right;
                image.color = r.Scouted ? rowScoutedColor : r.Me ? rowMeColor : r.Out ? rowOutColor : rowColor;
                Color textColor = r.Out ? new Color(0.6f, 0.6f, 0.6f) : Color.white;
                left.color = textColor;
                right.color = textColor;
            }
        }

        /// <summary>대기석 칸에 마우스를 올리면 정보 창 (내용은 GameRoot가, 빈 칸이면 null).</summary>
        public void SetBenchTooltips(TooltipView tooltip, Func<int, (string, string)?> bench)
        {
            for (int i = 0; i < _bench.Count; i++)
            {
                int b = i;
                var h = _bench[i].image.gameObject.AddComponent<HoverTarget>();
                h.Tooltip = tooltip;
                h.Provider = () => bench(b);
            }
        }

        /// <summary>구경 막대: 제목 + 대기석 칸 글 (빈 칸 = null).</summary>
        public void ShowScout(string title, IReadOnlyList<string> bench)
        {
            _scout.gameObject.SetActive(true);
            _scoutTitle.text = title;
            for (int i = 0; i < _bench.Count; i++)
            {
                string label = i < bench.Count ? bench[i] : null;
                _bench[i].text.text = label ?? "";
                _bench[i].image.color = label != null ? slotFilledColor : slotColor;
            }
        }

        public void HideScout()
        {
            if (_scout != null) _scout.gameObject.SetActive(false);
        }

        /// <summary>구경 중 남은 준비 시간 (seconds &lt; 0 = 숨김). warn이면 빨갛게.</summary>
        public void SetScoutTimer(int seconds, bool warn)
        {
            _scoutTimer.text = seconds < 0 ? "" : string.Format(HudText.PrepTimerFormat, seconds);
            _scoutTimer.color = warn ? new Color(1f, 0.35f, 0.3f) : Color.white;
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
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f); // 밝은 하늘 배경에서도 읽히게 (G-2)
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
