Shader "Mobile/ToyPlasticAnime"
{
    Properties
    {
        [Header(Base)]
        _BaseColor ("Base Color", Color) = (0.25, 0.65, 1.0, 1)
        _ShadowColor ("Shadow Tint", Color) = (0.45, 0.55, 0.72, 1)

        [Header(Lighting)]
        _Wrap ("Light Wrap", Range(0, 1)) = 0.35
        _LightSoftness ("Light Softness", Range(0.01, 1)) = 0.35
        _ShadowStrength ("Realtime Shadow Strength", Range(0, 1)) = 0.7

        [Header(Plastic Highlight)]
        _SpecColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _SpecSize ("Highlight Threshold", Range(0.1, 0.99)) = 0.72
        _SpecSoftness ("Highlight Edge Softness", Range(0.01, 0.5)) = 0.12
        _SpecStrength ("Highlight Strength", Range(0, 2)) = 0.35

        [Header(Anime Border Highlight)]
        _EdgeColor ("Edge Highlight Color", Color) = (0.78, 0.9, 1.0, 1)
        _EdgeThreshold ("Edge Size", Range(0, 0.95)) = 0.42
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.08
        _EdgeStrength ("Edge Strength", Range(0, 2)) = 0.65
        _EdgeDirection ("Highlight Direction View XYZ", Vector) = (-0.55, 0.80, 0.15, 0)
        _EdgeDirectionality ("Top Left Directionality", Range(0, 1)) = 0.85
        _EdgeShadowInfluence ("Shadow Influence", Range(0, 1)) = 0.35

        [Header(Subtle Rim)]
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimSize ("Rim Size", Range(0, 0.99)) = 0.6
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.04

        [Header(Hemisphere)]
        _TopTint ("Top Tint", Color) = (1.05, 1.05, 1.05, 1)
        _BottomTint ("Bottom Tint", Color) = (0.82, 0.86, 0.95, 1)
        _HemiStrength ("Hemisphere Strength", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        LOD 100

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma target 2.0

            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_instancing

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)

                half4 _BaseColor;
                half4 _ShadowColor;

                half _Wrap;
                half _LightSoftness;
                half _ShadowStrength;

                half4 _SpecColor;
                half _SpecSize;
                half _SpecSoftness;
                half _SpecStrength;

                half4 _EdgeColor;
                half _EdgeThreshold;
                half _EdgeSoftness;
                half _EdgeStrength;
                half4 _EdgeDirection;
                half _EdgeDirectionality;
                half _EdgeShadowInfluence;

                half4 _RimColor;
                half _RimSize;
                half _RimStrength;

                half4 _TopTint;
                half4 _BottomTint;
                half _HemiStrength;

            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS    : NORMAL;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                half3 normalWS     : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.shadowCoord = GetShadowCoord(positionInputs);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 N = normalize(input.normalWS);
                half3 V = normalize(GetWorldSpaceViewDir(input.positionWS));

                Light mainLight = GetMainLight(input.shadowCoord);
                half3 L = normalize(mainLight.direction);

                // ---------------------------------------------------
                // Wrapped diffuse
                // ---------------------------------------------------
                half NdotL = dot(N, L);

                half wrappedLight =
                    saturate(
                        (NdotL + _Wrap) /
                        (1.0h + _Wrap)
                    );

                half lower = 0.5h - (_LightSoftness * 0.5h);
                half upper = 0.5h + (_LightSoftness * 0.5h);

                half diffuse =
                    smoothstep(lower, upper, wrappedLight);

                half shadow =
                    lerp(
                        1.0h,
                        mainLight.shadowAttenuation,
                        _ShadowStrength
                    );

                diffuse *= shadow;

                // ---------------------------------------------------
                // Colored shadows
                // ---------------------------------------------------
                half3 surfaceColor =
                    _BaseColor.rgb *
                    lerp(
                        _ShadowColor.rgb,
                        half3(1.0h, 1.0h, 1.0h),
                        diffuse
                    );

                // ---------------------------------------------------
                // Fake hemisphere lighting
                // ---------------------------------------------------
                half hemi = N.y * 0.5h + 0.5h;

                half3 hemiTint =
                    lerp(
                        _BottomTint.rgb,
                        _TopTint.rgb,
                        hemi
                    );

                surfaceColor *=
                    lerp(
                        half3(1.0h, 1.0h, 1.0h),
                        hemiTint,
                        _HemiStrength
                    );

                surfaceColor *=
                    lerp(
                        half3(1.0h, 1.0h, 1.0h),
                        mainLight.color,
                        0.35h
                    );

                // ---------------------------------------------------
                // Stylized plastic specular
                // Sharper threshold gives the anime/toy highlight.
                // ---------------------------------------------------
                half3 H = normalize(L + V);
                half NdotH = saturate(dot(N, H));

                half specEnd =
                    min(
                        1.0h,
                        _SpecSize + max(_SpecSoftness, 0.001h)
                    );

                half spec =
                    smoothstep(
                        _SpecSize,
                        specEnd,
                        NdotH
                    );

                spec *=
                    _SpecStrength *
                    shadow;

                half3 specular =
                    _SpecColor.rgb *
                    mainLight.color *
                    spec;

                // ---------------------------------------------------
                // Anime border / bevel highlight
                //
                // Fresnel finds silhouette/bevel surfaces.
                // View-space direction masks the effect so it mainly
                // appears on the TOP + LEFT like painted casual-game art.
                //
                // EdgeDirection default:
                // X negative = left
                // Y positive = top
                // ---------------------------------------------------
                half fresnel =
                    1.0h - saturate(dot(N, V));

                half edgeEnd =
                    min(
                        1.0h,
                        _EdgeThreshold + max(_EdgeSoftness, 0.001h)
                    );

                half edgeBand =
                    smoothstep(
                        _EdgeThreshold,
                        edgeEnd,
                        fresnel
                    );

                half3 normalVS =
                    normalize(
                        (half3)mul(
                            (float3x3)UNITY_MATRIX_V,
                            (float3)N
                        )
                    );

                half3 edgeDir =
                    normalize(
                        _EdgeDirection.xyz +
                        half3(0.0001h, 0.0001h, 0.0001h)
                    );

                half directional =
                    smoothstep(
                        -0.20h,
                        0.60h,
                        dot(normalVS, edgeDir)
                    );

                directional =
                    lerp(
                        1.0h,
                        directional,
                        _EdgeDirectionality
                    );

                half edgeShadow =
                    lerp(
                        1.0h,
                        shadow,
                        _EdgeShadowInfluence
                    );

                half3 animeEdge =
                    _EdgeColor.rgb *
                    edgeBand *
                    directional *
                    _EdgeStrength *
                    edgeShadow;

                // ---------------------------------------------------
                // Old subtle rim, kept for compatibility / fill.
                // ---------------------------------------------------
                half rim =
                    smoothstep(
                        _RimSize,
                        1.0h,
                        fresnel
                    );

                rim *= _RimStrength;

                half3 rimLight =
                    _RimColor.rgb * rim;

                // ---------------------------------------------------
                // Final
                // ---------------------------------------------------
                half3 finalColor =
                    surfaceColor +
                    specular +
                    animeEdge +
                    rimLight;

                return half4(
                    finalColor,
                    _BaseColor.a
                );
            }

            ENDHLSL
        }

        // -----------------------------------------------------------
        // Shadow caster
        // -----------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"

            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM

            #pragma target 2.0

            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 GetShadowPosition(ShadowAttributes input)
            {
                float3 positionWS =
                    TransformObjectToWorld(input.positionOS.xyz);

                float3 normalWS =
                    TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW

                    float3 lightDirectionWS =
                        normalize(
                            _LightPosition -
                            positionWS
                        );

                #else

                    float3 lightDirectionWS =
                        _LightDirection;

                #endif

                float4 positionCS =
                    TransformWorldToHClip(
                        ApplyShadowBias(
                            positionWS,
                            normalWS,
                            lightDirectionWS
                        )
                    );

                #if UNITY_REVERSED_Z

                    positionCS.z =
                        min(
                            positionCS.z,
                            UNITY_NEAR_CLIP_VALUE
                        );

                #else

                    positionCS.z =
                        max(
                            positionCS.z,
                            UNITY_NEAR_CLIP_VALUE
                        );

                #endif

                return positionCS;
            }

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                output.positionCS = GetShadowPosition(input);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }
}
