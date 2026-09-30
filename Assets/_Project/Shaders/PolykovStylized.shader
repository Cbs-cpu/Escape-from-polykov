// Stylized lit shader for Escape from Polykov (concept-sheet look: inked low-poly, cold light, weathered concrete).
// - Banded (toon) main light with tinted shadows + SH ambient, soft point/spot lights, URP shadows, SSAO and fog.
// - Procedural world-space weathering, no textures needed: grime rising from the ground, rain streaks on vertical
//   faces, moss / dirt on up-facing surfaces, and a low-frequency colour variation so big walls are not flat.
// SRP Batcher compatible. Passes: ForwardLit, ShadowCaster, DepthOnly, DepthNormals.
Shader "Polykov/Stylized"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.55, 0.57, 0.56, 1)
        [NoScaleOffset] _BaseMap ("Albedo (optional)", 2D) = "white" {}
        [Header(Light)]
        _Bands ("Light bands", Range(1, 6)) = 3
        _BandSoftness ("Band softness", Range(0.005, 0.5)) = 0.06
        _ShadowTint ("Shadow tint", Color) = (0.62, 0.7, 0.78, 1)
        _AmbientStrength ("Ambient strength", Range(0, 2)) = 1
        [Header(Weathering)]
        _GrimeColor ("Grime color", Color) = (0.23, 0.21, 0.18, 1)
        _GrimeHeight ("Grime height (m)", Float) = 1.4
        _GrimeStrength ("Grime strength", Range(0, 1)) = 0.55
        _StreakStrength ("Rain streaks", Range(0, 1)) = 0.35
        _MossColor ("Moss color", Color) = (0.34, 0.42, 0.22, 1)
        _MossAmount ("Moss amount", Range(0, 1)) = 0.3
        _Variation ("Color variation", Range(0, 0.5)) = 0.12
        _NoiseScale ("Noise scale", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Bands, _BandSoftness, _AmbientStrength;
            half4 _ShadowTint;
            half4 _GrimeColor;
            float _GrimeHeight;
            half _GrimeStrength, _StreakStrength;
            half4 _MossColor;
            half _MossAmount, _Variation;
            float _NoiseScale;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        float Hash31(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }

        float ValueNoise(float3 p)
        {
            float3 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float n000 = Hash31(i), n100 = Hash31(i + float3(1, 0, 0)), n010 = Hash31(i + float3(0, 1, 0)), n110 = Hash31(i + float3(1, 1, 0));
            float n001 = Hash31(i + float3(0, 0, 1)), n101 = Hash31(i + float3(1, 0, 1)), n011 = Hash31(i + float3(0, 1, 1)), n111 = Hash31(i + float3(1, 1, 1));
            return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y), lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
        }

        float Fbm(float3 p)
        {
            return ValueNoise(p) * 0.55 + ValueNoise(p * 2.07) * 0.3 + ValueNoise(p * 4.13) * 0.15;
        }

        // Albedo with procedural weathering (world space; ground is y = 0).
        half3 WeatheredAlbedo(float3 positionWS, half3 normalWS, float2 uv)
        {
            half3 albedo = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
            float3 p = positionWS * _NoiseScale;
            float n = Fbm(p);
            albedo *= 1.0 + (n - 0.5) * 2.0 * _Variation;

            // Grime creeping up from the ground, broken by noise.
            float grime = saturate(1.0 - positionWS.y / max(_GrimeHeight, 0.01));
            grime = saturate(grime * (0.6 + n) ) * _GrimeStrength;
            // Rain streaks: vertical faces only, noise stretched along Y.
            float vertical = 1.0 - saturate(abs(normalWS.y) * 2.0);
            float streak = ValueNoise(float3(positionWS.x * 3.1 + positionWS.z * 2.7, positionWS.y * 0.18, positionWS.z * 0.5));
            streak = smoothstep(0.55, 0.95, streak) * vertical * _StreakStrength;
            albedo = lerp(albedo, _GrimeColor.rgb, saturate(grime + streak * 0.6));
            // Moss / dirt on up-facing surfaces.
            float up = saturate((normalWS.y - 0.6) * 2.5);
            float moss = smoothstep(0.45, 0.75, Fbm(p * 1.7 + 11.0)) * up * _MossAmount;
            albedo = lerp(albedo, _MossColor.rgb, moss);
            return albedo;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half Band(half x)
            {
                half bands = max(_Bands, 1.0h);
                half scaled = x * bands;
                half stepBase = floor(scaled);
                half t = smoothstep(0.5h - _BandSoftness, 0.5h + _BandSoftness, frac(scaled));
                return saturate((stepBase + t) / bands);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 n = normalize(i.normalWS);
                half3 albedo = WeatheredAlbedo(i.positionWS, n, i.uv);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);

                half ao = 1.0h;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                    ao = aoFactor.indirectAmbientOcclusion;
                #endif

                Light mainLight = GetMainLight(inputData.shadowCoord, i.positionWS, half4(1, 1, 1, 1));
                half ndl = saturate(dot(n, mainLight.direction));
                half lit = Band(ndl * mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half3 ambient = SampleSH(n) * _AmbientStrength * ao;
                half3 shadowSide = lerp(_ShadowTint.rgb, 1.0h, lit);
                half3 color = albedo * (ambient * shadowSide + mainLight.color * lit);

                #if defined(_ADDITIONAL_LIGHTS) || USE_FORWARD_PLUS || USE_CLUSTER_LIGHT_LOOP
                    uint count = GetAdditionalLightsCount();
                    #if USE_FORWARD_PLUS || USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint li = 0; li < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li++)
                    {
                        Light l = GetAdditionalLight(li, i.positionWS, half4(1, 1, 1, 1));
                        color += albedo * l.color * Band(saturate(dot(n, l.direction)) * l.distanceAttenuation * l.shadowAttenuation);
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(count)
                        Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                        half a = saturate(dot(n, l.direction) * 0.8h + 0.2h) * l.distanceAttenuation * l.shadowAttenuation;
                        color += albedo * l.color * smoothstep(0.0h, 0.35h, a) * a;
                    LIGHT_LOOP_END
                #endif

                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };

            V ShadowVert(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                V o;
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 nrm = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 dir = normalize(_LightPosition - p);
                #else
                    float3 dir = _LightDirection;
                #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(p, nrm, dir));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 ShadowFrag(V i) : SV_Target { return 0; }
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
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };
            V DVert(A v) { UNITY_SETUP_INSTANCE_ID(v); V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half DFrag(V i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull Back

            HLSLPROGRAM
            #pragma vertex NVert
            #pragma fragment NFrag
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };
            V NVert(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                V o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 NFrag(V i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
