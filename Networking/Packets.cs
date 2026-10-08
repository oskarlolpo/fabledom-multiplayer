using System;
using System.IO;

namespace FabledomMultiplayer.Networking
{
    public enum PacketType : byte
    {
        Handshake = 1,
        CursorAndCamera = 2,
        BuildingPlacement = 3,
        BuildingCancel = 4,
        BuildingRotate = 5,
        ChatMessage = 6,
        TimeScaleSync = 7,
        SaveDataSync = 8
    }

    public static class PacketSerializer
    {
        public static byte[] SerializeCursor(float curX, float curY, float curZ, float camX, float camZ, float camRotY, float camZoom, int holdingBuildingId)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((byte)PacketType.CursorAndCamera);
            writer.Write(curX);
            writer.Write(curY);
            writer.Write(curZ);
            writer.Write(camX);
            writer.Write(camZ);
            writer.Write(camRotY);
            writer.Write(camZoom);
            writer.Write(holdingBuildingId);
            return ms.ToArray();
        }

        public static byte[] SerializeBuildingPlacement(int objectId, int gridX, int gridZ, float rotY, bool isFree)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((byte)PacketType.BuildingPlacement);
            writer.Write(objectId);
            writer.Write(gridX);
            writer.Write(gridZ);
            writer.Write(rotY);
            writer.Write(isFree);
            return ms.ToArray();
        }

        public static byte[] SerializeTimeScale(float timeScale, bool isPaused)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((byte)PacketType.TimeScaleSync);
            writer.Write(timeScale);
            writer.Write(isPaused);
            return ms.ToArray();
        }

        public static byte[] SerializeHandshake(string playerName, string version)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((byte)PacketType.Handshake);
            writer.Write(playerName ?? "Player");
            writer.Write(version ?? "1.0");
            return ms.ToArray();
        }
    }
}
