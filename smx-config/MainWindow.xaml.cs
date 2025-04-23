using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using System.Text;
using System.IO.IsolatedStorage;

namespace smx_config
{
    public class SensitivityTab: Control
    {
        OnConfigChange onConfigChange;

        private TextBlock ThresholdWarningText {
            get { return GetTemplateChild("ThresholdWarningText") as TextBlock; }
        }

        private DockPanel ThresholdSliderContainer {
            get { return GetTemplateChild("ThresholdSliderContainer") as DockPanel; }
        }
        private TextBlock LegacyHelp {
            get { return GetTemplateChild("LegacyHelp") as TextBlock; }
        }
        private TextBlock NonLegacyHelp {
            get { return GetTemplateChild("NonLegacyHelp") as TextBlock; }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Recreate the threshold sliders if either of these settings change.
            var AdvancedModeEnabledCheckbox = GetTemplateChild("AdvancedModeEnabledCheckbox") as CheckBox;
            AdvancedModeEnabledCheckbox.Checked += delegate(object sender, RoutedEventArgs e) { CreateThresholdSliders(); };
            AdvancedModeEnabledCheckbox.Unchecked += delegate(object sender, RoutedEventArgs e) { CreateThresholdSliders(); };

            var LegacyViewCheckbox = GetTemplateChild("LegacyViewCheckbox") as CheckBox;
            LegacyViewCheckbox.Checked += delegate(object sender, RoutedEventArgs e) { CreateThresholdSliders(); };
            LegacyViewCheckbox.Unchecked += delegate(object sender, RoutedEventArgs e) { CreateThresholdSliders(); };

            CreateThresholdSliders();

            onConfigChange = new OnConfigChange(this, delegate (LoadFromConfigDelegateArgs args)
            {
                LoadUIFromConfig(args);
            });
        }

        private bool IsLegacyViewEnabled()
        {
            var LegacyViewCheckbox = GetTemplateChild("LegacyViewCheckbox") as CheckBox;
            return LegacyViewCheckbox.IsChecked == true;
        }

        private void LoadUIFromConfig(LoadFromConfigDelegateArgs args)
        {
            SMX.SMXConfig firstConfig = ActivePad.GetFirstActivePadConfig();

            // If a device has connected or disconnected, refresh the displayed threshold
            // sliders.  Don't do this otherwise, or we'll do this when the sliders are
            // dragged.
            if(args.ConnectionsChanged)
                CreateThresholdSliders();

            bool ShowThresholdWarningText = false;
            foreach(Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                SMX.SMXConfig config = activePad.Item2;
                for(int panelIdx = 0; panelIdx < 9; ++panelIdx)
                {
                    for(int sensor = 0; sensor < 4; ++sensor)
                    {
                        if(config.ShowThresholdWarning(panelIdx, sensor))
                            ShowThresholdWarningText = true;
                    }
                }
            }
            ThresholdWarningText.Visibility = ShowThresholdWarningText ? Visibility.Visible : Visibility.Hidden;
            LegacyHelp.Visibility = IsLegacyViewEnabled() ? Visibility.Visible : Visibility.Collapsed;
            NonLegacyHelp.Visibility = IsLegacyViewEnabled() ? Visibility.Collapsed : Visibility.Visible;
        }

        bool IsThresholdSliderShown(string type)
        {
            bool AdvancedModeEnabled = Properties.Settings.Default.AdvancedMode;
            SMX.SMXConfig config = ActivePad.GetFirstActivePadConfig();
            bool[] enabledPanels = config.GetEnabledPanels();

            // Check the list of sensors this slider controls.  If the list is empty, don't show it.
            // For example, if the user adds all four sensors on the up panel to custom-sensors, the
            // up button has nothing left to control, so we'll hide it.
            //
            // Don't do this for custom, inner-sensors or outer-sensors.  Those are always shown in
            // advanced mode.
            List<ThresholdSettings.PanelAndSensor> panelAndSensors = ThresholdSettings.GetControlledSensorsForSliderType(type, AdvancedModeEnabled, false);
            if(type == "custom-sensors" || type == "inner-sensors" || type == "outer-sensors")
            {
                if(!AdvancedModeEnabled || !config.fsr())
                    return false;
            }
            else
            {
                if(panelAndSensors.Count == 0)
                    return false;
            }

            // Hide thresholds that only affect panels that are disabled, so we don't show
            // corner panel sliders in advanced mode if the corner panels are disabled.  We
            // don't handle this in GetControlledSensorsForSliderType, since we do want cardinal
            // and corner to write thresholds to disabled panels, so they're in sync if they're
            // turned back on.
            switch(type)
            {
            case "up-left": return enabledPanels[0];
            case "up": return enabledPanels[1];
            case "up-right": return enabledPanels[2];
            case "left": return enabledPanels[3];
            case "center": return enabledPanels[4];
            case "right": return enabledPanels[5];
            case "down-left": return enabledPanels[6];
            case "down": return enabledPanels[7];
            case "down-right": return enabledPanels[8];

            // Show cardinal and corner if at least one panel they affect is enabled.
            case "cardinal": return enabledPanels[3] || enabledPanels[5] || enabledPanels[8];
            case "corner": return enabledPanels[0] || enabledPanels[2] || enabledPanels[6] || enabledPanels[8];
            default: return true;
            }
        }

