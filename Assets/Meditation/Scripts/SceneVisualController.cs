using System;
using UnityEngine;

namespace Meditation
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SceneVisualController : MonoBehaviour
    {
        [Serializable]
        public struct LightState
        {
            [Tooltip("World-space Euler rotation of the directional light.")]
            public Vector3 direction;

            [ColorUsage(true, true)]
            public Color color;

            [Min(0f)]
            public float intensity;

            [ColorUsage(true, true)]
            public Color skyTint;
        }

        static readonly int SkyboxLerpId = Shader.PropertyToID("_Lerp");
        static readonly int SkyboxTintId = Shader.PropertyToID("_Tint");

        [Header("Targets")]
        [SerializeField] Material skyboxMaterial;
        [SerializeField] Light directionalLight;

        [Header("States")]
        [SerializeField] LightState stateA = new LightState
        {
            direction = new Vector3(50f, -30f, 0f),
            color = Color.white,
            intensity = 1f,
            skyTint = Color.white
        };
        [SerializeField] LightState stateB = new LightState
        {
            direction = new Vector3(120f, 30f, 0f),
            color = Color.white,
            intensity = 1f,
            skyTint = Color.white
        };

        [Header("Blend")]
        [Range(0f, 1f)]
        [SerializeField] float lerp;

        public float Lerp => lerp;

        void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                ApplyCurrentState();
#endif
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            lerp = Mathf.Clamp01(lerp);
            stateA.intensity = Mathf.Max(0f, stateA.intensity);
            stateB.intensity = Mathf.Max(0f, stateB.intensity);

            if (!Application.isPlaying)
                ApplyCurrentState();
#endif
        }

        [ContextMenu("Apply Current State")]
        public void ApplyCurrentState()
        {
            Apply(lerp);
        }

        public void SetLerp(float value)
        {
            lerp = value;
            ApplyCurrentState();
        }

        public void Apply(float value)
        { 
                skyboxMaterial.SetFloat(SkyboxLerpId, value * value * value);
                skyboxMaterial.SetColor(SkyboxTintId, Color.Lerp(stateA.skyTint, stateB.skyTint, value));

            if (directionalLight == null)
                return;

            directionalLight.transform.eulerAngles =  Vector3.Slerp(
                stateA.direction, stateB.direction, value);
            directionalLight.color = Color.Lerp(stateA.color, stateB.color, value);
            directionalLight.intensity = Mathf.Lerp(
                stateA.intensity, stateB.intensity, value);
        }
    }
}
