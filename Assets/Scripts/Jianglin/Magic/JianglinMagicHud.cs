using UnityEngine;

namespace Game.Jianglin
{
    public static class JianglinMagicLayout
    {
        public const float ManaRadius = 52f;
        public const float ElementOrbit = 132f;
        public const float ElementRadius = 38f;
        /// <summary>Casting cluster X as a fraction of screen width. 0.50 is center; 0.72 is ~22% to the right.</summary>
        public const float CastClusterX = 0.72f;
        public const float PrayerInner = 44f;
        public const float PrayerMid = 108f;
        public const float PrayerOuter = 168f;
        public const float PrayerBand = 30f;
        public const float PrayerTokenRadius = 20f;

        public static Rect ModePanelRect => new Rect(10f, 140f, 220f, 178f);

        public static Rect ModeRow(int index)
        {
            return new Rect(20f, 152f + index * 38f, 200f, 34f);
        }

        public static Vector2 MouseToGui(Vector3 mouse)
        {
            return new Vector2(mouse.x, Screen.height - mouse.y);
        }

        public static Vector2 ManaGuiCenter()
        {
            float pad = ElementOrbit + ElementRadius + 16f;
            float x = Mathf.Clamp(Screen.width * CastClusterX, pad, Screen.width - pad);
            return new Vector2(x, Screen.height * 0.52f);
        }

        public static Rect RecipeHintRect()
        {
            Vector2 center = ManaGuiCenter();
            return new Rect(center.x - 230f, center.y + 190f, 460f, 52f);
        }

        public static Rect LockedSpellBannerRect()
        {
            Vector2 center = ManaGuiCenter();
            return new Rect(center.x - 180f, 86f, 360f, 36f);
        }

        public static Vector2 ElementGuiCenter(JianglinElement element)
        {
            Vector2 center = ManaGuiCenter();
            switch (element)
            {
                case JianglinElement.Fire:
                    return center + new Vector2(0f, -ElementOrbit);
                case JianglinElement.Wind:
                    return center + new Vector2(-ElementOrbit, 0f);
                case JianglinElement.Earth:
                    return center + new Vector2(ElementOrbit, 0f);
                case JianglinElement.Water:
                    return center + new Vector2(0f, ElementOrbit);
                default:
                    return center;
            }
        }

        public static bool HitMana(Vector2 guiPoint)
        {
            return Vector2.Distance(guiPoint, ManaGuiCenter()) <= ManaRadius;
        }

        public static bool HitElement(Vector2 guiPoint, out JianglinElement element)
        {
            JianglinElement[] all =
            {
                JianglinElement.Wind,
                JianglinElement.Fire,
                JianglinElement.Earth,
                JianglinElement.Water
            };

            for (int i = 0; i < all.Length; i++)
            {
                if (Vector2.Distance(guiPoint, ElementGuiCenter(all[i])) <= ElementRadius)
                {
                    element = all[i];
                    return true;
                }
            }

            element = JianglinElement.Wind;
            return false;
        }

        public static Rect CircleRect(Vector2 guiCenter, float radius)
        {
            float size = radius * 2f;
            return new Rect(guiCenter.x - radius, guiCenter.y - radius, size, size);
        }

        public static Vector2 PrayerCenter()
        {
            float pad = PrayerOuter + PrayerTokenRadius + 20f;
            float x = Mathf.Clamp(Screen.width * 0.78f, pad, Screen.width - pad);
            return new Vector2(x, Screen.height * 0.52f);
        }

        public static Rect PrayerHintRect()
        {
            Vector2 center = PrayerCenter();
            return new Rect(center.x - 210f, center.y + PrayerOuter + 18f, 420f, 122f);
        }

        public static float PrayerOrbitRadius(int ring)
        {
            return ring >= 2 ? PrayerOuter : PrayerMid;
        }

        public static int PrayerRingAt(Vector2 guiPoint)
        {
            float d = Vector2.Distance(guiPoint, PrayerCenter());
            if (Mathf.Abs(d - PrayerMid) <= PrayerBand)
            {
                return 1;
            }

            if (Mathf.Abs(d - PrayerOuter) <= PrayerBand)
            {
                return 2;
            }

            return -1;
        }

