Shader "Game/CellLitMasked"
{
    Properties
    {
        // ====================================================
        // COLORS
        // ====================================================

        [MainColor]
        _BaseColor ("Cell Color", Color) = (0.45, 0.45, 0.45, 1)

        _BottomColor ("Bottom Color", Color) = (0.15, 0.15, 0.15, 1)

        _BottomColorStrength
        (
            "Bottom Color Strength",
            Range(0, 1)
        ) = 1.0

        _BottomStart
        (
            "Bottom Start",
            Range(-1, 1)
        ) = -0.5

        _BottomEnd
        (
            "Bottom End",
            Range(-1, 1)
        ) = 0.0


        // ====================================================
        // LIGHTING
        // ====================================================

        [Toggle(_SPECULAR_SETUP)]
        _SpecularWorkflow
        (
            "Specular Workflow",
            Float
        ) = 1

        _Metallic
        (
            "Metallic",
            Range(0, 1)
        ) = 0.0

        _SpecColor
        (
            "Specular Color",
            Color
        ) = (0.2, 0.2, 0.2, 1)

        _Smoothness
        (
            "Smoothness",
            Range(0, 1)
        ) = 0.5

        _OcclusionStrength
        (
            "Occlusion",
            Range(0, 1)
        ) = 1.0


        // ====================================================
        // URP OPTIONS
        // ====================================================

        [ToggleOff]
        _SpecularHighlights
        (
            "Specular Highlights",
            Float
        ) = 1

        [ToggleOff]
        _EnvironmentReflections
        (
            "Environment Reflections",
            Float
        ) = 1
    }


    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }


        Pass
        {
            Name "ForwardLit"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Back
            ZWrite On
            ZTest LEqual


            // =================================================
            // FOG MASK
            //
            // Fog shader writes Stencil = 1.
            // Cells do NOT render where stencil == 1.
            // =================================================

            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
                Fail Keep
                ZFail Keep
            }


            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            // =================================================
            // URP LIGHTING
            // =================================================

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN

            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #pragma multi_compile_fog
            #pragma multi_compile_instancing


            // =================================================
            // MATERIAL FEATURES
            // =================================================

            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


            // =================================================
            // INPUT
            // =================================================

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                float3 positionWS : TEXCOORD0;
                half3 normalWS    : TEXCOORD1;

                half fogFactor    : TEXCOORD2;

                // Local Y coordinate for bottom coloring.
                float localY      : TEXCOORD3;

                UNITY_VERTEX_OUTPUT_STEREO
            };


            // =================================================
            // MATERIAL PARAMETERS
            // =================================================

            CBUFFER_START(UnityPerMaterial)

                half4 _BaseColor;

                half4 _BottomColor;
                half _BottomColorStrength;

                float _BottomStart;
                float _BottomEnd;

                half _Metallic;
                half4 _SpecColor;
                half _Smoothness;
                half _OcclusionStrength;

            CBUFFER_END


            // =================================================
            // VERTEX
            // =================================================

            Varyings Vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);


                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(
                        input.positionOS.xyz
                    );


                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(
                        input.normalOS
                    );


                output.positionCS =
                    positionInputs.positionCS;


                output.positionWS =
                    positionInputs.positionWS;


                output.normalWS =
                    normalInputs.normalWS;


                output.fogFactor =
                    ComputeFogFactor(
                        output.positionCS.z
                    );


                output.localY =
                    input.positionOS.y;


                return output;
            }


            // =================================================
            // FRAGMENT
            // =================================================

            half4 Frag(Varyings input) : SV_Target
            {
                // =================================================
                // NORMAL
                // =================================================

                half3 normalWS =
                    NormalizeNormalPerPixel(
                        input.normalWS
                    );


                // =================================================
                // BASE COLOR
                // =================================================

                half3 finalColor =
                    _BaseColor.rgb;


                // =================================================
                // BOTTOM COLOR MASK
                //
                // For a centered cube:
                //
                // Top    ~= +0.5
                // Bottom ~= -0.5
                //
                // Example:
                //
                // Bottom Start = -0.5
                // Bottom End   =  0.0
                // =================================================

                float bottomRange =
                    max(
                        0.0001,
                        _BottomEnd - _BottomStart
                    );


                float bottomGradient =
                    saturate(
                        (
                            input.localY -
                            _BottomStart
                        )
                        /
                        bottomRange
                    );


                half bottomMask =
                    1.0h -
                    smoothstep(
                        0.0h,
                        1.0h,
                        bottomGradient
                    );


                bottomMask *=
                    _BottomColorStrength;


                finalColor =
                    lerp(
                        finalColor,
                        _BottomColor.rgb,
                        bottomMask
                    );


                // =================================================
                // URP INPUT DATA
                // =================================================

                InputData inputData =
                    (InputData)0;


                inputData.positionWS =
                    input.positionWS;


                inputData.normalWS =
                    normalWS;


                inputData.viewDirectionWS =
                    GetWorldSpaceNormalizeViewDir(
                        input.positionWS
                    );


                inputData.shadowCoord =
                    TransformWorldToShadowCoord(
                        input.positionWS
                    );


                inputData.fogCoord =
                    input.fogFactor;


                inputData.vertexLighting =
                    VertexLighting(
                        input.positionWS,
                        normalWS
                    );


                inputData.bakedGI =
                    SampleSH(
                        normalWS
                    );


                inputData.normalizedScreenSpaceUV =
                    GetNormalizedScreenSpaceUV(
                        input.positionCS
                    );


                inputData.shadowMask =
                    half4(
                        1.0h,
                        1.0h,
                        1.0h,
                        1.0h
                    );


                // =================================================
                // URP PBR
                // =================================================

                half4 color =
                    UniversalFragmentPBR(
                        inputData,

                        // Albedo
                        finalColor,

                        // Metallic
                        _Metallic,

                        // Specular
                        _SpecColor.rgb,

                        // Smoothness
                        _Smoothness,

                        // Occlusion
                        _OcclusionStrength,

                        // Emission
                        half3(0, 0, 0),

                        // Alpha
                        1.0h
                    );


                // =================================================
                // UNITY FOG
                // =================================================

                color.rgb =
                    MixFog(
                        color.rgb,
                        inputData.fogCoord
                    );


                color.a = 1.0h;

                return color;
            }

            ENDHLSL
        }
    }

    FallBack Off
}