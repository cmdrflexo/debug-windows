/*
 * Applies the initial artistic screen-space deflection for registered celestial gravitational lenses.
 */

Shader "jcan/Celestial Systems/Gravitational Lensing Full Screen"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "Gravitational Lensing"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define JCAN_CELESTIAL_MAXIMUM_LENSES 8

            int _CelestialGravitationalLensCount;
            float4 _CelestialGravitationalLensData[
                JCAN_CELESTIAL_MAXIMUM_LENSES];
            float4 _CelestialGravitationalLensShapeData[
                JCAN_CELESTIAL_MAXIMUM_LENSES];

            half4 Fragment(
                Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(
                    input);

                float2 sourceUv =
                    input.texcoord;

                for (int index = 0;
                    index < _CelestialGravitationalLensCount;
                    index++)
                {
                    float4 lens =
                        _CelestialGravitationalLensData[
                            index];
                    float2 fromLens =
                        sourceUv -
                        lens.xy;
                    float distanceFromLens =
                        length(
                            fromLens);
                    float normalizedDistance =
                        distanceFromLens /
                        max(
                            lens.z,
                            0.00001);

                    if (normalizedDistance >= 1.0)
                    {
                        continue;
                    }

                    float radialFade =
                        pow(
                            saturate(
                                1.0 -
                                normalizedDistance),
                            max(
                                _CelestialGravitationalLensShapeData[
                                    index].x,
                                0.1));
                    float centerGuard =
                        smoothstep(
                            0.0,
                            0.035,
                            normalizedDistance);
                    float offsetMagnitude =
                        lens.w *
                        lens.z *
                        radialFade *
                        centerGuard;
                    float2 towardLens =
                        -fromLens /
                        max(
                            distanceFromLens,
                            0.00001);

                    sourceUv +=
                        towardLens *
                        offsetMagnitude;
                }

                return SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    saturate(
                        sourceUv));
            }
            ENDHLSL
        }
    }
}
