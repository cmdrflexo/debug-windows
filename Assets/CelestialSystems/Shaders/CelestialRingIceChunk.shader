/*
 * Procedural URP material for individual icy ring chunks. Dirt and mineral
 * inclusions are evaluated in object space, so no authored texture is needed.
 * Set _ChunkSeed through a MaterialPropertyBlock for varied instances.
 */

Shader "jcan/Celestial Systems/Celestial Ring Ice Chunk"
{
    Properties
    {
        [HDR] _IceColor ("Ice Color", Color) = (0.62, 0.82, 0.92, 1)
        [Range(0, 1)] _Opacity ("Opacity", Float) = 0.78
        [HDR] _TransmissionColor ("Transmission Color", Color) = (0.48, 0.78, 0.92, 1)
        [Range(0, 4)] _TranslucencyStrength ("Translucency Strength", Float) = 1.2
        [Range(0.25, 16)] _TranslucencyFalloff ("Translucency Falloff", Float) = 3
        [Range(0, 1)] _AmbientStrength ("Ambient Strength", Float) = 0.16

        [HDR] _DirtColor ("Dirt Color", Color) = (0.20, 0.14, 0.08, 1)
        [Range(0, 1)] _DirtAmount ("Dirt Amount", Float) = 0.12
        [Range(0.05, 16)] _DirtScale ("Dirt Scale", Float) = 2
        [Range(0.01, 1)] _DirtSoftness ("Dirt Softness", Float) = 0.22

        [HDR] _MineralColor ("Mineral Color", Color) = (0.62, 0.46, 0.28, 1)
        [Range(0, 1)] _MineralAmount ("Mineral Amount", Float) = 0.08
        [Range(0.05, 32)] _MineralScale ("Mineral Scale", Float) = 5
        [Range(0, 1)] _MineralColorStrength ("Mineral Color Strength", Float) = 0.65

        [Range(0, 1)] _SurfaceRoughness ("Surface Roughness", Float) = 0.55
        [Range(0, 1)] _ShadowOpacity ("Shadow Opacity", Float) = 0.85
        _ChunkSeed ("Chunk Seed", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _IceColor;
                half _Opacity;
                half4 _TransmissionColor;
                half _TranslucencyStrength;
                half _TranslucencyFalloff;
                half _AmbientStrength;
                half4 _DirtColor;
                half _DirtAmount;
                half _DirtScale;
                half _DirtSoftness;
                half4 _MineralColor;
                half _MineralAmount;
                half _MineralScale;
                half _MineralColorStrength;
                half _SurfaceRoughness;
                half _ShadowOpacity;
                float _ChunkSeed;
            CBUFFER_END

            float Hash31(
                float3 point)
            {
                point = frac(
                    point * 0.1031);
                point += dot(
                    point,
                    point.yzx + 33.33);
                return frac(
                    (point.x + point.y) *
                    point.z);
            }

            float ValueNoise3D(
                float3 point)
            {
                float3 cell =
                    floor(point);
                float3 fraction =
                    frac(point);
                fraction =
                    fraction * fraction *
                    (3.0 - 2.0 * fraction);

                float n000 = Hash31(cell);
                float n100 = Hash31(cell + float3(1.0, 0.0, 0.0));
                float n010 = Hash31(cell + float3(0.0, 1.0, 0.0));
                float n110 = Hash31(cell + float3(1.0, 1.0, 0.0));
                float n001 = Hash31(cell + float3(0.0, 0.0, 1.0));
                float n101 = Hash31(cell + float3(1.0, 0.0, 1.0));
                float n011 = Hash31(cell + float3(0.0, 1.0, 1.0));
                float n111 = Hash31(cell + float3(1.0, 1.0, 1.0));
                float nx00 = lerp(n000, n100, fraction.x);
                float nx10 = lerp(n010, n110, fraction.x);
                float nx01 = lerp(n001, n101, fraction.x);
                float nx11 = lerp(n011, n111, fraction.x);
                return lerp(
                    lerp(nx00, nx10, fraction.y),
                    lerp(nx01, nx11, fraction.y),
                    fraction.z);
            }

            float FractalNoise(
                float3 point)
            {
                float result = 0.0;
                float amplitude = 0.57;

                [unroll]
                for (int octave = 0;
                    octave < 3;
                    octave++)
                {
                    result +=
                        ValueNoise3D(point) *
                        amplitude;
                    point =
                        point * 2.07 +
                        19.17;
                    amplitude *=
                        0.5;
                }

                return saturate(
                    result / 1.0);
            }

            void EvaluateInclusions(
                float3 positionOS,
                out half dirt,
                out half mineral)
            {
                float3 seedOffset =
                    float3(
                        _ChunkSeed * 0.173,
                        _ChunkSeed * 0.419,
                        _ChunkSeed * 0.731);
                float dirtNoise =
                    FractalNoise(
                        positionOS *
                        _DirtScale +
                        seedOffset);
                float mineralNoise =
                    FractalNoise(
                        positionOS *
                        _MineralScale +
                        seedOffset * 3.17);

                dirt = smoothstep(
                    1.0 - _DirtAmount -
                        _DirtSoftness,
                    1.0 - _DirtAmount +
                        _DirtSoftness,
                    dirtNoise);
                mineral = smoothstep(
                    1.0 - _MineralAmount -
                        0.12,
                    1.0 - _MineralAmount +
                        0.12,
                    mineralNoise);
            }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex IceVertex
            #pragma fragment IceFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
            };

            Varyings IceVertex(
                Attributes input)
            {
                Varyings output =
                    (Varyings)0;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(
                        input.positionOS.xyz);
                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(
                        input.normalOS);
                output.positionCS =
                    positionInputs.positionCS;
                output.positionOS =
                    input.positionOS.xyz;
                output.normalWS =
                    normalInputs.normalWS;
                output.positionWS =
                    positionInputs.positionWS;
                output.shadowCoord =
                    TransformWorldToShadowCoord(
                        positionInputs.positionWS);
                output.fogFactor =
                    ComputeFogFactor(
                        positionInputs.positionCS.z);
                return output;
            }

            half4 IceFragment(
                Varyings input) : SV_Target
            {
                half dirt;
                half mineral;
                EvaluateInclusions(
                    input.positionOS,
                    dirt,
                    mineral);

                half3 baseColor =
                    lerp(
                        _IceColor.rgb,
                        _DirtColor.rgb,
                        dirt);
                baseColor =
                    lerp(
                        baseColor,
                        _MineralColor.rgb,
                        mineral *
                        _MineralColorStrength);

                half3 normalWS =
                    normalize(
                        input.normalWS);
                Light mainLight =
                    GetMainLight(
                        input.shadowCoord);
                half frontLight =
                    saturate(
                        dot(
                            normalWS,
                            mainLight.direction));
                half backLight =
                    pow(
                        saturate(
                            dot(
                                -normalWS,
                                mainLight.direction)),
                        _TranslucencyFalloff) *
                    _TranslucencyStrength;
                half3 ambient =
                    SampleSH(
                        normalWS) *
                    _AmbientStrength;
                half3 lighting =
                    ambient +
                    mainLight.color *
                    mainLight.shadowAttenuation *
                    (frontLight +
                        _TransmissionColor.rgb *
                        backLight);

                half3 viewDirection =
                    SafeNormalize(
                        GetCameraPositionWS() -
                        input.positionWS);
                half3 halfDirection =
                    SafeNormalize(
                        mainLight.direction +
                        viewDirection);
                half smoothness =
                    1.0h -
                    _SurfaceRoughness;
                half specularExponent =
                    lerp(
                        7.0h,
                        112.0h,
                        smoothness);
                half specular =
                    pow(
                        saturate(
                            dot(
                                normalWS,
                                halfDirection)),
                        specularExponent) *
                    lerp(
                        0.28h,
                        0.035h,
                        dirt);
                half3 color =
                    MixFog(
                        baseColor *
                        lighting +
                        mainLight.color *
                        mainLight.shadowAttenuation *
                        specular,
                        input.fogFactor);
                half alpha =
                    saturate(
                        _Opacity +
                        dirt * 0.14h -
                        mineral * 0.06h);
                return half4(
                    color,
                    alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            half InterleavedGradientNoise(
                float2 pixelPosition)
            {
                return frac(
                    52.9829189h *
                    frac(
                        dot(
                            pixelPosition,
                            float2(
                                0.06711056h,
                                0.00583715h))));
            }

            ShadowVaryings ShadowVertex(
                ShadowAttributes input)
            {
                ShadowVaryings output =
                    (ShadowVaryings)0;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(
                        input.positionOS.xyz);
                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(
                        input.normalOS);
                float3 lightDirectionWS =
                    _LightDirection;

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                lightDirectionWS =
                    normalize(
                        _LightPosition -
                        positionInputs.positionWS);
                #endif

                output.positionCS =
                    TransformWorldToHClip(
                        ApplyShadowBias(
                            positionInputs.positionWS,
                            normalInputs.normalWS,
                            lightDirectionWS));
                output.positionOS =
                    input.positionOS.xyz;
                return output;
            }

            half4 ShadowFragment(
                ShadowVaryings input) : SV_Target
            {
                half dirt;
                half mineral;
                EvaluateInclusions(
                    input.positionOS,
                    dirt,
                    mineral);
                half alpha =
                    saturate(
                        _Opacity +
                        dirt * 0.14h -
                        mineral * 0.06h) *
                    _ShadowOpacity;
                clip(
                    alpha -
                    InterleavedGradientNoise(
                        input.positionCS.xy));
                return 0.0h;
            }
            ENDHLSL
        }
    }
}