        ThresholdSlider CreateThresholdSlider(string type)
        {
            ThresholdSlider slider = new ThresholdSlider();
            slider.EnableLegacyView = IsLegacyViewEnabled();
            slider.Type = type;
            return slider;
        }

        void CreateThresholdSliders()
        {
            // Remove and recreate threshold sliders.
            ThresholdSliderContainer.Children.Clear();
            foreach(string sliderName in ThresholdSettings.thresholdSliderNames)
            {
                if(!IsThresholdSliderShown(sliderName))
                    continue;

                ThresholdSlider slider = CreateThresholdSlider(sliderName);
                DockPanel.SetDock(slider, Dock.Top);
                slider.Margin = new Thickness(0, 8, 0, 0);
                ThresholdSliderContainer.Children.Add(slider);
            }

            ThresholdSettings.SyncSliderThresholds();
        }

    }

    public partial class MainWindow: Window
    {
        //To be changed on each release
        private const int TOOL_REVISION = 1;
        
        OnConfigChange onConfigChange;
        ShowAutoLightsColor showAutoLightsColor = new();

        public MainWindow()
        {
            InitializeComponent();

             onConfigChange = new OnConfigChange(this, delegate (LoadFromConfigDelegateArgs args)
            {
                LoadUIFromConfig(args);
            });

            // If we're controlling GIF animations and the firmware doesn't support
            // doing animations internally, confirm exiting, since you can minimize
            // to tray to keep playing animations.  If we're not controlling animations,
            // or the firmware supports doing them automatically, don't bug the user
            // with a prompt.
            Closing += delegate (object sender, System.ComponentModel.CancelEventArgs e)
            {
                LoadFromConfigDelegateArgs args = CurrentSMXDevice.singleton.GetState();

                // Don't use ActivePads here.  Even if P1 is selected for configuration,
                // we can still be controlling animations for P2, so check both connected
                // pads.
                var shouldConfirmExit = false;
                for (int pad = 0; pad < 2; ++pad)
                {
                    //Ensure that the tool set the sensor test to off before closing
                    SMX.SMX.SetSensorTestMode(pad, SMX.SMX.SensorTestMode.Off);

                    if (!SMX.SMX.GetConfig(pad, out SMX.SMXConfig config))
                        continue;

                    // If the firmware is version 4 or higher, it supports animations directly.
                    // The user can upload GIF animations and doesn't need to leave us running
                    // for them to work.  You can still use this tool to drive animations, but
                    // don't confirm exiting.
                    if (config.IsNewGen())
                continue;

                    // If AutoLightingUsePressedAnimations isn't set, the panel is using step
                    // coloring instead of pressed animations.  All firmwares support this.
                    // Don't confirm exiting for this mode.
                    if ((config.configFlags & SMX.SMXConfigFlags.AutoLightingUsePressedAnimations) == 0)
                continue;

                    shouldConfirmExit = true;
                }

                if (!shouldConfirmExit)
        return;

                MessageBoxResult result = MessageBox.Show(
@"Close StepManiaX configuration?

GIF animations will keep playing if the application is minimized.",
                    "StepManiaX", System.Windows.MessageBoxButton.YesNo);
                if (result == MessageBoxResult.No)
                    e.Cancel = true;
            };

            Main.SelectedItem = MainTab;
        }

