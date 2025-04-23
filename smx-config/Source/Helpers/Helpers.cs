using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Resources;
using System.Windows.Threading;
using SMXJSON;

namespace smx_config
{
    // Track whether we're configuring one pad or both at once.
    static class ActivePad
    {
        public enum SelectedPad {
            P1,
            P2,
            Both,
        };
        
        // The actual pad selection.  This defaults to both, and doesn't change if
        // only one pad is selected.  We don't actually show "both" in the dropdown
        // unless two pads are connected, but the underlying setting remains.
        public static SelectedPad selectedPad = SelectedPad.Both;

        // A shortcut for when a LoadFromConfigDelegateArgs isn't available:
        public static IEnumerable<Tuple<int, SMX.SMXConfig>> ActivePads()
        {
            // In case we're called in design mode, just return an empty list.
            if (CurrentSMXDevice.singleton == null)
        return new List<Tuple<int, SMX.SMXConfig>>();

            return ActivePads(CurrentSMXDevice.singleton.GetState());
        }

        // Yield each connected pad which is currently active for configuration.
        public static IEnumerable<Tuple<int, SMX.SMXConfig>> ActivePads(LoadFromConfigDelegateArgs args)
        {
            var padConnected = (from controller in args.controller select controller.info.connected).ToArray();

            // If both pads are connected and a single pad is selected, ignore the deselected pad.
            if (padConnected.All(ci => ci))
            {
                padConnected[selectedPad switch
                {
                    SelectedPad.P1 => 1,
                    SelectedPad.P2 => 0,
                    SelectedPad.Both => throw new NotImplementedException(),
                    _ => throw new NotImplementedException(),
                }] = false;
            }
            for (int i = 0; i < padConnected.Length; ++i) {
                if (padConnected[i])
                    yield return Tuple.Create(i, args.controller[i].config);
            }
        }

        // We know the selected pads are synced if there are two active, and when refreshing a
        // UI we just want one of them to set the UI to.  For convenience, return the first one.
        public static SMX.SMXConfig GetFirstActivePadConfig(LoadFromConfigDelegateArgs args)
        {
            foreach (Tuple<int, SMX.SMXConfig> activePad in ActivePads(args))
        return activePad.Item2;

            // There aren't any pads connected.  Just return a dummy config, since the UI
            // isn't visible.
            return SMX.SMXConfig.Create();
        }

        public static SMX.SMXConfig GetFirstActivePadConfig()
        {
            return GetFirstActivePadConfig(CurrentSMXDevice.singleton.GetState());
        }
    }

    static class Helpers
    {
        // Return true if arg is in the commandline.
        public static bool HasCommandlineArgument(string arg)
        {
            foreach (string s in Environment.GetCommandLineArgs())
            {
                if (s == arg)
        return true;
            }
            return false;
        }

        // Return true if we're in debug mode.
        public static bool GetDebug()
        {
            return HasCommandlineArgument("-d");
        }

        // Return true if we were launched on startup.
        public static bool LaunchedOnStartup()
        {
            return HasCommandlineArgument("-s");
        }
        
        public static bool NoClampOutput()
        {
            return HasCommandlineArgument("--noclamp");
        }

