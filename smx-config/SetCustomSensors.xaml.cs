using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace smx_config
{
    public class SensorSelectionButton: ToggleButton
    {
    }

    // A control with one button for each of four sensors:
    class SensorSelector: System.Windows.Controls.Control
    {
        // The panel we're editing (0-8).
        public static readonly DependencyProperty PanelProperty = DependencyProperty.RegisterAttached("Panel",
            typeof(int), typeof(SensorSelector), new FrameworkPropertyMetadata(0));

        public int Panel {
            get { return (int) this.GetValue(PanelProperty); }
            set { this.SetValue(PanelProperty, value); }
        }

        readonly ToggleButton[] SensorSelectionButtons = new ToggleButton[4];

        private CurrentSMXDevice smxDevice;

        public SensorSelector(CurrentSMXDevice smxDevice)
        {
            this.smxDevice = smxDevice;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            for (int sensor_i = 0; sensor_i < 4; ++sensor_i)
            {
                int sensor = sensor_i; // bind
                // pucgenie: programming error if null is encountered here
                var _toggleButton = (GetTemplateChild($"Sensor{sensor}") as ToggleButton)!;
                SensorSelectionButtons[sensor] = _toggleButton;
                _toggleButton.Click += delegate (object sender, RoutedEventArgs e)
                {
                    //ClickedSensorButton(ThisSensor);
                    // Toggle the clicked sensor.
                    Console.WriteLine($"Clicked sensor {sensor}");
                    List<ThresholdSettings.PanelAndSensor> customSensors = ThresholdSettings.GetCustomSensors();

                    ThresholdSettings.PanelAndSensor customSensor = new(Panel, sensor);
                    if (!IsSensorEnabled(customSensors, sensor))
                        customSensors.Add(customSensor);
                    else
                        customSensors.Remove(customSensor);
                    ThresholdSettings.SetCustomSensors(customSensors);

                    // Sync thresholds after changing custom sensors.
                    ThresholdSettings.SyncSliderThresholds(smxDevice);

                    smxDevice.FireConfigurationChanged(this);
                };
            }

            // These settings are stored in the application settings, not on the pad.  However,
            // we treat changes to this as config changes, so we can use the same OnConfigChange
            // method for updating.
            OnConfigChange onConfigChange;
            onConfigChange = new OnConfigChange(this, delegate (LoadFromConfigDelegateArgs args)
            {
                LoadUIFromConfig(args);
            }, smxDevice);
        }

        // Return true if the given sensor is included in custom-sensors.
        bool IsSensorEnabled(List<ThresholdSettings.PanelAndSensor> customSensors, int sensor)
        {
            foreach (var panelAndSensor in customSensors)
            {
                if (panelAndSensor.panel == Panel && panelAndSensor.sensor == sensor)
        return true;
            }
            return false;
        }

        private void LoadUIFromConfig(LoadFromConfigDelegateArgs args)
        {
            // Check the selected custom-sensors.
            var customSensors = ThresholdSettings.GetCustomSensors();
            for (int sensor = 0; sensor < 4; ++sensor)
                SensorSelectionButtons[sensor].IsChecked = IsSensorEnabled(customSensors, sensor);
        }
    }

    // This dialog sets which sensors are controlled by custom-sensors.  The actual work is done
    // by SensorSelector above.
    public partial class SetCustomSensors: Window
    {
        public SetCustomSensors()
        {
            InitializeComponent();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
} 
