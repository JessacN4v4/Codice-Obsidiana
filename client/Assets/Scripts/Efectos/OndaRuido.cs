using UnityEngine;

/// <summary>
/// Disco en el suelo que crece hasta el radio del ruido y se desvanece.
/// Muestra hasta dónde llega el ruido de una piedra (los guardias dentro lo oyen).
/// </summary>
public class OndaRuido : MonoBehaviour
{
    public float radioFinal = 11.5f;
    public float duracion = 0.8f;
    public Material material;

    private float t = 0f;
    private Color colorInicial;

    void Start()
    {
        colorInicial = material.GetColor("_BaseColor");
    }

    void Update()
    {
        t += Time.deltaTime / duracion;
        if (t >= 1f)
        {
            Destroy(material);
            Destroy(gameObject);
            return;
        }
        float diametro = 2f * radioFinal * t;
        transform.localScale = new Vector3(diametro, 0.01f, diametro);
        Color c = colorInicial;
        c.a = colorInicial.a * (1f - t);       // se desvanece al crecer
        material.SetColor("_BaseColor", c);
    }
}
