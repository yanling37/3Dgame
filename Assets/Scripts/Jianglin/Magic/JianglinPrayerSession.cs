using System.Collections.Generic;
using UnityEngine;

namespace Game.Jianglin
{
    public enum JianglinPrayerKind
    {
        Element = 0,
        Semi = 1,
        Finished = 2
    }

    public enum JianglinPrayerClickResult
    {
        Miss = 0,
        Promoted = 1,
        Exploded = 2,
        Cast = 3
    }

    public sealed class JianglinPrayerToken
    {
        public JianglinPrayerKind Kind;
        public int Ring;
        public float Angle;
        public float Omega;
        public float Fly;
        public JianglinElement Element;
        public int Level = 1;
        public JianglinSpellId Spell;
        public readonly List<JianglinElementCharge> Charges = new List<JianglinElementCharge>(4);

        public bool CanSynthesize => Kind != JianglinPrayerKind.Finished && Fly >= 1f;

        public void SetElement(JianglinElement element, int level)
        {
            Element = element;
            Level = level;
            Charges.Clear();
            Charges.Add(new JianglinElementCharge(element, level));
        }

        public void Absorb(JianglinPrayerToken other)
        {
            Kind = JianglinPrayerKind.Semi;
            for (int i = 0; i < other.Charges.Count; i++)
            {
                JianglinSpellbook.StackCharge(Charges, other.Charges[i]);
            }

            if (Charges.Count > 0)
            {
                Element = Charges[0].Element;
                Level = Charges[0].Level;
            }
        }
    }

    public sealed class JianglinPrayerSession
    {
        public const int MidRing = 1;
        public const int OuterRing = 2;

        public bool HasCore;
        public JianglinElement CoreElement;
        public int CoreLevel = 1;
        public bool Dragging;
        public int DragTokenIndex = -1;
        public readonly List<JianglinPrayerToken> Tokens = new List<JianglinPrayerToken>(12);
        public Vector2 FlashGui;
        public Color FlashColor = Color.white;
        public float FlashUntil;
        public string LastHint = "";
        public float HintUntil;

        Vector2 _dragCursor;
        Vector2 _dragStart;
        Vector2 _dragVel;
        float _dragStartTime;
        int _dragOrigRing;
        float _dragOrigAngle;
        float _dragOrigOmega;

        public Vector2 DragCursor => _dragCursor;
        public Vector2 DragStart => _dragStart;
        public bool DraggingToken => Dragging && DragTokenIndex >= 0;

        public void PressKey(JianglinElement element)
        {
            if (HasCore && CoreElement == element)
            {
                CoreLevel = Mathf.Min(JianglinSpellbook.MaxLevel, CoreLevel + 1);
                return;
            }

            HasCore = true;
            CoreElement = element;
            CoreLevel = 1;
        }

        public bool TryBeginDrag(Vector2 gui)
        {
            if (Dragging)
            {
                return false;
            }

            int hit = HitToken(gui);
            if (hit >= 0)
            {
                var token = Tokens[hit];
                Dragging = true;
                DragTokenIndex = hit;
                _dragStart = gui;
                _dragCursor = gui;
                _dragVel = Vector2.zero;
                _dragStartTime = Time.unscaledTime;
                _dragOrigRing = token.Ring;
                _dragOrigAngle = token.Angle;
                _dragOrigOmega = token.Omega;
                token.Fly = 1f;
                token.Omega = 0f;
                return true;
            }

            if (!HasCore)
            {
                return false;
            }

            if (Vector2.Distance(gui, JianglinMagicLayout.PrayerCenter()) > JianglinMagicLayout.PrayerInner)
            {
                return false;
            }

            Dragging = true;
            DragTokenIndex = -1;
            _dragStart = gui;
            _dragCursor = gui;
            _dragVel = Vector2.zero;
            _dragStartTime = Time.unscaledTime;
            return true;
        }

        public void DragTo(Vector2 gui)
        {
            if (!Dragging)
            {
                return;
            }

            float dt = Mathf.Max(0.008f, Time.unscaledDeltaTime);
            Vector2 inst = (gui - _dragCursor) / dt;
            _dragVel = Vector2.Lerp(_dragVel, inst, 0.4f);
            _dragCursor = gui;

            if (DragTokenIndex < 0 || DragTokenIndex >= Tokens.Count)
            {
                return;
            }

            Vector2 center = JianglinMagicLayout.PrayerCenter();
            Vector2 radial = gui - center;
            if (radial.sqrMagnitude < 1f)
            {
                return;
            }

            var token = Tokens[DragTokenIndex];
            token.Angle = Mathf.Atan2(radial.y, radial.x);
            token.Ring = JianglinMagicLayout.PrayerNearestRing(gui);
            token.Fly = 1f;
        }

