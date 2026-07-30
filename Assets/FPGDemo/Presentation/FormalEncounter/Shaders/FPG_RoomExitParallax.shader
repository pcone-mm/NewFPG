Shader "FPG/Room Exit Parallax"
{
    Properties
    {
        [HDR] _BaseColor ("Portal Color", Color) = (0.15, 0.9, 0.3, 1)
        [HDR] _AccentColor ("Accent Color", Color) = (0.55, 1.0, 0.72, 1)
        _DepthColor ("Depth Color", Color) = (0.015, 0.055, 0.035, 1)
        _BackdropColor ("Backdrop Color", Color) = (0.002, 0.006, 0.004, 1)
        _LayerCount ("Layer Count", Range(2, 12)) = 8
        _DepthAmount ("Parallax Depth", Range(0, 0.12)) = 0.045
        _FadePower ("Depth Fade", Range(0.25, 8)) = 2.2
        _FlowSpeed ("Flow Speed", Range(-2, 2)) = 0.32
        _NoiseScale ("Angular Noise Scale", Range(1, 12)) = 5
        _RingDensity ("Ring Density", Range(2, 16)) = 7
        _PortalRadius ("Portal Radius", Range(0.35, 1.4)) = 0.92
        _RimWidth ("Rim Width", Range(0.01, 0.25)) = 0.07
        _Aspect ("UV Aspect", Range(0.5, 2)) = 1.3
        _Brightness ("Brightness", Range(0, 6)) = 2.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "RoomExitParallax"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _AccentColor;
                half4 _DepthColor;
                half4 _BackdropColor;
                float _LayerCount;
                float _DepthAmount;
                float _FadePower;
                float _FlowSpeed;
                float _NoiseScale;
                float _RingDensity;
                float _PortalRadius;
                float _RimWidth;
                float _Aspect;
                float _Brightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 tangentWS : TEXCOORD3;
                half3 bitangentWS : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.uv = input.uv;

                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                half3 tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                half tangentSign = input.tangentOS.w * GetOddNegativeScale();
                output.normalWS = normalize(normalWS);
                output.tangentWS = normalize(tangentWS);
                output.bitangentWS = normalize(cross(output.normalWS, output.tangentWS) * tangentSign);
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.55;
                value += ValueNoise(p) * amplitude;
                p = p * 2.03 + 7.17;
                amplitude *= 0.5;
                value += ValueNoise(p) * amplitude;
                p = p * 2.01 + 3.41;
                amplitude *= 0.5;
                value += ValueNoise(p) * amplitude;
                return value;
            }

            float2 ParallaxOffset(float depth, float3 viewTS)
            {
                float grazingClamp = max(viewTS.z, 0.18);
                return -(viewTS.xy / grazingClamp) * depth;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 normalWS = normalize(input.normalWS);
                float3 tangentWS = normalize(input.tangentWS);
                float3 bitangentWS = normalize(input.bitangentWS);
                float3 viewTS = float3(
                    dot(viewWS, tangentWS),
                    dot(viewWS, bitangentWS),
                    dot(viewWS, normalWS));

                float layerCount = max(round(_LayerCount), 2.0);
                float time = _Time.y;
                float strongest = 0.0;
                float nearestLayer = 0.0;
                float portalMask = 0.0;
                float rim = 0.0;

                [loop]
                for (int layer = 0; layer < 12; layer++)
                {
                    if ((float)layer >= layerCount)
                    {
                        break;
                    }

                    float t = (float)layer / max(layerCount - 1.0, 1.0);
                    float2 uv = input.uv + ParallaxOffset(t * _DepthAmount, viewTS);
                    float2 centered = (uv - 0.5) * 2.0;
                    centered.x *= _Aspect;

                    float radius = length(centered);
                    float normalizedRadius = radius / max(_PortalRadius, 0.001);
                    float angle = atan2(centered.x, centered.y) / 6.2831853 + 0.5;
                    float inside = 1.0 - smoothstep(_PortalRadius * 0.93, _PortalRadius, radius);
                    float centerCut = smoothstep(0.055, 0.22, normalizedRadius);

                    float2 noiseUv = float2(
                        angle * _NoiseScale + t * 2.31,
                        normalizedRadius * _RingDensity - time * _FlowSpeed + t * 2.7);
                    float noise = Fbm(noiseUv);
                    float twist = sin(angle * 18.8495559 + noise * 5.2 - time * _FlowSpeed * 1.7);
                    float wave = 0.5 + 0.5 * sin(
                        normalizedRadius * _RingDensity * 6.2831853
                        - time * _FlowSpeed * 6.2831853
                        + noise * 5.0
                        + twist * 0.7
                        + t * 5.4);
                    float ridge = smoothstep(0.48, 0.78, wave);
                    float depthFade = pow(saturate(1.0 - t), _FadePower);
                    float layerValue = ridge * inside * centerCut * depthFade;

                    if (layerValue > strongest)
                    {
                        strongest = layerValue;
                        nearestLayer = 1.0 - t;
                    }

                    portalMask = max(portalMask, inside);
                    float layerRim = smoothstep(
                        _PortalRadius - _RimWidth,
                        _PortalRadius,
                        radius);
                    layerRim *= 1.0 - smoothstep(
                        _PortalRadius,
                        _PortalRadius + _RimWidth,
                        radius);
                    rim = max(rim, layerRim * (0.35 + 0.65 * depthFade));
                }

                float fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), 2.0);
                float centerDepth = 1.0 - smoothstep(0.0, 0.48, length((input.uv - 0.5) * float2(2.0 * _Aspect, 2.0)) / _PortalRadius);
                half3 color = _BackdropColor.rgb;
                color = lerp(color, _DepthColor.rgb, portalMask * (0.72 + centerDepth * 0.28));
                color += _BaseColor.rgb * strongest * _Brightness * (0.55 + nearestLayer * 0.45);
                color += _AccentColor.rgb * pow(saturate(strongest), 3.0) * (_Brightness * 0.32);
                color += _BaseColor.rgb * rim * (1.15 + fresnel * 1.6);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
