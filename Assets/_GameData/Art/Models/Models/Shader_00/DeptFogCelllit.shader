Shader "Game/DepthFogCellLit"
{
    Properties
    {
        // ----------------------------------------------------
        // Surface
        // ----------------------------------------------------

        [MainTexture]
        _BaseMap ("Base Map", 2D) = "white" {}

        [MainColor]
        _BaseColor ("Base Color", Color) = (0.5, 0.5, 0.5, 1)


        // ----------------------------------------------------
        // Bottom Cell / Lower Section
        // ----------------------------------------------------

        _BottomColor ("Bottom Color", Color) = (0.25, 0.25, 0.25, 1)

        _BottomColorStrength
        (
            "Bottom Color Strength",
            Range(0, 1)
        ) = 1

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


        // ----------------------------------------------------
        // URP Lit style PBR
        // ----------------------------------------------------

        [Toggle(_SPECULAR_SETUP)]
        _SpecularWorkflow
        (
            "Use Specular Workflow",
            Float
        ) = 0

        _Metallic
        (
            "Metallic",
            Range(0, 1)
        ) = 0

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
        ) = 1


        // ----------------------------------------------------
        // Lighting toggles
        // ----------------------------------------------------

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


            // ------------------------------------------------
            // Anything marked by DepthFogMask won't render.
            // ------------------------------------------------

            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
            }


            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag


            // ------------------------------------------------
            // URP Lighting
            // ------------------------------------------------

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN

            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #pragma multi_compile_fog


            // ------------------------------------------------
            // Material features
            // ------------------------------------------------

            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF

            #pragma multi_compile_instancing


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"


            // =================================================
            // INPUT
            // =================================================

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;

                float2 uv         : TEXCOORD2;

                float fogFactor   : TEXCOORD3;

                float positionOSY : TEXCOORD4;

                UNITY_VERTEX_OUTPUT_STEREO
            };


            // =================================================
            // MATERIAL
            // =================================================

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);


            CBUFFER_START(UnityPerMaterial)

                float4 _BaseMap_ST;

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
                    GetVertexPositionInputs(input.positionOS.xyz);


                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS);


                output.positionCS =
                    positionInputs.positionCS;

                output.positionWS =
                    positionInputs.positionWS;

                output.normalWS =
                    normalInputs.normalWS;


                output.uv =
                    TRANSFORM_TEX(
                        input.uv,
                        _BaseMap
                    );


                output.fogFactor =
                    ComputeFogFactor(
                        output.positionCS.z
                    );


                // Used for Bottom Color
                output.positionOSY =
                    input.positionOS.y;


                return output;
            }


            // =================================================
            // FRAGMENT
            // =================================================

            half4 Frag(Varyings input) : SV_Target
            {
                // ---------------------------------------------
                // Texture
                // ---------------------------------------------

                half4 baseSample =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        input.uv
                    );


                half3 baseColor =
                    baseSample.rgb *
                    _BaseColor.rgb;


                // ---------------------------------------------
                // Bottom section color
                //
                // BottomStart = -0.5
                // BottomEnd   =  0
                //
                // works nicely with a centered cube.
                // ---------------------------------------------

                float bottomRange =
                    max(
                        0.0001,
                        _BottomEnd -
                        _BottomStart
                    );


                float bottomT =
                    saturate(
                        (
                            input.positionOSY -
                            _BottomStart
                        )
                        /
                        bottomRange
                    );


                half bottomMask =
                    1.0h -
                    smoothstep(
                        0.0,
                        1.0,
                        bottomT
                    );


                bottomMask *=
                    _BottomColorStrength;


                baseColor =
                    lerp(
                        baseColor,
                        baseColor *
                        _BottomColor.rgb,
                        bottomMask
                    );


                // ---------------------------------------------
                // Surface Data
                // ---------------------------------------------

                SurfaceData surfaceData =
                    (SurfaceData)0;


                surfaceData.albedo =
                    baseColor;


                surfaceData.alpha =
                    1.0h;


                surfaceData.metallic =
                    _Metallic;


                surfaceData.specular =
                    _SpecColor.rgb;


                surfaceData.smoothness =
                    _Smoothness;


                surfaceData.normalTS =
                    half3(
                        0.0h,
                        0.0h,
                        1.0h
                    );


                surfaceData.occlusion =
                    _OcclusionStrength;


                surfaceData.emission =
                    half3(
                        0.0h,
                        0.0h,
                        0.0h
                    );


                surfaceData.clearCoatMask =
                    0.0h;


                surfaceData.clearCoatSmoothness =
                    0.0h;


                // ---------------------------------------------
                // Input Data
                // ---------------------------------------------

                InputData inputData =
                    (InputData)0;


                inputData.positionWS =
                    input.positionWS;


                inputData.normalWS =
                    NormalizeNormalPerPixel(
                        input.normalWS
                    );


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
                        inputData.normalWS
                    );


                inputData.bakedGI =
                    SampleSH(
                        inputData.normalWS
                    );


                inputData.normalizedScreenSpaceUV =
                    GetNormalizedScreenSpaceUV(
                        input.positionCS
                    );


                inputData.shadowMask =
                    half4(
                        1,
                        1,
                        1,
                        1
                    );


                // ---------------------------------------------
                // URP PBR
                // ---------------------------------------------

                half4 color =
                    UniversalFragmentPBR(
                        inputData,
                        surfaceData
                    );


                color.rgb =
                    MixFog(
                        color.rgb,
                        inputData.fogCoord
                    );


                color.a = 1;


                return color;
            }

            ENDHLSL
        }
    }

    FallBack Off
}