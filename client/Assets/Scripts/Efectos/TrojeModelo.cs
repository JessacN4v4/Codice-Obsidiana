using System.Collections;
using UnityEngine;

/// <summary>
/// Modelo visual de un troje con sus tres estados:
/// 1. Intacto.
/// 2. Ardiendo: el mismo modelo intacto con fuego y humo.
/// 3. Quemado: después de unos segundos cambia al modelo quemado; queda un fuego bajo y humo.
/// VistaAgentes llama a Encender() cuando el servidor dice que el troje arde.
/// </summary>
public class TrojeModelo : MonoBehaviour
{
    public string id;                      // "A", "B" o "C" (igual que en el servidor)
    public GameObject intacto;
    public GameObject quemado;
    public ParticleSystem fuego;
    public ParticleSystem humo;
    public float segundosArdiendo = 6f;    // tiempo con el modelo intacto en llamas
    public float fuegoAlFinal = 0.3f;      // fracción del fuego que queda como brasas

    public bool Encendido { get; private set; }

    void Start()
    {
        intacto.SetActive(true);
        quemado.SetActive(false);
    }

    public void Encender()
    {
        if (Encendido)
        {
            return;
        }
        Encendido = true;
        fuego.Play();
        humo.Play();
        StartCoroutine(Arder());
    }

    IEnumerator Arder()
    {
        yield return new WaitForSeconds(segundosArdiendo);
        // el techo se cae: queda el troje quemado con un fuego más bajo
        intacto.SetActive(false);
        quemado.SetActive(true);
        ParticleSystem.EmissionModule emision = fuego.emission;
        emision.rateOverTime = emision.rateOverTime.constant * fuegoAlFinal;
    }
}
