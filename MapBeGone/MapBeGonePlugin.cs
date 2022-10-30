using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using System;
using System.IO;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using UnityEngine;

namespace MapBeGone;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class MapBeGonePlugin : BaseUnityPlugin
{
    #region Variables
    internal const string PLUGIN_NAME = "MapBeGone";
    internal const string PLUGIN_VERSION = "1.2.0";
    internal const string PLUGIN_GUID = "Yoyo." + PLUGIN_NAME;
    internal const string PLUGIN_AUTHOR = "GuyDeYoyo";

    private static string ConfigFileName = PLUGIN_GUID + ".cfg";
    private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

    internal static readonly ManualLogSource YoyoLogger = BepInEx.Logging.Logger.CreateLogSource(PLUGIN_NAME);

    private static MapBeGonePlugin _context;
    private readonly Harmony _harmony = new(PLUGIN_GUID);

    internal enum Toggle { On = 1, Off = 0 };

    private static bool AllowSmallMap = false;
    private static bool AllowLargeMap = false;
    #endregion Variables


    #region ServerSync
    internal static string ConnectionError = string.Empty;
    private static readonly ConfigSync configSync = new(PLUGIN_GUID) { DisplayName = PLUGIN_NAME, CurrentVersion = PLUGIN_VERSION, MinimumRequiredVersion = PLUGIN_VERSION };
    #endregion ServerSync


    #region Standard Methods
    private void Awake()
    {
        _context = this;
        
        #region Default Configuration
        _configEnabled = config("1 - MapBeGone", "Enabled", true, "Enable or disable MapBeGone.", true);
        _configServerSync = config("1 - MapBeGone", "ServerSync", true, "Enable or disable ServerSync (server will override this and relevant settings).", true);

        _configEnableMinimap = config("2 - Map Sources", "Minimap", Toggle.Off, "Enable or disable the overview minimap.", true);
        _configEnableLargeMap = config("2 - Map Sources", "Large Map", Toggle.Off, "Enable or disable the large map (accessed via M keybind in vanilla).", true);
        _configEnableCartographyTableView = config("2 - Map Sources", "Cartography Table", Toggle.Off, "Enable or disable viewing the Cartography Table.", true);
        _configEnableVegvisirMap = config("2 - Map Sources", "Vegvisir", Toggle.Off, "Enable or disable the large map via Vegvisir stones.", true);

        _configEnableCartographyTableWrite = config("3 - Map Extras", "Cartography Table", Toggle.Off, "Enable or disable updating the Cartography Table.", true);
        _configEnableClickToPing = config("3 - Map Extras", "Ping", Toggle.Off, "Enable or disable click-to-ping on maps.", true);
        _configEnableMapZoom = config("3 - Map Extras", "Minimap Zoom", Toggle.Off, "Enable or disable the ability to zoom the minimap.", true);
        _configEnableShowBiome = config("3 - Map Extras", "Biome", Toggle.Off, "Enable or disable the biome name on the minimap.", true);
        _configEnableShowWindDirection = config("3 - Map Extras", "Wind Direction", Toggle.Off, "Enable or disable the wind direction arrow on the minimap.", true);

        _configEnableDiscoveryRadius = config("4 - Discovery Radius", "Enabled", Toggle.On, "Enable or disable customization of map discovery radius.", true);
        _configDiscoveryRadiusMap = config("4 - Discovery Radius", "Standard", 100, "Radius of map discovery when not sailing on a boat (as a percentage).", true);
        _configDiscoveryRadiusBoat = config("4 - Discovery Radius", "Boat", 100, "Radius of map discovery when sailing on a boat (as a percentage).", true);
        #endregion Default Configuration

        configSync.AddLockingConfigEntry(_configServerSync);

        ShowMinimapBiome = _configEnableShowBiome.Value == Toggle.On ? true : false;
        ShowMinimapWindDirection = _configEnableShowWindDirection.Value == Toggle.On ? true : false;

        SetupWatcher();

        UpdateMinimapItemVisibility(1, ShowMinimapBiome);
        UpdateMinimapItemVisibility(2, ShowMinimapWindDirection);

        _configEnableShowBiome.SettingChanged += ConfigChanged_MinimapBiomes;
        _configEnableShowWindDirection.SettingChanged += ConfigChanged_MinimapWindDirection;

        if (_configEnabled.Value == true) { YoyoLogger.LogDebug($"{PLUGIN_GUID} v{PLUGIN_VERSION} enabled in configuration."); }
        else { YoyoLogger.LogDebug($"{PLUGIN_GUID} v{PLUGIN_VERSION} not enabled in configuration."); return; }

        Assembly assembly = Assembly.GetExecutingAssembly();
        _harmony.PatchAll(assembly);
    }