        bool IsThresholdSliderShown(string type)
        {
            SMX.SMXConfig config = ActivePad.GetFirstActivePadConfig();
            bool[] enabledPanels = config.GetEnabledPanels();

            // Check the list of sensors this slider controls.  If the list is empty, don't show it.
            // For example, if the user adds all four sensors on the up panel to custom-sensors, the
            // up button has nothing left to control, so we'll hide it.
            //
            // Don't do this for custom, inner-sensors or outer-sensors.  Those are always shown in
            // advanced mode.
            var panelAndSensors = ThresholdSettings.GetControlledSensorsForSliderType(type, config.HasAllPanels(), false);
            if (type == "custom-sensors" || type == "inner-sensors" || type == "outer-sensors")
            {
                if (!config.HasAllPanels() || !config.isFSR())
        return false;
            }
            else if (panelAndSensors.Count == 0)
        return false;

            // Hide thresholds that only affect panels that are disabled, so we don't show
            // corner panel sliders in advanced mode if the corner panels are disabled.  We
            // don't handle this in GetControlledSensorsForSliderType, since we do want cardinal
            // and corner to write thresholds to disabled panels, so they're in sync if they're
            // turned back on.
            // TODO: pucgenie: Create enum and use ordinal numbers as indices...
            return type switch
            {
                "up-left" => enabledPanels[0],
                "up" => enabledPanels[1],
                "up-right" => enabledPanels[2],
                "left" => enabledPanels[3],
                "center" => enabledPanels[4],
                "right" => enabledPanels[5],
                "down-left" => enabledPanels[6],
                "down" => enabledPanels[7],
                "down-right" => enabledPanels[8],
                // Show cardinal and corner if at least one panel they affect is enabled.
                "cardinal" => enabledPanels[3] || enabledPanels[5] || enabledPanels[8],
                "corner" => enabledPanels[0] || enabledPanels[2] || enabledPanels[6] || enabledPanels[8],
                _ => true,
            };
        }

        void CreateThresholdSliders()
        {
            // Remove and recreate threshold sliders.
            ThresholdSliderContainer.Children.Clear();
            foreach (string sliderName in ThresholdSettings.thresholdSliderNames)
            {
                if (!IsThresholdSliderShown(sliderName))
            continue;

                ThresholdSlider slider = new()
                {
                    Type = sliderName
                };
                DockPanel.SetDock(slider, Dock.Top);
                slider.Margin = new Thickness(0, 8, 0, 0);
                ThresholdSliderContainer.Children.Add(slider);
            }

            ThresholdSettings.SyncSliderThresholds();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Add our WndProc hook.
            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            source.AddHook(new HwndSourceHook(WndProcHook));

            Version1.Content = Version2.Content = $"SMXConfig version {SMX.SMX.Version()}";
            
            ToolRev1.Content = ToolRev2.Content = $"Tool Revision v{TOOL_REVISION}";

            AutoLightsColor.StartedDragging += delegate () { showAutoLightsColor.Start(); };
            AutoLightsColor.StoppedDragging += delegate () { showAutoLightsColor.Stop(); };
            // pucgenie: Why 2 times?
            //AutoLightsColor.StoppedDragging += delegate () { showAutoLightsColor.Stop(); };

            // This doesn't happen at the same time AutoLightsColor is used, since they're on different tabs.
            Diagnostics.SetShowAllLights += delegate (bool on)
            {
                if (on)
                    showAutoLightsColor.Start();
                else
                    showAutoLightsColor.Stop();
            };

            SetAllPanelsToCurrentColor.Click += delegate (object sender, RoutedEventArgs e)
            {
                // Get the color of the selected color button, and apply it to all other buttons.
                Color color = selectedButton.getColor();

                ColorButton[] colorButtons = getColorPickerButtons();
                foreach (ColorButton button in colorButtons)
                {
                    // Only apply this to panel colors, not the floor color.
                    if ((button as PanelColorButton) == null)
                continue;

                    button.setColor(color);
                }

                CurrentSMXDevice.singleton.FireConfigurationChanged(null);
            };

            // Listen to clicks on the panel color buttons.
            ColorButton[] buttons = getColorPickerButtons();
            foreach(ColorButton button in buttons)
            {
                button.Click += delegate (object sender, RoutedEventArgs e)
                {
                    ColorButton clickedButton = sender as ColorButton;
                    selectedButton = clickedButton;

                    RefreshSelectedColorPicker();
                };
            }
        }

