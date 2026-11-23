using UnityEngine;

/// <summary>
/// Piedra que vuela en arco desde el jugador hasta el punto elegido.
/// Tarda lo mismo que en el servidor (0.45 s). Al caer crea una onda de ruido y desaparece.
/// Solo es visual: el ruido de verdad lo calcula el servidor.
/// </summary>
public class PiedraVisual : MonoBehaviour
{
    public Vector3 desde;
    public Vector3 hacia;
    public float duracion = 0.45f;
    public float alturaArco = 2f;
    public float radioRuido = 11.5f;           // metros (230 px del mapa)
    public Material materialRuido;

    private float t = 0f;                      // 0 = recién lanzada, 1 = cayó

    void Update()
    {
        t += Time.deltaTime / duracion;
        if (t >= 1f)
        {
            CrearOnda();
            Destroy(gameObject);
            return;
        }
        Vector3 p = Vector3.Lerp(desde, hacia, t);
        p.y += Mathf.Sin(t * Mathf.PI) * alturaArco;   // sube y baja como parábola
        transform.position = p;
    }

    void CrearOnda()
    {
        GameObject onda = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        onda.name = "OndaRuido";
        Destroy(onda.GetComponent<Collider>());
        onda.transform.position = new Vector3(hacia.x, 0.06f, hacia.z);
        onda.transform.localScale = new Vector3(0.1f, 0.01f, 0.1f);
        onda.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        OndaRuido efecto = onda.AddComponent<OndaRuido>();
        efecto.radioFinal = radioRuido;
        efecto.material = new Material(materialRuido);   // copia propia para desvanecerla
        onda.GetComponent<Renderer>().sharedMaterial = efecto.material;
    }
}
