using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace smx_config
{
    // A button with a selectable highlight.
    public class SelectableButton : Button
    {
        public static readonly DependencyProperty SelectedProperty = DependencyProperty.Register("Selected",
            typeof(bool), typeof(SelectableButton), new FrameworkPropertyMetadata(false));
        public bool Selected
        {
            get { return (bool)GetValue(SelectedProperty); }
            set { SetValue(SelectedProperty, value); }
        }
    }


    // This is a Slider class with some added helpers.
    public class Slider2 : Slider
    {
        public delegate void DragEvent();
        public event DragEvent? StartedDragging, StoppedDragging;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            Track track = (Template.FindName("PART_Track", this) as Track)!;
            var Thumb = track.Thumb;

            // Fix the slider not dragging after clicking outside the thumb.
            // http://stackoverflow.com/a/30575638/136829
            bool clickedInSlider = false;
            MouseMove += delegate (object sender, MouseEventArgs args)
            {
                if (args.LeftButton == MouseButtonState.Released || !clickedInSlider || Thumb.IsDragging)
                    return;

                Thumb.RaiseEvent(new MouseButtonEventArgs(args.MouseDevice, args.Timestamp, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    Source = args.Source,
                });
            };

            AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new RoutedEventHandler((sender, args) =>
            {
                clickedInSlider = true;
            }), true);

            AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new RoutedEventHandler((sender, args) =>
            {
                clickedInSlider = false;
            }), true);
        }

        // How are there no events for this?
        protected override void OnThumbDragStarted(DragStartedEventArgs e)
        {
            base.OnThumbDragStarted(e);
            StartedDragging?.Invoke();
        }

        protected override void OnThumbDragCompleted(DragCompletedEventArgs e)
        {
            base.OnThumbDragCompleted(e);
            StoppedDragging?.Invoke();
        }

    };
}
