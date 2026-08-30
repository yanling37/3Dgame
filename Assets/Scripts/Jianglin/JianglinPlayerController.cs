using UnityEngine;

namespace Game.Jianglin
{
    /// <summary>
    /// Moves the CharacterController relative to the camera. The capsule itself
    /// does not yaw. Only <see cref="_facing"/> turns, so the mesh can be
    /// axis-corrected without fighting movement.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class JianglinPlayerController : MonoBehaviour
    {
        [SerializeField] JianglinCameraController cameraController;
        [SerializeField] Transform facing;
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float jumpHeight = 1.35f;
        [SerializeField] float gravity = -24f;
        [SerializeField] float turnSmoothTime = 0.08f;

        CharacterController _controller;
        Vector3 _velocity;
        float _turnVelocity;
        float _moveSlow = 1f;
        float _moveSlowUntil;
        Vector3 _dashVel;
        float _dashUntil;

        public bool IsDashing => Time.time < _dashUntil;

        public void SetMoveSlow(float multiplier, float duration)
        {
            _moveSlow = Mathf.Clamp(multiplier, 0.15f, 1f);
            _moveSlowUntil = Mathf.Max(_moveSlowUntil, Time.time + Mathf.Max(0.02f, duration));
        }

        public void Dash(Vector3 planarDir, float distance, float duration)
        {
            planarDir.y = 0f;
            if (planarDir.sqrMagnitude < 0.001f)
            {
                return;
            }

            duration = Mathf.Max(0.08f, duration);
            _dashVel = planarDir.normalized * (distance / duration);
            _dashUntil = Time.time + duration;
        }

        public void Configure(
            JianglinCameraController camControl,
            Transform facingRoot,
            float speed,
            float jump,
            float fallGravity)
        {
            cameraController = camControl;
            facing = facingRoot;
            moveSpeed = speed;
            jumpHeight = jump;
            gravity = fallGravity;
        }

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        void Update()
        {
            if (_controller == null)
            {
                return;
            }

            var health = GetComponent<JianglinHealth>();
            if (health != null && health.IsDead)
            {
                if (_controller.isGrounded && _velocity.y < 0f)
                {
                    _velocity.y = -2f;
                }

                _velocity.y += gravity * Time.deltaTime;
                _controller.Move(_velocity * Time.deltaTime);
                return;
            }

            bool grounded = _controller.isGrounded;
            if (grounded && _velocity.y < 0f)
            {
                _velocity.y = -2f;
            }

            if (IsDashing)
            {
                _controller.Move(_dashVel * Time.deltaTime);
                AlignFacingToMove(new Vector3(_dashVel.x, 0f, _dashVel.z));
            }
            else
            {
                Vector3 move = ReadCameraMove();
                float slow = Time.time < _moveSlowUntil ? _moveSlow : 1f;
                _controller.Move(move * moveSpeed * slow * Time.deltaTime);
                AlignFacingToMove(move);
            }

            if (!IsDashing && grounded && (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space)))
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        Vector3 ReadCameraMove()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            if (horizontal * horizontal + vertical * vertical < 0.001f)
            {
                return Vector3.zero;
            }

            Vector3 planarForward = Vector3.forward;
            Vector3 planarRight = Vector3.right;
            if (cameraController != null)
            {
                float yaw = cameraController.Yaw * Mathf.Deg2Rad;
                planarForward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                planarRight = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
            }

            return (planarForward * vertical + planarRight * horizontal).normalized;
        }

        void AlignFacingToMove(Vector3 move)
        {
            if (facing == null || move.sqrMagnitude < 0.001f)
            {
                return;
            }

            float targetAngle = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(
                facing.eulerAngles.y,
                targetAngle,
                ref _turnVelocity,
                turnSmoothTime);
            facing.rotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
