using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Observation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DivineWorld.Simulation.UI
{
    /// <summary>
    /// Region info panel: ScrollRect / Viewport / Content. Content grows with text;
    /// viewport clips; only this panel scrolls.
    /// </summary>
    public sealed class RegionObservationScrollView : MonoBehaviour
    {
        ObservationHost _host;
        Text _body;
        ScrollRect _scroll;
        RectTransform _panel;
        RectTransform _content;
        string _shownText;
        RegionId? _shownRegion;
        bool _bound;

        public static RegionObservationScrollView Create(ObservationHost host)
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("RegionObservationCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var view = canvasGo.AddComponent<RegionObservationScrollView>();
            view.Build();
            view.Bind(host);
            return view;
        }

        public void Bind(ObservationHost host)
        {
            if (_host != null && _bound)
            {
                _host.Changed -= OnChanged;
                _bound = false;
            }

            _host = host;
            if (_host != null)
            {
                _host.Changed += OnChanged;
                _bound = true;
            }

            Refresh(resetScroll: true);
        }

        public void SetImguiRect(Rect imgui)
        {
            if (_panel == null)
            {
                return;
            }

            _panel.anchorMin = new Vector2(0f, 1f);
            _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.anchoredPosition = new Vector2(imgui.x, -imgui.y);
            _panel.sizeDelta = new Vector2(Mathf.Max(80f, imgui.width), Mathf.Max(80f, imgui.height));
        }

        void OnDestroy()
        {
            if (_host != null && _bound)
            {
                _host.Changed -= OnChanged;
            }
        }

        void OnChanged()
        {
            var regionId = _host != null ? _host.SelectedRegionId : (RegionId?)null;
            bool regionChanged = _shownRegion != regionId;
            Refresh(resetScroll: regionChanged);
        }

        void LateUpdate()
        {
            Refresh(resetScroll: false);
        }

        void Refresh(bool resetScroll)
        {
            if (_body == null)
            {
                return;
            }

            var snap = _host != null ? _host.Current : null;
            var region = _host != null ? _host.SelectedRegion : null;
            string text = ObservationPanelText.FormatRegionPanel(snap, region);
            if (text != _shownText)
            {
                _body.text = text;
                _shownText = text;
                if (_content != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
                }
            }

            _shownRegion = _host != null ? _host.SelectedRegionId : (RegionId?)null;
            if (resetScroll && _scroll != null)
            {
                _scroll.verticalNormalizedPosition = 1f;
            }
        }

        void Build()
        {
            var sprite = WhiteSprite();

            _panel = CreateRect("RegionInfoPanel", transform);
            var panelImg = _panel.gameObject.AddComponent<Image>();
            panelImg.sprite = sprite;
            panelImg.color = new Color(0.07f, 0.08f, 0.11f, 0.94f);
            panelImg.raycastTarget = true;

            var viewport = CreateRect("Viewport", _panel);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(4f, 4f);
            viewport.offsetMax = new Vector2(-20f, -4f);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.sprite = sprite;
            vpImg.color = new Color(1f, 1f, 1f, 0.01f);
            vpImg.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = CreateRect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, 64f);
            var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(8, 8, 8, 16);
            var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("Body");
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.SetParent(_content, false);
            _body = textGo.AddComponent<Text>();
            _body.font = BuiltinFont();
            _body.fontSize = 14;
            _body.color = Color.white;
            _body.alignment = TextAnchor.UpperLeft;
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Overflow;
            _body.raycastTarget = false;
            var le = textGo.AddComponent<LayoutElement>();
            le.minHeight = 32f;

            var sbRt = CreateRect("Scrollbar", _panel);
            sbRt.anchorMin = new Vector2(1f, 0f);
            sbRt.anchorMax = new Vector2(1f, 1f);
            sbRt.pivot = new Vector2(1f, 1f);
            sbRt.anchoredPosition = Vector2.zero;
            sbRt.sizeDelta = new Vector2(16f, 0f);
            var sbBg = sbRt.gameObject.AddComponent<Image>();
            sbBg.sprite = sprite;
            sbBg.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);

            var handleRt = CreateRect("Handle", sbRt);
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = new Vector2(2f, 2f);
            handleRt.offsetMax = new Vector2(-2f, -2f);
            var handleImg = handleRt.gameObject.AddComponent<Image>();
            handleImg.sprite = sprite;
            handleImg.color = new Color(0.62f, 0.64f, 0.7f, 1f);

            var scrollbar = sbRt.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handleRt;
            scrollbar.targetGraphic = handleImg;
            scrollbar.transition = Selectable.Transition.None;

            _scroll = _panel.gameObject.AddComponent<ScrollRect>();
            _scroll.content = _content;
            _scroll.viewport = viewport;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 28f;
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            _scroll.inertia = true;
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static Font BuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static Sprite WhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
    }
}