        public static int PrayerNearestRing(Vector2 guiPoint)
        {
            float d = Vector2.Distance(guiPoint, PrayerCenter());
            float midSplit = (PrayerMid + PrayerOuter) * 0.5f;
            return d < midSplit ? 1 : 2;
        }
    }

    public class JianglinMagicHud : MonoBehaviour
    {
        JianglinMagicController _magic;
        Texture2D _circle;
        Texture2D _ring;
        GUIStyle _centerLabel;
        GUIStyle _smallLabel;
        GUIStyle _tinyLabel;

        public void Bind(JianglinMagicController magic)
        {
            _magic = magic;
        }

        void OnGUI()
        {
            if (_magic == null)
            {
                return;
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab)
            {
                Event.current.Use();
            }

            EnsureStyles();
            DrawCombatOverlay();
            DrawModeChrome();

            if (_magic.ModeWheelOpen)
            {
                DrawModeWheel();
            }

            if (_magic.Mode == JianglinPlayerMode.Casting && !_magic.ModeWheelOpen)
            {
                DrawCasting();
            }
            else if (_magic.Mode == JianglinPlayerMode.Prayer && !_magic.ModeWheelOpen)
            {
                DrawPrayer();
            }
            else if (_magic.Mode == JianglinPlayerMode.Design && !_magic.ModeWheelOpen)
            {
                DrawStub("设计模式", "配方编辑尚未接入。短按 Tab 切回上一个模式。");
            }

            if (_magic.LookEnabled)
            {
                DrawCrosshair();
            }
        }

        void DrawCombatOverlay()
        {
            var playerHealth = FindPlayerHealth();
            if (playerHealth != null)
            {
                DrawHpBar(new Rect(Screen.width * 0.5f - 110f, Screen.height - 36f, 220f, 18f), playerHealth, "你");
            }

            var targeting = _magic.Targeting;
            if (targeting == null || targeting.Locked == null || targeting.Locked.IsDead)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 sp = cam.WorldToScreenPoint(targeting.Locked.AimPoint);
            if (sp.z < 0.2f)
            {
                return;
            }

            Vector2 gui = JianglinMagicLayout.MouseToGui(sp);
            string lockLabel = targeting.FollowView ? "跟随" : "锁定";
            DrawHpBar(new Rect(gui.x - 48f, gui.y - 58f, 96f, 16f), targeting.Locked, lockLabel);
            GUI.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            GUI.Box(new Rect(gui.x - 22f, gui.y - 22f, 44f, 44f), GUIContent.none);
            GUI.color = Color.white;
        }

        static JianglinHealth FindPlayerHealth()
        {
            var player = FindObjectOfType<JianglinPlayerController>();
            return player != null ? player.GetComponent<JianglinHealth>() : null;
        }