        // We have two main modes: color mode and GIF animation mode.  These behave differently
        // on gen1-3 pads and gen4 pads.
        //
        // On gen4 pads, this determines whether we show animations on press, or show a solid color.
        // This makes it easy to pick a solid color if that's all you want to change, instead of having
        // to make a GIF with the color.  We always show animations on release in both modes.
        //
        // On gen1-3 pads, panels are black in color mode, so they behave the same as they did originally.
        // Animations are only shown in GIF animation mode.  These animations are enabled with
        // LightsAnimation_SetAuto.
        //
        // The gen1-3 firmware ignores the AutoLightingUsePressedAnimations flag, but we still use it to
        // store which mode the user has selected.
        private void PressedColorModeButton(object sender, RoutedEventArgs e)
        {
            // The user pressed either the "panel colors" or "GIF animations" button.
            bool pressedPanelColors = sender == PanelColorsButton;

            foreach(Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                SMX.SMXConfig config = activePad.Item2;

                // If we're in panel colors mode, clear the AutoLightingUsePressedAnimations flag.
                // Otherwise, set it.
                if (pressedPanelColors)
                    config.configFlags &= ~SMX.SMXConfigFlags.AutoLightingUsePressedAnimations;
                else
                    config.configFlags |= SMX.SMXConfigFlags.AutoLightingUsePressedAnimations;
                SMX.SMX.SetConfig(activePad.Item1, config);
            }

            CurrentSMXDevice.singleton.FireConfigurationChanged(null);
        }

        private void LoadUIFromConfig(LoadFromConfigDelegateArgs args)
        {
            SMX.SMXConfig firstConfig = ActivePad.GetFirstActivePadConfig();

            // Refresh whether LightsAnimation_SetAuto should be enabled.
            bool usePressedAnimationsEnabled = (firstConfig.configFlags & SMX.SMXConfigFlags.AutoLightingUsePressedAnimations) != 0;
            SMX.SMX.LightsAnimation_SetAuto(usePressedAnimationsEnabled);

            bool EitherControllerConnected = args.controller[0].info.connected || args.controller[1].info.connected;
            Main.Visibility = EitherControllerConnected ? Visibility.Visible : Visibility.Hidden;
            Searching.Visibility = EitherControllerConnected ? Visibility.Hidden : Visibility.Visible;
            ConnectedPads.Visibility = EitherControllerConnected ? Visibility.Visible : Visibility.Hidden;
            PanelColorP1.Visibility = args.controller[0].info.connected ? Visibility.Visible : Visibility.Collapsed;
            PanelColorP2.Visibility = args.controller[1].info.connected ? Visibility.Visible : Visibility.Collapsed;
            // TODO: pucgenie: Take into account minVersion of firmware for selectively hiding unavailable options
            EnableCenterTopSensorCheckbox.Visibility = P1_Floor.Visibility = P2_Floor.Visibility =
                args.firmwareVersion().Item2 >= 5 ? Visibility.Visible : Visibility.Collapsed;

            DebounceNodelayBox.Text = firstConfig.debounceNodelayMilliseconds.ToString();
            DebounceDelayBox.Text = firstConfig.debounceDelayMs.ToString();
            PanelDebounceBox.Text = firstConfig.panelDebounceMicroseconds.ToString();
            BadSensorBox.Text = firstConfig.badSensorMinimumDelaySeconds.ToString();
            AutoCalibMaxDeviationBox.Text = firstConfig.autoCalibrationMaxDeviation.ToString();
            AutoCalibAverageBox.Text = firstConfig.autoCalibrationAveragesPerUpdate.ToString();
            AutoCalibSamplesBox.Text = firstConfig.autoCalibrationSamplesPerAverage.ToString();
            AutoCalibMaxTare.Text = firstConfig.autoCalibrationMaxTare.ToString();
            AutoCalibButton.Content = firstConfig.autoCalibrationMaxTare == 16 ? "Enable Auto Calibration" : "Disable Auto Calibration";

            // Show the color slider or GIF UI depending on which one is set in flags.
            // If both pads are turned on, just use the first one.
            foreach (Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                SMX.SMXConfig config = activePad.Item2;

                // If SMXConfigFlags_AutoLightingUsePressedAnimations is set, show the GIF UI.
                // If it's not set, show the color slider UI.
                SMX.SMXConfigFlags flags = config.configFlags;
                bool usePressedAnimations = (flags & SMX.SMXConfigFlags.AutoLightingUsePressedAnimations) != 0;
                ColorPickerGroup.Visibility = usePressedAnimations ? Visibility.Collapsed : Visibility.Visible;
                GIFGroup.Visibility = usePressedAnimations ? Visibility.Visible : Visibility.Collapsed;

                // Tell the color mode buttons which one is selected, to set the button highlight.
                PanelColorsButton.Selected = !usePressedAnimations;
                GIFAnimationsButton.Selected = usePressedAnimations;

                break;
            }

            RefreshConnectedPadList(args);
            RefreshUploadPadText(args);

            // Show the threshold warning explanation if any panels are showing the threshold warning icon.
            bool ShowThresholdWarningText = false;
            foreach(Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                SMX.SMXConfig config = activePad.Item2;
                for(int panelIdx = 0; panelIdx < 9; ++panelIdx)
                {
                    for(int sensor = 0; sensor < 4; ++sensor)
                    {
                        if(config.ShowThresholdWarning(panelIdx, sensor))
                            ShowThresholdWarningText = true;
                    }
                }
            }
            //Useless for pro players
            //ThresholdWarningText.Visibility = ShowThresholdWarningText ? Visibility.Visible : Visibility.Hidden;
            // If a device has connected or disconnected, refresh the displayed threshold
            // sliders.  Don't do this otherwise, or we'll do this when the sliders are
            // dragged.
            if (args.ConnectionsChanged)
                CreateThresholdSliders();

            // If a second controller has connected and we're on Both, see if we need to prompt
            // to sync configs.  We only actually need to do this if a controller just connected.
            if (args.ConfigurationChanged)
                CheckConfiguringBothPads(args);
        }

