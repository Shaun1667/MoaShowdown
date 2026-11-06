using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Moa.Showdown.Game.Ui
{
    /// <summary>
    /// 마우스를 올린 대상의 정보 창 하나 (맨 위 캔버스). 마우스를 따라다니고 화면 밖으로 나가지 않는다.
    /// 누가 띄웠는지(owner)를 기억해서, 그 owner가 내릴 때만 사라진다 (UI 위 · 3D 몬스터 위 판정이 서로 지우지 않게).
    /// </summary>
    public sealed class TooltipView : MonoBehaviour
    {
        [SerializeField] private Color panelColor = new Color(0.04f, 0.05f, 0.07f, 0.95f);
        [SerializeField] private Vector2 cursorOffset = new Vector2(24f, -24f);

        private RectTransform _canvasRect;
        private RectTransform _panel;
        private Text _title;
        private Text _body;
        private object _owner;

        public bool IsShowing => _panel != null && _panel.gameObject.activeSelf;
        public object Owner => _owner;
        public string CurrentTitle => _title != null ? _title.text : "";
        public string CurrentBody => _body != null ? _body.text : "";

        public void Build()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("TooltipCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)canvasGo.transform;

            _panel = new GameObject("Tooltip", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            _panel.SetParent(_canvasRect, false);
            _panel.anchorMin = _panel.anchorMax = Vector2.zero;
            _panel.pivot = new Vector2(0f, 1f);
            var img = _panel.GetComponent<Image>();
            img.color = panelColor;
            img.raycastTarget = false; // 정보 창이 마우스를 가로채면 아래 대상의 '내림'이 일어나 깜빡인다
            var layout = _panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = _panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _title = NewText("Title", font, 26, new Color(1f, 0.85f, 0.4f));
            _body = NewText("Body", font, 22, Color.white);
            _panel.gameObject.SetActive(false);
        }

        public void Show(object owner, string title, string body)
        {
            _owner = owner;
            _title.text = title ?? "";
            _body.text = body ?? "";
            _panel.gameObject.SetActive(true);
            Follow();
        }

        /// <summary>owner가 지금 띄운 주인일 때만 숨긴다.</summary>
        public void Hide(object owner)
        {
            if (!ReferenceEquals(owner, _owner)) return;
            _owner = null;
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        public void HideAll()
        {
            _owner = null;
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (IsShowing) Follow();
        }

        private void Follow()
        {
            if (Mouse.current == null) return;
            Vector2 screen = Mouse.current.position.ReadValue();
            float scale = _canvasRect.lossyScale.x > 0f ? _canvasRect.lossyScale.x : 1f;
            Vector2 pos = screen / scale + cursorOffset;
            Vector2 size = _panel.rect.size;
            Vector2 canvas = _canvasRect.rect.size;
            // 오른쪽 · 아래로 넘치면 커서 반대쪽으로
            if (pos.x + size.x > canvas.x) pos.x = screen.x / scale - cursorOffset.x - size.x;
            if (pos.y - size.y < 0f) pos.y = screen.y / scale - cursorOffset.y + size.y;
            pos.x = Mathf.Clamp(pos.x, 0f, Mathf.Max(0f, canvas.x - size.x));
            pos.y = Mathf.Clamp(pos.y, size.y, canvas.y);
            _panel.anchoredPosition = pos;
        }

        private Text NewText(string name, Font font, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_panel, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }

    /// <summary>
    /// UI 요소에 붙이면 마우스를 올릴 때 정보 창을 띄우고 내리면 지운다. 내용은 띄우는 순간 provider에서 받는다 (최신 값).
    /// provider가 null을 주면 아무것도 띄우지 않는다 (빈 칸 등).
    /// </summary>
    public sealed class HoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public TooltipView Tooltip;
        public Func<(string title, string body)?> Provider;

        public void OnPointerEnter(PointerEventData eventData) => ShowNow();

        public void OnPointerExit(PointerEventData eventData) => Tooltip?.Hide(this);

        private void OnDisable() => Tooltip?.Hide(this);

        /// <summary>마우스가 올라간 것과 같은 동작 (확인 · 테스트용 진입점).</summary>
        public void ShowNow()
        {
            if (Tooltip == null || Provider == null) return;
            var info = Provider();
            if (info == null) Tooltip.Hide(this);
            else Tooltip.Show(this, info.Value.title, info.Value.body);
        }
    }
}
