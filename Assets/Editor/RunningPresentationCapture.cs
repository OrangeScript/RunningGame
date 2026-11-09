#if UNITY_EDITOR
using System.IO;
using UnityEngine;

/// <summary>Capture the real runtime camera and UI for visual review in graphical batch mode.</summary>
public static class RunningPresentationCapture
{
    public static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Camera camera = Camera.main;
        if (camera == null) return;
        var texture = new RenderTexture(1600,900,24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        bool postProcessing = cameraData != null && cameraData.renderPostProcessing;
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
        var modes = new RenderMode[canvases.Length];
        var cameras = new Camera[canvases.Length];
        var distances = new float[canvases.Length];
        var scales = new float[canvases.Length];
        var scrolls = Object.FindObjectsByType<UnityEngine.UI.ScrollRect>(FindObjectsSortMode.None);
        var scrollPositions = new Vector2[scrolls.Length];
        for (int i=0;i<scrolls.Length;i++) scrollPositions[i] = scrolls[i].normalizedPosition;
        try
        {
            camera.targetTexture = texture;
            if (cameraData != null) cameraData.renderPostProcessing = false;
            for (int i=0;i<canvases.Length;i++)
            {
                modes[i] = canvases[i].renderMode; cameras[i] = canvases[i].worldCamera;
                distances[i] = canvases[i].planeDistance;
                scales[i] = canvases[i].scaleFactor;
                if (modes[i] != RenderMode.ScreenSpaceOverlay) continue;
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = camera; canvases[i].planeDistance = 1;
                canvases[i].scaleFactor = 1;
            }
            Canvas.ForceUpdateCanvases();
            for(int i=0;i<scrolls.Length;i++) scrolls[i].normalizedPosition=scrollPositions[i];
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(1600,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,900),0,0); image.Apply();
            string directory = Path.Combine(Directory.GetCurrentDirectory(),"Logs","Presentation");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory,name+".png"),image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            if (cameraData != null) cameraData.renderPostProcessing = postProcessing;
            for(int i=0;i<canvases.Length;i++)
            {
                if (canvases[i] == null) continue;
                canvases[i].renderMode=modes[i]; canvases[i].worldCamera=cameras[i]; canvases[i].planeDistance=distances[i];
                canvases[i].scaleFactor=scales[i];
            }
            Canvas.ForceUpdateCanvases();
            for(int i=0;i<scrolls.Length;i++) if(scrolls[i]!=null) scrolls[i].normalizedPosition=scrollPositions[i];
            Object.DestroyImmediate(texture);
        }
    }
}
#endif