        ColorButton selectedButton;

        // Return all color picker buttons.
        ColorButton[] getColorPickerButtons()
        {
            return new ColorButton[] {
                P1_0, P1_1, P1_2,
                P1_3, P1_4, P1_5,
                P1_6, P1_7, P1_8,
                P1_Floor,

                P2_0, P2_1, P2_2,
                P2_3, P2_4, P2_5,
                P2_6, P2_7, P2_8,
                P2_Floor,
            };
        }

        // Update the selected color picker based on the value of selectedButton.
        private void RefreshSelectedColorPicker()
        {
            LoadFromConfigDelegateArgs args = CurrentSMXDevice.singleton.GetState();

            ColorButton[] buttons = getColorPickerButtons();

            // If our selected button isn't enabled (or no button is selected), try to select a
            // different one.
            if (selectedButton == null || !selectedButton.isEnabled(args))
            {
                foreach (ColorButton button in buttons)
                {
                    if (button.isEnabled(args))
                    {
                        selectedButton = button;
                break;
                    }
                }
            }

            // Tell the buttons which one is selected.
            foreach(ColorButton button in buttons)
                button.IsSelected = button == selectedButton;

            // Tell the color slider which button is selected.
            AutoLightsColor.colorButton = selectedButton;
        }

        // Update which of the "Leave this application running", etc. blocks to display.
        private void RefreshUploadPadText(LoadFromConfigDelegateArgs args)
        {
            foreach(Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                SMX.SMXConfig config = activePad.Item2;

                bool uploadsSupported = config.IsNewGen();
                LeaveRunning.Visibility = uploadsSupported ? Visibility.Collapsed : Visibility.Visible;
                break;
            }
        }

        private void ConnectedPadList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var newSelection = (ConnectedPadList.SelectedValue as SelectedPad?).Value;
            
            if (ActivePad.selectedPad == newSelection)
                return;

            ActivePad.selectedPad = newSelection;

            // Before firing and updating UI, run CheckConfiguringBothPads to see if we should
            // sync the config and/or change the selection again.
            CheckConfiguringBothPads(CurrentSMXDevice.singleton.GetState());

            CurrentSMXDevice.singleton.FireConfigurationChanged(null);
        }

