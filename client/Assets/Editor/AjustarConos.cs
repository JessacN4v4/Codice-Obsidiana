using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú "Codice > Actualizar conos de visión".
/// Pone el shader Codice/ConoVision (transparente, se desvanece hacia el borde) al material
/// ConoVision que ya usa VistaAgentes. Solo cambia el material: la escena no se toca.
/// ConectarServidor también usa MaterialCono() al crear la simulación.
/// </summary>
public static class AjustarConos
{
    const string RutaMaterial = "Assets/Graybox/Materiales/ConoVision.mat";

    /// <summary>También desde la línea de comandos: Unity.exe -batchmode -projectPath client -executeMethod AjustarConos.Actualizar -quit</summary>
    [MenuItem("Codice/Actualizar conos de visión")]
    public static void Actualizar()
    {
        MaterialCono();
        AssetDatabase.SaveAssets();
        Debug.Log("Material de los conos actualizado: " + RutaMaterial);
    }

    /// <summary>El material ConoVision con el shader Codice/ConoVision (lo crea si no existe).</summary>
    public static Material MaterialCono()
    {
        Shader shader = Shader.Find("Codice/ConoVision");
        if (shader == null)
        {
            Debug.LogError("No encontré el shader Codice/ConoVision (Assets/Art/Shaders/ConoVision.shader).");
            return AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        }
        Material material = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, RutaMaterial);
        }
        if (material.shader != shader)
        {
            material.shader = shader;          // mismo archivo .mat: VistaAgentes lo sigue usando
        }
        material.SetColor("_BaseColor", new Color(1f, 0.95f, 0.7f, 0.25f));
        material.renderQueue = -1;             // la del shader (Transparent)
        EditorUtility.SetDirty(material);
        return material;
    }
}
