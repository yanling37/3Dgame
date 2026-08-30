using UnityEngine;

namespace Game.Jianglin
{
    /// <summary>
    /// Tab mode wheel, conversion drag, Ctrl lock, click-to-fire. Owns cursor/look gating.
    /// </summary>
    public class JianglinMagicController : MonoBehaviour
    {
        public const float TabHoldSeconds = 0.28f;

        static readonly JianglinPlayerMode[] ModeOrder =
        {
            JianglinPlayerMode.Casting,
            JianglinPlayerMode.Prayer,
            JianglinPlayerMode.General,
            JianglinPlayerMode.Design
        };

        JianglinCameraController _camera;
        JianglinSpellCaster _caster;
        JianglinTargeting _targeting;
        JianglinPlayerMode _altMode = JianglinPlayerMode.Casting;
        float _tabHold;
        bool _modeWheelOpen;
        int _hoveredModeIndex;
        bool _channeling;

        public JianglinPlayerMode Mode { get; private set; } = JianglinPlayerMode.General;
        public JianglinCastingSession Session { get; } = new JianglinCastingSession();
        public JianglinPrayerSession Prayer { get; } = new JianglinPrayerSession();
        public JianglinTargeting Targeting => _targeting;
        public bool ModeWheelOpen => _modeWheelOpen;
        public int HoveredModeIndex => _hoveredModeIndex;
        public static JianglinPlayerMode[] Modes => ModeOrder;

        public bool LookEnabled
        {
            get
            {
                if (_modeWheelOpen)
                {
                    return false;
                }

                if (Mode == JianglinPlayerMode.General)
                {
                    return true;
                }

                if (Mode == JianglinPlayerMode.Casting && Session.Locked)
                {
                    return true;
                }

                return false;
            }
        }

        public void Bind(
            JianglinCameraController camera,
            Collider ownerCollider,
            JianglinPlayerController player,
            JianglinTargeting targeting)
        {
            _camera = camera;
            _targeting = targeting;
            _caster = gameObject.GetComponent<JianglinSpellCaster>();
            if (_caster == null)
            {
                _caster = gameObject.AddComponent<JianglinSpellCaster>();
            }

            _caster.Bind(camera, ownerCollider, player, targeting);
            ApplyCursorAndLook();
        }

        void Update()
        {
            HandleModeWheel();
            HandleTargeting();
            Session.Tick(Time.deltaTime);
            if (_targeting != null)
            {
                _targeting.ClearIfInvalid();
            }

            ApplyLockCamera();
            ApplyCursorAndLook();

            if (_modeWheelOpen)
            {
                Prayer.CancelDrag();
                return;
            }

            if (Mode == JianglinPlayerMode.Casting)
            {
                HandleCasting();
            }
            else if (Mode == JianglinPlayerMode.Prayer)
            {
                HandlePrayer();
            }

            if (Input.GetKeyDown(KeyCode.Escape) && Mode != JianglinPlayerMode.General)
            {
                StopChannel(false);
                SetMode(JianglinPlayerMode.General, true);
            }
        }

        void OnDestroy()
        {
            if (_caster != null)
            {
                _caster.EndChannel();
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (_camera != null)
            {
                _camera.LookEnabled = true;
            }
        }

        void HandleModeWheel()
        {
            if (Input.GetKey(KeyCode.Tab))
            {
                _tabHold += Time.deltaTime;
                if (_tabHold >= TabHoldSeconds)
                {
                    _modeWheelOpen = true;
                    _hoveredModeIndex = HoveredModeFromMouse();
                    for (int i = 0; i < ModeOrder.Length; i++)
                    {
                        if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                        {
                            SetMode(ModeOrder[i], true);
                            CloseModeWheel();
                            return;
                        }
                    }
                }
            }

            if (Input.GetKeyUp(KeyCode.Tab))
            {
                if (_modeWheelOpen)
                {
                    int index = HoveredModeFromMouse();
                    if (index >= 0)
                    {
                        SetMode(ModeOrder[index], true);
                    }
                }
                else
                {
                    SetMode(_altMode, false);
                }

                CloseModeWheel();
            }
        }

        void CloseModeWheel()
        {
            _modeWheelOpen = false;
            _tabHold = 0f;
        }

        int HoveredModeFromMouse()
        {
            Vector2 gui = JianglinMagicLayout.MouseToGui(Input.mousePosition);
            for (int i = 0; i < ModeOrder.Length; i++)
            {
                if (JianglinMagicLayout.ModeRow(i).Contains(gui))
                {
                    return i;
                }
            }

            return _modeWheelOpen ? _hoveredModeIndex : -1;
        }

        void SetMode(JianglinPlayerMode mode, bool refundCasting)
        {
            if (Mode == JianglinPlayerMode.Casting && mode != JianglinPlayerMode.Casting)
            {
                StopChannel(false);
                if (refundCasting)
                {
                    Session.CancelAndRefund();
                }
            }

            if (Mode == JianglinPlayerMode.Prayer && mode != JianglinPlayerMode.Prayer)
            {
                Prayer.CancelDrag();
            }

            if (mode != Mode)
            {
                _altMode = Mode;
                Mode = mode;
            }
        }

        void HandleTargeting()
        {
            if (_targeting == null || _modeWheelOpen)
            {
                return;
            }

            // Side mouse buttons replace Q / E: 3 = lock/follow/cancel, 4 = cycle target.
            if (Input.GetMouseButtonDown(3) || Input.GetKeyDown(KeyCode.Mouse3))
            {
                _targeting.AdvanceLock();
            }

            if (Input.GetMouseButtonDown(4) || Input.GetKeyDown(KeyCode.Mouse4))
            {
                _targeting.Cycle(1);
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                _targeting.AdvanceLock();
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                _targeting.Cycle(1);
            }
        }

        void ApplyLockCamera()
        {
            if (_camera == null)
            {
                return;
            }

            Transform follow = null;
            if (_targeting != null
                && _targeting.FollowView
                && _targeting.Locked != null
                && !_targeting.Locked.IsDead)
            {
                follow = _targeting.Locked.transform;
            }

            _camera.SetFollowTarget(follow);
        }

        void HandlePrayer()
        {
            Prayer.Tick(Time.deltaTime);
            Vector2 gui = JianglinMagicLayout.MouseToGui(Input.mousePosition);

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                TryInjectPrayer(JianglinElement.Wind);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                TryInjectPrayer(JianglinElement.Fire);
            }

            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                TryInjectPrayer(JianglinElement.Earth);
            }

            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                TryInjectPrayer(JianglinElement.Water);
            }

