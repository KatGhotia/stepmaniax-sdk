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

    // This is the Slider inside a ColorPicker.
    public class ColorPickerSlider : Slider2
    {
        public ColorPickerSlider()
        {
        }
    };

    public class ColorPicker : Control
    {
        ColorPickerSlider HueSlider;
        public delegate void Event();

        // The selected ColorButton.  This handles getting and setting the color to the
        // config.
        ColorButton _colorButton;
        public ColorButton colorButton
        {
            get { return _colorButton; }
            set
            {
                _colorButton = value;

                // Refresh on change.
                LoadFromConfigDelegateArgs args = CurrentSMXDevice.singleton.GetState();
                LoadUIFromConfig(args);
            }
        }


        public event Event StartedDragging, StoppedDragging;
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            HueSlider = GetTemplateChild("HueSlider") as ColorPickerSlider;
            HueSlider.ValueChanged += delegate (object sender, RoutedPropertyChangedEventArgs<double> e) {
                SaveToConfig();
            };

            HueSlider.StartedDragging += delegate () { StartedDragging?.Invoke(); };
            HueSlider.StoppedDragging += delegate () { StoppedDragging?.Invoke(); };

            DoubleCollection ticks = new DoubleCollection();
            // Add a tick at the minimum value, which is a negative value.  This is the
            // tick for white.
            ticks.Add(HueSlider.Minimum);

            // Add a tick for 0-359.  Don't add 360, since that's the same as 0.
            for (int i = 0; i < 360; ++i)
                ticks.Add(i);
            HueSlider.Ticks = ticks;

            OnConfigChange onConfigChange;
            onConfigChange = new OnConfigChange(this, delegate (LoadFromConfigDelegateArgs args) {
                LoadUIFromConfig(args);
            });
        }

        private void SaveToConfig()
        {
            if (UpdatingUI || _colorButton == null)
                return;

            Color color = Helpers.FromHSV(HueSlider.Value, 1, 1);

            // If we're set to the minimum value, use white instead.
            if (HueSlider.Value == HueSlider.Minimum)
                color = Color.FromRgb(255, 255, 255);

            _colorButton.setColor(color);
        }

        bool UpdatingUI = false;
        private void LoadUIFromConfig(LoadFromConfigDelegateArgs args)
        {
            if (UpdatingUI || _colorButton == null)
                return;

            // Make sure SaveToConfig doesn't treat these as the user changing values.
            UpdatingUI = true;

            // Reverse the scaling we applied in SaveToConfig.
            Color rgb = _colorButton.getColor();
            double h, s, v;
            Helpers.ToHSV(rgb, out h, out s, out v);

            // Check for white.  Since the conversion through LightsScaleFactor may not round trip
            // back to exactly #FFFFFF, give some room for error in the value (brightness).
            if (s <= 0.001 && v >= .90)
            {
                // This is white, so set it to the white block at the left edge of the slider.
                HueSlider.Value = HueSlider.Minimum;
            }
            else
            {
                HueSlider.Value = h;
            }

            UpdatingUI = false;
        }
    };

}
