using System.Globalization;
using HoldfastGame;
using UnityEngine;

namespace RyLib
{
    internal static class PlayerActions
    {
        private const int GroundMask = (1 << 0) | (1 << 9) | (1 << 13);
        private const int HealAmount = 100;

        private static ClientComponentReferenceManager Client
        {
            get { return ClientComponentReferenceManager.ClientInstance; }
        }

        internal static void Spectate(int id)
        {
            ClientComponentReferenceManager client = Client;
            if (client != null && client.clientSpectatorManager != null) client.clientSpectatorManager.SpectateStartRequest(id);
        }

        internal static void Slay(int id)
        {
            ClientComponentReferenceManager client = Client;
            if (client != null && client.clientBannedPlayersManager != null) client.clientBannedPlayersManager.RequestPlayerSlay(id, string.Empty);
        }

        internal static void Revive(int id)
        {
            ClientComponentReferenceManager client = Client;
            if (client != null && client.clientBannedPlayersManager != null) client.clientBannedPlayersManager.RequestPlayerRevive(id, string.Empty);
        }

        internal static void Heal(int id)
        {
            ClientComponentReferenceManager client = Client;
            if (client != null && client.clientBannedPlayersManager != null)
            {
                client.clientBannedPlayersManager.RequestPlayerSlap(id, string.Empty, -HealAmount);
            }
        }

        internal static void Slap(int id)
        {
            ClientComponentReferenceManager client = Client;
            if (client != null && client.clientBannedPlayersManager != null) client.clientBannedPlayersManager.RequestPlayerSlap(id, string.Empty, 0);
        }

        internal static void Kick(int id)
        {
            ClientComponentReferenceManager client = Client;
            if (client != null && client.clientBannedPlayersManager != null) client.clientBannedPlayersManager.RequestPlayerKick(id, string.Empty);
        }

        internal static void GoTo(int id)
        {
            ClientComponentReferenceManager client = Client;
            if (client == null) return;

            if (Freeflight(client))
            {
                Vector3 position;
                if (!client.clientRoundPlayerManager.TryGetRoundPlayerPosition(id, out position)) return;
                position.y += 1f;
                MoveCamera(client, position);
                return;
            }

            Run(client, "rc teleport me " + id);
        }

        internal static void Bring(int id)
        {
            ClientComponentReferenceManager client = Client;
            if (client == null) return;

            if (Freeflight(client))
            {
                Vector3 position;
                client.clientRoundPlayerManager.GetLocalRoundPlayerPosition(out position);
                Run(client, "rc teleport " + id + " " + Coords(position));
                return;
            }

            Run(client, "rc teleport " + id + " me");
        }

        internal static void Teleport(int id, Vector3 world)
        {
            ClientComponentReferenceManager client = Client;
            if (client == null) return;

            world.y = Ground(world) + 0.2f;
            Run(client, "rc teleport " + id + " " + Coords(world));
        }

        internal static void GoThere(Vector3 world)
        {
            ClientComponentReferenceManager client = Client;
            if (client == null) return;

            world.y = Ground(world);

            if (Freeflight(client))
            {
                MoveCamera(client, world + Vector3.up * 2f);
                return;
            }

            Run(client, "rc teleport me " + Coords(world + Vector3.up * 0.2f));
        }

        internal static bool Freeflight(ClientComponentReferenceManager client)
        {
            return client != null && client.clientFreeflightCameraManager != null &&
                   client.clientFreeflightCameraManager.currentlyUsingFreeflightCamera;
        }

        internal static float Ground(Vector3 world)
        {
            RaycastHit hit;
            Vector3 start = new Vector3(world.x, MapRender.Top + 50f, world.z);
            if (Physics.Raycast(start, Vector3.down, out hit, 5000f, GroundMask, QueryTriggerInteraction.Ignore)) return hit.point.y;

            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null) return terrain.SampleHeight(world) + terrain.transform.position.y;

            Camera camera = GameView.ActiveCamera;
            return (camera != null) ? camera.transform.position.y : 0f;
        }

        private static void MoveCamera(ClientComponentReferenceManager client, Vector3 position)
        {
            OwnerCameraManager cameras = client.ownerCameraManager;
            if (cameras == null || cameras.FreeflightCamera == null) return;

            cameras.FreeflightCamera.cameraRigidbody.position = position;
            cameras.cameraRootObject.transform.position = position;
        }

        private static void Run(ClientComponentReferenceManager client, string command)
        {
            if (client.pMenuPanel != null) client.pMenuPanel.ManualConsoleExecute(command);
        }

        private static string Coords(Vector3 position)
        {
            return position.x.ToString("0.0", CultureInfo.InvariantCulture) + "," +
                   position.y.ToString("0.0", CultureInfo.InvariantCulture) + "," +
                   position.z.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
