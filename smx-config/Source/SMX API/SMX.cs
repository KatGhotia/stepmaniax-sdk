using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using smx_config;

// This is a binding to the native SMX.dll.
namespace SMX
{
    [StructLayout(LayoutKind.Sequential, Pack=1)]
    public struct SMXInfo {
        [MarshalAs(UnmanagedType.I1)]  // work around C# bug: marshals bool as int
        public bool connected;

        // The 32-byte hex serial number of the device, followed by '\0'.
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 33)]
        public byte[] m_Serial;

        public Int16 m_iFirmwareVersion;

        // Padding to make this the same size as native, where MSVC adds padding even though
        // we tell it not to:
        private Byte dummy;
    };

    // Bits for SMXConfig::flags.
    public enum SMXConfigFlags {
        AutoLightingUsePressedAnimations = 1 << 0,
        PlatformFlags_FSR = 1 << 1,
    };

    [Serializable]
    public struct PackedSensorSettings {
        // Load cell thresholds:
        public Byte loadCellLowThreshold;
        public Byte loadCellHighThreshold;

        // FSR thresholds (16-bit):
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public Byte[] fsrLowThreshold;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public Byte[] fsrHighThreshold;

        public UInt16 combinedLowThreshold;
        public UInt16 combinedHighThreshold;

        // This must be left unchanged.
        public UInt16 reserved;
    };

    [StructLayout(LayoutKind.Sequential, Pack=1)]  
    [Serializable]
    public struct SMXConfig {  
        public Byte masterVersion;
        public Byte configVersion;

        // Packed flags (SMXConfigFlags).
        public Byte flags;

        //It would be nice if these variable were documented. I understand that fact the user should never touch it
        //But as a developper it's pretty frustrating to not know what's about. Maybe it can be useful for some reason.
        public UInt16 debounceNodelayMilliseconds;
        public UInt16 debounceDelayMs;
        public UInt16 panelDebounceMicroseconds;
        public Byte autoCalibrationMaxDeviation;
        public Byte badSensorMinimumDelaySeconds;
        public UInt16 autoCalibrationAveragesPerUpdate;
        public UInt16 autoCalibrationSamplesPerAverage;

        //So, here it is, the devil value that all ITG pro players doesn't like. I don't really know what other auto calibration variables means,
        //so i decided to go with the more explicit one to "hack" the auto calibration system, and finally shut it down.
        //Max tare seems to be a value between 0 and 65565 (max ushort), as 65565 is the default value. However, set a non-binary value will trigger the "bad jumpers" error.
        //So I recommand to put only power of 4 values in it. In the Enhanced config too, I do the automatic conversion between "classic" value and POF values, to avoid user panic.
        public UInt16 autoCalibrationMaxTare;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public Byte[] enabledSensors;

        public Byte autoLightsTimeout;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3*9)]
        public Byte[] stepColor;

        // The default color to set the platform LED strip to.
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public Byte[] platformStripColor;

        // Which panels to enable auto-lighting for.  Disabled panels will be unlit.
        // 0x01 = panel 0, 0x02 = panel 1, 0x04 = panel 2, etc.  This only affects
        // the master controller's built-in auto lighting and not lights data send
        // from the SDK.
        public UInt16 autoLightPanelMask;

        public Byte panelRotation;

        // Per-panel sensor settings:
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public PackedSensorSettings[] panelSettings;

        // These are internal tunables and should be left unchanged.
        public Byte preDetailsDelayMilliseconds;

        // Pad this struct to exactly 250 bytes.
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 49)]
        public Byte[] padding;

        // It would be simpler to set flags to [MarshalAs(UnmanagedType.U8)], but
        // that doesn't work.
        public SMXConfigFlags configFlags {
            get {
                return (SMXConfigFlags) flags;
            }

            set {
                flags = (Byte) value;
            }
        }

        // Return true if the platform is using FSRs, or false for load cells.
        public bool isFSR()
        {
            return IsNewGen() && (configFlags & SMXConfigFlags.PlatformFlags_FSR) != 0;
        }

        //Return true if the pad is "new generation". There was huge changes betwee Gen1-3 and Gen4+. This is useful for the tools to know this sometimes.
        public bool IsNewGen()
        {
            return masterVersion >= 4;
        }

        //Return true if the platform have 9 panels (gen 2 and above)
        public bool HasAllPanels()
        {
            return masterVersion != 0xFF && masterVersion >= 2;
        }

        // enabledSensors is a mask of which panels are enabled.  Return this as an array
        // for convenience.
        public bool[] GetEnabledPanels()
        {
            return new bool[] {
                (enabledSensors[0] & 0xF0) != 0,
                (enabledSensors[0] & 0x0F) != 0,
                (enabledSensors[1] & 0xF0) != 0,
                (enabledSensors[1] & 0x0F) != 0,
                (enabledSensors[2] & 0xF0) != 0,
                (enabledSensors[2] & 0x0F) != 0,
                (enabledSensors[3] & 0xF0) != 0,
                (enabledSensors[3] & 0x0F) != 0,
                (enabledSensors[4] & 0xF0) != 0,
            };
        }

        // Set enabledSensors from an array returned from GetEnabledPanels.
        public void SetEnabledPanels(bool[] panels)
        {
            enabledSensors[0] = (byte) ((panels[0] ? 0xF0 : 0) | (panels[1] ? 0x0F : 0));
            enabledSensors[1] = (byte) ((panels[2] ? 0xF0 : 0) | (panels[3] ? 0x0F : 0));
            enabledSensors[2] = (byte) ((panels[4] ? 0xF0 : 0) | (panels[5] ? 0x0F : 0));
            enabledSensors[3] = (byte) ((panels[6] ? 0xF0 : 0) | (panels[7] ? 0x0F : 0));
            enabledSensors[4] = (byte) (panels[8] ? 0xF0 : 0);
        }

        // Return the index of the first enabled panel, or 1 (up) if no panels
        // are enabled.
        public int? GetFirstEnabledPanel()
        {
            bool[] enabledPanels = GetEnabledPanels();
            for (int i = 0; i < 9; ++i)
            {
                if (enabledPanels[i])
        return i;
            }

            // pucgenie: Probably not supported by this App.
            return null;
        }

        // The layout of this structure (and the underlying C struct) matches the firmware configuration
        // data.  This is a bit inconvenient for the panel thresholds which aren't contiguous, so these
        // helpers just convert them to and from arrays.
        // XXX: used?
        public Byte[] GetLowThresholds()
        {
            return new Byte[] {
                panelSettings[0].loadCellLowThreshold,
                panelSettings[1].loadCellLowThreshold,
                panelSettings[2].loadCellLowThreshold,
                panelSettings[3].loadCellLowThreshold,
                panelSettings[4].loadCellLowThreshold,
                panelSettings[5].loadCellLowThreshold,
                panelSettings[6].loadCellLowThreshold,
                panelSettings[7].loadCellLowThreshold,
                panelSettings[8].loadCellLowThreshold,
            };
        }

        public Byte[] GetHighThresholds()
        {
            return new Byte[] {
                panelSettings[0].loadCellHighThreshold,
                panelSettings[1].loadCellHighThreshold,
                panelSettings[2].loadCellHighThreshold,
                panelSettings[3].loadCellHighThreshold,
                panelSettings[4].loadCellHighThreshold,
                panelSettings[5].loadCellHighThreshold,
                panelSettings[6].loadCellHighThreshold,
                panelSettings[7].loadCellHighThreshold,
                panelSettings[8].loadCellHighThreshold,
            };
        }

        public void SetLowThresholds(Byte[] values)
        {
            for (int panel = 0; panel < 9; ++panel)
                panelSettings[panel].loadCellLowThreshold = values[panel];
        }
        
        public void SetHighThresholds(Byte[] values)
        {
            for (int panel = 0; panel < 9; ++panel)
                panelSettings[panel].loadCellHighThreshold = values[panel];
        }

        // Create an empty SMXConfig.
        static public SMXConfig Create()
        {
            SMXConfig result = new()
            {
                enabledSensors = new Byte[5],
                stepColor = new Byte[3 * 9],
                panelSettings = new PackedSensorSettings[9],
                platformStripColor = new Byte[3]
            };
            for (int panel = 0; panel < 9; ++panel)
            {
                result.panelSettings[panel].fsrLowThreshold = new Byte[4];
                result.panelSettings[panel].fsrHighThreshold = new Byte[4];
            }
            return result;
        }

        // autoLightPanelMask controls which lights the master controller will light.  Only the
        // first 9 bits (0x1ff) are meaningful for our 9 panels.  As a special case, we use 0xFFFF
        // to indicate that "light all panels" was checked.  The controller doesn't care about this
        // since it only looks at the first 9 bits.
        public bool getLightAllPanelsMode() { return autoLightPanelMask == 0xFFFF; }

        public void setLightAllPanelsMode(bool enable)
        {
            if (enable)
                autoLightPanelMask = 0xFFFF;
            else
                refreshAutoLightPanelMask(false);
        }

        // If we're not in light all panels mode, set autoLightPanelMask to the currently
        // enabled panels.  This should be called if enabledSensors is changed.
        public void refreshAutoLightPanelMask(bool onlyIfEnabled=true)
        {
            if (onlyIfEnabled && getLightAllPanelsMode())
        return;

            // Set autoLightPanelMask to just the enabled panels.
            autoLightPanelMask = 0;
            var enabledPanels = GetEnabledPanels();
            for (int i = 0; i < 9; ++i)
                if (enabledPanels[i])
                    autoLightPanelMask |= (UInt16) (1 << i);
        }
    };  

    public struct SMXSensorTestModeData
    {
        // If false, sensorLevel[n][*] is zero because we didn't receive a response from that panel.
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType=UnmanagedType.I1, SizeConst = 9)]
        public bool[] bHaveDataFromPanel;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9*4)]
        public Int16[] sensorLevel;

        [MarshalAs(UnmanagedType.ByValArray, ArraySubType=UnmanagedType.I1, SizeConst = 9*4)]
        public bool[] bBadSensorInput;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public int[] iDIPSwitchPerPanel;

        [MarshalAs(UnmanagedType.ByValArray, ArraySubType=UnmanagedType.I1, SizeConst = 9*4)]
        public bool[] iWrongSensorJumper;

        public override bool Equals(object obj)
        {
            SMXSensorTestModeData other = (SMXSensorTestModeData) obj;
            return
                Helpers.SequenceEqual(bHaveDataFromPanel, other.bHaveDataFromPanel) &&
                Helpers.SequenceEqual(sensorLevel, other.sensorLevel) &&
                Helpers.SequenceEqual(bBadSensorInput, other.bBadSensorInput) &&
                Helpers.SequenceEqual(iDIPSwitchPerPanel, other.iDIPSwitchPerPanel) &&
                Helpers.SequenceEqual(iWrongSensorJumper, other.iWrongSensorJumper);
        }

        // Dummy override to silence a bad warning.  We don't use these in containers that need
        // a hash code implementation.
        public override int GetHashCode() { return base.GetHashCode(); }

        public bool HasSensorValid(int panel, int sensor, bool checkData = true)
        {
            if (checkData && !bHaveDataFromPanel[panel])
        return false;
            if (bBadSensorInput[panel * 4 + sensor])
        return false;

            return true;
        }

        public bool AnySensorsOnPanelNotResponding(int panel)
        {
            if (!bHaveDataFromPanel[panel])
        return false;
            for (int sensor = 0; sensor < 4; ++sensor)
                if (bBadSensorInput[panel*4 + sensor])
        return true;

            return false;
        }

        public bool AnyBadJumpersOnPanel(int panel)
        {
            if (!bHaveDataFromPanel[panel])
        return false;
            for (int sensor = 0; sensor < 4; ++sensor)
                if (iWrongSensorJumper[panel*4+sensor])
        return true;

            return false;
        }
    };

    public static class SMX
    {
        [System.Flags]
        enum LoadLibraryFlags : uint
        {    
              None = 0, 
              DONT_RESOLVE_DLL_REFERENCES = 0x00000001,
              LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010,
              LOAD_LIBRARY_AS_DATAFILE = 0x00000002,
              LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,
              LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020,
              LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x00000200,
              LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000,
              LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100,
              LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800,
              LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400,
              LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008
        }

        [DllImport("kernel32", SetLastError=true)]
        static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, LoadLibraryFlags dwFlags);
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SMX_Start(
            [MarshalAs(UnmanagedType.FunctionPtr)] InternalUpdateCallback callback,
            IntPtr user);
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SMX_Stop();
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SMX_GetInfo(int pad, out SMXInfo info);
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern UInt16 SMX_GetInputState(int pad);
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        [return:MarshalAs(UnmanagedType.I1)]
        private static extern bool SMX_GetConfig(int pad, out SMXConfig config);
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SMX_SetConfig(int pad, ref SMXConfig config);
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SMX_FactoryReset(int pad);
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SMX_ForceRecalibration(int pad);
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SMX_SetTestMode(int pad, int mode);
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SMX_GetTestData(int pad, out SMXSensorTestModeData data);
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SMX_SetPanelTestMode(int mode);
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SMX_SetLights2(byte[] buf, int lightDataSize);
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SMX_SetPlatformLights(byte[] buf, int lightDataSize);
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SMX_ReenableAutoLights();
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr SMX_Version();

        public enum LightsType
        {
            LightsType_Released, // animation while panels are released
            LightsType_Pressed, // animation while panel is pressed
        };

        public static string Version()
        {
            if (!DLLAvailable())
        return "(Simulation)";

            // I can't find any way to marshal a simple null-terminated string.  Marshalling
            // UnmanagedType.LPStr tries to deallocate the string, which crashes since it's
            // a static string.
            unsafe {
                sbyte *p = (sbyte *) SMX_Version();
                int length = 0;
                while (p[length] != 0)
                    ++length;
                return new string(p, 0, length);
            }
        }

