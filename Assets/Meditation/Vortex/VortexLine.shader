Shader "Custom/VortexLine"
{
    Properties
    {
        [HDR] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _Stretch("Stretch", Float) = 1
        _Dir ("Direction", Float) = 1
        _SpeedMul("Speed multiplier", Float) = 1
        _Speeds("Speeds", Vector) = (1, 2, 3, 4)
        _Scales("Scales", Vector) = (1, 2, 3, 4)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            ZWrite Off
            Blend One One
            Cull Off
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 vcol : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 vcol : COLOR;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float _Stretch, _SpeedMul;
                float _Dir;
                float4 _Speeds, _Scales;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.vcol = IN.vcol;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {

                float time = sign(_Dir) * _Time.y * _SpeedMul;
                half2 uvs = IN.uv.xy * float2(_Stretch, 1.0);
                half2 offset = float2(time, 0.0);
                half samp0 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvs * _Scales.x + offset * _Speeds.x).r;
                half samp1 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvs * _Scales.y + offset * _Speeds.y).r;
                half samp2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvs * _Scales.z + offset * _Speeds.z).r;
                half4 final = 1;
                final.rgb = (samp0 + samp1 + samp2) * 0.33 * _BaseColor.rgb * IN.vcol.a;

                return final;
            }
            ENDHLSL
        }
    }
}
