using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú "Codice > Colocar modelos de ambientación".
/// Pone cada modelo de Assets/Art/Models en el lugar de su caja del graybox y apaga la parte
/// visible de la caja (su Renderer), pero deja su collider: las colisiones siguen siendo
/// exactamente las del servidor.
/// - Almacenes completos (muros, puertas, brechas y casas interiores) en el centro de su piso.
/// - Trojes, casas (el tipo T1..T8 se elige por el tamaño de la caja), puestos, maizales y canoa.
/// - La yácata, el lago, la plaza y el suelo se quedan como están (todavía no hay modelo).
/// Menú "Codice > Quitar modelos de ambientación": regresa al graybox para comparar.
/// </summary>
public static class ColocarAmbientacion
{
    const string Modelos = "Assets/Art/Models/";
    const string RutaEscena = "Assets/Scenes/Pueblo.unity";

    // Huellas de los 8 tipos de casa (ancho X, fondo Z) en metros
    static readonly string[] TiposCasa = { "T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8" };
    static readonly Vector2[] HuellasCasa =
    {
        new Vector2(4.0f, 2.75f), new Vector2(4.0f, 2.5f), new Vector2(3.0f, 2.25f), new Vector2(3.5f, 2.75f),
        new Vector2(4.0f, 3.0f), new Vector2(3.5f, 3.0f), new Vector2(3.75f, 2.75f), new Vector2(3.0f, 2.5f),
    };

    [MenuItem("Codice/Colocar modelos de ambientación")]
    public static void Colocar()
    {
        ColocarEnEscenaActual();
    }

    [MenuItem("Codice/Quitar modelos de ambientación")]
    public static void Quitar()
    {
        Transform graybox = BuscarGraybox();
        if (graybox == null)
        {
            return;
        }
        QuitarAmbientacion(graybox);
        EditorSceneManager.MarkSceneDirty(graybox.gameObject.scene);
        Debug.Log("Modelos quitados: se ve el graybox otra vez.");
    }

    /// <summary>
    /// Para la línea de comandos (sin abrir el editor):
    /// Unity.exe -batchmode -projectPath client -executeMethod ColocarAmbientacion.ColocarEnEscenaPueblo -quit
    /// </summary>
    public static void ColocarEnEscenaPueblo()
    {
        EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        if (!ColocarEnEscenaActual())
        {
            EditorApplication.Exit(1);
            return;
        }
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("Escena guardada con los modelos: " + RutaEscena);
    }

    static bool ColocarEnEscenaActual()
    {
        Transform graybox = BuscarGraybox();
        if (graybox == null)
        {
            return false;
        }
        QuitarAmbientacion(graybox);             // así se puede usar el menú varias veces

        GameObject raiz = new GameObject("Ambientacion");
        Undo.RegisterCreatedObjectUndo(raiz, "Colocar ambientación");
        int colocados = 0;

        // ---------- almacenes completos ----------
        Transform almacenes = graybox.Find("Almacenes");
        Transform grupoAlmacenes = Grupo("Almacenes", raiz.transform);
        foreach (Transform almacen in almacenes)
        {
            string id = almacen.name.Substring("Almacen_".Length, 1);       // "Almacen_A (...)" -> "A"
            Transform piso = almacen.Find("Piso");
            if (Poner(Modelos + "Almacenes/SM_Almacen_" + id + ".fbx", piso.position, grupoAlmacenes))
            {
                colocados++;
            }
            Apagar(piso);
            Apagar(almacen.Find("Puerta"));
        }
        // los muros ya vienen dentro de los almacenes completos
        foreach (Transform muro in graybox.Find("Empalizadas"))
        {
            Apagar(muro);
        }

        // ---------- trojes ----------
        Transform grupoTrojes = Grupo("Trojes", raiz.transform);
        foreach (Transform troje in graybox.Find("Trojes"))
        {
            string id = troje.name.Substring("Troje_".Length);              // "Troje_A" -> "A"
            if (Poner(Modelos + "Trojes/SM_Troje_" + id + "_Intacto.fbx", troje.position, grupoTrojes))
            {
                colocados++;
            }
            Apagar(troje);
        }

        // ---------- casas ----------
        Transform grupoCasas = Grupo("Casas", raiz.transform);
        foreach (Transform casa in graybox.Find("Casas"))
        {
            Apagar(casa);
            if (DentroDeUnAlmacen(casa.position, almacenes))
            {
                continue;                        // ya viene dentro del modelo del almacén
            }
            string tipo = TipoDeCasa(casa.localScale.x, casa.localScale.z);
            if (Poner(Modelos + "Casas/SM_Casa_" + tipo + ".fbx", casa.position, grupoCasas))
            {
                colocados++;
            }
        }

        // ---------- puestos del mercado (en orden: 1, 2, 3) ----------
        Transform grupoMercado = Grupo("Mercado", raiz.transform);
        int numeroPuesto = 1;
        foreach (Transform puesto in graybox.Find("Puestos"))
        {
            if (Poner(Modelos + "Mercado/SM_Puesto_" + numeroPuesto + ".fbx", puesto.position, grupoMercado))
            {
                colocados++;
            }
            Apagar(puesto);
            numeroPuesto++;
        }

        // ---------- maizales (siguen escondiendo: su collider trigger queda activo) ----------
        Transform grupoMilpa = Grupo("Milpa", raiz.transform);
        foreach (Transform maizal in graybox.Find("Maizales"))
        {
            if (Poner(Modelos + "Milpa/SM_" + maizal.name + ".fbx", maizal.position, grupoMilpa))
            {
                colocados++;
            }
            Apagar(maizal);
        }

        // ---------- canoa ----------
        Transform canoa = graybox.Find("Canoa");
        if (Poner(Modelos + "Lago/SM_Canoa.fbx", canoa.position, raiz.transform))
        {
            colocados++;
        }
        Apagar(canoa);

        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Selection.activeGameObject = raiz;
        Debug.Log("Ambientación colocada: " + colocados + " modelos. Guarda la escena (Ctrl + S).");
        return true;
    }

