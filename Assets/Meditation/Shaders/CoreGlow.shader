// Additive URP shader for the earth-core orb: brightest looking straight at
// the centre of the sphere, fading softly toward the rim, with a slow
// breathing pulse. HDR colour pushes it over the bloom threshold so the core
// reads as a distant ball of light rather than a hard sphere.
Shader "Meditation/CoreGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2.2, 1.0, 0.35, 1)
        _SoftEdge    ("Edge Softness", Float) = 2.2
        _PulseSpeed  ("Pulse Speed (Hz)", Float) = 0.35
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float  _SoftEdge;
            float  _PulseSpeed;
            float  _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                o.viewDirWS  = _WorldSpaceCameraPos - posWS;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float ndv = saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                float glow = pow(ndv, _SoftEdge);
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed * 6.2831853);
                return half4(_Color.rgb * glow * pulse, 1);
            }
            ENDHLSL
        }
    }
}
