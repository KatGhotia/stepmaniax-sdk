using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace smx_config
{
    public class DiagnosticsPanelButton: PanelSelectButton
    {
        public static readonly DependencyProperty PanelProperty = DependencyProperty.RegisterAttached("Panel",
            typeof(int), typeof(PanelSelectButton), new FrameworkPropertyMetadata(0));

        public int Panel {
            get { return (int) this.GetValue(PanelProperty); }
            set { this.SetValue(PanelProperty, value); }
        }
        // Which panel is currently selected.
        public static readonly DependencyProperty SelectedPanelProperty = DependencyProperty.RegisterAttached("SelectedPanel",
            typeof(int), typeof(PanelSelectButton), new FrameworkPropertyMetadata(0));

        public int SelectedPanel {
            get { return (int) this.GetValue(SelectedPanelProperty); }
            set { this.SetValue(SelectedPanelProperty, value); }
        }

        // True if this panel is being pressed.
        public static readonly DependencyProperty PressedProperty = DependencyProperty.Register("Pressed",
            typeof(bool), typeof(DiagnosticsPanelButton), new FrameworkPropertyMetadata(false));

        public bool Pressed {
            get { return (bool) GetValue(PressedProperty); }
            set { SetValue(PressedProperty, value); }
        }

        // True if a warning icon should be displayed for this panel.
        public static readonly DependencyProperty WarningProperty = DependencyProperty.Register("Warning",
            typeof(bool), typeof(DiagnosticsPanelButton), new FrameworkPropertyMetadata(false));

        public bool Warning {
            get { return (bool) GetValue(WarningProperty); }
            set { SetValue(WarningProperty, value); }
        }

        private CurrentSMXDevice smxDevice;

        public DiagnosticsPanelButton(CurrentSMXDevice smxDevice)
        {
            this.smxDevice = smxDevice;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            OnConfigChange onConfigChange;
            onConfigChange = new OnConfigChange(this, delegate (LoadFromConfigDelegateArgs args)
            {
                int SelectedPad = Panel < 9 ? 0 : 1;
                int PanelIndex = Panel % 9;
                Pressed = args.controller[SelectedPad].inputs[PanelIndex];

                Warning = !args.controller[SelectedPad].test_data.bHaveDataFromPanel[PanelIndex] ||
                           args.controller[SelectedPad].test_data.AnySensorsOnPanelNotResponding(PanelIndex) ||
                           args.controller[SelectedPad].test_data.AnyBadJumpersOnPanel(PanelIndex);

                // Only show this panel button if the panel's input is enabled.
                SMX.SMXConfig config = ActivePad.GetFirstActivePadConfig(args);
                Visibility = ShouldBeDisplayed(config) ? Visibility.Visible : Visibility.Collapsed;
            }, smxDevice)
            {
                RefreshOnInputChange = true,
                RefreshOnTestDataChange = true
            };
        }

        protected override void OnClick()
        {
            base.OnClick();

            // Select this panel.
            SelectedPanel = Panel;

            smxDevice.FireConfigurationChanged(this);
        }

        // Return true if this button should be displayed.
        private bool ShouldBeDisplayed(SMX.SMXConfig config)
        {
            var enabledPanels = config.GetEnabledPanels();
            int PanelIndex = Panel % 9;
            return enabledPanels[PanelIndex];
        }
    }


    public class DiagnosticsControl: System.Windows.Controls.Control
    {
        // Which panel is currently selected:
        public static readonly DependencyProperty SelectedPanelProperty = DependencyProperty.Register("SelectedPanel",
            typeof(int?), typeof(DiagnosticsControl), new FrameworkPropertyMetadata(0));

        public int? SelectedPanel {
            get { return (int) this.GetValue(SelectedPanelProperty); }
            set { this.SetValue(SelectedPanelProperty, value); }
        }

        private LevelBar[]? LevelBars;
        private System.Windows.Controls.Label[]? LevelBarText;
        private System.Windows.Controls.ComboBox? DiagnosticMode;
        private System.Windows.Controls.Panel? CurrentDIPGroup;
        private FrameImage? CurrentDIP;
        private FrameImage? ExpectedDIP;
        private FrameworkElement? NoResponseFromPanel;
        private FrameworkElement? NoResponseFromSensors;
        private FrameworkElement? BadSensorDIPSwitches;
        private FrameworkElement? P1Diagnostics, P2Diagnostics;
        private FrameworkElement? DIPLabelLeft, DIPLabelRight;

        public delegate void ShowAllLightsEvent(bool on);
        public event ShowAllLightsEvent? SetShowAllLights;

        private CurrentSMXDevice smxDevice;

        public DiagnosticsControl(CurrentSMXDevice smxDevice)
        {
            this.smxDevice = smxDevice;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            LevelBars = new LevelBar[4];
            for (int i = 0; i < LevelBars.Length; ++i)
            {
                LevelBars[i] = (Template.FindName($"SensorBar{i}", this) as LevelBar)!;
            }

            LevelBarText = new System.Windows.Controls.Label[4];
            for (int i = 0; i < LevelBars.Length; ++i)
            {
                LevelBarText[i] = (System.Windows.Controls.Label) Template.FindName($"SensorBarLevel{i}", this);
            }

            DiagnosticMode = (System.Windows.Controls.ComboBox)Template.FindName("DiagnosticMode", this);
            CurrentDIPGroup = (System.Windows.Controls.Panel)Template.FindName("CurrentDIPGroup", this);
            CurrentDIP = (FrameImage)Template.FindName("CurrentDIP", this);
            ExpectedDIP = (FrameImage)Template.FindName("ExpectedDIP", this);
            NoResponseFromPanel = (FrameworkElement)Template.FindName("NoResponseFromPanel", this);
            NoResponseFromSensors = (FrameworkElement)Template.FindName("NoResponseFromSensors", this);
            BadSensorDIPSwitches = (FrameworkElement)Template.FindName("BadSensorDIPSwitches", this);
            P1Diagnostics = (FrameworkElement)Template.FindName("P1Diagnostics", this);
            P2Diagnostics = (FrameworkElement)Template.FindName("P2Diagnostics", this);

            DIPLabelRight = (Template.FindName("DIPLabelRight", this) as FrameworkElement)!;
            DIPLabelLeft = (Template.FindName("DIPLabelLeft", this) as FrameworkElement)!;

            var Recalibrate = (System.Windows.Controls.Button)Template.FindName("Recalibrate", this);
            Recalibrate.Click += delegate (object? sender, RoutedEventArgs e)
            {
                for (int pad = 0; pad < 2; ++pad)
                    SMX.SMX.ForceRecalibration(pad);
            };

            // Note that we won't get a MouseUp if the display is hidden due to a controller
            // disconnection while the mouse is held.  We handle this in Refresh().
            var LightAll = (System.Windows.Controls.Button)Template.FindName("LightAll", this);
            LightAll.PreviewMouseDown += delegate (object? sender, MouseButtonEventArgs e)
            {
                SetShowAllLights?.Invoke(true);
            };
            LightAll.PreviewMouseUp += delegate (object? sender, MouseButtonEventArgs e)
            {
                SetShowAllLights?.Invoke(false);
            };

            // Update the test mode when the dropdown is changed.
            DiagnosticMode!.AddHandler(System.Windows.Controls.ComboBox.SelectionChangedEvent, new RoutedEventHandler(delegate (object? sender, RoutedEventArgs e)
            {
                for (int pad = 0; pad < 2; ++pad)
                    SMX.SMX.SetSensorTestMode(pad, (SMX.SMX.SensorTestMode)DiagnosticMode.SelectedValue, "Diagnostics");
            }));

            OnConfigChange onConfigChange;
            onConfigChange = new OnConfigChange(this, delegate (LoadFromConfigDelegateArgs args)
            {
                            // First, make sure a valid panel is selected.
            SMX.SMXConfig config = ActivePad.GetFirstActivePadConfig(args);
            SelectValidPanel(config);

            // Update the selected diagnostics button based on the value of selectedButton.
            // Tell the buttons which one is selected.
            for (int i = 0; i < 18; ++i)
            {
                var button = (Template.FindName($"Panel{i}", this) as DiagnosticsPanelButton)!;
                button.IsSelected = button.Panel == SelectedPanel;
            }

            // Make sure SetShowAllLights is disabled if the controller is disconnected, since
            // we can miss mouse up events.
            bool EitherControllerConnected = args.controller[0].info.connected || args.controller[1].info.connected;
            if (!EitherControllerConnected)
                SetShowAllLights?.Invoke(false);

            P1Diagnostics.Visibility = args.controller[0].info.connected? Visibility.Visible:Visibility.Collapsed;
            P2Diagnostics.Visibility = args.controller[1].info.connected? Visibility.Visible:Visibility.Collapsed;

            // Adjust the DIP labels to match the PCB.
            bool DIPLabelsOnLeft = !config.IsNewGen();
            DIPLabelRight.Visibility = DIPLabelsOnLeft? Visibility.Collapsed:Visibility.Visible;
            DIPLabelLeft.Visibility = DIPLabelsOnLeft? Visibility.Visible:Visibility.Collapsed;

                // Update the displayed DIP switch icons.
                if (SelectedPanel == null)
                {
                    Console.Error.WriteLine("No panel selected - refreshing not possible.");
                }
                else
                {
                    int SelectedPad = SelectedPanel.Value < 9 ? 0 : 1;
                    int PanelIndex = SelectedPanel.Value % 9;
                    int dip = args.controller[SelectedPad].test_data.iDIPSwitchPerPanel[PanelIndex];
                    CurrentDIP.Frame = dip;
                    ExpectedDIP.Frame = PanelIndex;

                    // Show or hide the sensor error text.
                    bool AnySensorsNotResponding = false, HaveIncorrectSensorDIP = false;
                    if (args.controller[SelectedPad].test_data.bHaveDataFromPanel[PanelIndex])
                    {
                        AnySensorsNotResponding = args.controller[SelectedPad].test_data.AnySensorsOnPanelNotResponding(PanelIndex);

                        // Don't show both warnings.
                        HaveIncorrectSensorDIP = !AnySensorsNotResponding && args.controller[SelectedPad].test_data.AnyBadJumpersOnPanel(PanelIndex);
                    }
                    NoResponseFromSensors.Visibility = AnySensorsNotResponding ? Visibility.Visible : Visibility.Collapsed;
                    BadSensorDIPSwitches.Visibility = HaveIncorrectSensorDIP ? Visibility.Visible : Visibility.Collapsed;

                    // Update the level bar from the test mode data for the selected panel.
                    for (int sensor = 0; sensor < 4; ++sensor)
                    {
                        var controllerData = args.controller[SelectedPad];
                        var value = controllerData.test_data.sensorLevel[PanelIndex * 4 + sensor];

                        if ((SMX.SMX.SensorTestMode)DiagnosticMode.SelectedValue == SMX.SMX.SensorTestMode.Noise)
                        {
                            // In noise mode, we receive standard deviation values squared.  Display the square
                            // root, since the panels don't do this for us.  This makes the numbers different
                            // than the configured value (square it to convert back), but without this we display
                            // a bunch of 4 and 5-digit numbers that are too hard to read.
                            value = (Int16)Math.Sqrt(value);
                        }

                        LevelBarText[sensor].Visibility = Visibility.Visible;
                        if (!args.controller[SelectedPad].test_data.bHaveDataFromPanel[PanelIndex])
                        {
                            LevelBars[sensor].Value = 0;
                            LevelBarText[sensor].Visibility = Visibility.Hidden;
                            LevelBarText[sensor].Content = "-";
                            LevelBars[sensor].Error = false;
                        }
                        else if (args.controller[SelectedPad].test_data.bBadSensorInput[PanelIndex * 4 + sensor])
                        {
                            LevelBars[sensor].Value = 0;
                            LevelBarText[sensor].Content = "!";
                            LevelBars[sensor].Error = true;
                        }
                        else
                        {
                            if (!Helpers.NoClampOutput())
                            {
                                // Very slightly negative values happen due to noise.  They don't indicate a
                                // problem, but they're confusing in the UI, so clamp them away.
                                if (value < 0 && value >= -10)
                                    value = 0;
                            }

                            // Scale differently depending on if this is an FSR panel or a load cell panel.
                            bool isFSR = controllerData.config.isFSR();
                            if (isFSR)
                                value >>= 2;

                            SMXHelpers.ThresholdDefinition def = SMXHelpers.GetThresholdDefinition(config.isFSR());

                            LevelBars[sensor].Value = value / def.RealMax;
                            LevelBars[sensor].PanelActive = args.controller[SelectedPad].inputs[PanelIndex];

                            GetThresholdFromSensor(config, PanelIndex, sensor, out int lower, out int upper);
                            LevelBars[sensor].LowerThreshold = (((float)lower) - def.RealMin) / (def.RealMax - def.RealMin);
                            LevelBars[sensor].HigherThreshold = (((float)upper) - def.RealMin) / (def.RealMax - def.RealMin);

                            LevelBarText[sensor].Content = value;
                            LevelBars[sensor].Error = false;
                        }
                    }

                    NoResponseFromPanel.Visibility = Visibility.Collapsed;
                    CurrentDIPGroup.Visibility = Visibility.Visible;
                    if (!args.controller[SelectedPad].test_data.bHaveDataFromPanel[PanelIndex])
                    {
                        NoResponseFromPanel.Visibility = Visibility.Visible;
                        NoResponseFromSensors.Visibility = Visibility.Collapsed;
                        CurrentDIPGroup.Visibility = Visibility.Hidden;
                        return;
                    }
                }
            }, smxDevice)
            {
                RefreshOnTestDataChange = true
            };

            Loaded += delegate (object sender, RoutedEventArgs e)
            {
                for (int pad = 0; pad < 2; ++pad)
                    SMX.SMX.SetSensorTestMode(pad, (SMX.SMX.SensorTestMode)DiagnosticMode.SelectedValue, "Diagnostics");
            };

            Unloaded += delegate (object sender, RoutedEventArgs e)
            {
                for (int pad = 0; pad < 2; ++pad)
                    SMX.SMX.SetSensorTestMode(pad, SMX.SMX.SensorTestMode.Off, "Diagnostics");
            };
        }


        private void GetThresholdFromSensor(SMX.SMXConfig config, int panel, int sensor, out int lower, out int upper)
        {
            if (!config.isFSR())
            {
                lower = config.panelSettings[panel].loadCellLowThreshold;
                upper = config.panelSettings[panel].loadCellHighThreshold;
            }
            else
            {
                lower = config.panelSettings[panel].fsrLowThreshold[sensor];
                upper = config.panelSettings[panel].fsrHighThreshold[sensor];
            }
        }

        // If the selected panel isn't enabled for input, select another one.
        private void SelectValidPanel(SMX.SMXConfig config)
        {
            bool[] enabledPanels = config.GetEnabledPanels();
            int SelectedPanelIndex = SelectedPanel.GetValueOrDefault(0) % 9;

            // If we're not selected, or this button is visible, we don't need to do anything.
            if (!enabledPanels[SelectedPanelIndex]) {
                SelectedPanel = config.GetFirstEnabledPanel();
                if (SelectedPanel == null) {
                    Console.Error.WriteLine("No panels/sensors enabled at all! File a bug report on github.com/katghotia/stepmaniax-sdk if you can think of a good reason why to support that.");
                }
            }
        }

    }

}
