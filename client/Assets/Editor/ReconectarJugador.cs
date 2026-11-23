using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú "Codice > Reconectar referencias del jugador".
/// Cuando "Colocar jugador y cámara" vuelve a crear al jugador, los demás objetos se quedan
/// apuntando al jugador borrado (la red no manda la posición, no hay piedras ni HUD y la cámara
/// de seguimiento no sigue a nadie). Este menú NO borra nada: solo rellena las referencias vacías
/// y vuelve a poner InteraccionJugador en el jugador si le falta.
/// </summary>
public static class ReconectarJugador
{
    const string CarpetaMateriales = "Assets/Graybox/Materiales";
    const string RutaEscena = "Assets/Scenes/Pueblo.unity";

    [MenuItem("Codice/Reconectar referencias del jugador")]
    public static void Reconectar()
    {
        int arregladas = ReconectarEnEscenaActual();
        EditorUtility.DisplayDialog("Referencias del jugador",
            arregladas == 0 ? "Todo estaba conectado." : "Se reconectaron " + arregladas + " referencias. Guarda la escena (Ctrl + S).",
            "OK");
    }

    /// <summary>
    /// Para la línea de comandos (sin abrir el editor):
    /// Unity.exe -batchmode -projectPath client -executeMethod ReconectarJugador.ReconectarEnEscenaPueblo -quit
    /// </summary>
    public static void ReconectarEnEscenaPueblo()
    {
        EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        ReconectarEnEscenaActual();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Escena guardada: " + RutaEscena);
    }

    /// <summary>Rellena las referencias vacías al jugador. Devuelve cuántas arregló.</summary>
    public static int ReconectarEnEscenaActual()
    {
        MovimientoJugador jugador = Object.FindAnyObjectByType<MovimientoJugador>();
        if (jugador == null)
        {
            Debug.LogError("Reconectar: no hay jugador en la escena (usa Codice > Colocar jugador y cámara).");
            return 0;
        }
        int n = 0;
        ClienteSimulacion cliente = Object.FindAnyObjectByType<ClienteSimulacion>();

        // acciones del jugador (quemar trojes y piedras): viven en el propio jugador
        InteraccionJugador interaccion = jugador.GetComponent<InteraccionJugador>();
        if (interaccion == null)
        {
            interaccion = Undo.AddComponent<InteraccionJugador>(jugador.gameObject);
            n++;
            Debug.Log("Reconectar: InteraccionJugador agregado al jugador.");
        }
        n += Poner(ref interaccion.cliente, cliente, interaccion, "InteraccionJugador.cliente");
        n += Poner(ref interaccion.movimiento, jugador, interaccion, "InteraccionJugador.movimiento");
        n += Poner(ref interaccion.materialPiedra, Material("Piedra"), interaccion, "InteraccionJugador.materialPiedra");
        n += Poner(ref interaccion.materialRuido, Material("OndaRuido"), interaccion, "InteraccionJugador.materialRuido");

        if (cliente != null)
        {
            n += Poner(ref cliente.jugador, jugador, cliente, "ClienteSimulacion.jugador");
        }
        else
        {
            Debug.LogWarning("Reconectar: no hay ClienteSimulacion (usa Codice > Conectar con el servidor).");
        }

        HUD hud = Object.FindAnyObjectByType<HUD>();
        if (hud != null)
        {
            n += Poner(ref hud.movimiento, jugador, hud, "HUD.movimiento");
            n += Poner(ref hud.interaccion, interaccion, hud, "HUD.interaccion");
            n += Poner(ref hud.cliente, cliente, hud, "HUD.cliente");
        }

        CamaraCenital cenital = Object.FindAnyObjectByType<CamaraCenital>();
        if (cenital != null)
        {
            n += Poner(ref cenital.objetivo, jugador.transform, cenital, "CamaraCenital.objetivo");
        }

        CamaraSeguimiento seguimiento = Object.FindAnyObjectByType<CamaraSeguimiento>();
        if (seguimiento != null)
        {
            n += Poner(ref seguimiento.objetivo, jugador.transform, seguimiento, "CamaraSeguimiento.objetivo");
        }

        ControlDeCamaras control = Object.FindAnyObjectByType<ControlDeCamaras>();
        if (control != null)
        {
            n += Poner(ref control.movimiento, jugador, control, "ControlDeCamaras.movimiento");
            n += Poner(ref control.cliente, cliente, control, "ControlDeCamaras.cliente");
            if (control.camaraCenital == null && cenital != null)
            {
                n += Poner(ref control.camaraCenital, cenital.GetComponent<Camera>(), control, "ControlDeCamaras.camaraCenital");
            }
            if (control.camaraSeguimiento == null && seguimiento != null)
            {
                n += Poner(ref control.camaraSeguimiento, seguimiento.GetComponent<Camera>(), control, "ControlDeCamaras.camaraSeguimiento");
            }
        }

        if (n > 0)
        {
            EditorSceneManager.MarkSceneDirty(jugador.gameObject.scene);
        }
        Debug.Log("Reconectar: " + n + " referencias arregladas.");
        return n;
    }

    // Asigna solo si está vacía (no pisa lo que ya está bien conectado)
    static int Poner<T>(ref T campo, T valor, Object duenio, string nombre) where T : Object
    {
        if (campo != null || valor == null)
        {
            return 0;
        }
        Undo.RecordObject(duenio, "Reconectar " + nombre);
        campo = valor;
        EditorUtility.SetDirty(duenio);
        Debug.Log("Reconectar: " + nombre);
        return 1;
    }

    static Material Material(string nombre)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(CarpetaMateriales + "/" + nombre + ".mat");
    }
}
