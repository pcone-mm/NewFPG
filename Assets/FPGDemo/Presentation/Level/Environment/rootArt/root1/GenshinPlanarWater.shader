Shader "FPG/Water/Genshin Planar Water"
{
    Properties
    {
        [Header(Water Color)]
        _ShallowColor("Shallow Color", Color) = (0.08, 0.55, 0.62, 0.72)
        _DeepColor("Deep Color", Color) = (0.015, 0.12, 0.22, 0.92)
        _DepthRange("Depth Range", Range(0.1, 20)) = 5

        [Header(Normal Waves)]
        // BoatAttack stores the horizontal normal perturbation directly in RG.
        // It is deliberately imported as a linear Default texture, not as a Normal Map.
        [NoScaleOffset] _NormalMap("BoatAttack Surface Map", 2D) = "gray" {}
        [NoScaleOffset] _FlowMap("Flow Map", 2D) = "gray" {}
        _FlowMapTiling("Flow Map Tiling", Range(0.01, 8)) = 1
        _FlowMapStrength("Flow Map Strength", Range(0, 2)) = 0.45
        _NormalTiling("Normal World Tiling", Range(0.01, 2)) = 0.4
        _NormalStrength("Normal Strength", Range(0, 2)) = 0.35
        _NormalSpeed("Normal Speed", Range(0, 2)) = 0.08
        _FlowSpeed("Flow Speed", Range(0.01, 5)) = 0.35
        _Distortion("Reflection Distortion", Range(0, 0.08)) = 0.018

        [Header(Foam)]
        _FoamTex("Foam Texture", 2D) = "white" {}
        _FoamColor("Foam Color", Color) = (0.82, 0.96, 1, 1)
        _FoamWidth("Foam Width", Range(0.01, 3)) = 0.65
        _FoamCutoff("Foam Cutoff", Range(0, 1)) = 0.56
        _FoamSoftness("Foam Softness", Range(0.001, 0.3)) = 0.08
        _FoamTiling("Foam Tiling", Range(0.01, 4)) = 0.35
        _FoamSpeed("Foam Speed", Range(0, 3)) = 0.3

        [Header(Caustics)]
        [NoScaleOffset] _CausticTex("Caustic Texture", 2D) = "black" {}
        [HDR] _CausticColor("Caustic Color", Color) = (0.32, 0.85, 0.78, 1)
        _CausticScale("Caustic Scale", Range(0.1, 8)) = 1.4
        _CausticSpeed("Caustic Speed", Range(0, 2)) = 0.12
        _CausticStrength("Caustic Strength", Range(0, 4)) = 0.75
        _CausticDepthFade("Caustic Depth Fade", Range(0.1, 15)) = 4

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

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FlowMap);
            SAMPLER(sampler_FlowMap);
            TEXTURE2D(_FoamTex);
            SAMPLER(sampler_FoamTex);
            TEXTURE2D(_CausticTex);
            SAMPLER(sampler_CausticTex);
            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthRange;
                float _FlowMapTiling;
                float _FlowMapStrength;
                float _NormalTiling;
                float _NormalStrength;
                float _NormalSpeed;
                float _FlowSpeed;
                float _Distortion;
                float4 _FoamTex_ST;
                half4 _FoamColor;
                float _FoamWidth;
                float _FoamCutoff;
                float _FoamSoftness;
                float _FoamTiling;
                float _FoamSpeed;
                half4 _CausticColor;
                float _CausticScale;
                float _CausticSpeed;
                float _CausticStrength;
                float _CausticDepthFade;
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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float rawSceneDepth = SampleSceneDepth(screenUV);
                float3 scenePositionWS = ComputeWorldSpacePosition(screenUV, rawSceneDepth, UNITY_MATRIX_I_VP);
#if UNITY_REVERSED_Z
                float sceneGeometryMask = step(0.0001, rawSceneDepth);
#else
                float sceneGeometryMask = step(rawSceneDepth, 0.9999);
#endif
                float belowWaterMask = sceneGeometryMask *
                    step(scenePositionWS.y + 0.01, input.positionWS.y);
                float verticalWaterDepth = max(input.positionWS.y - scenePositionWS.y, 0.0);
                float waterDepth = lerp(_DepthRange, verticalWaterDepth, belowWaterMask);
                float depth01 = saturate(waterDepth / max(_DepthRange, 0.0001));

                // BoatAttack's surface texture is not a tangent-space normal map. Its RG
                // channels are two signed horizontal slopes. Sample it at two world-space
                // frequencies, matching BoatAttack's 0.1 / 0.4 detail layers.
                float normalTime = _Time.y * _NormalSpeed;
                half2 flowVector = SAMPLE_TEXTURE2D(
                    _FlowMap, sampler_FlowMap, input.positionWS.xz * _FlowMapTiling).rg * 2.0 - 1.0;
                flowVector *= _FlowMapStrength;
                float2 normalUVLarge = input.positionWS.xz * (_NormalTiling * 0.25) +
                    float2(normalTime * 0.5, normalTime * 0.35) + flowVector * normalTime * 0.15;
                float2 normalUVDetail = input.positionWS.xz * _NormalTiling -
                    float2(normalTime, normalTime * 0.73) - flowVector.yx * normalTime * 0.1;
                half2 detailBumpLarge = SAMPLE_TEXTURE2D(
                    _NormalMap, sampler_NormalMap, normalUVLarge).rg * 2.0h - 1.0h;
                half2 detailBumpSmall = SAMPLE_TEXTURE2D(
                    _NormalMap, sampler_NormalMap, normalUVDetail).rg * 2.0h - 1.0h;
                half2 detailBump = (detailBumpLarge + detailBumpSmall * 0.5h) *
                    lerp(0.35h, 1.0h, depth01);
                half3 baseNormal = normalize(input.normalWS);
                half3 waveNormal = normalize(
                    baseNormal + half3(detailBump.x, 0.0h, detailBump.y) * _NormalStrength);

                half3 viewDirection = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                float fresnel = _FresnelBias + (1.0 - _FresnelBias) *
                    pow(1.0 - saturate(dot(viewDirection, waveNormal)), _FresnelPower);

                float2 reflectionUV = screenUV;
                reflectionUV += waveNormal.xz * (_Distortion * saturate(waterDepth));
                reflectionUV = saturate(reflectionUV);
                half4 planarReflection = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, reflectionUV);

                // The reflection camera renders onto an opaque background. Sampling RGB directly
                // avoids applying transparent sprite alpha for a second time on the water surface.
                half3 reflection = planarReflection.rgb;

                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 halfDirection = SafeNormalize(viewDirection + mainLight.direction);
                float specularTerm = pow(saturate(dot(waveNormal, halfDirection)), _SpecularPower);
                half3 specular = _SpecularColor.rgb * specularTerm * _SpecularStrength *
                    mainLight.color * mainLight.shadowAttenuation;

                // BoatAttack packs thick, medium and light foam into R, G and B.
                // A weighted blend retains the internal breakup; max(R,G,B) would turn
                // most of this particular packed texture into a nearly solid mask.
                float foamTime = _Time.y * _FoamSpeed;
                float2 foamUV = input.positionWS.xz * _FoamTiling +
                    float2(foamTime * 0.16, foamTime * 0.11) + flowVector * foamTime * 0.08;
                half3 foamBands = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV).rgb;
                half foamTexture = saturate(dot(foamBands, half3(0.55h, 0.30h, 0.15h)) * 1.35h);
                float shoreline = (1.0 - saturate(waterDepth / max(_FoamWidth, 0.0001))) *
                    belowWaterMask;
                float foamSignal = foamTexture * shoreline;
                float foamMask = smoothstep(
                    _FoamCutoff - _FoamSoftness,
                    _FoamCutoff + _FoamSoftness,
                    foamSignal);

                // World-space projection keeps the caustics attached to submerged geometry.
                float causticTime = _Time.y * _CausticSpeed;
                float2 causticPosition = lerp(input.positionWS.xz, scenePositionWS.xz, belowWaterMask);
                float2 causticUV = causticPosition * _CausticScale;
                half causticA = SAMPLE_TEXTURE2D(
                    _CausticTex, sampler_CausticTex,
                    causticUV + float2(causticTime, causticTime * 0.43)).r;
                half causticB = SAMPLE_TEXTURE2D(
                    _CausticTex, sampler_CausticTex,
                    float2(causticUV.y, -causticUV.x) * 1.31 +
                    float2(-causticTime * 0.61, causticTime * 0.37)).r;
                float causticPattern = saturate(causticA * causticB * 2.0);
                float causticDepthMask = saturate(waterDepth * 3.0) *
                    (1.0 - saturate(waterDepth / max(_CausticDepthFade, 0.0001)));
                float projectedCaustic = causticPattern * causticDepthMask * belowWaterMask;
                // Transparent layered environment art does not contribute to the depth texture,
                // so keep a restrained surface fallback for those rooms.
                float surfaceCaustic = causticPattern * 0.08 * (1.0 - belowWaterMask);
                float causticMask = max(projectedCaustic, surfaceCaustic) * (1.0 - foamMask);

                half reflectionWeight = saturate(fresnel * _ReflectionStrength);
                half3 color = lerp(waterColor, reflection, reflectionWeight);
                color += specular;
                color += _CausticColor.rgb * causticMask * _CausticStrength *
                    mainLight.shadowAttenuation * (1.0 - reflectionWeight * 0.65);
                color = lerp(color, _FoamColor.rgb, foamMask * _FoamColor.a);

                half alpha = lerp(_ShallowColor.a, _DeepColor.a, depth01);
                alpha = saturate(max(alpha, fresnel));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
