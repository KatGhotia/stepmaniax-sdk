using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;

namespace smx_config
{
    // This implements the threshold slider widget for changing an upper/lower threshold pair.
    public class ThresholdSlider : Control
    {
        public static readonly DependencyProperty TypeProperty = DependencyProperty.Register("Type",
            typeof(string), typeof(ThresholdSlider), new FrameworkPropertyMetadata(""));

        public string Type
        {
            get { return (string)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }

        // If false, this threshold hasn't been enabled by the user.  The slider will be greyed out.  This
        // is different from our own IsEnabled, since setting that to false would also disable EnabledCheckbox,
        // preventing it from being turned back on.
        public static readonly DependencyProperty ThresholdEnabledProperty = DependencyProperty.Register("ThresholdEnabled",
            typeof(bool), typeof(ThresholdSlider), new FrameworkPropertyMetadata(true));

        public bool ThresholdEnabled
        {
            get { return (bool)GetValue(ThresholdEnabledProperty); }
            set { SetValue(ThresholdEnabledProperty, value); }
        }

        // This is set to true if the slider is enabled and the low/high values are displayed.  We set this to
        // false when the slider is disabled (or has no selected sensors, for custom-sliders).
        public static readonly DependencyProperty SliderActiveProperty = DependencyProperty.Register("SliderActive",
            typeof(bool), typeof(ThresholdSlider), new FrameworkPropertyMetadata(true));

        public bool SliderActive
        {
            get { return (bool)GetValue(SliderActiveProperty); }
            set { SetValue(SliderActiveProperty, value); }
        }

        public static readonly DependencyProperty AdvancedModeEnabledProperty = DependencyProperty.Register("AdvancedModeEnabled",
            typeof(bool), typeof(ThresholdSlider), new FrameworkPropertyMetadata(false));

        public bool AdvancedModeEnabled
        {
            get { return (bool)GetValue(AdvancedModeEnabledProperty); }
            set { SetValue(AdvancedModeEnabledProperty, value); }
        }

        DoubleSlider slider;
        Label LowerLabel, UpperLabel;
        //Image ThresholdWarning;
        PlatformSensorDisplay SensorDisplay;
        LevelBar SensorBar;

        OnConfigChange onConfigChange;
        OnConfigChange onConfigInputChange;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            slider = GetTemplateChild("Slider") as DoubleSlider;
            LowerLabel = GetTemplateChild("LowerValue") as Label;
            UpperLabel = GetTemplateChild("UpperValue") as Label;
            //ThresholdWarning = GetTemplateChild("ThresholdWarning") as Image;
            SensorDisplay = GetTemplateChild("PlatformSensorDisplay") as PlatformSensorDisplay;
            SensorBar = GetTemplateChild("SensorBar") as LevelBar;

            slider.ValueChanged += delegate (DoubleSlider slider) { SaveToConfig(); };

            // Show the edit button for the custom-sensors slider.
            Button EditCustomSensorsButton = GetTemplateChild("EditCustomSensorsButton") as Button;
            EditCustomSensorsButton.Visibility = Type == "custom-sensors" ? Visibility.Visible : Visibility.Hidden;
            EditCustomSensorsButton.Click += delegate (object sender, RoutedEventArgs e)
            {
                SetCustomSensors dialog = new SetCustomSensors();
                dialog.Owner = Window.GetWindow(this);
                dialog.ShowDialog();
            };

            onConfigChange = new OnConfigChange(this, delegate (LoadFromConfigDelegateArgs args) {
                LoadUIFromConfig(ActivePad.GetFirstActivePadConfig(args));
            });
            
            onConfigInputChange = new OnConfigChange(this, delegate (LoadFromConfigDelegateArgs args) {
                Refresh(args);
            });
            onConfigInputChange.RefreshOnTestDataChange = true;
            onConfigInputChange.RefreshOnInputChange = true;
        }

