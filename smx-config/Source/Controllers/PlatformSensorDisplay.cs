using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Collections.Generic;

namespace smx_config
{

    public class PanelIconWithSensorsSensor : Control
    {
        // 0: black
        // 1: dim highlight
        // 2: bright highlight
        public static readonly DependencyProperty HighlightProperty = DependencyProperty.Register("Highlight",
            typeof(int), typeof(PanelIconWithSensorsSensor), new FrameworkPropertyMetadata(0));
        public int Highlight
        {
            get { return (int)GetValue(HighlightProperty); }
            set { SetValue(HighlightProperty, value); }
        }
    }

    // A control with one button for each of four sensors:
    class PanelIconWithSensors : Control
    {
        PanelIconWithSensorsSensor[] panelIconWithSensorsSensor;
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            panelIconWithSensorsSensor = new PanelIconWithSensorsSensor[4];
            for (int sensor = 0; sensor < 4; ++sensor)
                panelIconWithSensorsSensor[sensor] = GetTemplateChild("Sensor" + sensor) as PanelIconWithSensorsSensor;
        }


        public PanelIconWithSensorsSensor GetSensorControl(int sensor)
        {
            return panelIconWithSensorsSensor[sensor];
        }
    }

    public class PlatformSensorDisplay : Control
    {
        PanelIconWithSensors[] panelIconWithSensors;
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            panelIconWithSensors = new PanelIconWithSensors[9];
            for (int panel = 0; panel < 9; ++panel)
                panelIconWithSensors[panel] = GetTemplateChild("Panel" + panel) as PanelIconWithSensors;
        }

        private PanelIconWithSensorsSensor GetSensor(int panel, int sensor)
        {
            return panelIconWithSensors[panel].GetSensorControl(sensor);
        }

        // Highlight the sensors included in panelAndSensors, and dimly highlight the sensors in
        // disabledPanelAndSensors.  If a sensor is in both lists, panelAndSensors takes priority.
        public void SetFromPanelAndSensors(
            List<ThresholdSettings.PanelAndSensor> panelAndSensors,
            List<ThresholdSettings.PanelAndSensor> disabledPanelAndSensors)
        {
            UnhighlightAllSensors();

            foreach (ThresholdSettings.PanelAndSensor panelAndSensor in disabledPanelAndSensors)
                GetSensor(panelAndSensor.panel, panelAndSensor.sensor).Highlight = 1;
            foreach (ThresholdSettings.PanelAndSensor panelAndSensor in panelAndSensors)
                GetSensor(panelAndSensor.panel, panelAndSensor.sensor).Highlight = 2;
        }

        // Clear all sensor highlighting.
        public void UnhighlightAllSensors()
        {
            for (int panel = 0; panel < 9; ++panel)
            {
                for (int sensor = 0; sensor < 4; ++sensor)
                    GetSensor(panel, sensor).Highlight = 0;
            }
        }

        public bool GetHighestSensorFromActivatedSensors(LoadFromConfigDelegateArgsPerController controllerData, out int activePanel, out int activeSensor)
        {
            activePanel = -1;
            activeSensor = -1;
            short sensorValue = -1;
            for (int panel = 0; panel < 9; ++panel)
            {
                for (int sensor = 0; sensor < 4; ++sensor)
                {
                    if (GetSensor(panel, sensor).Highlight == 2)
                    {
                        if (controllerData.test_data.HasSensorValid(panel, sensor))
                        {
                            int sensorIndex = (panel * 4) + sensor;
                            short sensorValueComp = controllerData.test_data.sensorLevel[sensorIndex];
                            if (sensorValueComp >= sensorValue)
                            {
                                activePanel = panel;
                                activeSensor = sensor;
                                sensorValue = sensorValueComp;
                            }
                        }
                    }
                }
            }
            return activePanel > 0 && activeSensor > 0;
        }
    }
}