    // ---------- ayudantes ----------
    static Transform BuscarGraybox()
    {
        GameObject graybox = GameObject.Find("Graybox");
        if (graybox == null)
        {
            Debug.LogError("No encontré el Graybox en la escena.");
            EditorUtility.DisplayDialog("Falta el graybox", "Abre la escena Pueblo (o genera el graybox) primero.", "OK");
            return null;
        }
        return graybox.transform;
    }

    static void QuitarAmbientacion(Transform graybox)
    {
        GameObject anterior = GameObject.Find("Ambientacion");
        if (anterior != null)
        {
            Undo.DestroyObjectImmediate(anterior);
        }
        // volver a encender todas las cajas del graybox
        foreach (Renderer r in graybox.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled)
            {
                Undo.RecordObject(r, "Encender graybox");
                r.enabled = true;
            }
        }
    }

    static bool Poner(string ruta, Vector3 posicionCaja, Transform padre)
    {
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (modelo == null)
        {
            Debug.LogWarning("No encontré el modelo " + ruta);
            return false;
        }
        GameObject copia = (GameObject)PrefabUtility.InstantiatePrefab(modelo, padre);
        // misma posición en el suelo que la caja, sin rotar (los modelos ya vienen orientados)
        copia.transform.position = new Vector3(posicionCaja.x, 0f, posicionCaja.z);
        copia.transform.rotation = Quaternion.identity;
        return true;
    }

    static void Apagar(Transform caja)
    {
        // se apaga solo lo que se ve; el collider sigue funcionando
        if (caja == null)
        {
            return;
        }
        Renderer dibujo = caja.GetComponent<Renderer>();
        if (dibujo != null)
        {
            Undo.RecordObject(dibujo, "Apagar caja del graybox");
            dibujo.enabled = false;
        }
    }

    static bool DentroDeUnAlmacen(Vector3 punto, Transform almacenes)
    {
        foreach (Transform almacen in almacenes)
        {
            Transform piso = almacen.Find("Piso");
            Vector3 c = piso.position;
            Vector3 s = piso.localScale;
            if (Mathf.Abs(punto.x - c.x) < s.x / 2f && Mathf.Abs(punto.z - c.z) < s.z / 2f)
            {
                return true;
            }
        }
        return false;
    }

    static string TipoDeCasa(float ancho, float fondo)
    {
        // el tipo cuya huella se parece más al tamaño de la caja
        int mejor = 0;
        float menorDiferencia = float.MaxValue;
        for (int i = 0; i < HuellasCasa.Length; i++)
        {
            float diferencia = Mathf.Abs(HuellasCasa[i].x - ancho) + Mathf.Abs(HuellasCasa[i].y - fondo);
            if (diferencia < menorDiferencia)
            {
                menorDiferencia = diferencia;
                mejor = i;
            }
        }
        return TiposCasa[mejor];
    }

    static Transform Grupo(string nombre, Transform padre)
    {
        GameObject grupo = new GameObject(nombre);
        grupo.transform.SetParent(padre, false);
        return grupo.transform;
    }
}
