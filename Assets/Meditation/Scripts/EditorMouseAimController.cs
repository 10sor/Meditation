using UnityEngine;

#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace Meditation
{
    [DisallowMultipleComponent]
    public sealed class EditorMouseAimController : MonoBehaviour
    {
        [SerializeField] Transform aim;
        [Min(0f)] [SerializeField] float sensitivity = 0.1f;
        [SerializeField] float minPitch = -89f;
        [SerializeField] float maxPitch = 89f;

#if UNITY_EDITOR
        float yaw;
        float pitch;

        void OnEnable()
        {
            Transform target = GetAim();
            Vector3 angles = target.localEulerAngles;
            yaw = angles.y;
            pitch = NormalizeAngle(angles.x);
        }

        void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !IsAnyButtonPressed(mouse))
                return;

            Vector2 delta = mouse.delta.ReadValue() * sensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
            GetAim().localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        static bool IsAnyButtonPressed(Mouse mouse)
        {
            return mouse.leftButton.isPressed
                || mouse.rightButton.isPressed
                || mouse.middleButton.isPressed
                || mouse.backButton.isPressed
                || mouse.forwardButton.isPressed;
        }

        Transform GetAim() => aim != null ? aim : transform;

        static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        void OnValidate()
        {
            sensitivity = Mathf.Max(0f, sensitivity);
            minPitch = Mathf.Clamp(minPitch, -90f, 90f);
            maxPitch = Mathf.Clamp(maxPitch, minPitch, 90f);
        }
#endif
    }
}
