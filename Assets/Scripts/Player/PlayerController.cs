using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Simple third-person CharacterController movement for the greybox MVP.
    /// Bind later to Input System actions as needed.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float jumpHeight = 1.4f;
        [SerializeField] float gravity = -20f;
        [SerializeField] Transform cameraPivot;

        CharacterController _controller;
        Vector3 _velocity;
        bool _grounded;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraPivot == null && Camera.main != null)
            {
                cameraPivot = Camera.main.transform;
            }
        }

        void Update()
        {
            _grounded = _controller.isGrounded;
            if (_grounded && _velocity.y < 0f)
            {
                _velocity.y = -2f;
            }

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            Vector3 input = new Vector3(horizontal, 0f, vertical).normalized;

            Vector3 move = input;
            if (cameraPivot != null && input.sqrMagnitude > 0.001f)
            {
                Vector3 forward = cameraPivot.forward;
                Vector3 right = cameraPivot.right;
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();
                move = forward * input.z + right * input.x;
                transform.rotation = Quaternion.LookRotation(move);
            }

            _controller.Move(move * moveSpeed * Time.deltaTime);

            if (Input.GetButtonDown("Jump") && _grounded)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }
    }
}
