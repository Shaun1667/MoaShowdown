using System;
using System.Collections.Generic;
using Moa.Showdown.Game.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Moa.Showdown.Game.Shop
{
    /// <summary>캐러셀 화면에 그릴 내용 (GameRoot가 로비 상태에서 만든다).</summary>
    public sealed class CarouselViewData
    {
        public string Order = "";
        public string Status = "";
        public string Message = "";
        public CarouselCard[] Cards = Array.Empty<CarouselCard>();
        /// <summary>내 차례라서 카드를 누를 수 있음.</summary>
        public bool CanPick;
        /// <summary>모두 골라 "상점으로" 버튼을 보임.</summary>
        public bool Done;
    }

    public struct CarouselCard
    {
        public string Text;
        public bool Taken;
        /// <summary>내가 가져간 카드.</summary>
        public bool Mine;
    }

    /// <summary>
    /// 캐러셀 화면 (L-4, uGUI 코드 생성, 상점 위에 따로 캔버스). 놓인 몬스터 카드 한 줄 · 고르는 순서 · 남은 시간 · 상점으로 버튼.
    /// 규칙은 모른다 — 카드 클릭 · 계속 이벤트만 낸다.
    /// </summary>
    public sealed class CarouselView : MonoBehaviour
    {
        [SerializeField] private Color panelColor = new Color(0.05f, 0.06f, 0.09f, 0.88f);
        [SerializeField] private Color cardColor = new Color(0.18f, 0.2f, 0.26f, 0.95f);
        [SerializeField] private Color cardPickableColor = new Color(0.25f, 0.45f, 0.75f, 0.95f);
        [SerializeField] private Color cardTakenColor = new Color(0.12f, 0.12f, 0.14f, 0.85f);
        [SerializeField] private Color cardMineColor = new Color(0.85f, 0.65f, 0.2f, 0.95f);
        [SerializeField] private Color continueColor = new Color(0.85f, 0.35f, 0.5f);

        public event Action<int> CardClicked;
        public event Action ContinueClicked;

        private Font _font;
        private RectTransform _root;
        private Text _order;
        private Text _status;
        private Text _message;
        private Text _timer;
        private GameObject _continue;
        private readonly List<(Image image, Text text)> _cards = new List<(Image, Text)>();

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        /// <summary>maxCards = 놓일 수 있는 최대 몬스터 수 (로비 인원 + 1).</summary>
        public void Build(int maxCards)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("CarouselCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _root = NewRect("CarouselRoot", (RectTransform)canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Vector2 center = new Vector2(0.5f, 0.5f);
            var panel = NewRect("Panel", _root, center, center, new Vector2(0f, -60f), new Vector2(1880f, 480f));
            panel.gameObject.AddComponent<Image>().color = panelColor;

            NewText("Title", _root, center, new Vector2(0f, 170f), new Vector2(1400f, 50f), 34).text = HudText.CarouselTitle;
            _order = NewText("CarouselOrder", _root, center, new Vector2(0f, 120f), new Vector2(1800f, 40f), 22);
            _status = NewText("CarouselStatus", _root, center, new Vector2(0f, 75f), new Vector2(1200f, 44f), 28);
            _status.color = new Color(1f, 0.9f, 0.5f);

            const float cardW = 190f, gap = 14f;
            float startX = -(maxCards - 1) * (cardW + gap) / 2f;
            for (int i = 0; i < maxCards; i++)
            {
                int c = i;
                _cards.Add(NewButton($"CarouselCard{c}", _root, center, new Vector2(startX + c * (cardW + gap), -60f), new Vector2(cardW, 150f), cardColor, 24, () => CardClicked?.Invoke(c)));
            }

            _message = NewText("CarouselMessage", _root, center, new Vector2(0f, -175f), new Vector2(1200f, 44f), 26);
            _message.color = new Color(1f, 0.75f, 0.6f);
            _timer = NewText("CarouselTimer", _root, center, new Vector2(0f, -235f), new Vector2(200f, 60f), 40);
            _continue = NewButton("CarouselContinue", _root, center, new Vector2(0f, -235f), new Vector2(280f, 80f), continueColor, 30, () => ContinueClicked?.Invoke()).image.gameObject;
            _continue.GetComponentInChildren<Text>().text = HudText.CarouselContinue;
            Hide();
        }

        /// <summary>카드에 마우스를 올리면 정보 창 (내용은 GameRoot가, 빈 칸이면 null).</summary>
        public void SetTooltips(TooltipView tooltip, Func<int, (string, string)?> card)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                int c = i;
                var h = _cards[i].image.gameObject.AddComponent<HoverTarget>();
                h.Tooltip = tooltip;
                h.Provider = () => card(c);
            }
        }

        public void Show(CarouselViewData d)
        {
            _root.gameObject.SetActive(true);
            _order.text = d.Order ?? "";
            _status.text = d.Status ?? "";
            _message.text = d.Message ?? "";
            for (int i = 0; i < _cards.Count; i++)
            {
                var (image, text) = _cards[i];
                bool has = i < d.Cards.Length;
                image.gameObject.SetActive(has);
                if (!has) continue;
                CarouselCard c = d.Cards[i];
                text.text = c.Text;
                image.color = c.Mine ? cardMineColor : c.Taken ? cardTakenColor : d.CanPick ? cardPickableColor : cardColor;
            }
            _continue.SetActive(d.Done);
            if (d.Done) _timer.text = "";
        }

        /// <summary>남은 고르기 시간 (seconds &lt; 0 = 숨김). warn이면 빨갛게.</summary>
        public void SetTimer(int seconds, bool warn)
        {
            _timer.text = seconds < 0 ? "" : string.Format(HudText.PrepTimerFormat, seconds);
            _timer.color = warn ? new Color(1f, 0.35f, 0.3f) : Color.white;
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private (Image image, Text text) NewButton(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, int fontSize, Action onClick)
        {
            var rt = NewRect(name, parent, anchor, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick());
            Text t = NewText("Label", rt, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, fontSize);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.sizeDelta = new Vector2(-10f, -10f);
            return (img, t);
        }

        private Text NewText(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize)
        {
            var rt = NewRect(name, parent, anchor, anchor, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
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
