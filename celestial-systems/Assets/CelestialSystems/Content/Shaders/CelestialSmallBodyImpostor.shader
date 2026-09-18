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
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
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
                // A tile atlas is populated only at mip 0. Explicitly reading
                // that level prevents a distant billboard from sampling empty
                // or neighbouring atlas mips.
                half4 albedo = SAMPLE_TEXTURE2D_LOD(
                    _CelestialImpostorAlbedoTransparencyAtlas,
                    sampler_CelestialImpostorAlbedoTransparencyAtlas,
                    input.uv,
                    0.0);
                half normalSilhouette = SAMPLE_TEXTURE2D_LOD(
                    _CelestialImpostorNormalAtlas,
                    sampler_CelestialImpostorNormalAtlas,
                    input.uv,
                    0.0).a;

                // Some source shaders don't preserve alpha in an off-screen
                // color capture. The visible albedo atlas is still a reliable
                // fallback for the current bright ice-rock variants.
                half albedoSilhouette = step(
                    _Cutoff,
                    max(
                        albedo.r,
                        max(
                            albedo.g,
                            albedo.b)));
                half silhouette = max(
                    normalSilhouette,
                    albedoSilhouette);
                clip(silhouette - _Cutoff);

                // Unity writes signed fade progress per renderer. This local
                // screen-space dither avoids a package-version-specific
                // LODCrossFade include while retaining a true cross-fade.
                #if defined(LOD_FADE_CROSSFADE)
                    float dither = frac(
                        52.9829189f * frac(dot(
                            floor(input.positionCS.xy),
                            float2(0.06711056f, 0.00583715f))));
                    float fade = unity_LODFade.x;
                    clip(fade >= 0.0f
                        ? fade - dither
                        : -fade - dither);
                #endif

                half3 emission = SAMPLE_TEXTURE2D_LOD(
                    _CelestialImpostorEmissionAtlas,
                    sampler_CelestialImpostorEmissionAtlas,
                    input.uv,
                    0.0).rgb;
                return half4(albedo.rgb + emission, 1.0h);
            }
            ENDHLSL
        }
    }
}
