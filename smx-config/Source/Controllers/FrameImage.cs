using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace smx_config
{
    public class FrameImage : Image
    {
        // The source image.  Changing this after load isn't supported.
        public static readonly DependencyProperty ImageProperty = DependencyProperty.Register("Image",
            typeof(BitmapSource), typeof(FrameImage), new FrameworkPropertyMetadata(null, ImageChangedCallback));

        public BitmapSource Image
        {
            get { return (BitmapSource)this.GetValue(ImageProperty); }
            set { this.SetValue(ImageProperty, value); }
        }

        // Which frame is currently displayed:
        public static readonly DependencyProperty FrameProperty = DependencyProperty.Register("Frame",
            typeof(int), typeof(FrameImage), new FrameworkPropertyMetadata(0, FrameChangedCallback));

        public int Frame
        {
            get { return (int)this.GetValue(FrameProperty); }
            set { this.SetValue(FrameProperty, value); }
        }

        public static readonly DependencyProperty FramesXProperty = DependencyProperty.Register("FramesX",
            typeof(int), typeof(FrameImage), new FrameworkPropertyMetadata(0, ImageChangedCallback));

        public int FramesX
        {
            get { return (int)this.GetValue(FramesXProperty); }
            set { this.SetValue(FramesXProperty, value); }
        }

        private static void ImageChangedCallback(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            FrameImage self = target as FrameImage;
            self.Load();
        }

        private static void FrameChangedCallback(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            FrameImage self = target as FrameImage;
            self.Refresh();
        }

        private BitmapSource[] ImageFrames;

        private void Load()
        {
            if (Image == null || FramesX == 0)
            {
                ImageFrames = null;
                return;
            }

            // Split the image into frames.
            int FrameWidth = Image.PixelWidth / FramesX;
            int FrameHeight = Image.PixelHeight;
            ImageFrames = new BitmapSource[FramesX];
            for (int i = 0; i < FramesX; ++i)
                ImageFrames[i] = new CroppedBitmap(Image, new Int32Rect(FrameWidth * i, 0, FrameWidth, FrameHeight));

            Refresh();
        }

        private void Refresh()
        {
            if (ImageFrames == null || Frame >= ImageFrames.Length)
            {
                this.Source = null;
                return;
            }

            this.Source = ImageFrames[Frame];
        }
    };
}
