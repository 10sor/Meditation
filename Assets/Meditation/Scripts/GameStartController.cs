using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace Meditation
{
    [DisallowMultipleComponent]
    public sealed class GameStartController : MonoBehaviour
    {
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] GameObject chakraControllers;
        [Min(0f)] [SerializeField] float fadeDuration = 0.25f;

        bool isStarting;

        void Awake()
        {
            SetWaitingState();
        }

        void OnEnable()
        {
            SetWaitingState();
        }

        void SetWaitingState()
        {
            isStarting = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (chakraControllers != null)
            {
                ChakraRendererController[] renderers =
                    chakraControllers.GetComponentsInChildren<ChakraRendererController>(true);
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].HideVisualization();

                chakraControllers.SetActive(false);
            }
        }

        void Update()
        {
            if (!isStarting && WasStartButtonPressed())
                StartCoroutine(StartGame());
        }

        static bool WasStartButtonPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                return true;

            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is not XRController)
                    continue;

                foreach (InputControl control in device.allControls)
                {
                    if (control is ButtonControl button && IsPhysicalXrButton(button.name) &&
                        button.wasPressedThisFrame)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static bool IsPhysicalXrButton(string controlName)
        {
            // Tracking state and capacitive touch controls are also represented
            // as buttons by Input System; they must not start the experience.
            return controlName != "isTracked" &&
                   controlName != "userPresence" &&
                   !controlName.EndsWith("Touched") &&
                   !controlName.EndsWith("Touch");
        }

        IEnumerator StartGame()
        {
            isStarting = true;
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;

                float elapsed = 0f;
                float duration = Mathf.Max(0f, fadeDuration);
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
                    yield return null;
                }

                canvasGroup.alpha = 0f;
            }

            if (chakraControllers != null)
                chakraControllers.SetActive(true);

            gameObject.SetActive(false);
        }
    }
}
