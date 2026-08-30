using UnityEngine;

namespace Game.Jianglin
{
    /// <summary>
    /// Handles both first-person and third-person camera behaviour for 降临吧.
    /// Uses Unity's legacy Input class so it is independent from the project's
    /// existing Input System asset.
    /// </summary>
    public class JianglinCameraController : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;
        [SerializeField] Transform player;
        [SerializeField] Vector3 thirdPersonOffset = new Vector3(0f, 2.4f, -6f);
        [SerializeField] float eyeHeight = 1.72f;
        [SerializeField] float mouseSensitivity = 2.2f;
        [SerializeField] float minPitch = -65f;
        [SerializeField] float maxPitch = 78f;
        [SerializeField] bool firstPerson;

        Transform _rig;
        Renderer[] _playerRenderers;
        float _yaw;
        float _pitch = 16f;
        Vector3 _smoothPosition;

        public bool FirstPerson => firstPerson;
        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public bool LookEnabled { get; set; } = true;
        public Camera TargetCamera => targetCamera;
        public Transform Player => player;
        public float EyeHeight => eyeHeight;

        Transform _followTarget;

        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
        }

        public void Configure(
            Camera camera,
            Transform playerTransform,
            Transform rig,
            Vector3 offset,
            float eye,
            float sensitivity,
            bool startFirstPerson)
        {
            targetCamera = camera;
            player = playerTransform;
            _rig = rig;
            thirdPersonOffset = offset;
            eyeHeight = eye;
            mouseSensitivity = sensitivity;
            firstPerson = startFirstPerson;

            if (player != null)
            {
                _yaw = player.eulerAngles.y;
                _playerRenderers = player.GetComponentsInChildren<Renderer>();
            }

            _smoothPosition = CalculateDesiredPosition();
            ApplyPlayerVisibility();
        }

        public bool ToggleView()
        {
            firstPerson = !firstPerson;
            if (!firstPerson)
            {
                _pitch = Mathf.Clamp(_pitch, -40f, 62f);
            }

            ApplyPlayerVisibility();
            return firstPerson;
        }

        public void SetFirstPerson(bool value)
        {
            if (firstPerson == value)
            {
                return;
            }

            ToggleView();
        }

        void Update()
        {
            if (targetCamera == null || player == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.V))
            {
                ToggleView();
            }
        }

        void LateUpdate()
        {
            if (targetCamera == null || player == null)
            {
                return;
            }

            bool following = _followTarget != null;
            if (following)
            {
                TrackFollowTarget();
            }
            else if (LookEnabled)
            {
                ReadLookInput();
            }

            var desired = CalculateDesiredPosition();
            _smoothPosition = Vector3.Lerp(_smoothPosition, desired, 1f - Mathf.Exp(-18f * Time.deltaTime));

            targetCamera.transform.position = _smoothPosition;

            if (firstPerson)
            {
                targetCamera.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
            else if (following)
            {
                Vector3 lookTarget = _followTarget.position + Vector3.up * 1.05f;
                Vector3 fromCam = lookTarget - targetCamera.transform.position;
                if (fromCam.sqrMagnitude > 0.001f)
                {
                    targetCamera.transform.rotation = Quaternion.Slerp(
                        targetCamera.transform.rotation,
                        Quaternion.LookRotation(fromCam),
                        1f - Mathf.Exp(-10f * Time.deltaTime));
                }
            }
            else
            {
                Vector3 lookTarget = player.position + Vector3.up * (eyeHeight * 0.72f);
                targetCamera.transform.rotation = Quaternion.LookRotation(lookTarget - targetCamera.transform.position);
            }
        }

        void TrackFollowTarget()
        {
            if (_followTarget == null || player == null)
            {
                return;
            }

            Vector3 origin = player.position + Vector3.up * eyeHeight;
            Vector3 dir = (_followTarget.position + Vector3.up * 1.05f) - origin;
            if (dir.sqrMagnitude < 0.01f)
            {
                return;
            }

            dir.Normalize();
            float wantYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float wantPitch = -Mathf.Asin(Mathf.Clamp(dir.y, -0.95f, 0.95f)) * Mathf.Rad2Deg;
            float k = 1f - Mathf.Exp(-8f * Time.deltaTime);
            _yaw = Mathf.LerpAngle(_yaw, wantYaw, k);
            _pitch = Mathf.Lerp(_pitch, Mathf.Clamp(wantPitch, minPitch, maxPitch), k);
        }

        void ReadLookInput()
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            _yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        public Ray GetAimRay()
        {
            Vector3 direction = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward;
            Vector3 origin;
            if (firstPerson && targetCamera != null)
            {
                origin = targetCamera.transform.position;
            }
            else if (player != null)
            {
                origin = player.position + Vector3.up * eyeHeight;
            }
            else if (targetCamera != null)
            {
                origin = targetCamera.transform.position;
            }
            else
            {
                origin = transform.position;
            }

            return new Ray(origin, direction);
        }

        Vector3 CalculateDesiredPosition()
        {
            if (player == null)
            {
                return targetCamera != null ? targetCamera.transform.position : transform.position;
            }

            if (firstPerson)
            {
                return player.position + Vector3.up * eyeHeight;
            }

            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desired = player.position + orbit * thirdPersonOffset;
            return desired;
        }

        void ApplyPlayerVisibility()
        {
            if (_playerRenderers == null)
            {
                return;
            }

            foreach (var renderer in _playerRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = !firstPerson;
                }
            }
        }

        void OnDestroy()
        {
            if (_playerRenderers != null)
            {
                foreach (var renderer in _playerRenderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                    }
                }
            }
        }
    }
}
