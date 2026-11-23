using UnityEngine;

/// <summary>
/// Mueve las texturas del agua para que el lago se vea vivo.
/// El material tiene dos capas de relieve: olas grandes (mapa principal) y rizos finos
/// (mapa de detalle). Cada una se desplaza en una dirección distinta y, al combinarse,
/// el agua parece moverse sin repetirse.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class AguaAnimada : MonoBehaviour
{
    public Vector2 velocidadOlas = new Vector2(0.012f, 0.007f);     // UV por segundo
    public Vector2 velocidadRizos = new Vector2(-0.035f, 0.02f);

    private Material material;
    private Vector2 desplazamientoOlas;
    private Vector2 desplazamientoRizos;

    void Start()
    {
        material = GetComponent<Renderer>().material;    // copia propia del material
    }

    void Update()
    {
        desplazamientoOlas += velocidadOlas * Time.deltaTime;
        desplazamientoRizos += velocidadRizos * Time.deltaTime;
        material.SetTextureOffset("_BaseMap", desplazamientoOlas);           // color y olas
        material.SetTextureOffset("_DetailAlbedoMap", desplazamientoRizos);  // rizos (detalle)
    }
}
