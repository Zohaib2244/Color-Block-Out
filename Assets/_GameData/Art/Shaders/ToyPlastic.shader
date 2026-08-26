Shader "Mobile/ToyPlasticTexture"
{
    Properties
    {
        [Header(Base)]
        [Toggle(_USE_TEXTURE)]
        _UseTexture ("Use Texture", Float) = 0

        [MainTexture]
        _BaseMap ("Base Texture", 2D) = "white" {}

        [MainColor]
        _BaseColor ("Base Color / Texture Tint", Color) = (0.25, 0.65, 1.0, 1)

        _ShadowColor ("Shadow Tint", Color) = (0.45, 0.55, 0.72, 1)

        [Header(Lighting)]
        _Wrap ("Light Wrap", Range(0, 1)) = 0.35
        _LightSoftness ("Light Softness", Range(0.01, 1)) = 0.4
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.7

        [Header(Plastic Highlight)]
        _SpecColor ("Highlight Color", Color) = (1,1,1,1)
        _SpecSize ("Highlight Size", Range(0.1, 0.99)) = 0.72
        _SpecStrength ("Highlight Strength", Range(0, 2)) = 0.3

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimSize ("Rim Size", Range(0, 0.99)) = 0.65
        _RimStrength ("Rim Strength", Range(0,1)) = 0.05

        [Header(Hemisphere)]
        _TopTint ("Top Tint", Color) = (1.05,1.05,1.05,1)
        _BottomTint ("Bottom Tint", Color) = (0.82,0.86,0.95,1)
        _HemiStrength ("Hemisphere Strength", Range(0,1)) = 0.3
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

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma target 2.0

            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local_fragment _USE_TEXTURE

            #pragma multi_compile_instancing

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);


            CBUFFER_START(UnityPerMaterial)

                float4 _BaseMap_ST;

                half4 _BaseColor;
                half4 _ShadowColor;

                half _Wrap;
                half _LightSoftness;
                half _ShadowStrength;

                half4 _SpecColor;
                half _SpecSize;
                half _SpecStrength;

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
                float2 uv         : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                float3 positionWS : TEXCOORD0;
                half3 normalWS    : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;

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

                output.uv =
                    TRANSFORM_TEX(input.uv, _BaseMap);

                output.shadowCoord =
                    GetShadowCoord(positionInputs);

                return output;
            }


            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 N =
                    normalize(input.normalWS);

                half3 V =
                    normalize(
                        GetWorldSpaceViewDir(input.positionWS)
                    );


                // ================================================
                // BASE COLOR
                // ================================================

                half4 baseColor = _BaseColor;

                #if defined(_USE_TEXTURE)

                    half4 tex =
                        SAMPLE_TEXTURE2D(
                            _BaseMap,
                            sampler_BaseMap,
                            input.uv
                        );

                    // Texture multiplied by color tint.
                    baseColor *= tex;

                #endif


                // ================================================
                // MAIN LIGHT
                // ================================================

                Light mainLight =
                    GetMainLight(input.shadowCoord);

                half3 L =
                    normalize(mainLight.direction);


                // ================================================
                // SOFT WRAPPED LIGHTING
                // ================================================

                half NdotL =
                    dot(N, L);

                half wrapped =
                    saturate(
                        (NdotL + _Wrap) /
                        (1.0h + _Wrap)
                    );

                half lower =
                    0.5h -
                    (_LightSoftness * 0.5h);

                half upper =
                    0.5h +
                    (_LightSoftness * 0.5h);

                half diffuse =
                    smoothstep(
                        lower,
                        upper,
                        wrapped
                    );


                // ================================================
                // SHADOW
                // ================================================

                half shadow =
                    lerp(
                        1.0h,
                        mainLight.shadowAttenuation,
                        _ShadowStrength
                    );

                diffuse *= shadow;


                // ================================================
                // COLORED SHADOW
                // ================================================

                half3 surfaceColor =
                    baseColor.rgb *
                    lerp(
                        _ShadowColor.rgb,
                        half3(1,1,1),
                        diffuse
                    );


                // ================================================
                // FAKE HEMISPHERE / STUDIO LIGHT
                // ================================================

                half hemi =
                    N.y * 0.5h + 0.5h;

                half3 hemiTint =
                    lerp(
                        _BottomTint.rgb,
                        _TopTint.rgb,
                        hemi
                    );

                surfaceColor *=
                    lerp(
                        half3(1,1,1),
                        hemiTint,
                        _HemiStrength
                    );


                // ================================================
                // BROAD PLASTIC SPECULAR
                // ================================================

                half3 H =
                    normalize(L + V);

                half NdotH =
                    saturate(dot(N, H));

                half spec =
                    smoothstep(
                        _SpecSize,
                        1.0h,
                        NdotH
                    );

                spec *=
                    _SpecStrength *
                    shadow;

                half3 specular =
                    _SpecColor.rgb *
                    mainLight.color *
                    spec;


                // ================================================
                // RIM
                // ================================================

                half fresnel =
                    1.0h -
                    saturate(dot(N, V));

                half rim =
                    smoothstep(
                        _RimSize,
                        1.0h,
                        fresnel
                    );

                rim *=
                    _RimStrength;

                half3 rimLight =
                    _RimColor.rgb *
                    rim;


                // ================================================
                // FINAL
                // ================================================

                half3 finalColor =
                    surfaceColor +
                    specular +
                    rimLight;

                return half4(
                    finalColor,
                    baseColor.a
                );
            }

            ENDHLSL
        }


        // =======================================================
        // SHADOW CASTER
        // =======================================================

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


            float4 GetShadowPosition(
                ShadowAttributes input
            )
            {
                float3 positionWS =
                    TransformObjectToWorld(
                        input.positionOS.xyz
                    );

                float3 normalWS =
                    TransformObjectToWorldNormal(
                        input.normalOS
                    );

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


            ShadowVaryings ShadowVert(
                ShadowAttributes input
            )
            {
                ShadowVaryings output;

                output.positionCS =
                    GetShadowPosition(input);

                return output;
            }


            half4 ShadowFrag(
                ShadowVaryings input
            ) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }
}