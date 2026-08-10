using UnityEngine;

namespace VeilWar.CameraRig
{
    /// <summary>
    /// Elevated orthographic-ish orbit over the grid — readable on mobile, cinematic enough for HD bar.
    /// </summary>
    public sealed class BoardCameraController : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new(0f, 9.5f, -8.5f);
        [SerializeField] float followLerp = 8f;
        [SerializeField] float orbitSensitivity = 0.15f;
        [SerializeField] float pitchMin = 35f;
        [SerializeField] float pitchMax = 70f;

        float _yaw = 25f;
        float _pitch = 52f;

        void LateUpdate()
        {
            if (target == null) return;

            if (UnityEngine.Input.GetMouseButton(1))
            {
                _yaw += UnityEngine.Input.GetAxis("Mouse X") * orbitSensitivity * 40f;
                _pitch -= UnityEngine.Input.GetAxis("Mouse Y") * orbitSensitivity * 40f;
                _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
            }

            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            var desired = target.position + rot * offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followLerp * Time.deltaTime));
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.LookRotation(target.position - transform.position, Vector3.up),
                1f - Mathf.Exp(-followLerp * Time.deltaTime));
        }

        public void Focus(Transform boardRoot)
        {
            target = boardRoot;
        }
    }
}