        // Return the last Win32 error as a string.
        public static string GetLastWin32ErrorString()
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 0)
        return "";
            return new System.ComponentModel.Win32Exception(error).Message;
        }

        // https://stackoverflow.com/a/129395/136829
        public static T DeepClone<T>(T obj)
        {
            using (var ms = new MemoryStream())
            {
                // TODO: pucgenie: https://github.com/dotnet/designs/blob/main/accepted/2020/better-obsoletion/binaryformatter-obsoletion.md
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;

                return (T) formatter.Deserialize(ms);
            }
        }

        // Work around Enumerable.SequenceEqual not checking if the arrays are null.
        public static bool SequenceEqual<TSource>(this IEnumerable<TSource> first, IEnumerable<TSource> second)
        {
            if (first == second)
        return true;
            if (first == null || second == null)
        return false;
            return Enumerable.SequenceEqual(first, second);
        }

        public static Color ColorFromFloatRGB(double r, double g, double b)
        {
            byte R = (byte) Math.Max(0, Math.Min(255, r * 255));
            byte G = (byte) Math.Max(0, Math.Min(255, g * 255));
            byte B = (byte) Math.Max(0, Math.Min(255, b * 255));
            return Color.FromRgb(R, G, B);
        }

        // Return a Color as an HTML color code.
        public static string ColorToString(Color color)
        {
            // WPF's Color.ToString() returns #AARRGGBB, which is just wrong.  Alpha is always
            // last in HTML color codes.  We don't need alpha, so just strip it off.
            return $"#{color.ToString().Substring(3)}";
        }

        // Parse #RRGGBB and return a Color, or white if the string isn't in the correct format.
        public static Color ParseColorString(string s)
        {
            // We only expect "#RRGGBB".
            if (s.Length != 7 || !s.StartsWith("#"))
        return Color.FromRgb(255,255,255);

            try {
        return (Color) ColorConverter.ConvertFromString(s);
            }
            catch(System.FormatException)
            {
        return Color.FromRgb(255,255,255);
            }
        }

        // Light values are actually in the range 0-170 and not 0-255, since higher values aren't
        // any brighter and just draw more power.  The auto-lighting colors that we're configuring
        // need to be scaled to this range too, but show full range colors in the UI.
        readonly static double LightsScaleFactor = 0.666666f;
        static public Byte ScaleColor(Byte c)
        {
            return (Byte) Math.Round(c * LightsScaleFactor);
        }
        static public Byte UnscaleColor(Byte c)
        {
            byte result = (byte) Math.Round(Math.Min(255, c / LightsScaleFactor));

            // The color values we output are quantized, since we're scaling an 8-bit value.
            // This doesn't have any real effect, but it causes #FFFFFF in the settings export
            // file to be written out as #FDFDFD (which has the same value in hardware).  Just
            // so the common value of white is clean, snap these values to 0xFF.  The end result
            // will be the same.
            if (result >= 0xFD)
        return 0xFF;
            return result;
        }

        static public Color ScaleColor(Color c)
        {
            return Color.FromRgb(ScaleColor(c.R), ScaleColor(c.G), ScaleColor(c.B));
        }

        static public Color UnscaleColor(Color c)
        {
            return Color.FromRgb(UnscaleColor(c.R), UnscaleColor(c.G), UnscaleColor(c.B));
        }

        public static Color FromHSV(double H, double S, double V)
        {
            H %= 360;
            S = Math.Max(0, Math.Min(1, S));
            V = Math.Max(0, Math.Min(1, V));
            if (H < 0) // pucgenie: Should not be possible after %360
                H += 360;
            H /= 60;
 
            if (S < 0.0001f)
                    return ColorFromFloatRGB(V, V, V);
 
            double C = V * S;
            double X = C * (1 - Math.Abs((H % 2) - 1));
            var ret = (int)Math.Round(Math.Floor(H)) switch
            {
                0 => ColorFromFloatRGB(C, X, 0),
                1 => ColorFromFloatRGB(X, C, 0),
                2 => ColorFromFloatRGB(0, C, X),
                3 => ColorFromFloatRGB(0, X, C),
                4 => ColorFromFloatRGB(X, 0, C),
                _ => ColorFromFloatRGB(C, 0, X),
            };
            ret -= ColorFromFloatRGB(C-V, C-V, C-V);
            return ret;
        }
        
        public static void ToHSV(Color c, out double h, out double s, out double v)
        {
            h = s = v = 0;
            if (c.R == 0 && c.G == 0 && c.B == 0)
        return;

            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;

            double m = Math.Min(Math.Min(r, g), b);
            double M = Math.Max(Math.Max(r, g), b);
            double C = M - m;
            if (Math.Abs(r-g) < 0.0001f && Math.Abs(g-b) < 0.0001f) // grey
                    h = 0;
            else if (Math.Abs(r-M) < 0.0001f) // M == R
                    h = ((g - b)/C) % 6;
            else if (Math.Abs(g-M) < 0.0001f) // M == G
                    h = (b - r)/C + 2;
            else // M == B
                    h = (r - g)/C + 4;

            h *= 60;
            if (h < 0)
                h += 360;
 
            s = C / M;
            v = M;
        }

        // Return our settings directory, creating it if it doesn't exist.
        public static DirectoryInfo GetSettingsDirectory()
        {
            var result = new DirectoryInfo($"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/StepManiaX/");
            result.Create();
            return result;
        }

        public static void SaveFileToSettings(string filename, byte[] data)
        {
            var outputFile = new System.IO.FileInfo($"{GetSettingsDirectory()}{filename}");
            outputFile.Directory.Create();
            System.IO.File.WriteAllBytes(outputFile.FullName, data);
        }

        public static Dictionary<SMX.SMX.LightsType, string> LightsTypeNames = new()
        {
            { SMX.SMX.LightsType.LightsType_Pressed, "pressed" },
            { SMX.SMX.LightsType.LightsType_Released, "released" },
        };

        // Load any saved animations from disk.
        public static void LoadSavedPanelAnimations()
        {
            for (int pad = 0; pad < 2; ++pad)
            {
                foreach (var it in LightsTypeNames)
                    LoadSavedAnimationType(pad, it.Key);
            }
        }

        public static void SaveAnimationToDisk(int pad, SMX.SMX.LightsType type, byte[] data)
        {
            // TODO: pucgenie: fix relative path?
            Helpers.SaveFileToSettings($"Animations/Pad{pad+1}/{LightsTypeNames[type]}.gif", data);
        }

        // Read a saved PanelAnimation.
        //
        // Data will always be returned.  If the user hasn't saved anything, we'll return
        // our default animation.
        private static byte[] ReadSavedAnimationType(int pad, SMX.SMX.LightsType type)
        {
            var filename = $"{LightsTypeNames[type]}.gif";
            var customGifFile = new FileInfo($"{GetSettingsDirectory()}Animations/Pad{pad+1}/{filename}");
            if (!customGifFile.Exists)
            {
                // If the user has never loaded a file, load our default.
                StreamResourceInfo info = Application.GetResourceStream(
                        new Uri($"pack://application:,,,/Resources/{filename}")
                    );
                byte[] gif = new byte[info.Stream.Length];
                info.Stream.Read(gif, 0, gif.Length);
        return gif;
            } else {
                // pucgenie: If this file can't be read, this would probably not be the user's only problem...
                try {
        return System.IO.File.ReadAllBytes(customGifFile.FullName);
                } catch (Exception e) {
                    // pucgenie: but let's give him something other than a "general error".
                    Console.Error.WriteLine($"Unexpected error reading file: {customGifFile}");
                    throw e;
                }
            }
        }

        // Load a PanelAnimation from disk.
        private static void LoadSavedAnimationType(int pad, SMX.SMX.LightsType type)
        {
            byte[] gif = ReadSavedAnimationType(pad, type);
            SMX.SMX.LightsAnimation_Load(gif, pad, type, out string error);
        }

        // Some broken antivirus software locks files when they're read.  This is horrifying and
        // breaks lots of software, including WPF's settings class.  This is a race condition,
        // so try to work around this by trying repeatedly.  There's not much else we can do about
        // it other than asking users to use a better antivirus.
        public static bool SaveApplicationSettings()
        {
            for (var i = 0; i < 10; ++i)
            {
                try {
                    Properties.Settings.Default.Save();
        return true;
                } catch (IOException e)
                {
                    Console.Error.WriteLine($"Error writing settings.  Trying again: {e}");
                }
            }
            return false;
        }

        // Create a .lnk.
        public static void CreateShortcut(FileInfo outputFile, FileInfo targetPath, string arguments)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(outputFile.FullName);

            shortcut.TargetPath = targetPath.FullName;
            shortcut.Arguments = arguments;
            shortcut.WindowStyle = 0;
            shortcut.Save();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);
    }

    // The threshold sliders in the advanced tab affect different panels and sensors depending
    // on the user's settings.  This handles managing which sensors each slider controls.
    static public class ThresholdSettings
    {
        [Serializable]
        public struct PanelAndSensor
        {
            public PanelAndSensor(int panel, int sensor)
            {
                this.panel = panel;
                this.sensor = sensor;
            }
            public int panel;
            public int sensor;
        };
        
        public static readonly List<string> thresholdSliderNames = new()
        {
            "up-left", "up", "up-right",
            "left", "center", "right",
            "down-left", "down", "down-right",
            "cardinal", "corner",
            "inner-sensors",
            "outer-sensors",
            "custom-sensors",
        };

        // These correspond with ThresholdSlider.Type.
        static readonly Dictionary<string, int> panelNameToIndex = new() {
            { "up-left",    0 },
            { "up",         1 },
            { "up-right",   2 },
            { "left",       3 },
            { "center",     4 },
            { "right",      5 },
            { "down-left",  6 },
            { "down",       7 },
            { "down-right", 8 },

            // The cardinal and corner sliders write to the down and up-right panels, and
            // are then synced to the other panels.
            { "cardinal",   7 },
            { "corner",     2 },
        };

        // Save and load the list of custom threshold sensors to settings.  These aren't saved to the pad, we
        // just keep them in application settings.
        static List<PanelAndSensor> cachedCustomSensors;
        static public void SetCustomSensors(List<PanelAndSensor> panelAndSensors)
        {
            List<object> result = new();
            foreach(PanelAndSensor panelAndSensor in panelAndSensors)
            {
                List<int> panelAndSensorArray = new() { panelAndSensor.panel, panelAndSensor.sensor };
                result.Add(panelAndSensorArray);
            }

            SetCustomSensorsJSON(result);
        }

        // Set CustomSensors from a [[1,1],[2,2]] array.  This is what we save to settings and
        // export to JSON.
        static public void SetCustomSensorsJSON(List<object> panelAndSensors)
        {
            StringBuilder sb = new();
            SerializeJSON.Serialize(panelAndSensors, sb, 0);
            sb.Append('\n');
            Properties.Settings.Default.CustomSensors = sb.ToString();
            sb.Length = 0;
            Helpers.SaveApplicationSettings();

            // Clear the cache.  Set it to null instead of assigning panelAndSensors to it to force
            // it to re-parse at least once, to catch problems early.
            cachedCustomSensors = null;
        }

        // Return the sensors that are controlled by the custom-sensors slider.  The other
        // threshold sliders will leave these alone.
        static public List<PanelAndSensor> GetCustomSensors()
        {
//            Properties.Settings.Default.CustomSensors = "[[0,0], [1,0]]";
            // This is only ever changed with calls to SetCustomSensors.
            if (cachedCustomSensors != null)
        return Helpers.DeepClone(cachedCustomSensors);

            var result = new List<PanelAndSensor>();
            if (Properties.Settings.Default.CustomSensors == "")
        return result;

            try {
                // This is a list of [panel,sensor] arrays:
                // [[0,0], [0,1], [1,0]]
                foreach (List<object> panelAndSensor in GetCustomSensorsJSON())
                {
                    int panel = panelAndSensor.Get(0, -1);
                    int sensor = panelAndSensor.Get(1, -1);
                    if (panel == -1 || sensor == -1)
                continue;

                    result.Add(new PanelAndSensor(panel, sensor));
                }
            } catch (ParseError) {
        return result;
            }

            cachedCustomSensors = result;

            return Helpers.DeepClone(cachedCustomSensors);
        }

        static public List<List<object>> GetCustomSensorsJSON()
        {
            try {
        return SMXJSON.ParseJSON.Parse<List<List<object>>>(Properties.Settings.Default.CustomSensors);
            } catch (ParseError) {
                // CustomSensors is empty by default.  We could test if it's empty, but as a more general
                // safety, just catch any JSON errors in case something invalid is saved to it.
        return new List<List<object>>();
            }
        }

        const int SensorLeft = 0;
        const int SensorRight = 1;
        const int SensorUp = 2;
        const int SensorDown = 3;
        static public List<PanelAndSensor> GetInnerSensors()
        {
            return new List<PanelAndSensor>()
            {
                new(1,SensorDown), // up panel, bottom sensor
                new(3,SensorRight), // left panel, right sensor
                new(5,SensorLeft), // right panel, left sensor
                new(7,SensorUp), // down panel, top sensor
            };
        }

        static public List<PanelAndSensor> GetOuterSensors()
        {
            return new List<PanelAndSensor>()
            {
                new(1,SensorUp), // up panel, top sensor
                new(3,SensorLeft), // left panel, left sensor
                new(5,SensorRight), // right panel, right sensor
                new(7,SensorDown), // down panel, bottom sensor
            };
        }
        // Return the sensors controlled by the given slider.  Most of the work is done
        // in GetControlledSensorsForSliderTypeInternal.  This just handles removing overlapping
        // sensors.  If inner-sensors is enabled, the inner sensors are removed from the normal
        // thresholds.
        //
        // This is really inefficient: it calls GetControlledSensorsForSliderTypeInternal a lot,
        // and the filtering is a linear search, but it doesn't matter.
        //
        // If includeOverridden is true, include sensors that would be controlled by this slider
        // by default, but which have been overridden by a higher priority slider, or which are
        // disabled by checkboxes.  This is used for the UI.
        static public List<PanelAndSensor> GetControlledSensorsForSliderType(string Type, bool hasAllPanels, bool includeOverridden)
        {
            var result = GetControlledSensorsForSliderTypeInternal(Type, hasAllPanels, includeOverridden);

            if (!includeOverridden)
            {
                // inner-sensors, outer-sensors and custom thresholds overlap each other and the standard
                // sliders.  inner-sensors and outer-sensors take over the equivalent sensors in the standard
                // sliders, and custom thresholds take priority over everything else.
                //
                // We always pass false to includeOverridden here, since we need to know the real state of the
                // sliders we're removing.
                if (Type == "inner-sensors" || Type == "outer-sensors")
                {
                    // Remove any sensors controlled by the custom threshold.
                    RemoveFromSensorList(result, GetControlledSensorsForSliderTypeInternal("custom-sensors", hasAllPanels, false));
                }
                else if (Type != "custom-sensors")
                {
                    // This is a regular slider.  Remove any sensors controlled by inner-sensors, outer-sensors
                    // or custom-sensors.
                    RemoveFromSensorList(result, GetControlledSensorsForSliderTypeInternal("inner-sensors", hasAllPanels, false));
                    RemoveFromSensorList(result, GetControlledSensorsForSliderTypeInternal("outer-sensors", hasAllPanels, false));
                    RemoveFromSensorList(result, GetControlledSensorsForSliderTypeInternal("custom-sensors", hasAllPanels, false));
                }
            }

            return result;
        }

        static private void RemoveFromSensorList(List<PanelAndSensor> target, List<PanelAndSensor> sensorsToRemove)
        {
            foreach(PanelAndSensor panelAndSensor in sensorsToRemove)
                target.Remove(panelAndSensor);
        }

        static private List<PanelAndSensor> GetControlledSensorsForSliderTypeInternal(string Type, bool hasAllPanels, bool includeOverridden)
        {
            // inner-sensors and outer-sensors do nothing if their checkbox is disabled.  We do this here because we
            // need to skip this for the RemoveFromSensorList logic above.
            if ((!includeOverridden)
                && (
                    (Type == "inner-sensors" && !Properties.Settings.Default.UseInnerSensorThresholds)
                    || (Type == "outer-sensors" && !Properties.Settings.Default.UseOuterSensorThresholds)
                )
            ) {
        return new List<PanelAndSensor>();
            }

            // Special sliders:
            switch (Type) {
                case "custom-sensors":
        return GetCustomSensors();
                case "inner-sensors":
        return GetInnerSensors();
                case "outer-sensors":
        return GetOuterSensors();
            }

            var result = new List<PanelAndSensor>();

            // Check if this slider is shown in this mode.
            if (hasAllPanels)
            {
                // Hide the combo sliders in advanced mode.
                if (Type == "cardinal" || Type == "corner")
        return result;
            }
            else // pucgenie: Just left here for understanding.
            {
                // Only these sliders are shown in normal mode.
                if (Type != "up" && Type != "center" && Type != "cardinal" && Type != "corner")
        return result;
            }

            // If advanced mode is disabled, save to all panels this slider affects.  The down arrow controls
            // all four cardinal panels.  (If advanced mode is enabled we'll never be a different cardinal
            // direction, since those widgets won't exist.)  If it's disabled, just write to our own panel.
            var saveToPanels = new List<int>();
            int ourPanelIdx = panelNameToIndex[Type];
            saveToPanels.Add(ourPanelIdx);
            if (!hasAllPanels)
                saveToPanels.AddRange(ConfigPresets.GetPanelsToSyncUnifiedThresholds(ourPanelIdx));

            foreach (int panelIdx in saveToPanels)
            {
                for (int sensor = 0; sensor < 4; ++sensor)
                    result.Add(new PanelAndSensor(panelIdx, sensor));
            }

            return result;
        }

        // If the user disables inner-sensors after setting a value and control of those thresholds
        // goes back to other sliders, the old inner-sensors thresholds will still be set in config
        // until the user changes them, which is confusing.  Make sure the value of each slider is
        // actually set to config, even if the user doesn't change them.
        //
        // This isn't perfect.  If the user assigns the first up sensor to custom and then removes it,
        // so that sensor goes back to the normal up slider, this will sync the custom value to up.
        // That's because we don't know which thresholds were actually being controlled by the up slider
        // before it was changed.  This is tricky to fix and not a big problem.
        private static void SyncSliderThresholdsForConfig(ref SMX.SMXConfig config)
        {
            if (!config.isFSR())
        return;

            foreach (string sliderName in thresholdSliderNames)
            {
                var controlledSensors = GetControlledSensorsForSliderType(sliderName, config.HasAllPanels(), false);
                if (controlledSensors.Count == 0)
            continue;
                var firstSensor = controlledSensors[0];
                var firstSensorFSRLowThreshold = config.panelSettings[firstSensor.panel].fsrLowThreshold[firstSensor.sensor];
                var firstSensorFSRHighThreshold = config.panelSettings[firstSensor.panel].fsrHighThreshold[firstSensor.sensor];

                foreach (var panelAndSensor in controlledSensors)
                {
                    var otherPanelSettings = config.panelSettings[panelAndSensor.panel];
                    otherPanelSettings.fsrLowThreshold[panelAndSensor.sensor] = firstSensorFSRLowThreshold;
                    otherPanelSettings.fsrHighThreshold[panelAndSensor.sensor] = firstSensorFSRHighThreshold;
                }
            }
        }

        public static void SyncSliderThresholds()
        {
            foreach(Tuple<int,SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                SMX.SMXConfig config = activePad.Item2;
                SyncSliderThresholdsForConfig(ref config);
                SMX.SMX.SetConfig(activePad.Item1, config);
            }

            CurrentSMXDevice.singleton.FireConfigurationChanged(null);
        }

        // Set all high thresholds to one greater than the low threshold.  This is required
        // for non-legacy mode.
        public static void ForceAllThresholdsOneApart()
        {
            foreach(Tuple<int,SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                SMX.SMXConfig config = activePad.Item2;
                for(int panel = 0; panel < 8; ++panel)
                {
                    for(int sensor = 0; sensor < 4; ++sensor)
                        config.panelSettings[panel].fsrHighThreshold[sensor] = (byte) (config.panelSettings[panel].fsrLowThreshold[sensor] + 1);
                }
                SMX.SMX.SetConfig(activePad.Item1, config);
            }

            CurrentSMXDevice.singleton.FireConfigurationChanged(null);
        }

        public static bool IsAdvancedModeRequired()
        {
            return false;
        }
    }

    // This class just makes it easier to assemble binary command packets.
    public class CommandBuffer
    {
        public void Write(string s)
        {
            char[] buf = s.ToCharArray();
            byte[] data = new byte[buf.Length];
            for(int i = 0; i < buf.Length; ++i)
                data[i] = (byte) buf[i];
            Write(data);
        }
        public void Write(byte[] s) { parts.AddLast(s); }
        public void Write(byte b) { Write(new byte[] { b }); }
        public void Write(char b) { Write((byte) b); }

        public byte[] Get()
        {
            int length = 0;
            foreach(byte[] part in parts)
                length += part.Length;

            byte[] result = new byte[length];
            int next = 0;
            foreach(byte[] part in parts)
            {
                Buffer.BlockCopy(part, 0, result, next, part.Length);
                next += part.Length;
            }
            return result;
        }

        private LinkedList<byte[]> parts = new();
    };

    // Manage launching on startup.
    static class LaunchOnStartup
    {
        public static FileInfo GetLaunchShortcutFilename()
        {
            return new FileInfo($"{Environment.GetFolderPath(Environment.SpecialFolder.Startup)}/StepManiaX.lnk");
        }

        // Enable or disable launching on startup.
        public static bool Enable
        {
            get {
                return Properties.Settings.Default.LaunchOnStartup;
            }

            set {
                if (Properties.Settings.Default.LaunchOnStartup == value)
                    return;

                // Remember whether we want to be launched on startup.  This is used as a sanity
                // check in case we're not able to remove our launch shortcut.
                Properties.Settings.Default.LaunchOnStartup = value;
                Helpers.SaveApplicationSettings();

                var shortcutFile = GetLaunchShortcutFilename();
                if (value)
                {
                    Helpers.CreateShortcut(shortcutFile, new System.IO.FileInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName), "-s");
                } else {
                    if (shortcutFile.Exists) {
                        try {
                            shortcutFile.Delete();
                        } catch (IOException ioex) {
                            Console.Error.WriteLine($"Failed to delete shortcut file: {ioex}");
                        } catch (SecurityException secex) {
                            Console.Error.WriteLine($"Security error while deleting shortcut file: {secex}");
                        } catch (UnauthorizedAccessException unauthex) {
                            Console.Error.WriteLine($"Unauthorized to delete shortcut file: {unauthex}");
                        }
                    }
                }
            }
        }
    };

    // When enabled, periodically set all lights to the current auto-lighting color.  This
    // is enabled while manipulating the step color slider.
    class ShowAutoLightsColor
    {
        private DispatcherTimer LightsTimer;

        public ShowAutoLightsColor()
        {
            LightsTimer = new DispatcherTimer
            {
                // Run at 30fps.
                Interval = new TimeSpan(0, 0, 0, 0, 1000 / 33)
            };

            LightsTimer.Tick += delegate(object sender, EventArgs e)
            {
                if (!LightsTimer.IsEnabled)
                    return;

                AutoLightsColorRefreshColor();
            };
        }

        public void Start()
        {
            // To show the current color, send a lights command periodically.  If we stop sending
            // this for a while the controller will return to auto-lights, which we won't want to
            // happen until AutoLightsColorEnd is called.
            if (LightsTimer.IsEnabled)
                return;

            // Don't wait for an interval to send the first update.
            //AutoLightsColorRefreshColor();

            LightsTimer.Start();
        }

        public void Stop()
        {
            LightsTimer.Stop();

            // Reenable pad auto-lighting.  If we're running animations in SMXPanelAnimation,
            // this will be overridden by it once it sends lights.
            SMX.SMX.ReenableAutoLights();
        }

        private void AutoLightsColorRefreshColor()
        {
            CommandBuffer cmd = new();

            for (int pad = 0; pad < 2; ++pad)
            {
                // Use this panel's color.  If a panel isn't connected, we still need to run the
                // loop below to insert data for the panel.
                var color = new byte[9*3];
                if (SMX.SMX.GetConfig(pad, out SMX.SMXConfig config))
                    color = config.stepColor;
                for ( int iPanel = 0; iPanel < 9; ++iPanel )
                {
                    for( int i = 0; i < 25; ++i )
                    {
                        // Auto-lights colors in the config packet are scaled so the firmware
                        // doesn't have to do it, but here we're setting the panel color to
                        // the auto-light color directly to preview the color.  SetLights
                        // will apply the scaling, so we need to remove it.
                        cmd.Write( Helpers.UnscaleColor(color[iPanel*3+0]) );
                        cmd.Write( Helpers.UnscaleColor(color[iPanel*3+1]) );
                        cmd.Write( Helpers.UnscaleColor(color[iPanel*3+2]) );
                    }
                }
            }

            SMX.SMX.SetLights2(cmd.Get());
        }
    };

    static class SMXHelpers
    {
        /// <summary>
        /// Export configurable values in SMXConfig to a JSON string.
        /// TODO: pucgenie: Serialize onto output stream/buffer, not to String.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static void ExportSettingsToJSON(SMX.SMXConfig config, StringBuilder result)
        {
            // The user only uses one of low or high thresholds.  Only export the
            // settings the user is actually using.
            var dict = new Dictionary<string, Object>();
            if (config.isFSR())
            {
                // pucgenie: I can't test it myself - no FSR at home
                dict.Add("fsrLowThresholds", (from panel in config.panelSettings select panel.fsrLowThreshold[0]).ToList());
                dict.Add("fsrHighThresholds", (from panel in config.panelSettings select panel.fsrHighThreshold[0]).ToList());
            }
            else
            {
                dict.Add("panelLowThresholds", (from panel in config.panelSettings select panel.loadCellLowThreshold).ToList());
                dict.Add("panelHighThresholds", (from panel in config.panelSettings select panel.loadCellHighThreshold).ToList());
            }

            // Store the enabled panel mask as a simple list of which panels are selected.
            var enabledPanels = config.GetEnabledPanels();
            var enabledPanelList = new List<int>();
            for (int panel = 0; panel < 9; ++panel)
            {
                if (enabledPanels[panel])
                    enabledPanelList.Add(panel);
            }
            dict.Add("enabledPanels", enabledPanelList);

            // Store panel colors.
            var panelColors = new List<string>();
            for (int PanelIndex = 0; PanelIndex < 9; ++PanelIndex)
            {
                // Scale colors from the hardware value back to the 0-255 value we use in the UI.
                Color color = Color.FromRgb(
                    config.stepColor[PanelIndex*3+0],
                    config.stepColor[PanelIndex*3+1],
                    config.stepColor[PanelIndex*3+2]);
                color = Helpers.UnscaleColor(color);
                panelColors.Add(Helpers.ColorToString(color));
            }
            dict.Add("panelColors", panelColors);

            dict.Add("useOuterSensorThresholds", Properties.Settings.Default.UseOuterSensorThresholds);
            dict.Add("useInnerSensorThresholds", Properties.Settings.Default.UseInnerSensorThresholds);
            dict.Add("customSensors", ThresholdSettings.GetCustomSensorsJSON());

            SMXJSON.SerializeJSON.Serialize(dict, result, 0);
            result.Append('\n');
        }

        // Import a saved JSON configuration to an SMXConfig.
        public static void ImportSettingsFromJSON(string json, ref SMX.SMXConfig config)
        {
            var dict = SMXJSON.ParseJSON.Parse<Dictionary<string, Object>>(json);
            // Read the thresholds.  If any values are missing, we'll leave the value in config alone.
            if (config.isFSR())
            {
                List<Object> newPanelLowThresholds = dict.Get("fsrLowThresholds", new List<Object>());
                List<Object> newPanelHighThresholds = dict.Get("fsrHighThresholds", new List<Object>());
                for(int panel = 0; panel < 9; ++panel)
                {
                    for(int sensor = 0; sensor < 4; ++sensor)
                    {
                        config.panelSettings[panel].fsrLowThreshold[sensor] = (byte) newPanelLowThresholds.Get(panel, (int) config.panelSettings[panel].fsrLowThreshold[sensor]);
                        config.panelSettings[panel].fsrHighThreshold[sensor] = (byte) newPanelHighThresholds.Get(panel, (int) config.panelSettings[panel].fsrHighThreshold[sensor]);
                    }
                }
            }
            else
            {
                var newPanelLowThresholds = dict.Get("panelLowThresholds", new List<Object>());
                var newPanelHighThresholds = dict.Get("panelHighThresholds", new List<Object>());
                for(int panel = 0; panel < 9; ++panel)
                {
                    config.panelSettings[panel].loadCellLowThreshold = newPanelLowThresholds.Get(panel, config.panelSettings[panel].loadCellLowThreshold);
                    config.panelSettings[panel].loadCellHighThreshold = newPanelHighThresholds.Get(panel, config.panelSettings[panel].loadCellHighThreshold);
                }
            }

            var enabledPanelList = dict.Get<List<Object>>("enabledPanels", null);
            if (enabledPanelList != null)
            {
                bool[] enabledPanels = new bool[9];
                for (int i = 0; i < enabledPanelList.Count; ++i)
                {
                    int panel = enabledPanelList.Get(i, 0);

                    // Sanity check:
                    if (panel < 0 || panel >= 9)
                continue;
                    enabledPanels[panel] = true;
                }
                config.SetEnabledPanels(enabledPanels);
            }

            var panelColors = dict.Get<List<Object>>("panelColors", null);
            if (panelColors != null)
            {
                for(int PanelIndex = 0; PanelIndex < 9 && PanelIndex < panelColors.Count; ++PanelIndex)
                {
                    string colorString = panelColors.Get(PanelIndex, "#FFFFFF");
                    Color color = Helpers.ParseColorString(colorString);
                    color = Helpers.ScaleColor(color);

                    config.stepColor[PanelIndex*3+0] = color.R;
                    config.stepColor[PanelIndex*3+1] = color.G;
                    config.stepColor[PanelIndex*3+2] = color.B;
                }
            }

            Properties.Settings.Default.UseOuterSensorThresholds = dict.Get<bool>("useOuterSensorThresholds", false);
            Properties.Settings.Default.UseInnerSensorThresholds = dict.Get<bool>("useInnerSensorThresholds", false);
            var customSensors = dict.Get<List<object>>("customSensors", null);
            if (customSensors != null)
                ThresholdSettings.SetCustomSensorsJSON(customSensors);
        }

        public struct ThresholdDefinition
        {
            public double UserMin;
            public double UserMax;
            public double RealMin;
            public double RealMax;
            public double MinRange;
        }

        private static ThresholdDefinition FSRThreshold = new() { UserMin = 1, UserMax = 250, RealMin = 0, RealMax = 250, MinRange = 1 };
        private static ThresholdDefinition LoadCellsThreshold = new() { UserMin = 1, UserMax = 200, RealMin = 0, RealMax = 500, MinRange = 1 };


        public static ThresholdDefinition GetThresholdDefinition(bool isFSR)
        {
            return isFSR ? FSRThreshold : LoadCellsThreshold;
        }
    };
}
