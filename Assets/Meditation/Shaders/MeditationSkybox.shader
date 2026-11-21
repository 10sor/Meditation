// Custom equivalent of Unity's standard cubemap skybox shader.
Shader "Meditation/Skybox Cubemap"
{
    Properties
    {
        [NoScaleOffset] [HDR] _Tex ("Cubemap", Cube) = "grey" {}
        [HDR] _Tint ("Tint", Color) = (1,1,1,1)
        _Rotation ("Rotation", Range(0, 360)) = 0


        [NoScaleOffset] [HDR] _Tex2 ("Cubemap", Cube) = "grey" {}
        _Axis("Axis", Vector) = (0,1,0,0)
        _Rotation2 ("Speed", Range(0, 5)) = 0

        _Lerp("Lerp", Range(0,1)) = 0
 //       [NoScaleOffset] [HDR] _TexMask ("Cubemap", Cube) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
    struct v2f { float4 position : SV_POSITION; float3 direction : TEXCOORD0; float3 direction2 : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
    samplerCUBE _Tex;
    samplerCUBE _Tex2;
 //   samplerCUBE _TexMask;
    half4 _Tex_HDR;
    half _Lerp;
    half4 _Axis;
    half4 _Tint;
    float _Rotation;
    float _Rotation2;

    float3 RotateAroundY(float3 p, float degrees)
    {
        float s, c;
        sincos(degrees * UNITY_PI / 180.0, s, c);
        return float3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);
    }

    float3 Rotate(float3 p, float3 axis, float angle)
    {
        float s, c;
        sincos(radians(angle), s, c);
        return p * c + cross(axis, p) * s + axis * dot(axis, p) * (1 - c);
    }

    v2f vert(appdata input)
    {
        v2f output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.position = UnityObjectToClipPos(input.vertex.xyz);
        output.direction = RotateAroundY(input.vertex.xyz, 504.0 + _Rotation);
        output.direction2 = Rotate(input.vertex.xyz, normalize(_Axis), _Rotation2 * _Time.y);
        return output;
    }

    half4 frag(v2f input) : SV_Target
    {
        half4 encodedColor = texCUBE(_Tex, input.direction);
        half mask = texCUBE(_Tex2, input.direction).a;
        half4 encodedColor2 = texCUBE(_Tex2, input.direction2);
        half3 color = DecodeHDR(encodedColor, _Tex_HDR);
        half3 final = lerp(encodedColor * _Tint, encodedColor2, _Lerp * mask);
        return half4(final, 1.0);
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
