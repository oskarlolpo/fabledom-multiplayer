using System;
using System.Reflection;
using FabledomMultiplayer.Networking;
using HarmonyLib;
using UnityEngine;

namespace FabledomMultiplayer
{
    [HarmonyPatch(typeof(PlacementManager))]
    public static class BuildingSyncPatch
    {
        public static bool IsRemotePlacing = false;

        private static readonly FieldInfo _posXField = typeof(PlacementManager).GetField("lastPlacementPosX", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo _posZField = typeof(PlacementManager).GetField("lastPlacementPosZ", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPrefix]
        [HarmonyPatch("PlaceObject")]
        public static void PlaceObject_Prefix(PlacementManager __instance)
        {
            if (IsRemotePlacing) return;

            try
            {
                if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
                {
                    if (__instance.latestPlacementData != null && __instance.placementObject != null)
                    {
                        int id = __instance.latestPlacementData.id;
                        int x = (int)(_posXField?.GetValue(__instance) ?? 0);
                        int z = (int)(_posZField?.GetValue(__instance) ?? 0);
                        float rotY = __instance.placementObject.transform.eulerAngles.y;

                        MultiplayerPlugin.Logger.LogInfo($"Отправка постройки здания [ID: {id}] в ({x}, {z})...");
                        byte[] packet = PacketSerializer.SerializeBuildingPlacement(id, x, z, rotY, true);
                        NetworkManager.Instance.SendPacket(packet);
                    }
                }
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.Logger.LogError($"Ошибка в PlaceObject_Prefix: {ex}");
            }
        }

        public static void ApplyRemoteBuilding(int objectId, int gridX, int gridZ, float rotY, bool isFree)
        {
            try
            {
                MultiplayerPlugin.Logger.LogInfo($"Получена постройка от напарника: ID={objectId} в ({gridX}, {gridZ})");

                if (PlacementManager.Instance == null || DataManager.Instance == null)
                {
                    MultiplayerPlugin.Logger.LogWarning("PlacementManager или DataManager недоступны.");
                    return;
                }

                // Look up WorldObjectData by ID
                WorldObjectData data = null;
                if (DataManager.Instance.gameData != null)
                {
                    data = DataManager.Instance.GetUnitDataFromId(objectId);
                }

                if (data == null)
                {
                    MultiplayerPlugin.Logger.LogWarning($"Не удалось найти WorldObjectData для ID={objectId}");
                    return;
                }

                IsRemotePlacing = true;
                try
                {
                    // Start placement, override coordinates and place
                    PlacementManager.Instance.BeginPlacement(data, false, isFree);

                    if (PlacementManager.Instance.placementObject != null)
                    {
                        PlacementManager.Instance.placementObject.transform.rotation = Quaternion.Euler(0, rotY, 0);
                        _posXField?.SetValue(PlacementManager.Instance, gridX);
                        _posZField?.SetValue(PlacementManager.Instance, gridZ);

                        PlacementManager.Instance.PlaceObject();
                        MultiplayerPlugin.Logger.LogInfo($"Успешно синхронизировано здание {data.name} в координатах ({gridX}, {gridZ})!");
                    }
                }
                finally
                {
                    IsRemotePlacing = false;
                }
            }
            catch (Exception ex)
            {
                IsRemotePlacing = false;
                MultiplayerPlugin.Logger.LogError($"Исключение при применении удаленного строительства: {ex}");
            }
        }
    }
}
