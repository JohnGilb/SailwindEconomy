using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace EconomyRevamp
{
    [BepInPlugin(PluginId, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginId = "com.zorkinian.economyrevamp";
        public const string PluginName = "Economy Revamp";
        public const string PluginVersion = "0.1.0";

        private ConfigEntry<bool> _serverEnabled;
        private ConfigEntry<int> _serverPort;
        private ConfigEntry<float> _snapshotIntervalSeconds;

        private Harmony _harmony;
        private EconomyHttpServer _server;
        private EconomySnapshotBuilder _snapshotBuilder;
        private string _snapshotJson;
        private float _snapshotTimer;

        internal static Plugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;

            _serverEnabled = Config.Bind("Loopback API", "Enabled", true, "Expose the read-only economy API on 127.0.0.1.");
            _serverPort = Config.Bind("Loopback API", "Port", 17777, "Loopback HTTP port used by the economy viewer.");
            _snapshotIntervalSeconds = Config.Bind("Loopback API", "SnapshotIntervalSeconds", 2f, "Seconds between economy snapshots.");

            _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginId);
            _snapshotBuilder = new EconomySnapshotBuilder();
            _snapshotJson = "{\"status\":\"waiting-for-game\"}";
            gameObject.AddComponent<EconomyDebugWindow>();

            if (_serverEnabled.Value)
            {
                try
                {
                    _server = new EconomyHttpServer(Logger.LogInfo, Logger.LogWarning, Logger.LogError);
                    _server.Start(_serverPort.Value, GetSnapshotJson, GetWebRoot());
                    Logger.LogInfo("Economy viewer listening at http://127.0.0.1:" + _serverPort.Value + "/");
                }
                catch (Exception ex)
                {
                    _server = null;
                    Logger.LogWarning("Economy viewer server did not start: " + ex.Message);
                }
            }
        }

        private void Start()
        {
            RefreshSnapshot();
        }

        private void Update()
        {
            _snapshotTimer -= Time.unscaledDeltaTime;
            if (_snapshotTimer <= 0f)
            {
                RefreshSnapshot();
                _snapshotTimer = Mathf.Max(0.25f, _snapshotIntervalSeconds.Value);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_server != null)
            {
                _server.Stop();
                _server = null;
            }

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }

        private string GetSnapshotJson()
        {
            return _snapshotJson;
        }

        private void RefreshSnapshot()
        {
            try
            {
                _snapshotJson = _snapshotBuilder.Build();
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed to build economy snapshot: " + ex);
                _snapshotJson = "{\"status\":\"snapshot-error\"}";
            }
        }

        internal void RequestSnapshotRefresh()
        {
            RefreshSnapshot();
            _snapshotTimer = Mathf.Max(0.25f, _snapshotIntervalSeconds.Value);
        }

        private static string GetWebRoot()
        {
            string pluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(pluginPath))
            {
                return null;
            }

            return Path.Combine(pluginPath, "web");
        }
    }
}
