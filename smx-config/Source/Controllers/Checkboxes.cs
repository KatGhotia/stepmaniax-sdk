using System;
using System.Windows;
using System.Windows.Controls;

namespace smx_config
{
    public class LightAllPanelsCheckbox : CheckBox
    {
        public static readonly DependencyProperty LightAllPanelsProperty = DependencyProperty.Register("LightAllPanels",
            typeof(bool), typeof(LightAllPanelsCheckbox), new FrameworkPropertyMetadata(false));
        public bool LightAllPanels
        {
            get { return (bool)GetValue(LightAllPanelsProperty); }
            set { SetValue(LightAllPanelsProperty, value); }
        }

        OnConfigChange onConfigChange;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            onConfigChange = new OnConfigChange(this, delegate (LoadFromConfigDelegateArgs args) {
                LoadUIFromConfig(ActivePad.GetFirstActivePadConfig(args));
            });
        }

        private void LoadUIFromConfig(SMX.SMXConfig config)
        {
            LightAllPanels = config.getLightAllPanelsMode();
        }

        protected override void OnClick()
        {
            //SMX.SMXConfig firstConfig = ActivePad.GetFirstActivePadConfig();
            foreach (Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                int pad = activePad.Item1;
                SMX.SMXConfig config = activePad.Item2;
                config.setLightAllPanelsMode(!LightAllPanels);
                SMX.SMX.SetConfig(pad, config);
            }
            CurrentSMXDevice.singleton.FireConfigurationChanged(this);

            // Refresh the UI.
            //LoadUIFromConfig(firstConfig);
        }
    }

    public class EnableCenterTopSensorCheckbox : CheckBox
    {
        public static readonly DependencyProperty EnableSensorProperty = DependencyProperty.Register("EnableSensor",
            typeof(bool), typeof(EnableCenterTopSensorCheckbox), new FrameworkPropertyMetadata(false));
        public bool EnableSensor
        {
            get { return (bool)GetValue(EnableSensorProperty); }
            set { SetValue(EnableSensorProperty, value); }
        }

        OnConfigChange onConfigChange;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            onConfigChange = new OnConfigChange(this, delegate (LoadFromConfigDelegateArgs args) {
                LoadUIFromConfig(ActivePad.GetFirstActivePadConfig(args));
            });
        }

        private void LoadUIFromConfig(SMX.SMXConfig config)
        {
            // Center panel, top sensor:
            bool enabled = config.panelSettings[4].fsrHighThreshold[2] < 255;
            EnableSensor = enabled;
        }

        protected override void OnClick()
        {
            foreach (Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads())
            {
                int pad = activePad.Item1;
                SMX.SMXConfig config = activePad.Item2;

                // Disable the sensor by setting its high threshold to 255, and enable it by syncing it up
                // with the other thresholds.
                if (!EnableSensor)
                    config.panelSettings[4].fsrHighThreshold[2] = config.panelSettings[4].fsrHighThreshold[0];
                else
                    config.panelSettings[4].fsrHighThreshold[2] = 255;
                SMX.SMX.SetConfig(pad, config);
            }
            CurrentSMXDevice.singleton.FireConfigurationChanged(this);
        }
    }

    public class PanelTestModeCheckbox : CheckBox
    {
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
        }

        protected override void OnClick()
        {
            base.OnClick();

            SMX.SMX.SetPanelTestMode((bool)IsChecked ? SMX.SMX.PanelTestMode.PressureTest : SMX.SMX.PanelTestMode.Off);
        }
    }
}
