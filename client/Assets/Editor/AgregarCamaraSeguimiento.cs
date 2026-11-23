using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Menú "Codice > Agregar cámara de seguimiento (Display 2)".
/// Crea el objeto "CamaraSeguimiento": una segunda cámara en tercera persona que se ve en el
/// Display 2, con los scripts CamaraSeguimiento y ControlDeCamaras ya conectados.
/// Si ya existía, la reemplaza.
/// </summary>
public static class AgregarCamaraSeguimiento
{
    const string RutaEscena = "Assets/Scenes/Pueblo.unity";

    [MenuItem("Codice/Agregar cámara de seguimiento (Display 2)")]
    public static void Agregar()
    {
        AgregarEnEscenaActual();
    }

    /// <summary>
    /// Para la línea de comandos (sin abrir el editor):
    /// Unity.exe -batchmode -projectPath client -executeMethod AgregarCamaraSeguimiento.AgregarEnEscenaPueblo -quit
    /// </summary>
    public static void AgregarEnEscenaPueblo()
    {
        EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        if (!AgregarEnEscenaActual())
        {
            EditorApplication.Exit(1);
            return;
        }
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Escena guardada con la cámara de seguimiento: " + RutaEscena);
    }

    static bool AgregarEnEscenaActual()
    {
        MovimientoJugador jugador = Object.FindAnyObjectByType<MovimientoJugador>();
        Camera principal = Camera.main;
        if (jugador == null || principal == null)
        {
            Debug.LogError("No encontré al jugador o a la cámara principal.");
            EditorUtility.DisplayDialog("Falta el jugador",
                "Primero usa el menú Codice > Colocar jugador y cámara.", "OK");
            return false;
        }
        ClienteSimulacion cliente = Object.FindAnyObjectByType<ClienteSimulacion>();
        if (cliente == null)
        {
            Debug.LogWarning("No encontré ClienteSimulacion; usa Codice > Conectar con el servidor y vuelve a correr este menú.");
        }

        GameObject anterior = GameObject.Find("CamaraSeguimiento");
        if (anterior != null)
        {
            Undo.DestroyObjectImmediate(anterior);
        }

        // Cámara con los mismos ajustes que la principal (cielo, capas, etc.), pero en el Display 2
        GameObject objeto = new GameObject("CamaraSeguimiento");
        Camera camara = objeto.AddComponent<Camera>();
        camara.CopyFrom(principal);
        camara.fieldOfView = 60f;
        camara.nearClipPlane = 0.1f;
        camara.targetDisplay = 1;              // 0 = Display 1, 1 = Display 2
        objeto.tag = "Untagged";               // la "MainCamera" sigue siendo la cenital
        // sin AudioListener: solo puede haber uno y ya lo tiene la cámara principal

        // mismos ajustes de URP (post-procesado y antialiasing)
        UniversalAdditionalCameraData datosPrincipal = principal.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData datos = camara.GetUniversalAdditionalCameraData();
        datos.renderPostProcessing = datosPrincipal.renderPostProcessing;
        datos.antialiasing = datosPrincipal.antialiasing;

        // detrás del jugador, para que en el editor ya se vea en su lugar
        Transform t = jugador.transform;
        objeto.transform.position = t.position + Vector3.up * 1.6f - t.forward * 5.5f + t.right * 0.6f;
        objeto.transform.rotation = Quaternion.Euler(15f, t.eulerAngles.y, 0f);

        CamaraSeguimiento seguimiento = objeto.AddComponent<CamaraSeguimiento>();
        seguimiento.objetivo = t;

        ControlDeCamaras control = objeto.AddComponent<ControlDeCamaras>();
        control.camaraCenital = principal;
        control.camaraSeguimiento = camara;
        control.movimiento = jugador;
        control.cliente = cliente;

        Undo.RegisterCreatedObjectUndo(objeto, "Agregar cámara de seguimiento");
        EditorSceneManager.MarkSceneDirty(objeto.scene);
        Selection.activeGameObject = objeto;
        Debug.Log("Cámara de seguimiento agregada. Guarda la escena (Ctrl + S), dale Play y elige Display 2 en la vista Game.");
        return true;
    }
}
