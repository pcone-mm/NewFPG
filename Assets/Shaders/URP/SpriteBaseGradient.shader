Shader "NewFPG/2D/Sprite Base Gradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Vertical Gradient)]
        _TopColor ("Top Color", Color) = (0.45, 0.45, 0.45, 1)
        _BottomColor ("Bottom Color", Color) = (1, 1, 1, 1)
        _GradientStrength ("Gradient Strength", Range(0, 1)) = 1
        _GradientCenter ("Gradient Center", Range(0, 1)) = 0.5
        _GradientSoftness ("Gradient Softness", Range(0.001, 1)) = 1

        [MaterialToggle] _ZWrite ("ZWrite", Float) = 0

        // SpriteRenderer compatibility properties.
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] PixelSnap ("Pixel Snap", Float) = 0
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Name "SpriteUnlit"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex SpriteVertex
            #pragma fragment SpriteFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _BaseColor;
                half4 _TopColor;
                half4 _BottomColor;
                half _GradientStrength;
                half _GradientCenter;
                half _GradientSoftness;
            CBUFFER_END

            Varyings SpriteVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 SpriteFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Preserve the original sprite albedo, alpha, and SpriteRenderer tint.
                half4 result = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                result *= input.color * _BaseColor;

                // UV.y is bottom (0) to top (1). The defaults produce a dark top
                // and bright bottom while retaining all texture detail.
                half halfWidth = max(_GradientSoftness * 0.5h, 0.0005h);
                half gradient = smoothstep(
                    _GradientCenter - halfWidth,
                    _GradientCenter + halfWidth,
                    input.uv.y);
                half3 gradientColor = lerp(_BottomColor.rgb, _TopColor.rgb, gradient);
                result.rgb *= lerp(half3(1.0h, 1.0h, 1.0h), gradientColor, _GradientStrength);

                return result;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
