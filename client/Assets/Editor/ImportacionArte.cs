using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Configura solo la importación del arte que entra a Assets/Art (un AssetPostprocessor
/// es una clase que Unity llama cada vez que importa un archivo).
/// Modelos: sin cámaras, luces, colliders ni animaciones (las colisiones las pone el graybox).
/// Texturas: los "_Normal" como mapa de normales, los "_Mask" en modo lineal, máximo 2K.
/// Solo actúa la primera vez que se importa cada archivo, para no borrar ajustes hechos a mano.
/// </summary>
public class ImportacionArte : AssetPostprocessor
{
    const string CarpetaModelos = "Assets/Art/Models/";
    const string CarpetaTexturas = "Assets/Art/Textures/";

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(CarpetaModelos) || !assetImporter.importSettingsMissing)
        {
            return;
        }
        ModelImporter modelo = (ModelImporter)assetImporter;
        modelo.importCameras = false;
        modelo.importLights = false;
        modelo.addCollider = false;                 // la colisión exacta ya está en el graybox
        modelo.isReadable = false;                  // ahorra memoria
        modelo.importAnimation = false;
        modelo.animationType = ModelImporterAnimationType.None;
        modelo.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(CarpetaTexturas) || !assetImporter.importSettingsMissing)
        {
            return;
        }
        TextureImporter textura = (TextureImporter)assetImporter;
        string nombre = Path.GetFileNameWithoutExtension(assetPath);
        textura.maxTextureSize = 2048;
        textura.mipmapEnabled = true;
        textura.wrapMode = TextureWrapMode.Repeat;  // los pisos se repiten (tiling)

        if (nombre.EndsWith("_Normal"))
        {
            textura.textureType = TextureImporterType.NormalMap;
        }
        else if (nombre.EndsWith("_Mask"))
        {
            textura.textureType = TextureImporterType.Default;
            textura.sRGBTexture = false;            // datos (metal, oclusión, suavidad), no color
        }
        else
        {
            textura.textureType = TextureImporterType.Default;
            textura.sRGBTexture = true;             // color
        }
    }
}
