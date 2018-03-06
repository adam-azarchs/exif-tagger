using PhotoTagger.Imaging;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PhotoTagger.Wpf {
    /// <summary>
    /// Interaction logic for ImageZoomer.xaml
    /// </summary>
    public partial class ImageZoomer : Border {
        public ImageZoomer() {
            InitializeComponent();
        }

        public bool MoveToPan {
            get {
                return (bool)GetValue(MoveToPanProperty);
            }
            set {
                SetValue(MoveToPanProperty, value);
            }
        }

        public static readonly DependencyProperty MoveToPanProperty =
            DependencyProperty.Register(nameof(MoveToPan), typeof(bool),
                typeof(ImageZoomer),
                new PropertyMetadata(false, setHandleMouseMove));

        private static void setHandleMouseMove(
            DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ImageZoomer zoom &&
                e.OldValue is bool oldV &&
                e.NewValue is bool newV &&
                oldV != newV) {
                if (!newV) {
                    zoom.setupDrag();
                } else {
                    zoom.teardownDrag();
                }
            }
        }

        #region Mouse handlers

        private void teardownDrag() {
            this.Cursor = Cursors.Arrow;
            this.MouseLeftButtonDown -= onMouseLeftButtonDown;
            this.MouseLeftButtonUp -= onMouseLeftButtonUp;
        }

        private void setupDrag() {
            this.Cursor = Cursors.Hand;
            this.MouseLeftButtonDown += onMouseLeftButtonDown;
            this.MouseLeftButtonUp += onMouseLeftButtonUp;
        }

        private void onMouseWheel(object sender, MouseWheelEventArgs e) {
            Point scrollPos = e.GetPosition(this);
            this.dragStart = scrollPos;
            try {
                this.computing = true;

                double oldScale = this.Scale;
                this.Scale += oldScale * e.Delta / 1200.0;
                double delta = this.Scale - oldScale;
                Size size = this.DesiredSize;
                Size crop = this.RenderSize;
                this.ImageX += scrollPos.X * delta * size.Width / crop.Width;
                this.ImageY += scrollPos.Y * delta * size.Height / crop.Height;
            } finally {
                this.computing = false;
            }
            this.computeTransform();
        }

        private Point dragStart;

        private void onMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            this.CaptureMouse();
            this.setDragStart(sender, e);
        }

        private void setDragStart(object sender, MouseEventArgs e) {
            this.dragStart = e.GetPosition(this);
        }

        private void onMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            this.ReleaseMouseCapture();
        }

        private void onMouseMove(object sender, MouseEventArgs e) {
            var newPos = e.GetPosition(this);
            if (this.IsMouseCaptured) {
                this.ImageX -= newPos.X - this.dragStart.X;
                this.ImageY -= newPos.Y - this.dragStart.Y;
            } else if (MoveToPan && this.Scale > 1) {
                Size crop = this.RenderSize;
                Size imgSize = this.DesiredSize;
                newPos.X -= (crop.Width - imgSize.Width) / 2;
                newPos.Y -= (crop.Height - imgSize.Height) / 2;
                double sc = Scale - 1;
                this.ImageX = newPos.X * sc;
                this.ImageY = newPos.Y * sc;
            }
            this.dragStart = newPos;
        }

        private void onTouchDown(object sender, TouchEventArgs e) {
            this.dragStart = e.GetTouchPoint(this).Position;
        }

        private void onTouchMove(object sender, TouchEventArgs e) {
            var newPos = e.GetTouchPoint(this).Position;
            this.ImageX -= newPos.X - this.dragStart.X;
            this.ImageY -= newPos.Y - this.dragStart.Y;
            this.dragStart = newPos;
        }

        private void onRightclick(object sender, MouseButtonEventArgs e) {
            if (this.Scale > 1) {
                this.Scale = 1;
            } else if (this.Source != null) {
                Size crop = this.RenderSize;
                Size imgSize = this.DesiredSize;
                var width = this.FullWidth ??
                    (int)Math.Ceiling(this.Source.Width);
                var height = this.FullHeight ??
                    (int)Math.Ceiling(this.Source.Height);
                this.Scale = Math.Max(
                    height / crop.Height,
                    width / crop.Width);
                double sc = Scale - 1;
                var pos = e.GetPosition(this);
                pos.X -= (crop.Width - imgSize.Width) / 2;
                pos.Y -= (crop.Height - imgSize.Height) / 2;
                this.ImageX = pos.X * sc;
                this.ImageY = pos.Y * sc;
            }
        }

        #endregion

        public Transform ImageTransform {
            get {
                return (Transform)GetValue(ImageTransformProperty);
            }
            private set {
                SetValue(ImageTransformProperty, value);
            }
        }

        public static readonly DependencyProperty ImageTransformProperty =
            DependencyProperty.Register(nameof(ImageTransform),
                typeof(Transform),
                typeof(ImageZoomer));


        public Photo Photo {
            get {
                return (Photo)GetValue(PhotoProperty);
            }
            set {
                SetValue(PhotoProperty, value);
            }
        }

        public static readonly DependencyProperty PhotoProperty =
            DependencyProperty.Register(nameof(Photo), typeof(Photo),
                typeof(ImageZoomer),
                new PropertyMetadata(null, zoomChangedCallback));


        public int? FullWidth {
            get {
                return this.Photo?.Width;
            }
        }

        public int? FullHeight {
            get {
                return this.Photo?.Height;
            }
        }


        public ImageSource Source {
            get {
                return this.Photo?.FullImage;
            }
        }

        public double ImageX {
            get {
                return (double)GetValue(ImageXProperty);
            }
            set {
                SetValue(ImageXProperty, value);
            }
        }

        public static readonly DependencyProperty ImageXProperty =
            DependencyProperty.Register(nameof(ImageX),
                typeof(double), typeof(ImageZoomer),
                new PropertyMetadata(0.0, zoomChangedCallback));

        public double ImageY {
            get {
                return (double)GetValue(ImageYProperty);
            }
            set {
                SetValue(ImageYProperty, value);
            }
        }

        public static readonly DependencyProperty ImageYProperty =
            DependencyProperty.Register(nameof(ImageY),
                typeof(double), typeof(ImageZoomer),
                new PropertyMetadata(0.0, zoomChangedCallback));

        private static void zoomChangedCallback(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e) {
            if (d is ImageZoomer zoom && e.OldValue != e.NewValue) {
                zoom.computeTransform();
            }
        }

        public double Scale {
            get {
                return (double)GetValue(ScaleProperty);
            }
            set {
                SetValue(ScaleProperty, value);
            }
        }

        public static readonly DependencyProperty ScaleProperty =
            DependencyProperty.Register(nameof(Scale), typeof(double),
                typeof(ImageZoomer),
                new PropertyMetadata(1.0, zoomChangedCallback));

        private bool computing = false;

        private void computeTransform() {
            if (computing) {
                return;
            }
            try {
                this.computing = true;
                ImageSource source = this.Source;
                if (source == null) {
                    return;
                }

                // First compute the scale.  The minimum scale is largest value
                // such that the image fits in the container.
                var scale = this.Scale;
                if (scale < 1) {
                    // Very small scales start running into
                    // numerical precision issues.
                    scale = 1;
                    this.Scale = scale;
                }

                Size crop = this.DesiredSize;
                var scaledWidth = crop.Width * scale;
                var scaledHeight = crop.Height * scale;
                // Clamp X and Y to avoid empty space on the left/top.
                var x = Math.Max(0.0,
                    Math.Min(this.ImageX,
                    scaledWidth - crop.Width));
                var y = Math.Max(0.0,
                    Math.Min(this.ImageY,
                    scaledHeight - crop.Height));
                // Set the values back.
                this.ImageX = x;
                this.ImageY = y;
                setTransform(scale, x, y);
            } finally {
                this.computing = false;
            }
        }

        private void setTransform(double scale, double x, double y) {
            if (this.ImageTransform is TransformGroup t) {
                var scaleT = (ScaleTransform)t.Children
                    .First(tr => tr is ScaleTransform);
                scaleT.ScaleX = scale;
                scaleT.ScaleY = scale;
                var transT = (TranslateTransform)t
                    .Children.First(tr => tr is TranslateTransform);
                transT.X = -x;
                transT.Y = -y;

            } else {
                var scaleT = new ScaleTransform(scale, scale);
                var transT = new TranslateTransform(-x, -y);
                var group = new TransformGroup();
                group.Children.Add(scaleT);
                group.Children.Add(transT);
                this.ImageTransform = group;
            }
        }

        private void sizeChanged(object sender, SizeChangedEventArgs e) {
            this.computeTransform();
        }
    }
}
