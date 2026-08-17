Shader "FPG/Water/Genshin Planar Water"
{
    Properties
    {
        [Header(Water Color)]
        _ShallowColor("Shallow Color", Color) = (0.08, 0.55, 0.62, 0.72)
        _DeepColor("Deep Color", Color) = (0.015, 0.12, 0.22, 0.92)
        _DepthRange("Depth Range", Range(0.1, 20)) = 5

        [Header(Waves)]
        _FlowSpeed("Flow Speed", Range(0.01, 5)) = 0.35
        _WaveScale("Wave Scale", Range(0.01, 2)) = 0.18
        _WaveStrength("Wave Strength", Range(0, 1)) = 0.18
        _Distortion("Reflection Distortion", Range(0, 0.08)) = 0.018

        [Header(Foam)]
        _FoamTex("Foam Texture", 2D) = "white" {}
        _FoamColor("Foam Color", Color) = (0.82, 0.96, 1, 1)
        _FoamWidth("Foam Width", Range(0.01, 3)) = 0.65
        _FoamCutoff("Foam Cutoff", Range(0, 1)) = 0.56
        _FoamSpeed("Foam Speed", Range(0, 3)) = 0.3

        [Header(Lighting)]
        _SpecularColor("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularPower("Specular Power", Range(1, 256)) = 96
        _SpecularStrength("Specular Strength", Range(0, 4)) = 1.1
        _ReflectionStrength("Reflection Strength", Range(0, 2)) = 1
        _FresnelPower("Fresnel Power", Range(0.5, 10)) = 5
        _FresnelBias("Fresnel Bias", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_FoamTex);
            SAMPLER(sampler_FoamTex);
            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthRange;
                float _FlowSpeed;
                float _WaveScale;
                float _WaveStrength;
                float _Distortion;
                float4 _FoamTex_ST;
                half4 _FoamColor;
                float _FoamWidth;
                float _FoamCutoff;
                float _FoamSpeed;
                half4 _SpecularColor;
                float _SpecularPower;
                float _SpecularStrength;
                float _ReflectionStrength;
                float _FresnelPower;
                float _FresnelBias;
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
                float4 screenPos : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float viewDepth : TEXCOORD4;
            };

            float2 Hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            float GradientNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float2 blend = local * local * (3.0 - 2.0 * local);

                float n00 = dot(Hash22(cell), local);
                float n10 = dot(Hash22(cell + float2(1, 0)), local - float2(1, 0));
                float n01 = dot(Hash22(cell + float2(0, 1)), local - float2(0, 1));
                float n11 = dot(Hash22(cell + float2(1, 1)), local - float2(1, 1));
                return lerp(lerp(n00, n10, blend.x), lerp(n01, n11, blend.x), blend.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _FoamTex);
                output.viewDepth = -TransformWorldToView(positions.positionWS).z;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float rawSceneDepth = SampleSceneDepth(screenUV);
                float sceneDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                float waterDepth = max(sceneDepth - input.viewDepth, 0.0);
                float depth01 = saturate(waterDepth / max(_DepthRange, 0.0001));

                float2 waveUV = input.positionWS.xz * _WaveScale;
                float timeOffset = _Time.y * _FlowSpeed;
                float waveA = GradientNoise(waveUV + float2(timeOffset, timeOffset * 0.37));
                float waveB = GradientNoise(waveUV * 1.83 + float2(-timeOffset * 0.61, timeOffset * 0.43));
                float waveHeight = waveA + waveB * 0.5;

                float3 wavePosition = float3(input.positionWS.x, waveHeight * _WaveStrength, input.positionWS.z);
                float3 dpdx = ddx(wavePosition);
                float3 dpdy = ddy(wavePosition);
                half3 waveNormal = normalize(cross(dpdy, dpdx));
                if (dot(waveNormal, input.normalWS) < 0.0)
                    waveNormal = -waveNormal;
                waveNormal = normalize(lerp(input.normalWS, waveNormal, saturate(_WaveStrength * 3.0)));

                half3 viewDirection = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                float fresnel = _FresnelBias + (1.0 - _FresnelBias) *
                    pow(1.0 - saturate(dot(viewDirection, waveNormal)), _FresnelPower);

                float2 reflectionUV = screenUV;
                reflectionUV += waveNormal.xz * (_Distortion * saturate(waterDepth));
                reflectionUV = saturate(reflectionUV);
                half4 planarReflection = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, reflectionUV);

                half3 reflectionDirection = reflect(-viewDirection, waveNormal);
                half3 environmentReflection = GlossyEnvironmentReflection(reflectionDirection, input.positionWS, 0.12, 1.0, screenUV);
                half3 reflection = lerp(environmentReflection, planarReflection.rgb, planarReflection.a);

                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 halfDirection = SafeNormalize(viewDirection + mainLight.direction);
                float specularTerm = pow(saturate(dot(waveNormal, halfDirection)), _SpecularPower);
                half3 specular = _SpecularColor.rgb * specularTerm * _SpecularStrength *
                    mainLight.color * mainLight.shadowAttenuation;

                float2 foamUV = input.uv + float2(0.0, -_Time.y * _FoamSpeed);
                half foamTexture = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV).r;
                float shoreline = 1.0 - saturate(waterDepth / max(_FoamWidth, 0.0001));
                float foamNoise = saturate(foamTexture * 0.65 + waveHeight * 0.35 + 0.35);
                float foamMask = step(_FoamCutoff, foamNoise * shoreline);

                half3 color = lerp(waterColor, reflection * _ReflectionStrength, saturate(fresnel));
                color += specular;
                color = lerp(color, _FoamColor.rgb, foamMask * _FoamColor.a);

                half alpha = lerp(_ShallowColor.a, _DeepColor.a, depth01);
                alpha = saturate(max(alpha, fresnel * 0.85));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