        public bool TryRelease(Vector2 gui)
        {
            if (!Dragging)
            {
                return false;
            }

            if (DragTokenIndex >= 0)
            {
                return ReleaseToken(gui);
            }

            return ReleaseCore(gui);
        }

        public void CancelDrag()
        {
            if (Dragging && DragTokenIndex >= 0 && DragTokenIndex < Tokens.Count)
            {
                var token = Tokens[DragTokenIndex];
                token.Ring = _dragOrigRing;
                token.Angle = _dragOrigAngle;
                token.Omega = _dragOrigOmega;
                token.Fly = 1f;
            }

            Dragging = false;
            DragTokenIndex = -1;
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < Tokens.Count; i++)
            {
                if (Dragging && i == DragTokenIndex)
                {
                    continue;
                }

                var token = Tokens[i];
                if (token.Fly < 1f)
                {
                    token.Fly = Mathf.Min(1f, token.Fly + deltaTime * 3.2f);
                }
                else
                {
                    token.Angle += token.Omega * deltaTime;
                }
            }

            ResolveCollisions();
        }

        public JianglinPrayerClickResult TryRightClick(Vector2 gui, out JianglinSpellId spell)
        {
            spell = JianglinSpellId.None;
            int index = HitToken(gui);
            if (index < 0)
            {
                return JianglinPrayerClickResult.Miss;
            }

            var token = Tokens[index];
            Vector2 pos = TokenGui(JianglinMagicLayout.PrayerCenter(), token);
            if (token.Kind == JianglinPrayerKind.Finished)
            {
                spell = token.Spell;
                Tokens.RemoveAt(index);
                SetHint("施放  " + JianglinSpellbook.ShortSpellName(spell));
                return JianglinPrayerClickResult.Cast;
            }

            if (token.Kind != JianglinPrayerKind.Element && token.Kind != JianglinPrayerKind.Semi)
            {
                return JianglinPrayerClickResult.Miss;
            }

            JianglinSpellId resolved = JianglinSpellbook.ResolveUnordered(token.Charges);
            if (resolved == JianglinSpellId.None)
            {
                Tokens.RemoveAt(index);
                Flash(pos, new Color(1f, 0.35f, 0.12f, 0.95f));
                SetHint("锁定失败，爆炸");
                return JianglinPrayerClickResult.Exploded;
            }

            token.Kind = JianglinPrayerKind.Finished;
            token.Spell = resolved;
            token.Omega *= 0.45f;
            Flash(pos, new Color(1f, 0.92f, 0.45f, 0.9f));
            SetHint("锁定  " + JianglinSpellbook.ShortSpellName(resolved) + "  ·  不能再合成  ·  再右键施法");
            return JianglinPrayerClickResult.Promoted;
        }

        public int HitToken(Vector2 gui)
        {
            Vector2 center = JianglinMagicLayout.PrayerCenter();
            float best = JianglinMagicLayout.PrayerTokenRadius;
            int found = -1;
            for (int i = 0; i < Tokens.Count; i++)
            {
                float d = Vector2.Distance(gui, TokenGui(center, Tokens[i]));
                if (d <= best)
                {
                    best = d;
                    found = i;
                }
            }

            return found;
        }

        public static Vector2 TokenGui(Vector2 center, JianglinPrayerToken token)
        {
            float targetR = JianglinMagicLayout.PrayerOrbitRadius(token.Ring);
            float r = Mathf.Lerp(JianglinMagicLayout.PrayerInner * 0.35f, targetR, token.Fly);
            return center + new Vector2(Mathf.Cos(token.Angle), Mathf.Sin(token.Angle)) * r;
        }

        bool ReleaseCore(Vector2 gui)
        {
            Dragging = false;
            DragTokenIndex = -1;
            _dragCursor = gui;
            int ring = JianglinMagicLayout.PrayerRingAt(gui);
            if (ring < MidRing || !HasCore)
            {
                return false;
            }

            Vector2 center = JianglinMagicLayout.PrayerCenter();
            Vector2 radial = gui - center;
            if (radial.sqrMagnitude < 1f)
            {
                return false;
            }

            float omega;
            float angle;
            ComputeOrbit(gui, ring, out angle, out omega);
            var token = new JianglinPrayerToken
            {
                Kind = JianglinPrayerKind.Element,
                Ring = ring,
                Angle = angle,
                Omega = omega,
                Fly = 0f
            };
            token.SetElement(CoreElement, CoreLevel);
            Tokens.Add(token);
            HasCore = false;
            CoreLevel = 1;
            return true;
        }

