using HoldfastGame;
using UnityEngine;

namespace RyLib
{
    internal static class GameView
    {
        public static Camera ActiveCamera
        {
            get
            {
                ClientComponentReferenceManager client = ClientComponentReferenceManager.ClientInstance;
                if (client != null && client.ownerCameraManager != null)
                {
                    Camera camera = client.ownerCameraManager.ActiveCamera;
                    if (camera == null) camera = client.ownerCameraManager.ownerCamera;
                    if (camera != null) return camera;
                }

                return Camera.main;
            }
        }
    }
}
