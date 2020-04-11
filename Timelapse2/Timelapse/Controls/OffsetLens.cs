using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Controls
{
    public class OffsetLens : Magnifier
    {
        private Point Offset = new Point(125, -125);
        private MagHandleAdorner magHandleAdorner;
        private AdornerLayer myAdornerLayer;
        public OffsetLens()
        {
            // Lens appearance
            this.Radius = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2;
            this.BorderBrush = MakeOutlineBrush();
            this.BorderThickness = new Thickness(3);
            this.Background = Brushes.Black;
            this.FrameType = FrameType.Circle;
            this.Loaded += this.OffsetLens_Loaded;

            // Makes mouse wheel operations (usually used to change the magnification level) into a no-op
            this.ZoomFactorOnMouseWheel = 0;
            this.IsUsingZoomOnMouseWheel = false;   
        }

        private void OffsetLens_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.magHandleAdorner.Visibility = ((bool)e.NewValue) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OffsetLens_Loaded(object sender, RoutedEventArgs e)
        {
            this.IsEnabled = true;
            // Lens transformation
            TranslateTransform tt = new TranslateTransform(this.Offset.X, this.Offset.Y);
            this.RenderTransform = tt;
            this.ZoomFactor = 0.4;

            // Handle adorner
            myAdornerLayer = AdornerLayer.GetAdornerLayer(this);
            //myAdornerLayer.IsHitTestVisible = false;
            magHandleAdorner = new MagHandleAdorner(this, this.Offset);
            magHandleAdorner.IsHitTestVisible = false;
            magHandleAdorner.RenderTransform = tt;
            myAdornerLayer.Add(magHandleAdorner);
            this.IsVisibleChanged += this.OffsetLens_IsVisibleChanged;
        }


        private LinearGradientBrush MakeOutlineBrush()
        {
            LinearGradientBrush outlineBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            ColorConverter cc = new ColorConverter();
            outlineBrush.GradientStops.Add(new GradientStop((Color)cc.ConvertFrom("#AAA"), 0));
            outlineBrush.GradientStops.Add(new GradientStop((Color)cc.ConvertFrom("#111"), 1));
            return outlineBrush;
        }
    }

    public class MagHandleAdorner : Adorner
    {
        private Point Offset;

        public MagHandleAdorner(UIElement adornedElement, Point offset)
         : base(adornedElement)
        {
            this.Offset = offset;
        }
        // A common way to implement an adorner's rendering behavior is to override the OnRender
        // method, which is called by the layout system as part of a rendering pass.
        protected override void OnRender(DrawingContext drawingContext)
        {
            if (drawingContext == null)
            {
                System.Diagnostics.Debug.Print(nameof(drawingContext) + " null");
            }
            Rect adornedElementRect = new Rect(this.AdornedElement.DesiredSize);
            int centerOffset = 75;
            //Point handleStartOffset = new Point(20, -20);
            Point handleStartOffset = new Point(0, 0);
            Point handleEndOffset = new Point(-39, 39);
            Point center = new Point(adornedElementRect.Width / 2, adornedElementRect.Height / 2);
            Point centerLeft = PointSubtract(center, new Point(-centerOffset, 0));
            Point centerRight = PointSubtract(center, new Point(centerOffset, 0));
            Point centerTop = PointSubtract(center, new Point(0, -centerOffset));
            Point centerBottom = PointSubtract(center, new Point(0, centerOffset));
            Point handleStart = PointSubtract(adornedElementRect.BottomLeft, handleStartOffset);
            Point handleEnd = PointSubtract(adornedElementRect.BottomLeft, handleEndOffset);

            // Draw the handle
            Pen handlePen = new Pen(new SolidColorBrush(Colors.Green), 4);
            drawingContext.DrawLine(handlePen, handleStart, handleEnd) ;
            drawingContext.DrawLine(handlePen, handleStart, handleEnd);

            // Draw the crosshairs
            Pen crosshairPen = new Pen(new SolidColorBrush(Colors.LightGray), .5);
            drawingContext.DrawLine(crosshairPen, centerLeft, centerRight);
            drawingContext.DrawLine(crosshairPen, centerTop, centerBottom);
        }

        private Point PointSubtract(Point p1, Point p2)
        {
            return new Point(p1.X - p2.X, p1.Y - p2.Y);
        }
    }
}
