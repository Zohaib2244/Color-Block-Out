Shader "Game/DepthFogMask"
{
    Properties
    {
        [HDR]
        _FogColor ("Fog Color", Color) = (0.02, 0.02, 0.02, 1)

        _FogOpacity
        (
            "Fog Transparency",
            Range(0, 1)
        ) = 1.0

        _FogDepth
        (
            "Fog Depth",
            Range(0.01, 10.0)
        ) = 1.0

        [Enum(UnityEngine.Rendering.CullMode)]
        _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"

            // Important:
            // Render after normal background,
            // but BEFORE our masked cells.
            "Queue" = "Geometry+5"
        }

        Pass
        {
            Name "DepthFog"

            // ===============================================
            // TRANSPARENCY
            // ===============================================

            Blend SrcAlpha OneMinusSrcAlpha

            ZWrite Off
            ZTest LEqual

            Cull [_Cull]


            // ===============================================
            // STENCIL MASK
            //
            // Cells will check for Stencil != 1.
            // ===============================================

            Stencil
            {
                Ref 1

                Comp Always
                Pass Replace

                Fail Keep
                ZFail Keep
            }


            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            struct Attributes
            {
                float4 positionOS : POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                UNITY_VERTEX_OUTPUT_STEREO
            };


            CBUFFER_START(UnityPerMaterial)

                half4 _FogColor;

                half _FogOpacity;

                float _FogDepth;

            CBUFFER_END


            // ===============================================
            // VERTEX
            // ===============================================

            Varyings Vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);


                float3 positionOS =
                    input.positionOS.xyz;


                // ===========================================
                // FOG DEPTH
                //
                // Assumes centered cube:
                //
                // Top    = +0.5
                // Bottom = -0.5
                //
                // Top remains fixed.
                // Bottom extends downward.
                // ===========================================

                float distanceFromTop =
                    0.5 - positionOS.y;


                positionOS.y =
                    0.5 -
                    distanceFromTop *
                    _FogDepth;


                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(
                        positionOS
                    );


                output.positionCS =
                    positionInputs.positionCS;


                return output;
            }


            // ===============================================
            // FRAGMENT
            // ===============================================

            half4 Frag(Varyings input) : SV_Target
            {
                half alpha =
                    _FogColor.a *
                    _FogOpacity;


                return half4(
                    _FogColor.rgb,
                    alpha
                );
            }

            ENDHLSL
        }
    }

    FallBack Off
}