    private void Start()
    {
        Game.isModded = true;
    }


    private void OnDestroy()
    {
        Config.Save();
        _harmony?.UnpatchSelf();
    }
    #endregion Standard Methods



    #region Small and Large Map
    private static bool ShowMinimapBiome = false;
    private static bool ShowMinimapWindDirection = false;

    internal static void UpdateMinimapItemVisibility(int MinimapItem, bool InUse)
    {
        if (!_configEnabled.Value || Player.m_localPlayer == null || ZInput.instance == null) return;
        if (Minimap.instance == null) return;

        switch (MinimapItem)
        {

            case 1: // Biome
                if (InUse) { Minimap.instance.m_biomeNameSmall.enabled = true; ShowMinimapBiome = true; }
                else { Minimap.instance.m_biomeNameSmall.enabled = false; ShowMinimapBiome = false; }
                break;
            case 2: // Wind Direction
                if (InUse)
                {
                    Minimap.instance.m_windMarker.sizeDelta = new Vector2() { x = 32, y = 32 };
                    ShowMinimapWindDirection = true;
                }
                else
                {
                    Minimap.instance.m_windMarker.sizeDelta = new Vector2() { x = 0, y = 0 };
                    ShowMinimapWindDirection = false;
                }
                break;
        }
    }


    [HarmonyPatch(typeof(Minimap), nameof(Minimap.Awake))]
    internal static class MapBeGoneMinimapAwake
    {
        internal static void Postfix(Minimap __instance)
        {
            if (!_configEnabled.Value || Player.m_localPlayer == null || ZInput.instance == null) return;
            if (Minimap.instance == null) return;

            ShowMinimapBiome = _configEnableShowBiome.Value == Toggle.On ? true : false;
            ShowMinimapWindDirection = _configEnableShowWindDirection.Value == Toggle.On ? true : false;

            UpdateMinimapItemVisibility(1, ShowMinimapBiome);
            UpdateMinimapItemVisibility(2, ShowMinimapWindDirection);
        }
    }


    [HarmonyPatch(typeof(Minimap), nameof(Minimap.Update))]
    internal static class MapBeGoneMinimapUpdate
    {
        internal static bool Prefix(Minimap __instance)
        {
            if (!_configEnabled.Value || Player.m_localPlayer == null || ZInput.instance == null) return true;

            if (_configEnableMapZoom.Value == Toggle.Off)
            {
                if (ZInput.GetButtonDown("MapZoomOut") || Input.GetButtonDown("MapZoomIn"))
                    return false;
            }

            return true;
        }


