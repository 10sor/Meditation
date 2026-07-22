// Additive URP shader for the spiralling energy streams of the grounding
// vortex. The mesh is a bundle of helix ribbons built by EnergyVortex; this
// shader (a) rotates the whole spiral around its local Y axis over time so it
// swirls like stirred water, and (b) scrolls bright pulses along each strand
// (uv.y = 0 at the body, 1 at the earth core) so the energy visibly flows.
// Positive _FlowSpeed = pulses travel body -> core (descending); negative =
// core -> body (ascending). HDR colour + bloom make the pulses glow.
Shader "Meditation/EnergyStream"
{
    Properties
    {
        [HDR] _Color   ("Color", Color) = (0.5, 0.8, 1.6, 1)
        _FlowSpeed     ("Flow Speed (+down / -up)", Float) = 0.6
        _SwirlSpeed    ("Swirl Speed (rad/s)", Float) = 1.2
        _PulseCount    ("Pulses Along Strand", Float) = 4
        _PulseSharpness("Pulse Sharpness", Float) = 2.5
        _BaseGlow      ("Base Glow", Range(0, 1)) = 0.3
        _HeadV         ("Head Position (uv.y of the moving front)", Range(0, 1)) = 0
        _HeadGlow      ("Head Glow", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One      // pure additive glow
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float  _FlowSpeed;
            float  _SwirlSpeed;
            float  _PulseCount;
            float  _PulseSharpness;
            float  _BaseGlow;
            float  _HeadV;
            float  _HeadGlow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;

                // Spin the whole helix around its local Y axis: the vortex
                // itself rotates, like water being stirred in a cup.
                float3 p = IN.positionOS.xyz;
                float s, c;
                sincos(_SwirlSpeed * _Time.y, s, c);
                p.xz = float2(p.x * c - p.z * s, p.x * s + p.z * c);

                o.positionCS = TransformObjectToHClip(p);
                o.uv = IN.uv;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Soft falloff across the ribbon width (1 centre -> 0 edges).
                float across = 1.0 - abs(IN.uv.x * 2.0 - 1.0);
                across *= across;

                // Bright pulses travelling along the strand.
                float phase = IN.uv.y * _PulseCount - _Time.y * _FlowSpeed;
                float pulse = pow(0.5 + 0.5 * cos(phase * 6.2831853), _PulseSharpness);

                // Only the part between the moving front (_HeadV) and the
                // core (uv.y = 1) is visible: the vortex grows upward as the
                // head climbs to 0 and drains as it returns to 1.
                float visible = smoothstep(_HeadV - 0.015, _HeadV + 0.015, IN.uv.y);

                // Fade near the core end so the stream doesn't cut hard.
                float coreFade = smoothstep(1.0, 0.93, IN.uv.y);

                // Bright leading tip at the front: a hot core with a longer
                // energy tail, so the travelling whirl reads clearly.
                float d = abs(IN.uv.y - _HeadV);
                float head = _HeadGlow * (exp(-d * 45.0) + 0.6 * exp(-d * 9.0));

                float i = across * coreFade * visible
                        * (_BaseGlow + (1.0 - _BaseGlow) * pulse + head);
                return half4(_Color.rgb * i, 1);
            }
            ENDHLSL
        }
    }
}
