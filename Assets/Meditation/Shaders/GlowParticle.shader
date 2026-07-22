// Additive URP billboard-particle shader for the vortex swarm: a soft round
// glow tinted by the HDR material colour and the per-particle vertex colour
// (alpha = individual brightness). Bloom turns the dense parts of the swarm
// into a solid whirl of light.
Shader "Meditation/GlowParticle"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.color = IN.color;
                o.uv = IN.uv;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float d = length(IN.uv - 0.5) * 2.0;
                float a = saturate(1.0 - d);
                a *= a;
                return half4(_Color.rgb * IN.color.rgb * (a * IN.color.a), 1);
            }
            ENDHLSL
        }
    }
}