        void DrawHpBar(Rect rect, JianglinHealth health, string label)
        {
            GUI.Box(rect, GUIContent.none);
            float ratio = health.MaxHp > 0f ? Mathf.Clamp01(health.Hp / health.MaxHp) : 0f;
            GUI.color = health.IsDead ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.82f, 0.18f, 0.16f);
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * ratio, rect.height - 4f), Texture2D.whiteTexture);
            if (health.Shield > 0.5f)
            {
                GUI.color = new Color(0.95f, 0.85f, 0.25f, 0.7f);
                float shieldW = Mathf.Clamp01(health.Shield / 55f) * (rect.width - 4f);
                GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, shieldW, 4f), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
            GUI.Label(rect, label + "  " + Mathf.CeilToInt(health.Hp) + "/" + Mathf.CeilToInt(health.MaxHp), _centerLabel);
        }

        void DrawModeChrome()
        {
            var session = _magic.Session;
            string line = JianglinSpellbook.ModeName(_magic.Mode)
                          + "  魔力 "
                          + Mathf.RoundToInt(session.Mana)
                          + "/"
                          + Mathf.RoundToInt(session.ManaMax);
            GUI.Label(new Rect(14f, 118f, 360f, 20f), line, _smallLabel);
        }

        void DrawModeWheel()
        {
            GUI.Box(JianglinMagicLayout.ModePanelRect, "长按 Tab 选模式 · 短按切回");
            var modes = JianglinMagicController.Modes;
            for (int i = 0; i < modes.Length; i++)
            {
                Rect row = JianglinMagicLayout.ModeRow(i);
                bool hover = i == _magic.HoveredModeIndex;
                GUI.color = hover ? new Color(1f, 0.92f, 0.55f) : Color.white;
                GUI.Box(row, (i + 1) + "  " + JianglinSpellbook.ModeName(modes[i]));
                GUI.color = Color.white;
            }
        }

        void DrawCasting()
        {
            var session = _magic.Session;
            if (session.Locked)
            {
                string title = session.IsReadyToFire
                    ? JianglinSpellbook.CastHint(session.Resolved)
                    : "未知配方  ·  右键 / R 重做";
                DrawCenterBanner(title);
                return;
            }

            GUI.color = new Color(0f, 0f, 0f, 0.22f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            DrawCircle(JianglinMagicLayout.ManaGuiCenter(), JianglinMagicLayout.ManaRadius, new Color(0.08f, 0.08f, 0.1f, 0.92f));
            GUI.Label(
                JianglinMagicLayout.CircleRect(JianglinMagicLayout.ManaGuiCenter(), JianglinMagicLayout.ManaRadius),
                "魔力\n" + Mathf.RoundToInt(session.Mana) + "/" + Mathf.RoundToInt(session.ManaMax),
                _centerLabel);

            DrawElement(JianglinElement.Wind, new Color(0.28f, 0.78f, 0.38f));
            DrawElement(JianglinElement.Fire, new Color(0.92f, 0.28f, 0.18f));
            DrawElement(JianglinElement.Earth, new Color(0.82f, 0.68f, 0.22f));
            DrawElement(JianglinElement.Water, new Color(0.25f, 0.55f, 0.92f));

            if (session.Dragging)
            {
                DrawDragLine(
                    JianglinMagicLayout.ManaGuiCenter(),
                    JianglinMagicLayout.MouseToGui(Input.mousePosition),
                    new Color(0.95f, 0.9f, 0.55f, 0.9f));
            }

            string preview = session.Recipe.Count == 0
                ? ""
                : "  ·  " + JianglinSpellbook.SpellName(JianglinSpellbook.Resolve(session.Recipe));
            string hint = "拖魔力到元素  ·  滚轮等级 " + session.DraftLevel + "  ·  Ctrl 锁定\n配方  "
                          + session.RecipeText()
                          + preview
                          + "  ·  右键/R 取消";
            GUI.Box(JianglinMagicLayout.RecipeHintRect(), hint);
            GUI.Box(
                new Rect(JianglinMagicLayout.RecipeHintRect().x, JianglinMagicLayout.RecipeHintRect().y + 56f, 460f, 58f),
                JianglinSpellbook.RecipeSheet());
        }

        void DrawElement(JianglinElement element, Color color)
        {
            Vector2 center = JianglinMagicLayout.ElementGuiCenter(element);
            var session = _magic.Session;
            int stacked = 0;
            for (int i = 0; i < session.Recipe.Count; i++)
            {
                if (session.Recipe[i].Element == element)
                {
                    stacked += session.Recipe[i].Level;
                }
            }

            float glow = stacked > 0 ? 1f : 0.55f;
            color.a = glow;
            DrawCircle(center, JianglinMagicLayout.ElementRadius, color);

            string label = JianglinSpellbook.ElementName(element);
            if (stacked > 0)
            {
                label += " " + stacked;
            }

            GUI.Label(JianglinMagicLayout.CircleRect(center, JianglinMagicLayout.ElementRadius), label, _centerLabel);
        }

        void DrawPrayer()
        {
            var prayer = _magic.Prayer;
            Vector2 center = JianglinMagicLayout.PrayerCenter();
            Vector2 mouse = JianglinMagicLayout.MouseToGui(Input.mousePosition);
            int hoverRing = -1;
            if (prayer.Dragging)
            {
                hoverRing = JianglinMagicLayout.PrayerRingAt(mouse);
                if (hoverRing < 0 && prayer.DraggingToken)
                {
                    hoverRing = JianglinMagicLayout.PrayerNearestRing(mouse);
                }
            }

            GUI.color = new Color(0f, 0f, 0f, 0.18f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            DrawRing(center, JianglinMagicLayout.PrayerInner, hoverRing < 0
                ? new Color(0.85f, 0.8f, 0.55f, 0.7f)
                : new Color(0.55f, 0.55f, 0.5f, 0.4f));
            DrawRing(
                center,
                JianglinMagicLayout.PrayerMid,
                hoverRing == 1 ? new Color(1f, 0.92f, 0.45f, 0.95f) : new Color(0.78f, 0.76f, 0.62f, 0.7f));
            DrawRing(
                center,
                JianglinMagicLayout.PrayerOuter,
                hoverRing == 2 ? new Color(1f, 0.92f, 0.45f, 0.95f) : new Color(0.78f, 0.76f, 0.62f, 0.7f));

            Color well = new Color(0.08f, 0.08f, 0.1f, 0.88f);
            if (prayer.HasCore)
            {
                well = JianglinSpellbook.ElementColor(prayer.CoreElement);
                well.a = 0.92f;
            }

            DrawCircle(center, JianglinMagicLayout.PrayerInner - 8f, well);
            string coreLabel = prayer.HasCore
                ? JianglinSpellbook.ElementName(prayer.CoreElement) + prayer.CoreLevel
                : "源";
            GUI.Label(
                JianglinMagicLayout.CircleRect(center, JianglinMagicLayout.PrayerInner - 8f),
                coreLabel,
                _centerLabel);

            for (int i = 0; i < prayer.Tokens.Count; i++)
            {
                DrawPrayerToken(center, prayer.Tokens[i]);
            }

            if (prayer.Dragging)
            {
                Vector2 from = prayer.DraggingToken ? prayer.DragStart : center;
                DrawDragLine(from, mouse, new Color(0.95f, 0.9f, 0.55f, 0.9f));
            }

            if (Time.unscaledTime < prayer.FlashUntil)
            {
                float t = Mathf.Clamp01((prayer.FlashUntil - Time.unscaledTime) / 0.28f);
                Color flash = prayer.FlashColor;
                flash.a *= t;
                DrawCircle(prayer.FlashGui, 18f + (1f - t) * 22f, flash);
            }

            string live = Time.unscaledTime < prayer.HintUntil ? prayer.LastHint : "";
            GUI.Box(
                JianglinMagicLayout.PrayerHintRect(),
                "1风 2火 3土 4水 注入源（同源加强 / 异源替换）\n"
                + "左键拖源入圈  ·  点环上物可移动/轻点反向/甩改转速\n"
                + "同层相撞可继续合成  ·  右键元素或半成品锁定（锁定后不能再合成）\n"
                + "锁定失败则爆  ·  再右键施法"
                + (string.IsNullOrEmpty(live) ? "" : "\n" + live));
        }

        void DrawPrayerToken(Vector2 center, JianglinPrayerToken token)
        {
            Vector2 pos = JianglinPrayerSession.TokenGui(center, token);
            float radius = JianglinMagicLayout.PrayerTokenRadius;
            if (token.Kind == JianglinPrayerKind.Finished)
            {
                DrawRing(pos, radius + 6f, new Color(1f, 0.88f, 0.35f, 0.95f));
                DrawCircle(pos, radius, new Color(0.18f, 0.16f, 0.08f, 0.92f));
                GUI.Label(
                    JianglinMagicLayout.CircleRect(pos, radius),
                    JianglinSpellbook.ShortSpellName(token.Spell),
                    _tinyLabel);
                return;
            }

            if (token.Kind == JianglinPrayerKind.Semi)
            {
                DrawSemiCharges(pos, radius, token.Charges);
                GUI.Label(
                    JianglinMagicLayout.CircleRect(pos, radius + 4f),
                    JianglinSpellbook.ChargeText(token.Charges),
                    _tinyLabel);
                return;
            }

            Color color = JianglinSpellbook.ElementColor(token.Element);
            color.a = 0.95f;
            DrawCircle(pos, radius, color);
            GUI.Label(
                JianglinMagicLayout.CircleRect(pos, radius),
                JianglinSpellbook.ElementName(token.Element) + token.Level,
                _tinyLabel);
        }

        void DrawSemiCharges(Vector2 pos, float radius, System.Collections.Generic.IList<JianglinElementCharge> charges)
        {
            int count = charges == null ? 0 : charges.Count;
            if (count <= 0)
            {
                return;
            }

            if (count == 1)
            {
                Color color = JianglinSpellbook.ElementColor(charges[0].Element);
                color.a = 0.95f;
                DrawCircle(pos, radius, color);
                return;
            }

            float spread = Mathf.Min(10f, 6f + count * 1.5f);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1) - 0.5f;
                Color color = JianglinSpellbook.ElementColor(charges[i].Element);
                color.a = 0.95f;
                DrawCircle(pos + new Vector2(t * spread * 2f, 0f), radius * 0.7f, color);
            }
        }

        void DrawStub(string title, string body)
        {
            GUI.Box(new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.42f, 360f, 72f), title + "\n" + body);
        }

        void DrawCenterBanner(string text)
        {
            GUI.Box(JianglinMagicLayout.LockedSpellBannerRect(), text);
        }

        void DrawCrosshair()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(cx - 8f, cy - 1f, 16f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 8f, 2f, 16f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawCircle(Vector2 guiCenter, float radius, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(JianglinMagicLayout.CircleRect(guiCenter, radius), _circle);
            GUI.color = Color.white;
        }

        void DrawRing(Vector2 guiCenter, float radius, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(JianglinMagicLayout.CircleRect(guiCenter, radius), _ring);
            GUI.color = Color.white;
        }

        static void DrawDragLine(Vector2 fromGui, Vector2 toGui, Color color)
        {
            Vector2 delta = toGui - fromGui;
            float length = delta.magnitude;
            if (length < 1f)
            {
                return;
            }

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, fromGui);
            GUI.color = color;
            GUI.DrawTexture(new Rect(fromGui.x, fromGui.y - 2f, length, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.matrix = old;
        }

        void EnsureStyles()
        {
            if (_circle == null)
            {
                _circle = CreateCircleTexture(64);
            }

            if (_ring == null)
            {
                _ring = CreateRingTexture(128);
            }

            if (_centerLabel == null)
            {
                _centerLabel = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    wordWrap = true
                };
                _centerLabel.normal.textColor = Color.white;
            }

            if (_smallLabel == null)
            {
                _smallLabel = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12
                };
                _smallLabel.normal.textColor = new Color(0.92f, 0.92f, 0.86f);
            }

            if (_tinyLabel == null)
            {
                _tinyLabel = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    wordWrap = true
                };
                _tinyLabel.normal.textColor = Color.white;
            }
        }

        static Texture2D CreateCircleTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            float r = (size - 1) * 0.5f;
            float inner = r - 1.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r;
                    float dy = y - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(inner - d + 1f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            texture.Apply();
            return texture;
        }

        static Texture2D CreateRingTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            float r = (size - 1) * 0.5f;
            float thickness = 3.2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r;
                    float dy = y - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 1f - Mathf.Abs(d - (r - thickness)) / thickness;
                    texture.SetPixel(x, y, new Color(1f, 1f, 0.95f, Mathf.Clamp01(a)));
                }
            }

            texture.Apply();
            return texture;
        }

        void OnDestroy()
        {
            if (_circle != null)
            {
                Destroy(_circle);
            }

            if (_ring != null)
            {
                Destroy(_ring);
            }
        }
    }
}