        bool ReleaseToken(Vector2 gui)
        {
            int index = DragTokenIndex;
            Dragging = false;
            DragTokenIndex = -1;
            _dragCursor = gui;
            if (index < 0 || index >= Tokens.Count)
            {
                return false;
            }

            var token = Tokens[index];
            Vector2 center = JianglinMagicLayout.PrayerCenter();
            float dist = Vector2.Distance(gui, center);
            if (dist < JianglinMagicLayout.PrayerInner)
            {
                token.Ring = _dragOrigRing;
                token.Angle = _dragOrigAngle;
                token.Omega = _dragOrigOmega;
                token.Fly = 1f;
                return false;
            }

            float elapsed = Mathf.Max(0.016f, Time.unscaledTime - _dragStartTime);
            float dragDist = Vector2.Distance(gui, _dragStart);
            if (dragDist < 14f && elapsed < 0.22f)
            {
                token.Ring = _dragOrigRing;
                token.Angle = _dragOrigAngle;
                token.Omega = _dragOrigOmega == 0f ? 0.85f : -_dragOrigOmega;
                token.Fly = 1f;
                SetHint("反向旋转");
                return true;
            }

            int ring = JianglinMagicLayout.PrayerRingAt(gui);
            if (ring < MidRing)
            {
                ring = JianglinMagicLayout.PrayerNearestRing(gui);
            }

            float angle;
            float omega;
            ComputeOrbit(gui, ring, out angle, out omega);
            token.Ring = ring;
            token.Angle = angle;
            token.Fly = 1f;

            Vector2 radial = (gui - center).normalized;
            Vector2 tangent = new Vector2(-radial.y, radial.x);
            Vector2 vel = Vector2.Lerp(_dragVel, (gui - _dragStart) / elapsed, 0.45f);
            float tangentSpeed = Vector2.Dot(vel, tangent);
            if (Mathf.Abs(tangentSpeed) > 48f)
            {
                token.Omega = omega;
            }
            else
            {
                token.Omega = _dragOrigOmega;
            }

            return true;
        }

        void ComputeOrbit(Vector2 gui, int ring, out float angle, out float omega)
        {
            Vector2 center = JianglinMagicLayout.PrayerCenter();
            Vector2 radial = gui - center;
            angle = Mathf.Atan2(radial.y, radial.x);
            if (radial.sqrMagnitude < 1f)
            {
                omega = 0.85f;
                return;
            }

            radial.Normalize();
            Vector2 tangent = new Vector2(-radial.y, radial.x);
            float elapsed = Mathf.Max(0.05f, Time.unscaledTime - _dragStartTime);
            Vector2 avg = (gui - _dragStart) / elapsed;
            Vector2 vel = Vector2.Lerp(_dragVel, avg, 0.45f);
            float radius = JianglinMagicLayout.PrayerOrbitRadius(ring);
            omega = Vector2.Dot(vel, tangent) / Mathf.Max(28f, radius);
            omega = Mathf.Clamp(omega, -10f, 10f);
            if (Mathf.Abs(omega) < 0.4f)
            {
                omega = 0.85f * (omega < 0f ? -1f : 1f);
            }
        }

        void Flash(Vector2 gui, Color color)
        {
            FlashGui = gui;
            FlashColor = color;
            FlashUntil = Time.unscaledTime + 0.28f;
        }

        void SetHint(string text)
        {
            LastHint = text;
            HintUntil = Time.unscaledTime + 2.4f;
        }

        void ResolveCollisions()
        {
            for (int i = 0; i < Tokens.Count; i++)
            {
                if (Dragging && i == DragTokenIndex)
                {
                    continue;
                }

                var a = Tokens[i];
                if (!a.CanSynthesize)
                {
                    continue;
                }

                for (int j = i + 1; j < Tokens.Count; j++)
                {
                    if (Dragging && j == DragTokenIndex)
                    {
                        continue;
                    }

                    var b = Tokens[j];
                    if (!b.CanSynthesize || a.Ring != b.Ring)
                    {
                        continue;
                    }

                    float delta = Mathf.Abs(Mathf.DeltaAngle(a.Angle * Mathf.Rad2Deg, b.Angle * Mathf.Rad2Deg));
                    float radius = JianglinMagicLayout.PrayerOrbitRadius(a.Ring);
                    float need = (JianglinMagicLayout.PrayerTokenRadius * 2.15f / radius) * Mathf.Rad2Deg;
                    if (delta > need)
                    {
                        continue;
                    }

                    Vector2 dirA = new Vector2(Mathf.Cos(a.Angle), Mathf.Sin(a.Angle));
                    Vector2 dirB = new Vector2(Mathf.Cos(b.Angle), Mathf.Sin(b.Angle));
                    Vector2 merged = dirA + dirB;
                    a.Absorb(b);
                    a.Angle = merged.sqrMagnitude < 0.0001f
                        ? a.Angle
                        : Mathf.Atan2(merged.y, merged.x);
                    a.Omega = (a.Omega + b.Omega) * 0.5f;
                    a.Fly = 1f;
                    Tokens.RemoveAt(j);
                    SetHint("合成  " + JianglinSpellbook.ChargeText(a.Charges) + "  ·  可继续撞  ·  右键锁定");
                    return;
                }
            }
        }
    }
}
