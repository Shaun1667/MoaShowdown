using System;
using System.Collections.Generic;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Shop;
using Moa.Showdown.Game.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Moa.Showdown.Game.Shop
{
    /// <summary>상점 화면에 그릴 내용 (GameRoot가 판 상태에서 만든다).</summary>
    public sealed class ShopViewData
    {
        public int Round;
        public int Gold;
        public int RerollCost;
        public ShopCard[] Shop = Array.Empty<ShopCard>();
        /// <summary>칸별 표시 글 (빈칸 = null).</summary>
        public string[] Board = new string[BattleTeam.SlotCount];
        public string[] Bench = new string[RunState.BenchSlotCount];
        public Location? Selected;
        /// <summary>선택한 몬스터의 판매가 (선택 없으면 null).</summary>
        public int? SellValue;
        public string Message = "";
    }

    public struct ShopCard
    {
        public bool Empty;
        public string Name;
        public string Rarity;
        public int Price;
        public bool Affordable;
    }

    /// <summary>
    /// 상점 · 배치 화면 (uGUI 코드 생성, 전투 HUD 위에 따로 캔버스). 조작은 클릭 → 클릭: 상점 카드 클릭 = 사기(대기석으로),
    /// 내 몬스터(보드 · 대기석) 클릭 = 선택, 다른 칸 클릭 = 옮기기/맞바꾸기, 판매 버튼 = 선택한 몬스터 팔기. 규칙은 모른다 — 이벤트만 낸다.
    /// </summary>
    public sealed class ShopView : MonoBehaviour
    {
        [SerializeField] private Color cardColor = new Color(0.18f, 0.2f, 0.26f, 0.95f);
        [SerializeField] private Color cardDisabledColor = new Color(0.12f, 0.12f, 0.14f, 0.8f);
        [SerializeField] private Color slotColor = new Color(1f, 1f, 1f, 0.08f);
        [SerializeField] private Color slotFilledColor = new Color(0.3f, 0.55f, 0.95f, 0.25f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.3f, 0.55f);
        [SerializeField] private Color buttonColor = new Color(0.2f, 0.2f, 0.25f);
        [SerializeField] private Color startColor = new Color(0.85f, 0.35f, 0.5f);

        public event Action<int> ShopCardClicked;
        public event Action RerollClicked;
        public event Action SellClicked;
        public event Action StartBattleClicked;
        public event Action<Location> LocationClicked;

        private Font _font;
        private Camera _camera;
        private Func<int, Vector3> _boardAnchor;
        private RectTransform _root;
        private RectTransform _boardLayer;
        private Text _gold;
        private Text _message;
        private Text _startLabel;
        private Text _timer;
        private Text _rerollLabel;
        private Text _sellLabel;
        private GameObject _sellButton;
        private readonly List<(Image image, Text text)> _cards = new List<(Image, Text)>();
        private readonly List<(Image image, Text text)> _bench = new List<(Image, Text)>();
        private readonly List<(RectTransform rt, Image image, Text text)> _board = new List<(RectTransform, Image, Text)>();

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void Build(Camera cam, Func<int, Vector3> boardAnchor, int shopSlots)
        {
            _camera = cam;
            _boardAnchor = boardAnchor;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("ShopCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _root = NewRect("ShopRoot", (RectTransform)canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _boardLayer = NewRect("BoardSlots", _root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            for (int i = 0; i < BattleTeam.SlotCount; i++)
            {
                int slot = i;
                var (img, txt) = NewButton($"Board{slot}", _boardLayer, Vector2.zero, Vector2.zero, new Vector2(170f, 150f), slotColor, 22, () => LocationClicked?.Invoke(Location.Board(slot)));
                txt.alignment = TextAnchor.LowerCenter;
                _board.Add((img.rectTransform, img, txt));
            }

            Vector2 bottom = new Vector2(0.5f, 0f);
            NewText("BenchTitle", _root, bottom, new Vector2(-480f, 330f), 22).text = HudText.Bench;
            for (int i = 0; i < RunState.BenchSlotCount; i++)
            {
                int b = i;
                _bench.Add(NewButton($"Bench{b}", _root, bottom, new Vector2(-735f + b * 170f, 265f), new Vector2(160f, 90f), slotColor, 22, () => LocationClicked?.Invoke(Location.Bench(b))));
            }

            float startX = -(shopSlots - 1) * 220f / 2f - 120f;
            for (int i = 0; i < shopSlots; i++)
            {
                int c = i;
                _cards.Add(NewButton($"Card{c}", _root, bottom, new Vector2(startX + c * 220f, 105f), new Vector2(205f, 130f), cardColor, 24, () => ShopCardClicked?.Invoke(c)));
            }

            _rerollLabel = NewButton("Reroll", _root, bottom, new Vector2(startX + shopSlots * 220f + 10f, 105f), new Vector2(200f, 80f), buttonColor, 24, () => RerollClicked?.Invoke()).text;
            var sell = NewButton("Sell", _root, bottom, new Vector2(150f, 265f), new Vector2(200f, 70f), buttonColor, 24, () => SellClicked?.Invoke());
            _sellLabel = sell.text;
            _sellButton = sell.image.gameObject;
            _startLabel = NewButton("StartBattle", _root, new Vector2(1f, 0f), new Vector2(-180f, 265f), new Vector2(280f, 80f), startColor, 30, () => StartBattleClicked?.Invoke()).text;
            _startLabel.text = HudText.StartBattle;
            // 준비 시간 (L-3): 전투 시작 버튼 위
            _timer = NewText("PrepTimer", _root, new Vector2(1f, 0f), new Vector2(-180f, 340f), 40);
            _timer.text = "";

            _gold = NewText("Gold", _root, new Vector2(0f, 0f), new Vector2(170f, 175f), 34);
            _gold.color = new Color(1f, 0.85f, 0.35f);
            _message = NewText("Message", _root, bottom, new Vector2(0f, 395f), 26);
            _message.color = new Color(1f, 0.7f, 0.6f);
            _message.rectTransform.sizeDelta = new Vector2(1100f, 44f);
            Hide();
        }

        /// <summary>마우스를 올리면 정보 창: 상점 카드 · 대기석 · 보드 칸. 내용은 GameRoot가 준다 (빈 칸이면 null).</summary>
        public void SetTooltips(TooltipView tooltip, Func<int, (string, string)?> card, Func<Location, (string, string)?> location)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                int c = i;
                Hover(_cards[i].image.gameObject, tooltip, () => card(c));
            }
            for (int i = 0; i < _bench.Count; i++)
            {
                int b = i;
                Hover(_bench[i].image.gameObject, tooltip, () => location(Location.Bench(b)));
            }
            for (int i = 0; i < _board.Count; i++)
            {
                int s = i;
                Hover(_board[i].image.gameObject, tooltip, () => location(Location.Board(s)));
            }
        }

        private static void Hover(GameObject go, TooltipView tooltip, Func<(string, string)?> provider)
        {
            var h = go.AddComponent<HoverTarget>();
            h.Tooltip = tooltip;
            h.Provider = provider;
        }

        public void Show(ShopViewData d)
        {
            _root.gameObject.SetActive(true);
            _gold.text = string.Format(HudText.GoldFormat, d.Gold);
            _rerollLabel.text = string.Format(HudText.RerollFormat, d.RerollCost);
            _message.text = d.Message ?? "";

            for (int i = 0; i < _cards.Count; i++)
            {
                var (image, text) = _cards[i];
                bool has = i < d.Shop.Length && !d.Shop[i].Empty;
                if (!has)
                {
                    image.color = cardDisabledColor;
                    text.text = HudText.SoldOut;
                    continue;
                }
                ShopCard c = d.Shop[i];
                image.color = c.Affordable ? cardColor : cardDisabledColor;
                text.text = string.Format(HudText.ShopCardFormat, c.Name, c.Rarity, c.Price);
            }

            for (int i = 0; i < _bench.Count; i++)
                Paint(_bench[i].image, _bench[i].text, d.Bench[i], d.Selected.HasValue && d.Selected.Value.Equals(Location.Bench(i)));
            for (int i = 0; i < _board.Count; i++)
                Paint(_board[i].image, _board[i].text, d.Board[i], d.Selected.HasValue && d.Selected.Value.Equals(Location.Board(i)));

            _sellButton.SetActive(d.SellValue.HasValue);
            if (d.SellValue.HasValue) _sellLabel.text = string.Format(HudText.SellFormat, d.SellValue.Value);
        }

        /// <summary>남은 준비 시간 표시 (seconds &lt; 0 = 숨김). warn이면 빨갛게.</summary>
        public void SetTimer(int seconds, bool warn)
        {
            _timer.text = seconds < 0 ? "" : string.Format(HudText.PrepTimerFormat, seconds);
            _timer.color = warn ? new Color(1f, 0.35f, 0.3f) : Color.white;
        }

        /// <summary>전투 시작 버튼 글 (로비 = "준비 완료", 다른 모드 = "전투 시작").</summary>
        public void SetStartLabel(string text) => _startLabel.text = text;

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!IsVisible) return;
            for (int i = 0; i < _board.Count; i++)
            {
                Vector3 screen = _camera.WorldToScreenPoint(_boardAnchor(i));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_boardLayer, screen, null, out Vector2 local);
                _board[i].rt.anchoredPosition = local + _boardLayer.rect.size * 0.5f;
            }
        }

        private void Paint(Image image, Text text, string label, bool selected)
        {
            text.text = label ?? "";
            image.color = selected ? selectedColor : label != null ? slotFilledColor : slotColor;
        }

        private (Image image, Text text) NewButton(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, int fontSize, Action onClick)
        {
            var rt = NewRect(name, parent, anchor, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick());
            Text t = NewText("Label", rt, new Vector2(0.5f, 0.5f), Vector2.zero, fontSize);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.sizeDelta = new Vector2(-10f, -10f);
            return (img, t);
        }

        private Text NewText(string name, RectTransform parent, Vector2 anchor, Vector2 pos, int size)
        {
            var rt = NewRect(name, parent, anchor, anchor, pos, new Vector2(320f, size * 1.6f));
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
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
