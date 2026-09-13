/*
 * Renders generated planetary ring bands from baked radial color and density data.
 * The visible and shadow-caster passes sample the same density channel so divisions remain transparent to light.
 */

Shader "jcan/Celestial Systems/Celestial Ring"
{
    Properties
    {
        _RingData ("Radial Ring Data", 2D) = "white" {}
        [Range(0, 1)] _Opacity ("Opacity", Float) = 1
        [Range(0, 0.25)] _DensityCutoff ("Density Cutoff", Float) = 0.005
        [Range(0, 1)] _AmbientStrength ("Ambient Strength", Float) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RingVertex
            #pragma fragment RingFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_RingData);
            SAMPLER(sampler_RingData);

            CBUFFER_START(UnityPerMaterial)
                half _Opacity;
                half _DensityCutoff;
                half _AmbientStrength;
            CBUFFER_END

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
                half4 ringData =
                    SAMPLE_TEXTURE2D(
                        _RingData,
                        sampler_RingData,
                        input.uv);
                half density =
                    saturate(
                        ringData.a *
                        _Opacity);
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
                        ringData.rgb *
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
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_RingData);
            SAMPLER(sampler_RingData);

            float3 _LightDirection;
            float3 _LightPosition;

            CBUFFER_START(UnityPerMaterial)
                half _Opacity;
                half _DensityCutoff;
                half _AmbientStrength;
            CBUFFER_END

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
                half density =
                    saturate(
                        SAMPLE_TEXTURE2D(
                            _RingData,
                            sampler_RingData,
                            input.uv).a *
                        _Opacity);
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
