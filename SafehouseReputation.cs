using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using ModSettings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

[assembly: MelonInfo(typeof(SafehouseReputation.MainMod), "Safehouse Reputation", "1.1.0", "Bloodtroo")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace SafehouseReputation
{
    public class MainMod : MelonMod
    {
        private float updateTimer = 0f;
        private string lastScene = null;
        private float lastGameHour = -1f;

        public override void OnInitializeMelon()
        {
            Settings.instance.AddToModSettings("Safehouse Reputation");
            SafehouseData.Load();

            TryPatchSheltersEmbraceWarmth();

            MelonLogger.Msg("Safehouse Reputation Loaded");
        }

        public override void OnUpdate()
        {
            if (!Settings.instance.EnableMod)
                return;

            updateTimer += Time.deltaTime;

            if (updateTimer < 5f)
                return;

            updateTimer = 0f;

            try
            {
                string scene = SceneHelper.GetCurrentSceneName();

                if (string.IsNullOrEmpty(scene))
                    return;

                float currentGameHour = GameTimeHelper.GetCurrentGameHour();

                if (currentGameHour < 0f)
                    return;

                if (!SceneHelper.IsProbablyIndoorScene(scene))
                {
                    lastScene = null;
                    lastGameHour = currentGameHour;

                    SheltersEmbrace.UpdateNotification();
                    return;
                }

                if (lastScene != scene)
                {
                    lastScene = scene;
                    lastGameHour = currentGameHour;

                    if (Settings.instance.DebugLog)
                        MelonLogger.Msg($"Entered indoor scene: {scene}");

                    SheltersEmbrace.UpdateNotification();
                    return;
                }

                float deltaHours = currentGameHour - lastGameHour;

                if (deltaHours <= 0f)
                {
                    lastGameHour = currentGameHour;

                    SheltersEmbrace.UpdateNotification();
                    return;
                }

                if (deltaHours > 24f)
                    deltaHours = 24f;

                lastGameHour = currentGameHour;

                SafehouseData.AddHours(scene, deltaHours);

                SheltersEmbrace.UpdateNotification();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Safehouse Reputation update error: {ex}");
            }
        }

        private void TryPatchSheltersEmbraceWarmth()
        {
            try
            {
                Type freezingType = typeof(Freezing);

                MethodInfo target = freezingType.GetMethod(
                    "GetWarmthBonusCelsius",
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                if (target == null)
                {
                    MelonLogger.Warning("Shelter's Embrace warmth patch skipped: Freezing.GetWarmthBonusCelsius not found.");
                    return;
                }

                MethodInfo postfix = typeof(SheltersEmbraceWarmthPatch).GetMethod(
                    nameof(SheltersEmbraceWarmthPatch.Postfix),
                    BindingFlags.Static |
                    BindingFlags.NonPublic);

                HarmonyInstance.Patch(
                    target,
                    postfix: new HarmonyMethod(postfix));

                MelonLogger.Msg("Shelter's Embrace warmth patch applied.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Shelter's Embrace warmth patch failed: {ex.Message}");
            }
        }
    }

    internal class Settings : JsonModSettings
    {
        internal static Settings instance = new Settings();

        [Section("General")]
        [Name("Enable Safehouse Reputation")]
        public bool EnableMod = true;

        [Name("Hours Required Per Level")]
        [Slider(1, 100)]
        public int HoursPerLevel = 24;

        [Name("Maximum Safehouse Level")]
        [Slider(1, 10)]
        public int MaxLevel = 5;

        [Name("Show Level Up Messages")]
        public bool ShowLevelMessages = true;

        [Section("Shelter's Embrace Buff")]
        [Name("Enable Shelter's Embrace")]
        public bool EnableSheltersEmbrace = true;

        [Name("Required Safehouse Level")]
        [Slider(1, 5)]
        public int SheltersEmbraceRequiredLevel = 3;

        [Name("Warmth Bonus Per Level")]
        [Slider(0.1f, 2f, 20)]
        public float SheltersEmbraceBonusPerLevel = 0.5f;

        [Name("Maximum Warmth Bonus")]
        [Slider(0.5f, 5f, 10)]
        public float SheltersEmbraceMaxBonus = 1.5f;

        [Name("Show Shelter's Embrace Messages")]
        public bool ShowSheltersEmbraceMessages = true;

        [Section("Debug")]
        [Name("Enable Debug Logging")]
        public bool DebugLog = false;
    }

    internal static class SafehouseData
    {
        private static readonly Dictionary<string, float> SceneHours =
            new Dictionary<string, float>();

        private static readonly Dictionary<string, int> SceneLevels =
            new Dictionary<string, int>();

        private static string DataPath
        {
            get
            {
                string dir = Path.Combine(Environment.CurrentDirectory, "UserData");

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                return Path.Combine(dir, "SafehouseReputation.txt");
            }
        }

        internal static void Load()
        {
            SceneHours.Clear();
            SceneLevels.Clear();

            try
            {
                if (!File.Exists(DataPath))
                {
                    Save();
                    return;
                }

                string[] lines = File.ReadAllLines(DataPath);

                foreach (string rawLine in lines)
                {
                    if (string.IsNullOrWhiteSpace(rawLine))
                        continue;

                    string line = rawLine.Trim();

                    if (line.StartsWith("#"))
                        continue;

                    string[] parts = line.Split('|');

                    if (parts.Length < 3)
                        continue;

                    string scene = parts[0];

                    if (!SceneHelper.IsProbablyIndoorScene(scene))
                        continue;

                    if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float hours))
                        hours = 0f;

                    if (!int.TryParse(parts[2], out int level))
                        level = 0;

                    SceneHours[scene] = hours;
                    SceneLevels[scene] = level;
                }

                MelonLogger.Msg($"Safehouse Reputation data loaded. Shelters: {SceneHours.Count}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to load Safehouse Reputation data: {ex}");
            }
        }

        internal static void Save()
        {
            try
            {
                List<string> lines = new List<string>();

                lines.Add("# Safehouse Reputation data");
                lines.Add("# Format: SceneName|Hours|Level");

                foreach (KeyValuePair<string, float> pair in SceneHours)
                {
                    string scene = pair.Key;

                    if (!SceneHelper.IsProbablyIndoorScene(scene))
                        continue;

                    float hours = pair.Value;
                    int level = GetLevel(scene);

                    lines.Add(
                        scene + "|" +
                        hours.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                        level);
                }

                File.WriteAllLines(DataPath, lines.ToArray());
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to save Safehouse Reputation data: {ex}");
            }
        }

        internal static void AddHours(string scene, float hoursToAdd)
        {
            if (string.IsNullOrEmpty(scene))
                return;

            if (!SceneHelper.IsProbablyIndoorScene(scene))
                return;

            if (hoursToAdd <= 0f)
                return;

            if (!SceneHours.ContainsKey(scene))
                SceneHours[scene] = 0f;

            int oldLevel = CalculateLevel(SceneHours[scene]);

            SceneHours[scene] += hoursToAdd;

            int newLevel = CalculateLevel(SceneHours[scene]);

            SceneLevels[scene] = newLevel;

            if (Settings.instance.DebugLog)
            {
                MelonLogger.Msg(
                    $"Safehouse progress: {scene} +{hoursToAdd:F1}h | Total: {SceneHours[scene]:F1}h | Level: {newLevel}");
            }

            if (newLevel > oldLevel)
                OnLevelUp(scene, newLevel);

            Save();
        }

        internal static int GetLevel(string scene)
        {
            if (string.IsNullOrEmpty(scene))
                return 0;

            if (!SceneHelper.IsProbablyIndoorScene(scene))
                return 0;

            if (SceneLevels.TryGetValue(scene, out int level))
                return level;

            if (SceneHours.TryGetValue(scene, out float hours))
                return CalculateLevel(hours);

            return 0;
        }

        internal static float GetHours(string scene)
        {
            if (string.IsNullOrEmpty(scene))
                return 0f;

            if (!SceneHelper.IsProbablyIndoorScene(scene))
                return 0f;

            if (SceneHours.TryGetValue(scene, out float hours))
                return hours;

            return 0f;
        }

        private static int CalculateLevel(float hours)
        {
            int hoursPerLevel = Math.Max(1, Settings.instance.HoursPerLevel);
            int level = Mathf.FloorToInt(hours / hoursPerLevel);

            return Mathf.Clamp(level, 0, Settings.instance.MaxLevel);
        }

        private static void OnLevelUp(string scene, int level)
        {
            string title = GetLevelTitle(level);

            MelonLogger.Msg($"{scene} is now Level {level} - {title}");

            if (Settings.instance.ShowLevelMessages)
            {
                MelonLogger.Msg($"{scene}: Level {level} - {title}");
            }
        }

        internal static string GetLevelTitle(int level)
        {
            switch (level)
            {
                case 1:
                    return "Familiar Shelter";

                case 2:
                    return "Trusted Shelter";

                case 3:
                    return "Safehouse";

                case 4:
                    return "Home Base";

                case 5:
                    return "Survivor Haven";

                default:
                    if (level > 5)
                        return "Legendary Haven";

                    return "Unknown Shelter";
            }
        }
    }

    internal static class SheltersEmbrace
    {
        private static bool wasActive = false;
        private static string lastScene = null;
        private static int lastLevel = 0;

        internal static bool TryGetBonus(out float bonus, out int level, out string scene)
        {
            bonus = 0f;
            level = 0;
            scene = SceneHelper.GetCurrentSceneName();

            if (!Settings.instance.EnableSheltersEmbrace)
                return false;

            if (string.IsNullOrEmpty(scene))
                return false;

            if (!SceneHelper.IsProbablyIndoorScene(scene))
                return false;

            level = SafehouseData.GetLevel(scene);

            if (level < Settings.instance.SheltersEmbraceRequiredLevel)
                return false;

            int bonusLevel =
                level - Settings.instance.SheltersEmbraceRequiredLevel + 1;

            bonus =
                bonusLevel * Settings.instance.SheltersEmbraceBonusPerLevel;

            bonus =
                Mathf.Min(bonus, Settings.instance.SheltersEmbraceMaxBonus);

            return bonus > 0f;
        }

        internal static void UpdateNotification()
        {
            bool active =
                TryGetBonus(out float bonus, out int level, out string scene);

            if (active)
            {
                if (!wasActive || lastScene != scene || lastLevel != level)
                {
                    if (Settings.instance.ShowSheltersEmbraceMessages)
                    {
                        MelonLogger.Msg(
                            $"Shelter's Embrace active: {scene} | Level {level} | +{bonus:F1}°C");
                    }

                    wasActive = true;
                    lastScene = scene;
                    lastLevel = level;
                }
            }
            else
            {
                if (wasActive && Settings.instance.ShowSheltersEmbraceMessages)
                {
                    MelonLogger.Msg("Shelter's Embrace faded.");
                }

                wasActive = false;
                lastScene = null;
                lastLevel = 0;
            }
        }
    }

    internal static class SheltersEmbraceWarmthPatch
    {
        internal static void Postfix(ref float __result)
        {
            if (SheltersEmbrace.TryGetBonus(
                    out float bonus,
                    out int level,
                    out string scene))
            {
                __result += bonus;
            }
        }
    }

    internal static class SceneHelper
    {
        internal static string GetCurrentSceneName()
        {
            try
            {
                return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            }
            catch
            {
                return null;
            }
        }

        internal static bool IsProbablyIndoorScene(string scene)
        {
            if (string.IsNullOrEmpty(scene))
                return false;

            string s = scene.ToLowerInvariant();

            if (s.Contains("mainmenu"))
                return false;

            if (s.Contains("menu"))
                return false;

            if (s.Contains("frontend"))
                return false;

            if (s.Contains("loading"))
                return false;

            if (s.Contains("boot"))
                return false;

            if (s.Contains("region"))
                return false;

            if (s.Contains("outdoor"))
                return false;

            if (s.Contains("terrain"))
                return false;

            if (s.Contains("world"))
                return false;

            if (s.Contains("road"))
                return false;

            if (s.Contains("river"))
                return false;

            if (s.Contains("ravine"))
                return false;

            if (s.Contains("marsh"))
                return false;

            if (s.Contains("valley"))
                return false;

            if (s.Contains("mountain"))
                return false;

            if (s.Contains("coast"))
                return false;

            return true;
        }
    }

    internal static class GameTimeHelper
    {
        internal static float GetCurrentGameHour()
        {
            try
            {
                object tod = GameManager.GetTimeOfDayComponent();

                if (tod == null)
                    return -1f;

                int day = CallIntMethod(tod, "GetDayNumber", 0);
                int hour = CallIntMethod(tod, "GetHour", 0);

                return day * 24f + hour;
            }
            catch
            {
                return -1f;
            }
        }

        private static int CallIntMethod(object obj, string methodName, int fallback)
        {
            if (obj == null)
                return fallback;

            try
            {
                MethodInfo method = obj.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);

                if (method == null)
                    return fallback;

                object result = method.Invoke(obj, null);

                if (result == null)
                    return fallback;

                return Convert.ToInt32(result);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
