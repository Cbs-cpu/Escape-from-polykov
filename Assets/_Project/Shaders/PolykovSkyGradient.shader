// Overcast sky for the stylized look: three-colour vertical gradient with a soft cloud band noise, no sun disc.
Shader "Polykov/SkyGradient"
{
    Properties
    {
        _Top ("Top", Color) = (0.58, 0.64, 0.68, 1)
        _Horizon ("Horizon", Color) = (0.78, 0.81, 0.81, 1)
        _Bottom ("Bottom", Color) = (0.55, 0.57, 0.57, 1)
        _HorizonSharpness ("Horizon sharpness", Range(0.5, 8)) = 2.5
        _Clouds ("Cloud streaks", Range(0, 0.2)) = 0.05
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Top, _Horizon, _Bottom;
                half _HorizonSharpness, _Clouds;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            V Vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.dir = v.positionOS.xyz; return o; }
            half4 Frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                half3 c = y > 0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(y), 1.0 / _HorizonSharpness))
                                : lerp(_Horizon.rgb, _Bottom.rgb, saturate(-y * 6));
                float band = sin(d.x * 7.0 + d.z * 3.0) * sin(d.z * 5.0 - d.x * 2.0) * saturate(y * 4) * saturate(1 - y * 1.5);
                c += band * _Clouds;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
