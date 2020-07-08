using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;

namespace smx_config
{
    public class LevelBar : Control
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value",
            typeof(double), typeof(LevelBar), new FrameworkPropertyMetadata(0.5, ValueChangedCallback));

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ErrorProperty = DependencyProperty.Register("Error",
            typeof(bool), typeof(LevelBar), new FrameworkPropertyMetadata(false, ValueChangedCallback));

        public bool Error
        {
            get { return (bool)GetValue(ErrorProperty); }
            set { SetValue(ErrorProperty, value); }
        }

        private Rectangle Fill, Back;


        private static void ValueChangedCallback(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            LevelBar self = target as LevelBar;
            self.Refresh();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            Fill = Template.FindName("Fill", this) as Rectangle;
            Back = Template.FindName("Back", this) as Rectangle;
            Refresh();
        }

        private void Refresh()
        {
            // If Error is true, fill the bar red.
            double FillVal = Error ? 1 : Value;
            if (Back.Width > Back.Height) //Horizontal
            {
                Fill.Width = Math.Round(Math.Max(FillVal, 0) * Back.Width);
            }
            else //Vertical
            {
                Fill.Height = Math.Round(Math.Max(FillVal, 0) * (Back.Height - 2));
            }

            if (Error)
            {
                Fill.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            }
            else
            {
                // Scale from green (#00FF00) to Red (#FF0000)
                double RedValue = Value / 0.5;
                double GreenValue = 1 - ((Value - 0.5) / 0.5);
                Byte Red = (Byte)(Math.Max(0, Math.Min(255, RedValue * 255)));
                Byte Green = (Byte)(Math.Max(0, Math.Min(255, GreenValue * 255)));
                Fill.Fill = new SolidColorBrush(Color.FromRgb(Red, Green, 0));
            }
        }
    }
}
