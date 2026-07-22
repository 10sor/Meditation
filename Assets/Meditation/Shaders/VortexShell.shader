// Additive URP shader for the vortex funnel surface built by EnergyVortex
// (Shell style). Diagonal spiral bands rotate around the funnel (uv.x = angle,
// uv.y = 0 at the body / 1 at the core), which makes the surface read as a
// solid swirling vortex of energy. The sign of _SwirlSpeed sets the screw
// direction (climbing or sinking); _HeadV reveals/hides the stream from the
// core upward. Silhouette edges are softened for a gaseous look.
Shader "Meditation/VortexShell"
{
    Properties
    {
        [HDR] _Color   ("Color", Color) = (0.5, 0.8, 1.6, 1)
        _Stripes       ("Spiral Bands Around", Float) = 6
        _Twist         ("Band Wraps Top To Bottom", Float) = 3.5
        _SwirlSpeed    ("Swirl Speed (rad/s, sign = screw direction)", Float) = 2
        _FlowSpeed     ("Flow Speed (+down / -up)", Float) = 1
        _PulseCount    ("Pulses Along", Float) = 6
        _BaseGlow      ("Base Glow", Range(0, 1)) = 0.25
        _BandSharpness ("Band Sharpness", Float) = 2.2
        _EdgeSoft      ("Silhouette Softness", Float) = 1.6
        _HeadV         ("Head Position", Range(0, 1)) = 0
        _HeadGlow      ("Head Glow", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One      // pure additive glow
            ZWrite Off
            Cull Off           // both walls of the funnel add up -> volume

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define TWO_PI 6.2831853

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float  _Stripes;
            float  _Twist;
            float  _SwirlSpeed;
            float  _FlowSpeed;
            float  _PulseCount;
            float  _BaseGlow;
            float  _BandSharpness;
            float  _EdgeSoft;
            float  _HeadV;
            float  _HeadGlow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 viewDirWS  : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.uv = IN.uv;
                o.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                o.viewDirWS = _WorldSpaceCameraPos - posWS;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Rotating diagonal bands: the vortex "walls" swirling.
                float phase = IN.uv.x * _Stripes + IN.uv.y * _Twist
                            - _Time.y * _SwirlSpeed / TWO_PI * _Stripes;
                float band = pow(0.5 + 0.5 * cos(phase * TWO_PI), _BandSharpness);

                // Lengthwise pulses give the up/down flow cue.
                float fphase = IN.uv.y * _PulseCount - _Time.y * _FlowSpeed;
                float pulse = pow(0.5 + 0.5 * cos(fphase * TWO_PI), 2.0);

                // Moving front: only the part between the head and the core
                // is visible; a hot tip with an energy tail leads it.
                float visible = smoothstep(_HeadV - 0.02, _HeadV + 0.02, IN.uv.y);
                float d = abs(IN.uv.y - _HeadV);
                float head = _HeadGlow * (exp(-d * 45.0) + 0.6 * exp(-d * 9.0));

                // Soft fades: at the core end and on silhouette edges.
                float coreFade = smoothstep(1.0, 0.93, IN.uv.y);
                float ndv = abs(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                float soft = pow(ndv, _EdgeSoft);

                float i = visible * coreFade * soft
                        * (_BaseGlow + band * (0.5 + 0.5 * pulse) + head * 0.6);
                return half4(_Color.rgb * i, 1);
            }
            ENDHLSL
        }
    }
}
