// Custom equivalent of Unity's standard cubemap skybox shader.
Shader "Meditation/Skybox Cubemap"
{
    Properties
    {
        [NoScaleOffset] _Tex ("Cubemap", Cube) = "grey" {}
        _Tint ("Tint Color", Color) = (0.5, 0.5, 0.5, 0.5)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
    struct v2f { float4 position : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
    samplerCUBE _Tex;
    half4 _Tex_HDR;
    half4 _Tint;
    half _Exposure;
    float _Rotation;

    float3 RotateAroundY(float3 p, float degrees)
    {
        float s, c;
        sincos(degrees * UNITY_PI / 180.0, s, c);
        return float3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);
    }

    v2f vert(appdata input)
    {
        v2f output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        float3 rotatedPosition = RotateAroundY(input.vertex.xyz, _Rotation);
        output.position = UnityObjectToClipPos(float4(rotatedPosition, 1.0));
        output.direction = rotatedPosition;
        return output;
    }

    half4 frag(v2f input) : SV_Target
    {
        half4 encodedColor = texCUBE(_Tex, input.direction);
        half3 color = DecodeHDR(encodedColor, _Tex_HDR);
        color *= _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
        return half4(color, 1.0);
    }
    ENDCG

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }
    Fallback Off
}
