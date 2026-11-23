using UnityEngine;

/// <summary>
/// Cambia el atuendo de Tariácuri cuando arden los tres trojes.
/// Antes: disfrazado de tameme (cargador), se confunde con los cargadores del pueblo.
/// Después: con su atuendo de guerrero, para que se note que el disfraz ya no sirve
/// y que los guardias lo reconocen más rápido (eso lo decide el servidor en la fase 2).
/// Los dos modelos son hijos del jugador; aquí solo se prende uno y se apaga el otro.
/// </summary>
public class AtuendoJugador : MonoBehaviour
{
    public ClienteSimulacion cliente;
    public HUD hud;                            // opcional: para avisar del cambio
    public GameObject modeloDisfraz;           // "Modelo": Tariácuri vestido de tameme
    public GameObject modeloGuerrero;          // "ModeloGuerrero": Tariácuri con atuendo de guerrero

    public int trojesParaCambiar = 3;

    private bool deGuerrero = false;

    void Start()
    {
        if (cliente == null)
        {
            cliente = FindAnyObjectByType<ClienteSimulacion>();
        }
        if (hud == null)
        {
            hud = FindAnyObjectByType<HUD>();
        }
        Vestir(false);                         // siempre empieza disfrazado
    }

    void Update()
    {
        if (deGuerrero || cliente == null || cliente.Estado == null)
        {
            return;
        }
        if (cliente.Estado.burned >= trojesParaCambiar)
        {
            Vestir(true);
            if (hud != null)
            {
                hud.Avisar("Tariácuri se quita el disfraz: ahora te reconocen más rápido.", new Color(1f, 0.6f, 0.35f));
            }
        }
    }

    void Vestir(bool guerrero)
    {
        deGuerrero = guerrero;
        if (modeloGuerrero == null)
        {
            return;                            // sin modelo de guerrero se queda con el disfraz
        }
        if (modeloDisfraz != null)
        {
            modeloDisfraz.SetActive(!guerrero);
        }
        modeloGuerrero.SetActive(guerrero);
    }
}
