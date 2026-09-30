// Ink outline for the inverted-hull meshes (faces already pushed out and flipped in Blender): plain unlit colour,
// back faces culled so only the silhouette rim shows. Fog applied so distant outlines melt into the haze like the
// concept sheets instead of staying pitch black.
Shader "Polykov/Outline"
{
    Properties
    {
        _Color ("Ink color", Color) = (0.05, 0.05, 0.06, 1)
        _FogInfluence ("Fog influence", Range(0, 1)) = 0.85
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _FogInfluence;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; half fog : TEXCOORD0; };
            V Vert(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                V o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(V i) : SV_Target
            {
                half3 c = MixFog(_Color.rgb, i.fog);
                return half4(lerp(_Color.rgb, c, _FogInfluence), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex DVert
            #pragma fragment DFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };
            V DVert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half DFrag(V i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
}
