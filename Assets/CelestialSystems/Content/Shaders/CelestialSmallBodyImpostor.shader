Shader "jcan/Celestial Small Body Impostor"
{
    Properties
    {
        _CelestialImpostorAlbedoTransparencyAtlas("Albedo Transparency Atlas", 2D) = "white" {}
        _CelestialImpostorNormalAtlas("Normal Atlas", 2D) = "bump" {}
        _CelestialImpostorEmissionAtlas("Emission Atlas", 2D) = "black" {}
        _CelestialImpostorScaleOffset("Atlas Scale Offset", Vector) = (1, 1, 0, 0)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CelestialImpostorAlbedoTransparencyAtlas);
            SAMPLER(sampler_CelestialImpostorAlbedoTransparencyAtlas);
            TEXTURE2D(_CelestialImpostorNormalAtlas);
            SAMPLER(sampler_CelestialImpostorNormalAtlas);
            TEXTURE2D(_CelestialImpostorEmissionAtlas);
            SAMPLER(sampler_CelestialImpostorEmissionAtlas);

            CBUFFER_START(UnityPerMaterial)
                float4 _CelestialImpostorScaleOffset;
                float _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv * _CelestialImpostorScaleOffset.xy +
                    _CelestialImpostorScaleOffset.zw;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(
                    _CelestialImpostorAlbedoTransparencyAtlas,
                    sampler_CelestialImpostorAlbedoTransparencyAtlas,
                    input.uv);
                half silhouette = SAMPLE_TEXTURE2D(
                    _CelestialImpostorNormalAtlas,
                    sampler_CelestialImpostorNormalAtlas,
                    input.uv).a;
                clip(silhouette - _Cutoff);

                half3 emission = SAMPLE_TEXTURE2D(
                    _CelestialImpostorEmissionAtlas,
                    sampler_CelestialImpostorEmissionAtlas,
                    input.uv).rgb;
                return half4(albedo.rgb + emission, 1.0h);
            }
            ENDHLSL
        }
    }
}
