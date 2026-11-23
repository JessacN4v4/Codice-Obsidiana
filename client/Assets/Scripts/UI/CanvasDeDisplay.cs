using UnityEngine;

/// <summary>
/// Pega un Canvas a la cámara que se ve en su display (modo Screen Space - Camera),
/// justo delante de ella. Así el Canvas no queda como un rectángulo gigante en el origen
/// del mundo (sobre el pueblo), que es lo que hace el modo Overlay y se veía con los Gizmos.
/// Si la tecla V intercambia los displays de las cámaras, el Canvas cambia de cámara solo.
/// Si no hay cámara en ese display, vuelve al modo Overlay.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class CanvasDeDisplay : MonoBehaviour
{
    public int display;                        // 0 = Display 1, 1 = Display 2

    private Canvas canvas;
    private static Camera[] camaras = new Camera[8];

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        Ajustar();
    }

    void LateUpdate()
    {
        Ajustar();
    }

    void Ajustar()
    {
        Camera camara = CamaraDelDisplay(display);
        if (camara == null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = display;
            return;
        }
        if (canvas.renderMode != RenderMode.ScreenSpaceCamera || canvas.worldCamera != camara)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camara;
        }
        // justo detrás del plano cercano: nada del mundo queda entre la cámara y la interfaz
        canvas.planeDistance = camara.nearClipPlane + 0.05f;
    }

    static Camera CamaraDelDisplay(int display)
    {
        int n = Camera.allCamerasCount;
        if (n > camaras.Length)
        {
            camaras = new Camera[n];
        }
        n = Camera.GetAllCameras(camaras);
        for (int i = 0; i < n; i++)
        {
            Camera c = camaras[i];
            if (c != null && c.targetDisplay == display && c.targetTexture == null)
            {
                return c;
            }
        }
        return null;
    }
}
