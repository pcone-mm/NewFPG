// The procedural noise in this shader is adapted from the MIT-licensed Shadertoy
// by Giorgi Azmaipharashvili, using MIT-licensed snippets by David Hoskins and
// Nikita Miropolskiy. See the source supplied with this project task.
Shader "NewFPG/Post Processing/Dodge Speed Lines"
{
    Properties
    {
        _Opacity ("Opacity", Range(0, 1)) = 0.82
        _NoiseScale ("Noise Scale", Float) = 32
        _AnimationSpeed ("Animation Speed", Float) = 2
        _LineThreshold ("Line Threshold", Range(0, 1)) = 0.3
        _RadialScale ("Radial Scale", Float) = 1
        _LineColor ("Line Color", Color) = (0.72, 0.9, 1, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "DodgeSpeedLines"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Opacity;
            float _NoiseScale;
            float _AnimationSpeed;
            float _LineThreshold;
            float _RadialScale;
            float4 _LineColor;

            float3 Hash33(float3 p3)
            {
                p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yxz + 33.33);
                return frac((p3.xxy + p3.yxx) * p3.zyx) - 0.5;
            }

            float Simplex3D(float3 p)
            {
                float3 s = floor(p + dot(p, float3(1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0)));
                float3 x = p - s + dot(s, float3(1.0 / 6.0, 1.0 / 6.0, 1.0 / 6.0));
                float3 e = step(0.0, x - x.yzx);
                float3 i1 = e * (1.0 - e.zxy);
                float3 i2 = 1.0 - e.zxy * (1.0 - e);
                float3 x1 = x - i1 + 1.0 / 6.0;
                float3 x2 = x - i2 + 1.0 / 3.0;
                float3 x3 = x - 0.5;
                float4 w = max(0.6 - float4(dot(x, x), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
                w *= w;
                return dot(
                    float4(dot(Hash33(s), x), dot(Hash33(s + i1), x1), dot(Hash33(s + i2), x2), dot(Hash33(s + 1.0), x3)) * w * w,
                    float4(52.0, 52.0, 52.0, 52.0));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                float2 screenUv = input.texcoord * 2.0 - 1.0;
                screenUv.x *= _ScreenParams.x / _ScreenParams.y;
                screenUv /= _RadialScale;

                float radialDistance = length(screenUv);
                float2 radialDirection = radialDistance > 0.0001 ? screenUv / radialDistance : float2(0.0, 1.0);
                float2 noiseUv = 0.5 + radialDirection * min(radialDistance, 0.05);
                float time = _Time.y * _AnimationSpeed;
                float3 noisePosition = 13.0 * float3(noiseUv, 0.0) + float3(0.0, 0.0, time * 0.025);
                float noise = Simplex3D(noisePosition * _NoiseScale) * 0.5 + 0.5;

                float distanceMask = abs(saturate(radialDistance / 12.0) * noise * 2.0 - 1.0);
                float stepped = smoothstep(_LineThreshold - 0.5, _LineThreshold + 0.5, noise * (1.0 - pow(distanceMask, 4.0)));
                float speedLines = smoothstep(_LineThreshold - 0.05, _LineThreshold + 0.05, noise * stepped);
                float blend = saturate(speedLines * _Opacity);

                sceneColor.rgb = lerp(sceneColor.rgb, _LineColor.rgb, blend);
                return sceneColor;
            }
            ENDHLSL
        }
    }
}