        internal static void Postfix(Minimap __instance)
        {
            if (!_configEnabled.Value || Player.m_localPlayer == null || ZInput.instance == null) return;

            AllowSmallMap = _configEnableMinimap.Value == Toggle.On ? true : false;
            AllowLargeMap = _configEnableLargeMap.Value == Toggle.On ? true : false;

            if (AllowSmallMap && __instance.m_mode == Minimap.MapMode.None) __instance.SetMapMode(Minimap.MapMode.Small);
            if (!AllowSmallMap && __instance.m_mode != Minimap.MapMode.Large) __instance.SetMapMode(Minimap.MapMode.None);


            // Show or Hide the Minimap Items                
            if (AllowSmallMap && __instance.m_mode == Minimap.MapMode.Small)
            {
                if (_configEnableShowBiome.Value == Toggle.On && !__instance.m_biomeNameSmall.enabled) UpdateMinimapItemVisibility(1, true);
                
                if (_configEnableShowWindDirection.Value == Toggle.On && __instance.m_windMarker.sizeDelta != new Vector2() { x = 32, y = 32 }) UpdateMinimapItemVisibility(2, true);
                
                if (_configEnableShowBiome.Value == Toggle.Off && __instance.m_biomeNameSmall.enabled) UpdateMinimapItemVisibility(1, false);

                if (_configEnableShowWindDirection.Value == Toggle.Off && __instance.m_windMarker.sizeDelta != new Vector2() { x = 0, y = 0 }) UpdateMinimapItemVisibility(2, false);
            }
            


            if (AllowLargeMap)
            {
                if (ZInput.GetButtonDown("Map") || ZInput.GetButtonDown("JoyMap"))
                {
                    if (__instance.m_mode == Minimap.MapMode.Large)
                    {
                        __instance.SetMapMode(Minimap.MapMode.Large);
                    }
                    else
                    {
                        if (AllowSmallMap) __instance.SetMapMode(Minimap.MapMode.Small);
                        else __instance.SetMapMode(Minimap.MapMode.None);
                    }
                }
            }
            else
            {
                if (ZInput.GetButtonDown("Map") || ZInput.GetButtonDown("JoyMap"))
                {
                    if (AllowSmallMap) __instance.SetMapMode(Minimap.MapMode.Small);
                    else __instance.SetMapMode(Minimap.MapMode.None);
                }
            }
        }
    }
    #endregion Small and Large Map


    #region Cartography Table
    [HarmonyPatch(typeof(MapTable), nameof(MapTable.OnRead))]
    internal static class MapBeGoneTableOnRead
    {
        internal static void Postfix(MapTable __instance, ItemDrop.ItemData item)
        {
            if (!_configEnabled.Value || Player.m_localPlayer == null || item != null || __instance == null) return;

            if (_configEnableCartographyTableView.Value == Toggle.On)
                Minimap.instance.SetMapMode(Minimap.MapMode.Large);
        }
    }


    [HarmonyPatch(typeof(MapTable), nameof(MapTable.OnWrite))]
    internal static class MapBeGoneTableOnWrite
    {
        internal static bool Prefix(MapTable __instance)
        {
            if (!_configEnabled.Value || Player.m_localPlayer == null || __instance == null) return true;

            if (_configEnableCartographyTableWrite.Value == Toggle.On) return true;

            return false;
        }
    }
    #endregion Cartography Table



