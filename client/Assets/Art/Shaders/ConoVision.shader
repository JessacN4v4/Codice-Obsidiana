// Cono de visión de los guardias: color plano y transparente (sin luz ni sombras).
// La transparencia de cada vértice (alfa del color del vértice) la pone VistaAgentes:
// fuerte junto al guardia y más tenue hacia el borde del alcance, para que se desvanezca.
Shader "Codice/ConoVision"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.95, 0.7, 0.25)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ConoVision"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
            };

            Varyings vert(Attributes entrada)
            {
                Varyings salida;
                salida.positionHCS = TransformObjectToHClip(entrada.positionOS.xyz);
                salida.color = entrada.color;
                return salida;
            }

            half4 frag(Varyings entrada) : SV_Target
            {
                half4 color = _BaseColor;
                color.a *= entrada.color.a;
                return color;
            }
            ENDHLSL
        }
    }
}
