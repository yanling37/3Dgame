using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// Basic third-person follow camera. Replace with Cinemachine FreeLook when ready.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 3f, -6f);
        [SerializeField] float followLerp = 10f;
        [SerializeField] float mouseSensitivity = 120f;
        [SerializeField] float minPitch = -20f;
        [SerializeField] float maxPitch = 60f;

        float _yaw;
        float _pitch = 15f;

        void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            _yaw += Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desired = target.position + rotation * offset;
            transform.position = Vector3.Lerp(transform.position, desired, followLerp * Time.deltaTime);
            transform.LookAt(target.position + Vector3.up * 1.5f);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