    #region Ping
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnMapMiddleClick))]
    internal static class MapBeGoneRemovePingsOnMap
    {
        internal static bool Prefix()
        {
            if (!_configEnabled.Value || _configEnableClickToPing.Value == Toggle.On) return true;

            return false;
        }
    }


    [HarmonyPatch(typeof(Chat), nameof(Chat.SendPing))]
    internal static class MapBeGoneRemoveChatMapPings
    {
        internal static bool Prefix(Chat __instance)
        {
            if (!_configEnabled.Value || Player.m_localPlayer == null || __instance == null || ZInput.instance == null) return true;

            if (_configEnableClickToPing.Value == Toggle.Off) return false;

            return true;
        }
    }
    #endregion Ping


    #region Vegvisir
    [HarmonyPatch(typeof(Vegvisir), nameof(Vegvisir.Interact))]
    internal static class MapBeGoneRemoveInteractVegvisir
    {
        internal static bool Prefix(Vegvisir __instance)
        {
            if (!_configEnabled.Value || Player.m_localPlayer == null | __instance == null || ZInput.instance == null) return true;

            if (_configEnableVegvisirMap.Value == Toggle.Off) return false;

            return true;
        }
    }
    #endregion Vegvisir


    #region Discovery Methods
    private static int _boatDiscoveryRadius = 10000;
    private static int _mapDiscoveryRadius = 10000;
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.UpdateExplore))]
    internal static class MapBeGoneUpdateExplore
    {
        internal static void Postfix(float dt, Player player)
        {
            if (!Player.m_localPlayer) return;
            if (ZInput.instance == null) return;
            if (_configEnabled.Value != true || _configEnableDiscoveryRadius.Value != Toggle.On) return;
            
            if (_configDiscoveryRadiusBoat.Value > -1 || _configDiscoveryRadiusBoat.Value < 10001) _boatDiscoveryRadius = _configDiscoveryRadiusBoat.Value;
            else _boatDiscoveryRadius = 10000;

            if (_configDiscoveryRadiusMap.Value > -1 || _configDiscoveryRadiusMap.Value > 10001) _mapDiscoveryRadius = _configDiscoveryRadiusMap.Value;
            else _mapDiscoveryRadius = 10000;

            if (player.IsAttachedToShip())
            {
                Minimap.instance.Explore(player.transform.position, _boatDiscoveryRadius);
                return;
            }

            Minimap.instance.Explore(player.transform.position, _mapDiscoveryRadius);
        }
    }
    #endregion Discovery Methods


    #region Configuration Watcher
    private void SetupWatcher()
    {
        FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
        watcher.Changed += ReadConfigValues;
        watcher.Created += ReadConfigValues;
        watcher.Renamed += ReadConfigValues;
        watcher.IncludeSubdirectories = true;
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.EnableRaisingEvents = true;
    }


    private void ReadConfigValues(object sender, FileSystemEventArgs e)
    {
        if (!File.Exists(ConfigFileFullPath)) return;
        try
        {
            YoyoLogger.LogDebug("ReadConfigValues called.");
            Config.Reload();
        }
        catch
        {
            YoyoLogger.LogError($"There was an issue loading file: {ConfigFileName}");
            YoyoLogger.LogError("Check your config entries for correct spelling and format.");
        }
    }

    private void ConfigChanged_MinimapBiomes(object sender, EventArgs e)
    {
        ShowMinimapBiome = _configEnableShowBiome.Value == Toggle.On ? true : false;
        UpdateMinimapItemVisibility(1, ShowMinimapBiome);
    }

    private void ConfigChanged_MinimapWindDirection(object sender, EventArgs e)
    {
        ShowMinimapWindDirection = _configEnableShowWindDirection.Value == Toggle.On ? true : false;
        UpdateMinimapItemVisibility(2, ShowMinimapWindDirection);
    }
    #endregion Configuration Watcher


    #region Configuration Options
    #nullable enable
    internal static ConfigEntry<bool>? _configEnabled = null!;
    internal static ConfigEntry<bool>? _configServerSync = null!;

    internal static ConfigEntry<Toggle>? _configEnableMinimap = null!;
    internal static ConfigEntry<Toggle>? _configEnableMapZoom = null!;
    internal static ConfigEntry<Toggle>? _configEnableClickToPing = null!;
    internal static ConfigEntry<Toggle>? _configEnableLargeMap = null!;
    internal static ConfigEntry<Toggle>? _configEnableVegvisirMap = null!;
    internal static ConfigEntry<Toggle>? _configEnableCartographyTableView = null!;
    internal static ConfigEntry<Toggle>? _configEnableCartographyTableWrite = null!;
    internal static ConfigEntry<Toggle>? _configEnableShowBiome = null!;
    internal static ConfigEntry<Toggle>? _configEnableShowWindDirection = null!;
    internal static ConfigEntry<Toggle>? _configEnableDiscoveryRadius = null!;
    internal static ConfigEntry<int>? _configDiscoveryRadiusMap = null!;
    internal static ConfigEntry<int>? _configDiscoveryRadiusBoat = null!;
    #nullable disable

    private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
    {
        ConfigDescription extendedDescription = new(description.Description + (synchronizedSetting ? " [ServerSync]" : ""), description.AcceptableValues, description.Tags);
        ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);

        if (synchronizedSetting)
        {
            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;
        }

        return configEntry;
    }

    private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
    {
        return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
    }

    private class ConfigurationManagerAttributes
    {
        public bool? Browsable = false;
    }
    #endregion Configuration Options

}
