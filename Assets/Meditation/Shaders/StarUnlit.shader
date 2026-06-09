// Additive, unlit star shader for URP. Each quad is a soft round glow tinted by
// its per-vertex colour. A per-star twinkle phase (packed in TEXCOORD1) makes
// stars softly shimmer over time. Bright "hero" stars push past the bloom
// threshold so they sparkle. Renders both sides (Cull Off).
Shader "Meditation/StarUnlit"
{
    Properties
    {
        _Brightness   ("Brightness", Float) = 1
        _TwinkleSpeed ("Twinkle Speed", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One     // additive glow, weighted by alpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 phase      : TEXCOORD1;  // x = twinkle phase, y = twinkle amount
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            float _Brightness;
            float _TwinkleSpeed;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;

                float tw = 0.6 + 0.4 * sin(_Time.y * _TwinkleSpeed + IN.phase.x);
                float factor = lerp(1.0, tw, IN.phase.y);

                float4 c = IN.color;
                c.rgb *= factor;
                c.a   *= factor;
                OUT.color = c;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 d = IN.uv * 2.0 - 1.0;
                float dist = length(d);
                float falloff = saturate(1.0 - dist);

                // Tight bright core + soft surrounding halo = a star, not a disc.
                float core = pow(falloff, 5.0);
                float halo = pow(falloff, 1.6) * 0.3;

                // Faint 4-point diffraction spikes, strongest at the centre.
                float spikes = (pow(saturate(1.0 - abs(d.x)), 18.0)
                              + pow(saturate(1.0 - abs(d.y)), 18.0))
                              * pow(falloff, 2.0) * 0.35;

                float a = saturate(core + halo + spikes);
                return half4(IN.color.rgb * _Brightness, IN.color.a * a);
            }
            ENDHLSL
        }
    }
}
