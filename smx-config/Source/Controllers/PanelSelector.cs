using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;

namespace smx_config
{

    public class PanelButton : ToggleButton
    {
        public static readonly DependencyProperty ButtonProperty = DependencyProperty.RegisterAttached("Button",
            typeof(string), typeof(PanelButton), new FrameworkPropertyMetadata(null));

        public string Button
        {
            get { return (string)this.GetValue(ButtonProperty); }
            set { this.SetValue(ButtonProperty, value); }
        }

        protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnIsPressedChanged(e);
        }
    }

    // A base class for buttons used to select a panel to work with.
    public class PanelSelectButton : System.Windows.Controls.Button
    {
        // Whether this button is selected.
        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.RegisterAttached("IsSelected",
            typeof(bool), typeof(PanelSelectButton), new FrameworkPropertyMetadata(false));

        public bool IsSelected
        {
            get { return (bool)this.GetValue(IsSelectedProperty); }
            set { this.SetValue(IsSelectedProperty, value); }
        }
    }

    // A button that selects which color is being set.
    public abstract class ColorButton : PanelSelectButton
    {
        // The color configured for this panel:
        public static readonly DependencyProperty PanelColorProperty = DependencyProperty.RegisterAttached("PanelColor",
            typeof(SolidColorBrush), typeof(ColorButton), new FrameworkPropertyMetadata(new SolidColorBrush()));

        protected CurrentSMXDevice smxDevice;

        public ColorButton(CurrentSMXDevice smxDevice)
        {
            this.smxDevice = smxDevice;
        }

        public SolidColorBrush PanelColor
        {
            get { return (SolidColorBrush)this.GetValue(PanelColorProperty); }
            set { this.SetValue(PanelColorProperty, value); }
        }

        // Return 0 if this is for the P1 pad, or 1 if it's for P2.
        protected abstract int getPadNo();

        // Return true if this panel is enabled and should be selectable.
        public abstract bool isEnabled(LoadFromConfigDelegateArgs args);

        // Get and set our color to the pad configuration.
        abstract public System.Windows.Media.Color getColor();
        abstract public void setColor(System.Windows.Media.Color color);

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            OnConfigChange onConfigChange;
            onConfigChange = new OnConfigChange(this, delegate (LoadFromConfigDelegateArgs args) {
                LoadUIFromConfig(args);
            }, smxDevice);
        }

        // Set PanelColor.  This widget doesn't change the color, it only reflects the current configuration.
        private void LoadUIFromConfig(LoadFromConfigDelegateArgs args)
        {
            SMX.SMXConfig config = args.controller[getPadNo()].config;

            // Hide disabled color buttons.
            Visibility = isEnabled(args) ? Visibility.Visible : Visibility.Hidden;

            System.Windows.Media.Color rgb = getColor();
            PanelColor = new SolidColorBrush(rgb);
        }

        System.Windows.Point MouseDownPosition;

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            MouseDownPosition = e.GetPosition(null);
            base.OnMouseDown(e);
        }

        // Handle initiating drag.
        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                System.Windows.Point position = e.GetPosition(null);

                // Why do we have to handle drag thresholding manually?  This is the platform's job.
                // If we don't do this, clicks won't work at all.
                if (Math.Abs(position.X - MouseDownPosition.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - MouseDownPosition.Y) >= SystemParameters.MinimumVerticalDragDistance)
                {
                    DragDrop.DoDragDrop(this, Helpers.ColorToString(PanelColor.Color), System.Windows.DragDropEffects.Copy);
                }
            }

            base.OnMouseMove(e);
        }

        private bool HandleDrop(System.Windows.DragEventArgs e)
        {
            var Button = e.Source as PanelColorButton;
            if (Button == null)
        return false;

            // A color is being dropped from another button.  Don't just update our color, since
            // that will just change the button color and not actually apply it.
            var data = e.Data as System.Windows.DataObject;
            if (data == null)
        return false;

            // Parse the color being dragged onto us, and set it.
            var color = Helpers.ParseColorString(data.GetData(typeof(string)) as string);
            setColor(color);

            return true;
        }

        protected override void OnDrop(System.Windows.DragEventArgs e)
        {
            if (!HandleDrop(e))
                base.OnDrop(e);
        }
    }

    // A ColorButton for setting a panel color.
    public class PanelColorButton : ColorButton
    {
        // Which panel this is (P1 0-8, P2 9-17):
        public static readonly DependencyProperty PanelProperty = DependencyProperty.RegisterAttached("Panel",
            typeof(int), typeof(PanelColorButton), new FrameworkPropertyMetadata(0));

        public int Panel
        {
            get { return (int)this.GetValue(PanelProperty); }
            set { this.SetValue(PanelProperty, value); }
        }

        public PanelColorButton(CurrentSMXDevice smxDevice) : base(smxDevice) { }

        protected override int getPadNo()
        {
            return Panel < 9 ? 0 : 1;
        }

        // A panel is enabled if it's enabled in the panel mask, which can be
        // changed on the advanced tab.
        public override bool isEnabled(LoadFromConfigDelegateArgs args)
        {
            int pad = getPadNo();
            SMX.SMXConfig config = args.controller[pad].config;

            if (!args.controller[pad].info.connected)
        return false;

            var enabledPanels = config.GetEnabledPanels();
            return enabledPanels[Panel % 9];
        }

        public override void setColor(System.Windows.Media.Color color)
        {
            // Apply the change and save it to the device.
            var pad = getPadNo();
            if (!SMX.SMX.GetConfig(pad, out SMX.SMXConfig config))
                return;

            // Light colors are 8-bit values, but we only use values between 0-170.  Higher values
            // don't make the panel noticeably brighter, and just draw more power.
            int PanelIndex = Panel % 9;
            config.stepColor[PanelIndex * 3 + 0] = Helpers.ScaleColor(color.R);
            config.stepColor[PanelIndex * 3 + 1] = Helpers.ScaleColor(color.G);
            config.stepColor[PanelIndex * 3 + 2] = Helpers.ScaleColor(color.B);

            SMX.SMX.SetConfig(pad, config);
            smxDevice.FireConfigurationChanged(this);
        }

        // Return the color set for this panel in config.
        public override Color getColor()
        {
            int pad = getPadNo();
            if (!SMX.SMX.GetConfig(pad, out SMX.SMXConfig config))
                return Color.FromRgb(0, 0, 0);

            int PanelIndex = Panel % 9;
            return Helpers.UnscaleColor(Color.FromRgb(
                config.stepColor[PanelIndex * 3 + 0],
                config.stepColor[PanelIndex * 3 + 1],
                config.stepColor[PanelIndex * 3 + 2]));
        }
    }

    public class FloorColorButton : ColorButton
    {
        // 0 if this is for P1, 1 for P2.
        public static readonly DependencyProperty PadProperty = DependencyProperty.RegisterAttached("Pad",
            typeof(int), typeof(FloorColorButton), new FrameworkPropertyMetadata(0));

        public FloorColorButton(CurrentSMXDevice smxDevice) : base(smxDevice) { }

        public int Pad
        {
            get { return (int)this.GetValue(PadProperty); }
            set { this.SetValue(PadProperty, value); }
        }
        protected override int getPadNo() { return Pad; }

        // The floor color button is available if the firmware is v4 or greater.
        public override bool isEnabled(LoadFromConfigDelegateArgs args)
        {
            int pad = getPadNo();
            SMX.SMXConfig config = args.controller[pad].config;
            return config.IsNewGen();
        }

        public override void setColor(Color color)
        {
            // Apply the change and save it to the device.
            int pad = getPadNo();
            if (!SMX.SMX.GetConfig(pad, out SMX.SMXConfig config))
        return;

            // pucgenie: No C# 12.0 available for compatibility reasons
            config.platformStripColor[0] = color.R;
            config.platformStripColor[1] = color.G;
            config.platformStripColor[2] = color.B;

            SMX.SMX.SetConfig(pad, config);
            smxDevice.FireConfigurationChanged(this);

            QueueSetPlatformLights();
        }

        // Queue SetPlatformLights to happen after a brief delay.  This rate limits setting
        // the color, so we don't spam the device when dragging the color slider.
        DispatcherTimer? PlatformLightsTimer;
        void QueueSetPlatformLights()
        {
            // Stop if SetPlatformLights is already queued.
            if (PlatformLightsTimer != null)
                return;

            PlatformLightsTimer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 0, 0, 33)
            };
            PlatformLightsTimer.Start();

            PlatformLightsTimer.Tick += delegate (object sender, EventArgs e)
            {
                PlatformLightsTimer.Stop();
                PlatformLightsTimer = null;
                SetPlatformLights();
            };
        }

        // The config update doesn't happen in realtime, so set the platform LED colors directly.
        void SetPlatformLights()
        {
            CommandBuffer cmd = new();

            for (int pad = 0; pad < 2; ++pad)
            {
                // Use this panel's color.  If a panel isn't connected, we still need to run the
                // loop below to insert data for the panel.
                byte[] color = SMX.SMX.GetConfig(pad, out SMX.SMXConfig config)
                    ? config.platformStripColor
                    : new byte[3];
                for (int i = 0; i < 44; ++i)
                    cmd.Write(color);
            }

            SMX.SMX.SMX_SetPlatformLights(cmd.Get());

        }

        // Return the color set for this panel in config.
        public override Color getColor()
        {
            int pad = getPadNo();
            if (!SMX.SMX.GetConfig(pad, out SMX.SMXConfig config))
                return Color.FromRgb(0, 0, 0);

            return Color.FromRgb(config.platformStripColor[0], config.platformStripColor[1], config.platformStripColor[2]);
        }
    }


    // This widget selects which panels are enabled.  We only show one of these for both pads.
    class PanelSelector : Control
    {
        readonly PanelButton[] EnabledPanelButtons = new PanelButton[9];
        OnConfigChange? onConfigChange;

        private readonly CurrentSMXDevice smxDevice;

        public PanelSelector(CurrentSMXDevice smxDevice)
        {
            this.smxDevice = smxDevice;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            byte[] PanelToIndex = new byte[] {
                7, 8, 9,
                4, 5, 6,
                1, 2, 3,
            };

            for (int i = 0; i < 9; ++i)
            {
                var button = EnabledPanelButtons[i] = (GetTemplateChild($"EnablePanel{PanelToIndex[i]}") as PanelButton)!;
                button.Click += EnabledPanelButtonClicked;
            }

            onConfigChange = new OnConfigChange(this, delegate (LoadFromConfigDelegateArgs args)
            {
                LoadUIFromConfig(ActivePad.GetFirstActivePadConfig(args));
            }, smxDevice);
        }

        private void LoadUIFromConfig(SMX.SMXConfig config)
        {
            // The firmware configuration allows disabling each of the four sensors in a panel
            // individually, but currently we only have a UI for toggling the whole sensor.  Taking
            // individual sensors out isn't recommended.
            bool[] enabledPanels = {
                (config.enabledSensors[0] & 0xF0) != 0,
                (config.enabledSensors[0] & 0x0F) != 0,
                (config.enabledSensors[1] & 0xF0) != 0,
                (config.enabledSensors[1] & 0x0F) != 0,
                (config.enabledSensors[2] & 0xF0) != 0,
                (config.enabledSensors[2] & 0x0F) != 0,
                (config.enabledSensors[3] & 0xF0) != 0,
                (config.enabledSensors[3] & 0x0F) != 0,
                (config.enabledSensors[4] & 0xF0) != 0,
            };

            for (int i = 0; i < 9; ++i)
                EnabledPanelButtons[i].IsChecked = enabledPanels[i];
        }

        private int GetIndexFromButton(object sender)
        {
            for (int i = 0; i < 9; i++)
            {
                if (sender == EnabledPanelButtons[i])
        return i;
            }

            return 0;
        }

        private void EnabledPanelButtonClicked(object sender, EventArgs e)
        {
            // One of the panel buttons on the panel toggle UI was clicked.  Toggle the
            // panel.
            int button = GetIndexFromButton(sender);
            Console.WriteLine($"Clicked {button}");

            // Set the enabled sensor mask on both pads to the state of the UI.
            foreach (Tuple<int, SMX.SMXConfig> activePad in ActivePad.ActivePads(smxDevice.GetState()))
            {
                int pad = activePad.Item1;
                SMX.SMXConfig config = activePad.Item2;

                // This could be done algorithmically, but this is clearer.
                // pucgenie: Code duplication, nevertheless.
                int[] PanelButtonToSensorIndex = {
                    0, 0, 1, 1, 2, 2, 3, 3, 4
                };
                byte[] PanelButtonToSensorMask = {
                    0xF0, 0x0F,
                    0xF0, 0x0F,
                    0xF0, 0x0F,
                    0xF0, 0x0F,
                    0xF0,
                };
                for (int i = 0; i < 5; ++i)
                    config.enabledSensors[i] = 0;

                for (int Panel = 0; Panel < 9; ++Panel)
                {
                    int index = PanelButtonToSensorIndex[Panel];
                    byte mask = PanelButtonToSensorMask[Panel];
                    if (EnabledPanelButtons[Panel].IsChecked == true)
                        config.enabledSensors[index] |= (byte)mask;
                }

                // If we're not in "light all panels" mode, sync up autoLightPanelMask
                // with the new enabledSensors.
                config.refreshAutoLightPanelMask();

                SMX.SMX.SetConfig(pad, config);
            }
        }
    };


}