            if (Input.GetMouseButtonDown(0))
            {
                Prayer.TryBeginDrag(gui);
            }

            if (Prayer.Dragging)
            {
                Prayer.DragTo(gui);
                if (!Input.GetMouseButton(0))
                {
                    Prayer.TryRelease(gui);
                }
            }

            if (Input.GetMouseButtonDown(1) && !Prayer.Dragging)
            {
                JianglinSpellId spell;
                if (Prayer.TryRightClick(gui, out spell) == JianglinPrayerClickResult.Cast
                    && _caster != null)
                {
                    _caster.TryCastBorrowed(spell);
                }
            }
        }

        void TryInjectPrayer(JianglinElement element)
        {
            if (Prayer.HasCore
                && Prayer.CoreElement == element
                && Prayer.CoreLevel >= JianglinSpellbook.MaxLevel)
            {
                return;
            }

            if (!Session.TrySpend(1))
            {
                return;
            }

            Prayer.PressKey(element);
        }

        void HandleCasting()
        {
            if (!Session.Locked)
            {
                StopChannel(false);
                HandleConversion();
                if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
                {
                    Session.LockRecipe();
                }
            }
            else if (Session.IsReadyToFire)
            {
                var kind = JianglinSpellbook.KindOf(Session.Resolved);
                if (kind == JianglinCastKind.Channel)
                {
                    HandleChannel();
                }
                else if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Mouse0))
                {
                    if (_caster != null && _caster.TryCast(Session.Resolved))
                    {
                        Session.ConsumeReadySpell();
                    }
                }
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.R))
            {
                StopChannel(false);
                Session.CancelAndRefund();
            }
        }

        void HandleChannel()
        {
            bool hold = Input.GetMouseButton(0);
            if (hold && !_channeling && _caster != null)
            {
                _caster.BeginChannel(Session.Resolved);
                _channeling = true;
            }

            if (_channeling && _caster != null)
            {
                if (hold)
                {
                    _caster.TickChannel(Time.deltaTime);
                }
                else
                {
                    StopChannel(true);
                }
            }
        }

        void StopChannel(bool consume)
        {
            if (_caster != null)
            {
                _caster.EndChannel();
            }

            if (consume && _channeling)
            {
                Session.ConsumeReadySpell();
            }

            _channeling = false;
        }

        void HandleConversion()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Session.AdjustDraft(scroll > 0f ? 1 : -1);
            }

            Vector2 gui = JianglinMagicLayout.MouseToGui(Input.mousePosition);

            if (Input.GetMouseButtonDown(0) && JianglinMagicLayout.HitMana(gui))
            {
                Session.Dragging = true;
            }

            if (!Session.Dragging)
            {
                return;
            }

            if (Input.GetMouseButtonUp(0))
            {
                JianglinElement element;
                if (JianglinMagicLayout.HitElement(gui, out element))
                {
                    Session.TryConvert(element, Session.DraftLevel);
                }

                Session.Dragging = false;
            }
        }

        void ApplyCursorAndLook()
        {
            bool look = LookEnabled;
            if (_camera != null)
            {
                _camera.LookEnabled = look;
            }

            Cursor.lockState = look ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !look;
        }
    }
}
