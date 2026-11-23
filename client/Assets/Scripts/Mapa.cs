using UnityEngine;

/// <summary>
/// Conversión entre las coordenadas del mapa del servidor y las de Unity.
/// Mapa (servidor): x a la derecha, y hacia ABAJO, en píxeles (mundo de 2000 x 1300).
/// Unity: x a la derecha, z hacia adelante (norte), y hacia arriba, en metros.
/// Un píxel del mapa = 0.05 m, así el pueblo mide 100 x 65 m.
/// </summary>
public static class Mapa
{
    public const float UnidadesPorPixel = 0.05f;

    /// <summary>Punto del mapa (x, y) a posición en Unity (sobre el suelo).</summary>
    public static Vector3 AMundo(float x, float y)
    {
        return new Vector3(x * UnidadesPorPixel, 0f, -y * UnidadesPorPixel);
    }

    /// <summary>Posición en Unity a punto del mapa (x, y).</summary>
    public static Vector2 AMapa(Vector3 posicion)
    {
        return new Vector2(posicion.x / UnidadesPorPixel, -posicion.z / UnidadesPorPixel);
    }

    /// <summary>
    /// Ángulo del mapa (radianes, 0 = este, +pi/2 = sur) a rotación en Unity (grados alrededor de Y).
    /// En el mapa la dirección es (cos a, sin a); en Unity queda (cos a, 0, -sin a).
    /// </summary>
    public static float AnguloAGrados(float anguloMapa)
    {
        return Mathf.Atan2(Mathf.Cos(anguloMapa), -Mathf.Sin(anguloMapa)) * Mathf.Rad2Deg;
    }
}