        private void Refresh(LoadFromConfigDelegateArgs args)
        {
            int selectedPad = ActivePad.selectedPad == ActivePad.SelectedPad.P2 ? 1 : 0;
            var controllerData = args.controller[selectedPad];

            if (SensorDisplay.GetHighestSensorFromActivatedSensors(controllerData, out int activePanel, out int activeSensor))
            {
                SensorBar.Visibility = Visibility.Visible;
                if (!controllerData.test_data.HasSensorValid(activePanel, activeSensor))
                {
                    SensorBar.Value = 0;
                    return;
                }

                int sensorIndex = (activePanel * 4) + activeSensor;
                Int16 value = controllerData.test_data.sensorLevel[sensorIndex];

                if (value < 0)
                    value = 0;

                // Scale differently depending on if this is an FSR panel or a load cell panel.
                bool isFSR = controllerData.config.isFSR();
                if (isFSR)
                    value >>= 2;
                float maxValue = isFSR ? 250 : 500;
                SensorBar.Value = value / maxValue;
                SensorBar.PanelActive = controllerData.inputs[activePanel];
            }
            else
            {
                SensorBar.Visibility = Visibility.Hidden;
            }
        }

        private void RefreshSliderActiveProperty()
        {
            if (Type == "custom-sensors")
                SliderActive = ThresholdSettings.GetCustomSensors().Count > 0;
            else
                SliderActive = ThresholdEnabled;
        }

        // Return the panel/sensors this widget controls.
        //
        // This returns values for FSRs.  We don't configure individual sensors with load cells,
        // and the sensor value will be ignored.
        private List<ThresholdSettings.PanelAndSensor> GetControlledSensors(SMX.SMXConfig config, bool includeOverridden)
        {
            return ThresholdSettings.GetControlledSensorsForSliderType(Type, config.HasAllPanels(), includeOverridden);
        }


        private void SetValueToConfig(ref SMX.SMXConfig config)
        {
            List<ThresholdSettings.PanelAndSensor> panelAndSensors = GetControlledSensors(config, false);
            foreach (ThresholdSettings.PanelAndSensor panelAndSensor in panelAndSensors)
            {
                if (!config.isFSR())
                {
                    byte lower = (byte)slider.LowerValue;
                    byte upper = (byte)slider.UpperValue;
                    config.panelSettings[panelAndSensor.panel].loadCellLowThreshold = lower;
                    config.panelSettings[panelAndSensor.panel].loadCellHighThreshold = upper;
                }
                else
                {
                    byte lower = (byte)slider.LowerValue;
                    byte upper = (byte)slider.UpperValue;
                    config.panelSettings[panelAndSensor.panel].fsrLowThreshold[panelAndSensor.sensor] = lower;
                    config.panelSettings[panelAndSensor.panel].fsrHighThreshold[panelAndSensor.sensor] = upper;
                }
            }
        }

        private void GetValueFromConfig(SMX.SMXConfig config, out int lower, out int upper)
        {
            lower = upper = 0;

            // Use the first controlled sensor.  The rest should be the same.
            foreach (ThresholdSettings.PanelAndSensor panelAndSensor in GetControlledSensors(config, false))
            {
                if (!config.isFSR())
                {
                    lower = config.panelSettings[panelAndSensor.panel].loadCellLowThreshold;
                    upper = config.panelSettings[panelAndSensor.panel].loadCellHighThreshold;
                }
                else
                {
                    lower = config.panelSettings[panelAndSensor.panel].fsrLowThreshold[panelAndSensor.sensor];
                    upper = config.panelSettings[panelAndSensor.panel].fsrHighThreshold[panelAndSensor.sensor];
                }
                return;
            }
        }

        private void SaveToConfig()
        {
            if (UpdatingUI)
                return;

            // Apply the change and save it to the devices.
            foreach (Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                int pad = activePad.Item1;
                SMX.SMXConfig config = activePad.Item2;

                SetValueToConfig(ref config);
                SMX.SMX.SetConfig(pad, config);
                CurrentSMXDevice.singleton.FireConfigurationChanged(this);
            }
        }

