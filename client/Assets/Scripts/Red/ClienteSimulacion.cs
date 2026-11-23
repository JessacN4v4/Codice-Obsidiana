using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Habla con el servidor de simulación (FastAPI + Mesa).
/// Al empezar crea una partida (POST /sessions). Después, cada 0.1 s manda la posición
/// del jugador (POST /sessions/{id}/step) y recibe el estado: guardias, aldeanos,
/// sospecha, trojes, eventos...
/// Si el juego se atrasa, pide varios pasos a la vez (máximo 10).
/// </summary>
public class ClienteSimulacion : MonoBehaviour
{
    public string urlServidor = "http://127.0.0.1:8000";
    public MovimientoJugador jugador;
    public float intervalo = 0.1f;             // segundos entre pasos (10 por segundo)

    // Último estado recibido y su hora (los demás scripts lo leen)
    public EstadoDatos Estado { get; private set; }
    public float HoraUltimoEstado { get; private set; }
    public bool Conectado { get; private set; }
    public string ErrorConexion { get; private set; }   // vacío si todo va bien

    // Se llama cada vez que llega un estado nuevo
    public event Action<EstadoDatos> AlRecibirEstado;

    // Lo que pueden pedir otros scripts para el siguiente paso
    [HideInInspector] public string trojeQuemando = "";    // id del troje mientras se mantiene E
    private bool hayPiedra = false;
    private Vector2 destinoPiedra;

    private string idSesion;

    // ¿Hay una piedra esperando a mandarse en el siguiente paso?
    public bool PiedraPendiente
    {
        get { return hayPiedra; }
    }

    public void LanzarPiedra(Vector3 destinoEnMundo)
    {
        hayPiedra = true;
        destinoPiedra = Mapa.AMapa(destinoEnMundo);
    }

    IEnumerator Start()
    {
        yield return CrearSesion();
        if (idSesion == null)
        {
            yield break;
        }

        float ultimoPaso = Time.time;
        while (true)
        {
            float transcurrido = Time.time - ultimoPaso;
            if (transcurrido < intervalo)
            {
                yield return null;              // esperar al siguiente frame
                continue;
            }
            int pasos = Mathf.Clamp(Mathf.FloorToInt(transcurrido / intervalo), 1, 10);
            ultimoPaso += pasos * intervalo;
            if (Time.time - ultimoPaso > 1f)
            {
                ultimoPaso = Time.time;         // muy atrasados: no intentar recuperar todo
            }

            yield return EnviarPaso(pasos);

            if (Estado != null && Estado.over)
            {
                Debug.Log("Partida terminada: " + Estado.result);
                yield break;
            }
        }
    }

    IEnumerator CrearSesion()
    {
        // "using" libera la petición al terminar (si no, Unity avisa de memoria sin liberar)
        using (UnityWebRequest peticion = CrearPeticion(urlServidor + "/sessions", "{}"))
        {
            yield return peticion.SendWebRequest();
            if (peticion.result != UnityWebRequest.Result.Success)
            {
                ErrorConexion = "No se pudo conectar con el servidor en " + urlServidor
                                + ". ¿Está corriendo uvicorn? (" + peticion.error + ")";
                Debug.LogError(ErrorConexion);
                yield break;
            }
            RecibirEstado(peticion.downloadHandler.text);
        }
        idSesion = Estado.sessionId;
        Conectado = true;
        Debug.Log("Conectado al servidor. Partida " + idSesion);
    }

    IEnumerator EnviarPaso(int pasos)
    {
        JugadorDatos datosJugador = new JugadorDatos();
        Vector2 p = Mapa.AMapa(jugador.transform.position);
        datosJugador.x = p.x;
        datosJugador.y = p.y;
        datosJugador.running = jugador.Corriendo;
        datosJugador.lighting = trojeQuemando;

        string json;
        if (hayPiedra)
        {
            PasoConPiedraPeticion conPiedra = new PasoConPiedraPeticion();
            conPiedra.steps = pasos;
            conPiedra.player = datosJugador;
            conPiedra.stone = new PuntoPiedra();
            conPiedra.stone.x = destinoPiedra.x;
            conPiedra.stone.y = destinoPiedra.y;
            json = JsonUtility.ToJson(conPiedra);
            hayPiedra = false;
        }
        else
        {
            PasoPeticion paso = new PasoPeticion();
            paso.steps = pasos;
            paso.player = datosJugador;
            json = JsonUtility.ToJson(paso);
        }

        using (UnityWebRequest peticion = CrearPeticion(urlServidor + "/sessions/" + idSesion + "/step", json))
        {
            yield return peticion.SendWebRequest();
            if (peticion.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("Falló un paso: " + peticion.error);
                yield break;
            }
            RecibirEstado(peticion.downloadHandler.text);
        }
    }

    void RecibirEstado(string json)
    {
        Estado = JsonUtility.FromJson<EstadoDatos>(json);
        HoraUltimoEstado = Time.time;
        if (Estado.events != null)
        {
            foreach (EventoDatos e in Estado.events)
            {
                Debug.Log("[" + e.type + "] " + e.message);    // por ahora en la consola; luego en el HUD
            }
        }
        if (AlRecibirEstado != null)
        {
            AlRecibirEstado(Estado);
        }
    }

    UnityWebRequest CrearPeticion(string url, string json)
    {
        UnityWebRequest peticion = new UnityWebRequest(url, "POST");
        peticion.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        peticion.downloadHandler = new DownloadHandlerBuffer();
        peticion.SetRequestHeader("Content-Type", "application/json");
        return peticion;
    }
}