#if DEBUG
        private static int callCount = 0;
#endif
        private static byte smxDLLstatus = 0;
        // Check if the native DLL is available.  This is mostly to avoid exceptions in the designer.
        // This returns false if the DLL doesn't load.
        // pucgenie: The latest released smx-config App from StepRevolution in 2020 called this method 187 times on startup.
        public static bool DLLAvailable()
        {
#if DEBUG
            Console.Out.WriteLine($"DLLAvailable#{callCount+=1:X4} at {DateTime.UtcNow.Ticks:X16}");
#endif
            if (smxDLLstatus == 0) {
                // App needs to be restarted in any case if SMX.dll wasn't available beforehand.
                smxDLLstatus = (byte)(LoadLibrary("SMX.dll") != IntPtr.Zero
                    ? 0x1
                    : 0x2
                );
            }
            return (smxDLLstatus & 0x1) != 0;
        }

        // Check if the native DLL exists.  This will return false if SMX.dll is missing entirely,
        // but not if it fails to load for another reason like runtime dependencies.  This just lets
        // us print a more specific error message.
        public static bool DLLExists()
        {
            // will be called on App startup (once)
            return LoadLibraryEx("SMX.dll", (IntPtr)0, LoadLibraryFlags.LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE) != IntPtr.Zero;
        }
        
        public delegate void UpdateCallback(int PadNumber, SMXUpdateCallbackReason reason);

        // The C API allows a user pointer, but we don't use that here.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void InternalUpdateCallback(int PadNumber, int reason, IntPtr user);
        private static InternalUpdateCallback CurrentUpdateCallback;

        public static void Start(UpdateCallback callback)
        {
            if (!DLLAvailable())
        return;

            // Sanity check SMXConfig, which should be 250 bytes.  If this is incorrect,
            // check the padding array.
            {
                SMXConfig config = new();
                int bytes = Marshal.SizeOf(config);
                if (bytes != 250)
        throw new Exception($"SMXConfig is {bytes} bytes, but should be 250 bytes");
            }

            // Make a wrapper to convert from the native enum to SMXUpdateCallbackReason.
            InternalUpdateCallback NewCallback = delegate(int PadNumber, int reason, IntPtr user) {
                SMXUpdateCallbackReason ReasonEnum = (SMXUpdateCallbackReason) Enum.ToObject(typeof(SMXUpdateCallbackReason), reason);
                callback(PadNumber, ReasonEnum);
            };
            if (callback == null)
                NewCallback = null;

            SMX_Start(NewCallback, IntPtr.Zero);

            // Keep a reference to the delegate, so it isn't garbage collected.  Do this last.  Once
            // we do this the old callback may be collected, so we want to be sure that native code
            // has already updated the callback.
            CurrentUpdateCallback = NewCallback;
            
            Console.WriteLine($"Struct sizes (C#): {Marshal.SizeOf(typeof(SMXConfig))} {Marshal.SizeOf(typeof(SMXInfo))} {Marshal.SizeOf(typeof(SMXSensorTestModeData))}");
        }

        public static void Stop()
        {
            if (!DLLAvailable())
        return;
            SMX_Stop();
        }

        public enum SMXUpdateCallbackReason {
            Updated,
            FactoryResetCommandComplete
        };

        public static void GetInfo(int pad, out SMXInfo info)
        {
            if (!DLLAvailable()) {
                info = new SMXInfo();
        return;
            }
            SMX_GetInfo(pad, out info);
        }

        public static UInt16 GetInputState(int pad)
        {
            if (!DLLAvailable())
        return 0;

            return SMX_GetInputState(pad);
        }
        
        public static bool GetConfig(int pad, out SMXConfig config)
        {
            if (!DLLAvailable()) {
                config = SMXConfig.Create();
        return false;
            }

            return SMX_GetConfig(pad, out config);
        }
                
        public static void SetConfig(int pad, SMXConfig config)
        {
            if (!DLLAvailable())
        return;

            // Always bump the configVersion to the version we support on write.
            config.configVersion = 2;

            SMX_SetConfig(pad, ref config);
        }

        [AttributeUsage(AttributeTargets.Field)]
        public sealed class DisplayStringAttribute : Attribute
        {
            private readonly string value;
            public string Value
            {
                get { return value; }
            }

            public string ResourceKey { get; set; }

            public DisplayStringAttribute(string v)
            {
                this.value = v;
            }

            public DisplayStringAttribute()
            {
            }
        }

        public enum SensorTestMode {
                [DisplayString("Off")] Off = 0,
                [DisplayString("Raw values")] UncalibratedValues = '0',
                [DisplayString("Calibrated values")] CalibratedValues = '1',
                [DisplayString("Noise")] Noise = '2',
                [DisplayString("Tare")] Tare = '3',
        };

        //Avoid multiple call for the same test mode (make the call of some multiples controls easier)
        private static bool[] m_sensorHasBeenSetOnce = new bool[2] { false, false };
        private static SensorTestMode[] m_currentMode = new SensorTestMode[2] { SensorTestMode.Off, SensorTestMode.Off };
        private static Dictionary<string, SensorTestMode> m_modeBySource = new();

        //Cas 1
        //Off  Test1
        //Test1 Test1
        //Test1 Off

        //Cas 2
        //Off Test1
        //Off Off
        //Test1 Off

        //Cas 3
        //Off Test1
        //Off Test2

        public static void SetSensorTestMode(int pad, SensorTestMode mode, string source = null)
        {
            if (!DLLAvailable())
        return;

            if (!string.IsNullOrEmpty(source))
            {
                m_modeBySource[source] = mode;
                foreach (var kvp in m_modeBySource)
                {
                    if (kvp.Key == source)
                continue;

                    //Tried to disable test mode but another source set it to something else
                    if (mode == SensorTestMode.Off && kvp.Value != SensorTestMode.Off)
        return;
                }
            }

            if (m_sensorHasBeenSetOnce[pad] && mode == m_currentMode[pad])
        return;

            SMX_SetTestMode(pad, (int) mode);
            m_currentMode[pad] = mode;
            m_sensorHasBeenSetOnce[pad] = true;
        }

        public static bool GetTestData(int pad, out SMXSensorTestModeData data)
        {
            if (!DLLAvailable()) {
                data = new SMXSensorTestModeData();
        return false;
            }

            return SMX_GetTestData(pad, out data);
        }

        public enum PanelTestMode {
            Off = '0',
            PressureTest = '1',
        };

        public static void SetPanelTestMode(PanelTestMode mode)
        {
            if (!DLLAvailable())
        return;
            SMX_SetPanelTestMode((int) mode);
        }

        public static void FactoryReset(int pad)
        {
            if (!DLLAvailable())
        return;
            SMX_FactoryReset(pad);
        }

        public static void ForceRecalibration(int pad)
        {
            if (!DLLAvailable())
        return;
            SMX_ForceRecalibration(pad);
        }

        public static void SetLights2(byte[] buf)
        {
            if (!DLLAvailable())
        return;

            SMX_SetLights2(buf, buf.Length);
        }

        public static void SMX_SetPlatformLights(byte[] buf)
        {
            if (!DLLAvailable())
        return;

            SMX_SetPlatformLights(buf, buf.Length);
        }

        public static void ReenableAutoLights()
        {
            if (!DLLAvailable())
        return;

            SMX_ReenableAutoLights();
        }

        // SMXPanelAnimation
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        [return:MarshalAs(UnmanagedType.I1)]
        private static extern bool SMX_LightsAnimation_Load(byte[] buf, int size, int pad, int type, out IntPtr error);
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void SMX_LightsAnimation_SetAuto(bool enable);

        public static bool LightsAnimation_Load(byte[] buf, int pad, LightsType type, out string error)
        {
            if (!DLLAvailable())
            {
                error = "SMX.DLL not available";
        return false;
            }

            error = "";
            var result = SMX_LightsAnimation_Load(buf, buf.Length, pad, (int)type, out IntPtr error_pointer);
            if (!result)
            {
                // SMX_LightsAnimation_Load takes a char **error, which is set to the error
                // string.
                error = Marshal.PtrToStringAnsi(error_pointer);
            }

            return result;
        }

        public static void LightsAnimation_SetAuto(bool enable)
        {
            if (!DLLAvailable())
        return;
            SMX_LightsAnimation_SetAuto(enable);
        }
        
        // SMXPanelAnimationUpload
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void SMX_LightsUpload_BeginUpload(int pad,
            [MarshalAs(UnmanagedType.FunctionPtr)] InternalLightsUploadCallback callback,
            IntPtr user);

        public delegate void LightsUploadCallback(int progress);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void InternalLightsUploadCallback(int reason, IntPtr user);
        public static void LightsUpload_BeginUpload(int pad, LightsUploadCallback callback)
        {
            if (!DLLAvailable())
        return;

            GCHandle handle = new();
            InternalLightsUploadCallback wrapper = delegate(int progress, IntPtr user)
            {
                try {
                    callback(progress);
                } finally {
                    // When progress = 100, this is the final call and we can release this
                    // object to GC.
                    if (progress == 100)
                        handle.Free();
                }
            };

            // Pin the callback until we get the last call.
            handle = GCHandle.Alloc(wrapper);

            SMX_LightsUpload_BeginUpload(pad, wrapper, IntPtr.Zero);
        }
    }
}