        // If the user selects "Both", or connects a second pad while on "Both", both pads need
        // to have the same configuration, since we're configuring them together.  Check if the
        // configuration is out of sync, and ask the user before syncing them up so we don't
        // clobber P2's configuration if this wasn't intended.
        //
        // If the user cancels, change the pad selection to P1 so we don't clobber P2.
        private void CheckConfiguringBothPads(LoadFromConfigDelegateArgs args)
        {
            // Check if we're actually in "Both" mode with two controllers connected.  If not,
            // we don't have to do anything.
            bool Pad1Connected = args.controller[0].info.connected;
            bool Pad2Connected = args.controller[1].info.connected;
            if (ActivePad.selectedPad != SelectedPad.Both || !Pad1Connected || !Pad2Connected)
                return;

            // If the two pads have the same configuration, there's nothing to do.
            SMX.SMXConfig config1 = args.controller[0].config;
            SMX.SMXConfig config2 = args.controller[1].config;
            if (ConfigurationsSynced(config1, config2))
                return;

            string messageBoxText = @"The two pads have different settings.  Do you want to 
match P2 settings to P1 and configure both pads together?  (This won't affect panel colors.)";
            MessageBoxResult result = MessageBox.Show(messageBoxText, "StepManiaX", MessageBoxButton.YesNo, MessageBoxImage.None);
            if (result == MessageBoxResult.Yes)
            {
                SyncP2FromP1(config1, config2);
                return;
            }
            else
            {
                // Switch to P1.
                ActivePad.selectedPad = SelectedPad.P1;
                RefreshConnectedPadList(CurrentSMXDevice.singleton.GetState());
            }

        }

        // Return true if the two pads have the same configuration, so we can configure them together
        // without clobbering separate configurations.
        bool ConfigurationsSynced(SMX.SMXConfig config1, SMX.SMXConfig config2)
        {
            if (!Enumerable.SequenceEqual(config1.GetLowThresholds(), config2.GetLowThresholds()))
        return false;
            if (!Enumerable.SequenceEqual(config1.GetHighThresholds(), config2.GetHighThresholds()))
        return false;
            if (!Enumerable.SequenceEqual(config1.enabledSensors, config2.enabledSensors))
        return false;
            return true;
        }

        // Copy the P2 pad configuration to P1.
        //
        // This only copies settings that we actually configure, and it doesn't copy pad
        // colors, which is separate from pad selection.
        void SyncP2FromP1(SMX.SMXConfig config1, SMX.SMXConfig config2)
        {
            // Copy P1's configuration to P2.
            Array.Copy(config1.enabledSensors, config2.enabledSensors, config1.enabledSensors.Count());
            config2.SetLowThresholds(config1.GetLowThresholds());
            config2.SetHighThresholds(config1.GetHighThresholds());
            SMX.SMX.SetConfig(1, config2);
            CurrentSMXDevice.singleton.FireConfigurationChanged(null);
        }

        // Refresh which items are visible in the connected pads list, and which item is displayed as selected.
        private void RefreshConnectedPadList(LoadFromConfigDelegateArgs args)
        {
            var PxConnected = (from controller in args.controller select controller.info.connected).ToArray();
            bool TwoControllersConnected = PxConnected.All(ci => ci);

            // Only show the dropdown if two controllers are connected.
            ConnectedPadList.Visibility = TwoControllersConnected ? Visibility.Visible : Visibility.Collapsed;

            // Only show the P1/P2 text if only one controller is connected, since it takes the place
            // of the dropdown. Make sure to set the selectedPad too in case callers use that to determine
            // which pad is being configured.
            if (!TwoControllersConnected && PxConnected[0])
            {
                P1Connected.Visibility = Visibility.Visible;
                ActivePad.selectedPad = SelectedPad.P1;
            } else
            {
                P1Connected.Visibility = Visibility.Collapsed;
            }

            if (!TwoControllersConnected && PxConnected[1])
            {
                P2Connected.Visibility = Visibility.Visible;
                ActivePad.selectedPad = SelectedPad.P2;
            }
            else
            {
                P2Connected.Visibility = Visibility.Collapsed;
            }

            if (!TwoControllersConnected)
        return;

            // Set the current selection.
            ConnectedPadList.SelectedValue = ActivePad.selectedPad;
        }

        private void UpdateAdvancedValues_Click(object sender, RoutedEventArgs e)
        {
            foreach (Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                int pad = activePad.Item1;
                SMX.SMXConfig config = activePad.Item2;

                string error = null;
                config.debounceNodelayMilliseconds = TryParseShortTextData(DebounceNodelayBox, "Debounce Nodelay", ref error);
                config.debounceDelayMs = TryParseShortTextData(DebounceDelayBox, "Debounce Delay", ref error);
                config.panelDebounceMicroseconds = TryParseShortTextData(PanelDebounceBox, "Panel Debounce", ref error);
                config.badSensorMinimumDelaySeconds = TryParseByteTextData(BadSensorBox, "Bad sensor", ref error);
                config.autoCalibrationMaxDeviation = TryParseByteTextData(AutoCalibMaxDeviationBox, "AutoCalib Max Deviation", ref error);
                config.autoCalibrationAveragesPerUpdate = TryParseShortTextData(AutoCalibAverageBox, "AutoCalib Average per Update", ref error);
                config.autoCalibrationSamplesPerAverage = TryParseShortTextData(AutoCalibSamplesBox, "AutoCalib Samples per Average", ref error);
                config.autoCalibrationMaxTare = TryParseShortTextData(AutoCalibMaxTare, "AutoCalib Max Tare", ref error);

                if (!string.IsNullOrEmpty(error))
                {
                    AdvancedValueError.Text = error;
                    AdvancedValueError.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#AA0000"));
                }
                else
                {
                    SMX.SMX.SetConfig(pad, config);
                    AdvancedValueError.Text = $"Correctly updated at {DateTime.Now.ToLongTimeString()}";
                    AdvancedValueError.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#00AA00"));
                }
            }
            CurrentSMXDevice.singleton.FireConfigurationChanged(null);
        }

        private ushort TryParseShortTextData(TextBox box, string title, ref string error)
        {
            //An error already occured
            if (!string.IsNullOrEmpty(error))
                return default;

            error = null;
            if (ushort.TryParse(box.Text, out ushort result))
        return result;
            error = $"Update failed : {title} has a wrong value";
            return default;
        }

        private byte TryParseByteTextData(TextBox box, string title, ref string error)
        {
            //An error already occured
            if (!string.IsNullOrEmpty(error))
                return default;

            error = null;
            if (byte.TryParse(box.Text, out byte result))
                return result;
            error = $"Update failed : {title} has a wrong value";
            return default;
        }

        private void AutoCalibButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                int pad = activePad.Item1;
                SMX.SMXConfig config = activePad.Item2;

                config.autoCalibrationMaxTare = config.autoCalibrationMaxTare == 16 ? ushort.MaxValue : (ushort)16;
                SMX.SMX.SetConfig(pad, config);
            }
            CurrentSMXDevice.singleton.FireConfigurationChanged(null);
        }

        private void FactoryReset_Click(object sender, RoutedEventArgs e)
        {
            foreach(Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                int pad = activePad.Item1;
                SMX.SMX.FactoryReset(pad);
            }
            CurrentSMXDevice.singleton.FireConfigurationChanged(null);
        }

        private void ExportSettings(object sender, RoutedEventArgs e)
        {
            StringBuilder sb = new();
            // Save the current thresholds on the first available pad as a preset.
            foreach(Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                int pad = activePad.Item1;
                SMX.SMXConfig config = activePad.Item2;
                SMXHelpers.ExportSettingsToJSON(config, sb);
                string json = sb.ToString();
                sb.Length = 0;

                Microsoft.Win32.SaveFileDialog dialog = new()
                {
                    FileName = "StepManiaX settings",
                    DefaultExt = ".smxcfg",
                    Filter = "StepManiaX settings (.smxcfg)|*.smxcfg"
                };
                bool result = dialog.ShowDialog().GetValueOrDefault(false);
                if (!result)
        return;

                System.IO.File.WriteAllText(dialog.FileName, json);
                return;
            }
        }

        private void ImportSettings(object sender, RoutedEventArgs evt)
        {
            // Prompt for a file to read.
            Microsoft.Win32.OpenFileDialog dialog = new()
            {
                FileName = "StepManiaX settings",
                DefaultExt = ".smxcfg",
                Filter = "StepManiaX settings (.smxcfg)|*.smxcfg"
            };
            bool result = dialog.ShowDialog().GetValueOrDefault(false);
            if (!result)
        return;

            string json = System.IO.File.ReadAllText(dialog.FileName);

            // Apply settings from the file to all active pads.
            foreach (Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                int pad = activePad.Item1;
                SMX.SMXConfig config = activePad.Item2;

                try {
                    SMXHelpers.ImportSettingsFromJSON(json, ref config);
                } catch (SMXJSON.ParseError e2) {
                    MessageBox.Show(e2.Message, "Error importing configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
            break;
                }
                SMX.SMX.SetConfig(pad, config);
            }

            CurrentSMXDevice.singleton.FireConfigurationChanged(null);
        }

        private void LoadGIF(object sender, RoutedEventArgs e)
        {
            // If the "load idle GIF" button was pressed, load the released animation.
            // Otherwise, load the pressed animation.
            bool pressed = sender == this.LoadPressed;

            // Prompt for a file to read.
            Microsoft.Win32.OpenFileDialog dialog = new()
            {
                FileName = "Select an animated GIF",
                DefaultExt = ".gif",
                Filter = "Animated GIF (.gif)|*.gif"
            };
            bool result = dialog.ShowDialog().GetValueOrDefault(false);
            if (!result)
        return;

            byte[] buf = System.IO.File.ReadAllBytes(dialog.FileName);
            SMX.SMX.LightsType type = pressed ? SMX.SMX.LightsType.LightsType_Pressed : SMX.SMX.LightsType.LightsType_Released;

            foreach (Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                int pad = activePad.Item1;

                // Load the animation.
                if (!SMX.SMX.LightsAnimation_Load(buf, pad, type, out string error))
                {
                    // Any errors here are problems with the GIF, so there's no point trying
                    // to load it for the second pad if the first returns an error.  Just show the
                    // error and stop.
                    MessageBox.Show(error, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                    // Return without saving to settings on error.
                    return;
                }

                // Save the GIF to disk so we can load it quickly later.
                Helpers.SaveAnimationToDisk(pad, type, buf);

                // Refresh after loading a GIF to update the "Leave this application running" text.
                CurrentSMXDevice.singleton.FireConfigurationChanged(null);
            }

            // For firmwares that support it, upload the animation to the pad now.  Otherwise,
            // we'll run the animation directly.
            foreach (Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                int pad = activePad.Item1;

                if (!SMX.SMX.GetConfig(pad, out SMX.SMXConfig config))
                    continue;

                if (config.IsNewGen())
                    UploadLatestGIF();

                break;
            }
        }

        private void UploadLatestGIF()
        {
            // Create a progress window.  Center it on top of the main window.
            ProgressWindow dialog = new();
            dialog.Left = (Left + Width/2) - (dialog.Width/2);
            dialog.Top = (Top + Height/2) - (dialog.Height/2);
            dialog.Title = "Storing animations on pad...";

            int[] CurrentProgress = new int[] { 0, 0 };

            // Upload graphics for all connected pads.  If two pads are connected
            // we can start both of these simultaneously, and they'll be sent in
            // parallel.
            int total = 0;
            foreach (Tuple<int,SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                int pad = activePad.Item1;
                SMX.SMX.LightsUpload_BeginUpload(pad, delegate(int progress) {
                    // This is called from a thread, so dispatch back to the main thread.
                    Dispatcher.Invoke(delegate() {
                        // Store progress, so we can sum both pads.
                        CurrentProgress[pad] = progress;

                        dialog.SetProgress(CurrentProgress[0] + CurrentProgress[1]);
                        if (progress == 100)
                            dialog.Close();
                    });
                });

                // Each pad that we start uploading to is 100 units of progress.
                total += 100;
                dialog.SetTotal(total);
            }

            // Show the progress window as a modal dialog.  This function won't return
            // until we call dialog.Close above.
            dialog.ShowDialog();
        }

        const int WM_SYSCOMMAND = 0x0112;
        const int SC_MINIMIZE = 0xF020;
        private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            App application = (App) Application.Current;

            if (msg == WM_SYSCOMMAND && ((int)wparam & 0xFFF0) == SC_MINIMIZE)
            {
                // Cancel minimize, and call MinimizeToTray instead.
                handled = true;
                application.MinimizeToTray(); 
            }

            return IntPtr.Zero;
        }

        private void MainTab_Selected(object sender, RoutedEventArgs e)
        {
            if (Main.SelectedItem == ColorTab)
            {
                RefreshSelectedColorPicker();
            }

            if (Main.SelectedItem == SensitivityTab)
            {
                // Refresh the threshold sliders, in case the enabled panels were changed
                // on the advanced tab.
                CreateThresholdSliders();

                for (int pad = 0; pad < 2; ++pad)
                    SMX.SMX.SetSensorTestMode(pad, SMX.SMX.SensorTestMode.CalibratedValues, "MainWindow");
            }
            else if (Main.SelectedItem != DiagnosticTab)
            {
                for (int pad = 0; pad < 2; ++pad)
                    SMX.SMX.SetSensorTestMode(pad, SMX.SMX.SensorTestMode.Off, "MainWindow");
            }
        }
    }
} 
