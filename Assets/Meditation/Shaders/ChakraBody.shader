// URP body shader for the meditating avatar. Renders her as a soft lit white
// figure, then adds the seven chakra glows: each is a bright core ("dot") that
// fades outward as a coloured gradient across the skin. Chakra positions,
// colours, radii and intensities are fed in as GLOBAL shader arrays by the
// ChakraSystem script (Shader.SetGlobalVectorArray), so every material using
// this shader picks them up. Bloom turns the bright cores into a real glow.
Shader "Meditation/ChakraBody"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.88, 0.92, 1, 1)
        _BaseGlow  ("Base Glow", Range(0,2)) = 0.2
        _CoreExp   ("Core Sharpness", Float) = 7
        _HaloExp   ("Halo Softness", Float) = 2
        _CoreBoost ("Core Boost", Float) = 1.6
        _HaloBoost ("Halo Boost", Float) = 0.55
        _LightDir  ("Fake Light Directon", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_CHAKRA 7
            float4 _ChakraPos[MAX_CHAKRA];    // xyz = world position, w = radius
            float4 _ChakraColor[MAX_CHAKRA];  // rgb = colour, a = intensity
            int    _ChakraCount;

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float  _BaseGlow;
            float  _CoreExp;
            float  _HaloExp;
            float  _CoreBoost;
            float  _HaloBoost;
            float3 _LightDir;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 directLight = saturate(dot(N, _LightDir) * 0.5 + 0.5);
                float3 baseLit = _BaseColor.rgb * (directLight + 0.3 + _BaseGlow);

                float3 glow = float3(0, 0, 0);
                [unroll(7)]
                for (int i = 0; i < _ChakraCount; i++)
                {
                    float radius = max(_ChakraPos[i].w, 1e-4);
                    // Depth-independent: distance in the world XY plane, so the
                    // glow lands on the correct body part regardless of how far
                    // forward/back that part sits.
                    float d = length(IN.positionWS.xy - _ChakraPos[i].xy);
                    float g = 1.0 - saturate(d / radius);
                    float core = pow(g, _CoreExp);
                    float halo = pow(g, _HaloExp);
                    float intensity = _ChakraColor[i].a;
                    glow += _ChakraColor[i].rgb * intensity * (core * _CoreBoost + halo * _HaloBoost);
                }

                return half4(baseLit + glow, 1);
            }
            ENDHLSL
        }
    }
}
