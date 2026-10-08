using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using FabledomMultiplayer.Networking;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FabledomMultiplayer
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class MultiplayerPlugin : BaseUnityPlugin
    {
        public static MultiplayerPlugin Instance { get; private set; }
        public static new ManualLogSource Logger { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;

            Logger.LogInfo($"=== {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} Запущен! ===");

            // 1. Initialize Network Manager
            var netObj = new GameObject("FabledomMultiplayer_NetworkManager");
            DontDestroyOnLoad(netObj);
            var net = netObj.AddComponent<NetworkManager>();
            net.OnPacketReceived += HandleIncomingPacket;

            // 2. Initialize Visualizer
            var visObj = new GameObject("FabledomMultiplayer_CursorVisualizer");
            DontDestroyOnLoad(visObj);
            visObj.AddComponent<PlayerCursorVisualizer>();

            // 2.5 Initialize UI
            var uiObj = new GameObject("FabledomMultiplayer_UI");
            DontDestroyOnLoad(uiObj);
            uiObj.AddComponent<MultiplayerUI>();

            // 3. Apply Harmony Patches
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            _harmony.PatchAll();

            Logger.LogInfo("Все патчи Harmony успешно применены!");

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Logger.LogInfo($"Загружена сцена: {scene.name}");
        }

        private void HandleIncomingPacket(byte[] data)
        {
            if (data == null || data.Length < 1) return;

            var type = (PacketType)data[0];
            using var ms = new MemoryStream(data, 1, data.Length - 1);
            using var reader = new BinaryReader(ms);

            switch (type)
            {
                case PacketType.CursorAndCamera:
                    float curX = reader.ReadSingle();
                    float curY = reader.ReadSingle();
                    float curZ = reader.ReadSingle();
                    float camX = reader.ReadSingle();
                    float camZ = reader.ReadSingle();
                    float camRotY = reader.ReadSingle();
                    float camZoom = reader.ReadSingle();
                    int buildingId = reader.ReadInt32();

                    PlayerCursorVisualizer.Instance?.UpdateRemoteData(curX, curY, curZ, camX, camZ, camRotY, camZoom, buildingId);
                    break;

                case PacketType.BuildingPlacement:
                    int objId = reader.ReadInt32();
                    int gridX = reader.ReadInt32();
                    int gridZ = reader.ReadInt32();
                    float rotY = reader.ReadSingle();
                    bool isFree = reader.ReadBoolean();

                    BuildingSyncPatch.ApplyRemoteBuilding(objId, gridX, gridZ, rotY, isFree);
                    break;

                case PacketType.TimeScaleSync:
                    float timeScale = reader.ReadSingle();
                    bool isPaused = reader.ReadBoolean();

                    if (GameManager.Instance != null)
                    {
                        if (isPaused)
                        {
                            GameManager.Instance.PauseTimeScale();
                        }
                        else
                        {
                            GameManager.Instance.SetTimeScale(timeScale);
                        }
                    }
                    break;

                default:
                    Logger.LogInfo($"Получен неизвестный тип пакета: {type}");
                    break;
            }
        }
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.fabledom.multiplayer";
        public const string PLUGIN_NAME = "Fabledom Multiplayer";
        public const string PLUGIN_VERSION = "0.1.0";
    }
}
