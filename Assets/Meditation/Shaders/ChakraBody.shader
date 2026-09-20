Shader "Meditation/ChakraBody"
{
    Properties
    {
        [MainTexture] _BaseMap ("Diffuse", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
        _RoughnessMap ("Roughness (R)", 2D) = "white" {}
        _Roughness ("Roughness", Range(0, 1)) = 0.6

        [Header(Single Chakra Emission)]
        _ChakraHeight ("Chakra Height (World Space)", Float) = 1
        [HDR] _ChakraColor ("Chakra Color", Color) = (2, 0.15, 0.05, 1)
        _ChakraRadius ("Chakra Radius", Range(0.001, 2)) = 0.25
        _ChakraIntensity ("Chakra Intensity", Range(0, 1)) = 1
        _ChakraCore ("Core Sharpness", Range(0.25, 16)) = 5
        _ChakraCoreBoost ("Core Boost", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _Roughness;
                float _ChakraHeight;
                half4 _ChakraColor;
                half _ChakraRadius;
                half _ChakraIntensity;
                half _ChakraCore;
                half _ChakraHalo;
                half _ChakraCoreBoost;
                half _ChakraHaloBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half3 vertexLighting : TEXCOORD5;
                half fogFactor : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.vertexLighting = VertexLighting(positionInputs.positionWS, normalInputs.normalWS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half3 EvaluateChakraEmission(float3 positionWS)
            {
                float distanceToCenter = distance(positionWS.xy, float2(0.0, _ChakraHeight));
                half radial = 1.0h - saturate(distanceToCenter / _ChakraRadius);
               half core = pow(radial, _ChakraCore) * _ChakraCoreBoost;
                return _ChakraColor.rgb * _ChakraIntensity * core;
            }

            half3 EvaluateLight(Light lightData, half3 normalWS, half3 viewDirectionWS,
                                half3 albedo, half roughness)
            {
                half attenuation = lightData.distanceAttenuation * lightData.shadowAttenuation;
                half ndotl = saturate(dot(normalWS, lightData.direction));
                half3 diffuse = albedo * lightData.color * ndotl;

                half3 halfDirection = SafeNormalize(lightData.direction + viewDirectionWS);
                half smoothness = 1.0h - roughness;
                half specularPower = exp2(1.0h + smoothness * 10.0h);
                half specularTerm = pow(saturate(dot(normalWS, halfDirection)), specularPower);
                half3 specular = lightData.color * 0.04h * specularTerm * smoothness;
                return (diffuse + specular) * attenuation;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 albedoSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half roughness = saturate(_Roughness * (1.0 - SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, input.uv).a));
                half3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);

                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 albedo = albedoSample.rgb * _BaseColor.rgb;

                // SampleSH is the URP ambient probe and therefore follows
                // Environment Lighting / Ambient Color in Render Settings.
                half3 color = SampleSH(normalWS) * albedo;
                Light mainLight = GetMainLight(input.shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                color += EvaluateLight(mainLight, normalWS, viewDirectionWS, albedo, roughness);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(additionalLightCount)
                        Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        color += EvaluateLight(additionalLight, normalWS, viewDirectionWS, albedo, roughness);
                    LIGHT_LOOP_END
                #elif defined(_ADDITIONAL_LIGHTS_VERTEX)
                    color += input.vertexLighting * albedo;
                #endif

                color += EvaluateChakraEmission(input.positionWS);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/Meta"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