        bool UpdatingUI = false;
        private void LoadUIFromConfig(SMX.SMXConfig config)
    {
            // Make sure SaveToConfig doesn't treat these as the user changing values.
            UpdatingUI = true;

            RefreshSliderActiveProperty();

            // Set the range for the slider.
            // 16-bit FSR thresholds.
            // 8-bit load cell thresholds
            SMXHelpers.ThresholdDefinition def = SMXHelpers.GetThresholdDefinition(config.isFSR());
            slider.Minimum = def.UserMin;
            slider.Maximum = def.UserMax;
            slider.MinimumDistance = def.MinRange;

            int lower, upper;
            GetValueFromConfig(config, out lower, out upper);

            // Firmware versions before 4 allowed 0xFF to be used to disable a threshold.
            // This isn't used in newer firmwares.
            if (!config.IsNewGen() && lower == 0xFF)
            {
                LowerLabel.Content = "Off";
                UpperLabel.Content = "";
                SensorBar.LowerThreshold = 1;
                SensorBar.HigherThreshold = 1;
            }
            else
            {
                slider.LowerValue = lower;
                slider.UpperValue = upper;
                LowerLabel.Content = lower.ToString();
                UpperLabel.Content = upper.ToString();
                SensorBar.LowerThreshold = (lower - def.RealMin) / (def.RealMax - def.RealMin);
                SensorBar.HigherThreshold = (upper - def.RealMin) / (def.RealMax - def.RealMin);
            }


            List<ThresholdSettings.PanelAndSensor> controlledSensors = GetControlledSensors(config, false);

            // SensorDisplay shows which sensors we control.  If this sensor is enabled, show the
            // sensors this sensor controls.
            // 
            // If we're disabled, the icon will be empty.  That looks
            // weird, so in that case we show 
            // Set the icon next to the slider to show which sensors we control.
            List<ThresholdSettings.PanelAndSensor> defaultControlledSensors = GetControlledSensors(config, true);
            SensorDisplay.SetFromPanelAndSensors(controlledSensors, defaultControlledSensors);

            UpdatingUI = false;
        }
    }

    // The checkbox next to the threshold slider to turn it on or off.  This is only used
    // for inner-sensors and outer-sensors, and hides itself automatically for others.
    public class ThresholdEnabledButton : CheckBox
    {
        // Which threshold slider this is for.  This is bound to ThresholdSlider.Type above.
        public static readonly DependencyProperty TypeProperty = DependencyProperty.Register("Type",
            typeof(string), typeof(ThresholdEnabledButton), new FrameworkPropertyMetadata(""));
        public string Type
        {
            get { return (string)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (Type != "inner-sensors" && Type != "outer-sensors")
            {
                Visibility = Visibility.Hidden;
                IsChecked = true;
                return;
            }

            OnConfigChange onConfigChange;
            onConfigChange = new OnConfigChange(this, delegate (LoadFromConfigDelegateArgs args) {
                LoadFromSettings();
            });
        }

        protected override void OnClick()
        {
            IsChecked = !IsChecked;
            SaveToSettings();
        }

        private void LoadFromSettings()
        {
            if (Type == "inner-sensors")
                IsChecked = Properties.Settings.Default.UseInnerSensorThresholds;
            else if (Type == "outer-sensors")
                IsChecked = Properties.Settings.Default.UseOuterSensorThresholds;
        }

        private void SaveToSettings()
        {
            if (Type == "inner-sensors")
                Properties.Settings.Default.UseInnerSensorThresholds = (bool)IsChecked;
            else if (Type == "outer-sensors")
                Properties.Settings.Default.UseOuterSensorThresholds = (bool)IsChecked;

            Helpers.SaveApplicationSettings();

            // Sync thresholds after enabling or disabling a slider.
            ThresholdSettings.SyncSliderThresholds();

            CurrentSMXDevice.singleton.FireConfigurationChanged(this);
        }
    }
}
