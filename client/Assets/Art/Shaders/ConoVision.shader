// Cono de visión de los guardias: color plano y transparente (sin luz ni sombras).
// La transparencia de cada vértice (alfa del color del vértice) la pone VistaAgentes:
// fuerte junto al guardia y más tenue hacia el borde del alcance, para que se desvanezca.
// La sospecha se pinta en el cono: desde el guardia hasta _Progreso (0 a 1) de su alcance
// queda del color _ColorSospecha. Así se ve qué guardia te está viendo y cuánto le falta.
Shader "Codice/ConoVision"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.95, 0.7, 0.25)
        _ColorSospecha ("Color de la sospecha", Color) = (1, 0.1, 0.05, 0.5)
        _Progreso ("Sospecha (0 a 1)", Range(0, 1)) = 0
        _Origen ("Posición del guardia (mundo)", Vector) = (0, 0, 0, 0)
        _Alcance ("Alcance (m)", Float) = 10
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
                half4 _ColorSospecha;
                float _Progreso;
                float4 _Origen;
                float _Alcance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes entrada)
            {
                Varyings salida;
                salida.positionWS = TransformObjectToWorld(entrada.positionOS.xyz);
                salida.positionHCS = TransformWorldToHClip(salida.positionWS);
                salida.color = entrada.color;
                return salida;
            }

            half4 frag(Varyings entrada) : SV_Target
            {
                // qué tan lejos del guardia está este punto (0 = en el guardia, 1 = en el alcance)
                float d = distance(entrada.positionWS.xz, _Origen.xz) / max(_Alcance, 0.001);
                // 1 dentro de la parte ya pintada, 0 fuera; el frente de la sospecha con borde suave
                half pintado = _Progreso > 0.001 ? 1.0 - smoothstep(_Progreso - 0.03, _Progreso, d) : 0.0;
                half4 color = lerp(_BaseColor, _ColorSospecha, pintado);
                // el rojo se desvanece menos hacia la punta, para que se vea hasta dónde llegó
                color.a *= lerp(entrada.color.a, 1.0, pintado * 0.6);
                return color;
            }
            ENDHLSL
        }
    }
}
