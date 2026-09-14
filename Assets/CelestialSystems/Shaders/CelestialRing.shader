/*
 * Renders generated planetary ring bands directly from radial gradient and
 * AnimationCurve key data supplied by CelestialRingMeshPresentation.  Both
 * forward and shadow-caster passes evaluate the same density curve, so
 * divisions are continuous rather than limited by a baked texture resolution.
 */

Shader "jcan/Celestial Systems/Celestial Ring"
{
    Properties
    {
        [Range(0, 1)] _Opacity ("Opacity", Float) = 1
        [Range(0, 0.25)] _DensityCutoff ("Density Cutoff", Float) = 0.005
        [Range(0, 1)] _AmbientStrength ("Ambient Strength", Float) = 0.2
        [HideInInspector] _RingInnerRadiusMeters ("Ring Inner Radius", Float) = 0
        [HideInInspector] _RingOuterRadiusMeters ("Ring Outer Radius", Float) = 1
        [HideInInspector] _RingMicrostructure ("Ring Microstructure", Vector) = (0, 0, 0, 0)
        [HideInInspector] _RingMicrostructurePhase ("Ring Microstructure Phase", Float) = 0
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

            // Albedo: (time, red, green, blue).
            // Curves: (time, value, inTangent, outTangent).
            StructuredBuffer<float4> _RingAlbedoKeys;
            StructuredBuffer<float4> _RingDensityKeys;
            StructuredBuffer<float4> _RingPopulationKeys;
            int _RingAlbedoKeyCount;
            int _RingDensityKeyCount;
            int _RingPopulationKeyCount;

            CBUFFER_START(UnityPerMaterial)
                half _Opacity;
                half _DensityCutoff;
                half _AmbientStrength;
                float _RingInnerRadiusMeters;
                float _RingOuterRadiusMeters;
                float4 _RingMicrostructure;
                float _RingMicrostructurePhase;
            CBUFFER_END

            half3 EvaluateAlbedo(
                float radialFraction)
            {
                if (_RingAlbedoKeyCount < 1)
                {
                    return half3(1.0h, 1.0h, 1.0h);
                }

                float4 previous = _RingAlbedoKeys[0];
                if (radialFraction <= previous.x)
                {
                    return previous.yzw;
                }

                [loop]
                for (int index = 1;
                    index < _RingAlbedoKeyCount;
                    index++)
                {
                    float4 next = _RingAlbedoKeys[index];
                    if (radialFraction <= next.x)
                    {
                        float interval = max(
                            next.x - previous.x,
                            0.000001);
                        float blend = saturate(
                            (radialFraction - previous.x) /
                            interval);
                        return lerp(
                            previous.yzw,
                            next.yzw,
                            blend);
                    }

                    previous = next;
                }

                return previous.yzw;
            }

            half EvaluateDensityCurve(
                float radialFraction)
            {
                if (_RingDensityKeyCount < 1)
                {
                    return 1.0h;
                }

                float4 previous =
                    _RingDensityKeys[0];
                if (radialFraction <= previous.x)
                {
                    return previous.y;
                }

                [loop]
                for (int index = 1;
                    index < _RingDensityKeyCount;
                    index++)
                {
                    float4 next =
                        _RingDensityKeys[index];
                    if (radialFraction <= next.x)
                    {
                        float duration = max(
                            next.x - previous.x,
                            0.000001);
                        float u = saturate(
                            (radialFraction - previous.x) /
                            duration);
                        float u2 = u * u;
                        float u3 = u2 * u;
                        return (2.0 * u3 - 3.0 * u2 + 1.0) *
                                previous.y +
                            (u3 - 2.0 * u2 + u) *
                                duration * previous.w +
                            (-2.0 * u3 + 3.0 * u2) *
                                next.y +
                            (u3 - u2) *
                                duration * next.z;
                    }

                    previous = next;
                }

                return previous.y;
            }

            half EvaluatePopulationCurve(
                float radialFraction,
                half fallback)
            {
                if (_RingPopulationKeyCount < 1)
                {
                    return fallback;
                }

                float4 previous =
                    _RingPopulationKeys[0];
                if (radialFraction <= previous.x)
                {
                    return previous.y;
                }

                [loop]
                for (int index = 1;
                    index < _RingPopulationKeyCount;
                    index++)
                {
                    float4 next =
                        _RingPopulationKeys[index];
                    if (radialFraction <= next.x)
                    {
                        float duration = max(
                            next.x - previous.x,
                            0.000001);
                        float u = saturate(
                            (radialFraction - previous.x) /
                            duration);
                        float u2 = u * u;
                        float u3 = u2 * u;
                        return (2.0 * u3 - 3.0 * u2 + 1.0) *
                                previous.y +
                            (u3 - 2.0 * u2 + u) *
                                duration * previous.w +
                            (-2.0 * u3 + 3.0 * u2) *
                                next.y +
                            (u3 - u2) *
                                duration * next.z;
                    }

                    previous = next;
                }

                return previous.y;
            }


            half EvaluateMicrostructure(
                float radiusMeters)
            {
                float spacing =
                    _RingMicrostructure.x;
                float contrast =
                    saturate(
                        _RingMicrostructure.w);

                if (spacing <= 0.0001 ||
                    contrast <= 0.0001)
                {
                    return 1.0h;
                }

                float primary =
                    radiusMeters / spacing +
                    _RingMicrostructurePhase;
                float secondary =
                    primary / max(
                        0.0001,
                        _RingMicrostructure.y);
                float tertiary =
                    primary / max(
                        0.0001,
                        _RingMicrostructure.z);
                float signal =
                    (sin(primary) +
                        0.52 * sin(
                            secondary +
                            _RingMicrostructurePhase * 1.73) +
                        0.24 * sin(
                            tertiary -
                            _RingMicrostructurePhase * 0.61)) /
                    1.76;
                float ringlet =
                    smoothstep(
                        0.0,
                        1.0,
                        signal * 0.5 +
                        0.5);
                return lerp(
                    1.0h - contrast,
                    1.0h,
                    ringlet);
            }

            void EvaluateRingData(
                float radialFraction,
                out half3 albedo,
                out half density)
            {
                density = saturate(
                    EvaluateDensityCurve(
                        radialFraction));
                half population = saturate(
                    EvaluatePopulationCurve(
                        radialFraction,
                        density));
                half microstructure =
                    EvaluateMicrostructure(
                        lerp(
                            _RingInnerRadiusMeters,
                            _RingOuterRadiusMeters,
                            radialFraction));
                density *= microstructure;
                population *= microstructure;
                half coverage = sqrt(
                    density * population);
                albedo = EvaluateAlbedo(
                    radialFraction) *
                    lerp(
                        0.18h,
                        1.0h,
                        coverage);
            }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVertex
            #pragma fragment RingFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings RingVertex(
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
                output.uv =
                    input.uv;
                output.normalWS =
                    normalInputs.normalWS;
                output.shadowCoord =
                    TransformWorldToShadowCoord(
                        positionInputs.positionWS);
                output.fogFactor =
                    ComputeFogFactor(
                        positionInputs.positionCS.z);
                return output;
            }

            half4 RingFragment(
                Varyings input) : SV_Target
            {
                half3 albedo;
                half density;
                EvaluateRingData(
                    input.uv.x,
                    albedo,
                    density);
                density *=
                    _Opacity;
                clip(
                    density -
                    _DensityCutoff);

                Light mainLight =
                    GetMainLight(
                        input.shadowCoord);
                half directLight =
                    abs(
                        dot(
                            normalize(
                                input.normalWS),
                            mainLight.direction));
                half3 lighting =
                    SampleSH(
                        input.normalWS) *
                    _AmbientStrength;
                lighting +=
                    mainLight.color *
                    mainLight.shadowAttenuation *
                    directLight;

                half3 color =
                    MixFog(
                        albedo *
                        lighting,
                        input.fogFactor);
                return half4(
                    color,
                    density);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
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

                float3 biasedPositionWS =
                    ApplyShadowBias(
                        positionInputs.positionWS,
                        normalInputs.normalWS,
                        lightDirectionWS);
                output.positionCS =
                    TransformWorldToHClip(
                        biasedPositionWS);
                output.uv =
                    input.uv;
                return output;
            }

            half4 ShadowFragment(
                ShadowVaryings input) : SV_Target
            {
                half3 ignoredAlbedo;
                half density;
                EvaluateRingData(
                    input.uv.x,
                    ignoredAlbedo,
                    density);
                density *=
                    _Opacity;
                clip(
                    density -
                    _DensityCutoff);
                clip(
                    density -
                    InterleavedGradientNoise(
                        input.positionCS.xy));
                return 0.0h;
            }
            ENDHLSL
        }
    }
}