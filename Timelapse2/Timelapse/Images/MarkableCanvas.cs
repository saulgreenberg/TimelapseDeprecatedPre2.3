using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Timelapse.Controls;
using Timelapse.Util;

namespace Timelapse.Images
{
    /// <summary>
    /// MarkableCanvas is a canvas that
    /// - contains an image that can be scaled and translated by the user with the mouse 
    /// - can draw and track markers atop the image
    /// - can show a magnified portion of the image in a magnifying glass
    /// - can save and restore a zoom+pan setting
    /// - can display a video 
    /// </summary>
    // SAULXXX Todd has a somewhat different solution to Markable Canvases, but it includes a pan/zoom bug that also affects magnifying glass position.
    public class MarkableCanvas : Canvas
    {
        private static readonly SolidColorBrush MarkerFillBrush = new SolidColorBrush(Color.FromArgb(2, 0, 0, 0));

        // bookmark for pan and zoom setting
        private ZoomBookmark bookmark;

        // the canvas to magnify contains both an image and markers so the magnifying glass view matches the display image
        private Canvas canvasToMagnify;

        // render transforms
        private ScaleTransform imageToDisplayScale;
        private TransformGroup transformGroup;
        private TranslateTransform imageToDisplayTranslation;

        private MagnifyingGlass magnifyingGlass;
        // increment for increasing or decreasing magnifying glass zoom
        private double magnifyingGlassZoomStep;

        private List<Marker> markers;

        // mouse and position states used to discriminate clicks from drags
        private UIElement mouseDownSender;
        private DateTime mouseDownTime;
        private DateTime mouseDoubleClickTime = DateTime.Now;
        private bool isDoubleClick = false;
        private Point mouseDownLocation;
        private Point previousMousePosition;

        /// <summary>
        /// Gets the image displayed across the MarkableCanvas for image files
        /// </summary>
        public Image ImageToDisplay { get; private set; }

        /// <summary>
        /// Gets the image displayed in the magnifying glass
        /// </summary>
        public Image ImageToMagnify { get; private set; }

        /// <summary>
        /// Gets the video displayed across the MarkableCanvas for video files
        /// </summary>
        public VideoPlayer VideoToDisplay { get; private set; }

        /// <summary>
        /// Gets or sets the markers on the image
        /// </summary>
        public List<Marker> Markers
        {
            get
            {
                return this.markers;
            }
            set
            {
                // update markers
                this.markers = value;
                // render new markers and update display image
                this.RedrawMarkers();
            }
        }

        /// <summary>
        /// Gets or sets the maximum zoom of the display image
        /// </summary>
        public double ZoomMaximum { get; set; }

        /// <summary>
        /// Gets or sets the amount we should zoom (scale) the image in the magnifying glass
        /// </summary>
        private double MagnifyingGlassZoom
        {
            get
            {
                return this.magnifyingGlass.Zoom;
            }
            set
            {
                // clamp the value
                if (value < Constant.MarkableCanvas.MagnifyingGlassMaximumZoom)
                {
                    value = Constant.MarkableCanvas.MagnifyingGlassMaximumZoom;
                }
                else if (value > Constant.MarkableCanvas.MagnifyingGlassMinimumZoom)
                {
                    value = Constant.MarkableCanvas.MagnifyingGlassMinimumZoom;
                }
                this.magnifyingGlass.Zoom = value;

                // update magnifier content if there is something to magnify
                if (this.ImageToMagnify.Source != null && this.ImageToDisplay.ActualWidth > 0)
                {
                    this.RedrawMagnifyingGlassIfVisible();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the magnifying glass is generally visible or hidden, and returns its state
        /// </summary>
        public bool MagnifyingGlassEnabled
        {
            get
            {
                return this.magnifyingGlass.IsEnabled;
            }
            set
            {
                this.magnifyingGlass.IsEnabled = value;
                if (value && this.VideoToDisplay.Visibility != Visibility.Visible)
                {
                    this.magnifyingGlass.Show();
                }
                else
                {
                    this.magnifyingGlass.Hide();
                }
            }
        }

        public event EventHandler<MarkerEventArgs> MarkerEvent;

        private void SendMarkerEvent(MarkerEventArgs e)
        {
            if (this.MarkerEvent != null)
            {
                this.MarkerEvent(this, e);
            }
        }

        public MarkableCanvas()
        {
            // configure self
            this.Background = Brushes.Black;
            this.ClipToBounds = true;
            this.Focusable = true;
            this.ResetMaximumZoom();
            this.SizeChanged += this.MarkableImageCanvas_SizeChanged;

            this.markers = new List<Marker>();

            // initialize render transforms
            // scale transform's center is set during layout once the image size is known
            // default bookmark is default zoomed out, normal pan state
            this.bookmark = new ZoomBookmark();
            this.imageToDisplayScale = new ScaleTransform(this.bookmark.Scale.X, this.bookmark.Scale.Y);
            this.imageToDisplayTranslation = new TranslateTransform(this.bookmark.Translation.X, this.bookmark.Translation.Y);

            this.transformGroup = new TransformGroup();
            this.transformGroup.Children.Add(this.imageToDisplayScale);
            this.transformGroup.Children.Add(this.imageToDisplayTranslation);

            // set up display image
            this.ImageToDisplay = new Image();
            this.ImageToDisplay.HorizontalAlignment = HorizontalAlignment.Left;
            this.ImageToDisplay.MouseDown += this.ImageOrCanvas_MouseDown;
            this.ImageToDisplay.MouseUp += this.ImageOrCanvas_MouseUp;
            this.ImageToDisplay.MouseWheel += this.ImageOrCanvas_MouseWheel;
            this.ImageToDisplay.RenderTransform = this.transformGroup;
            this.ImageToDisplay.SizeChanged += this.ImageToDisplay_SizeChanged;
            this.ImageToDisplay.VerticalAlignment = VerticalAlignment.Top;
            Canvas.SetLeft(this.ImageToDisplay, 0);
            Canvas.SetTop(this.ImageToDisplay, 0);
            this.Children.Add(this.ImageToDisplay);

            // set up display video
            this.VideoToDisplay = new VideoPlayer();
            this.VideoToDisplay.HorizontalAlignment = HorizontalAlignment.Left;
            this.VideoToDisplay.SizeChanged += this.VideoToDisplay_SizeChanged;
            this.VideoToDisplay.VerticalAlignment = VerticalAlignment.Top;
            Canvas.SetLeft(this.VideoToDisplay, 0);
            Canvas.SetTop(this.VideoToDisplay, 0);
            this.Children.Add(this.VideoToDisplay);

            // set up image to magnify
            this.ImageToMagnify = new Image();
            this.ImageToMagnify.HorizontalAlignment = HorizontalAlignment.Left;
            this.ImageToMagnify.SizeChanged += this.ImageToMagnify_SizeChanged;
            this.ImageToMagnify.VerticalAlignment = VerticalAlignment.Top;
            Canvas.SetLeft(this.ImageToMagnify, 0);
            Canvas.SetTop(this.ImageToMagnify, 0);

            this.canvasToMagnify = new Canvas();
            this.canvasToMagnify.SizeChanged += this.CanvasToMagnify_SizeChanged;
            this.canvasToMagnify.Children.Add(this.ImageToMagnify);

            // set up the magnifying glass
            this.magnifyingGlass = new MagnifyingGlass(this);
            this.magnifyingGlassZoomStep = Constant.MarkableCanvas.MagnifyingGlassZoomIncrement;

            Canvas.SetZIndex(this.magnifyingGlass, 1000); // Should always be in front
            this.Children.Add(this.magnifyingGlass);

            // event handlers for image interaction: keys, mouse handling for markers
            this.MouseLeave += this.ImageOrCanvas_MouseLeave;
            this.MouseMove += this.MarkableCanvas_MouseMove;
            this.PreviewKeyDown += this.MarkableCanvas_PreviewKeyDown;
            this.Loaded += this.MarkableCanvas_Loaded;
        }

        // Hide the magnifying glass initially, as the mouse pointer may not be atop the canvas
        private void MarkableCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            this.magnifyingGlass.Hide();
        }

        // Return to the zoom / pan levels saved as a bookmark
        public void ApplyBookmark()
        {
            this.bookmark.Apply(this.imageToDisplayScale, this.imageToDisplayTranslation);
            this.RedrawMarkers();
        }

        private void CanvasToMagnify_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // redraw markers so they're in the right place to appear in the magnifying glass
            this.RedrawMarkers();

            // update the magnifying glass's contents
            this.RedrawMagnifyingGlassIfVisible();
        }

        private Canvas DrawMarker(Marker marker, Size canvasRenderSize, bool doTransform)
        {
            Canvas markerCanvas = new Canvas();
            markerCanvas.MouseRightButtonUp += new MouseButtonEventHandler(this.Marker_MouseRightButtonUp);
            markerCanvas.MouseWheel += new MouseWheelEventHandler(this.ImageOrCanvas_MouseWheel); // Make the mouse wheel work over marks as well as the image

            if (marker.Tooltip.Trim() == String.Empty)
            {
                markerCanvas.ToolTip = null;
            }
            else
            {
                markerCanvas.ToolTip = marker.Tooltip;
            }
            markerCanvas.Tag = marker;

            // Create a marker
            Ellipse mark = new Ellipse();
            mark.Width = Constant.MarkableCanvas.MarkerDiameter;
            mark.Height = Constant.MarkableCanvas.MarkerDiameter;
            mark.Stroke = marker.Brush;
            mark.StrokeThickness = Constant.MarkableCanvas.MarkerStrokeThickness;
            mark.Fill = MarkableCanvas.MarkerFillBrush;
            markerCanvas.Children.Add(mark);

            // Draw another Ellipse as a black outline around it
            Ellipse blackOutline = new Ellipse();
            blackOutline.Stroke = Brushes.Black;
            blackOutline.Width = mark.Width + 1;
            blackOutline.Height = mark.Height + 1;
            blackOutline.StrokeThickness = 1;
            markerCanvas.Children.Add(blackOutline);

            // And another Ellipse as a white outline around it
            Ellipse whiteOutline = new Ellipse();
            whiteOutline.Stroke = Brushes.White;
            whiteOutline.Width = blackOutline.Width + 1;
            whiteOutline.Height = blackOutline.Height + 1;
            whiteOutline.StrokeThickness = 1;
            markerCanvas.Children.Add(whiteOutline);

            // maybe add emphasis
            double outerDiameter = whiteOutline.Width;
            Ellipse glow = null;
            if (marker.Emphasise)
            {
                glow = new Ellipse();
                glow.Width = whiteOutline.Width + Constant.MarkableCanvas.MarkerGlowDiameterIncrease;
                glow.Height = whiteOutline.Height + Constant.MarkableCanvas.MarkerGlowDiameterIncrease;
                glow.StrokeThickness = Constant.MarkableCanvas.MarkerGlowStrokeThickness;
                glow.Stroke = mark.Stroke;
                glow.Opacity = Constant.MarkableCanvas.MarkerGlowOpacity;
                markerCanvas.Children.Add(glow);

                outerDiameter = glow.Width;
            }

            markerCanvas.Width = outerDiameter;
            markerCanvas.Height = outerDiameter;

            double position = (markerCanvas.Width - mark.Width) / 2.0;
            Canvas.SetLeft(mark, position);
            Canvas.SetTop(mark, position);

            position = (markerCanvas.Width - blackOutline.Width) / 2.0;
            Canvas.SetLeft(blackOutline, position);
            Canvas.SetTop(blackOutline, position);

            position = (markerCanvas.Width - whiteOutline.Width) / 2.0;
            Canvas.SetLeft(whiteOutline, position);
            Canvas.SetTop(whiteOutline, position);

            if (marker.Emphasise)
            {
                position = (markerCanvas.Width - glow.Width) / 2.0;
                Canvas.SetLeft(glow, position);
                Canvas.SetTop(glow, position);
            }

            if (marker.ShowLabel)
            {
                Label label = new Label();
                label.Content = marker.Tooltip;
                label.Opacity = 0.6;
                label.Background = Brushes.White;
                label.Padding = new Thickness(0, 0, 0, 0);
                label.Margin = new Thickness(0, 0, 0, 0);
                markerCanvas.Children.Add(label);

                position = (markerCanvas.Width / 2.0) + (whiteOutline.Width / 2.0);
                Canvas.SetLeft(label, position);
                Canvas.SetTop(label, markerCanvas.Height / 2);
            }

            // Get the point from the marker, and convert it so that the marker will be in the right place
            Point screenPosition = Marker.ConvertRatioToPoint(marker.Position, canvasRenderSize.Width, canvasRenderSize.Height);
            if (doTransform)
            {
                screenPosition = this.transformGroup.Transform(screenPosition);
            }

            Canvas.SetLeft(markerCanvas, screenPosition.X - markerCanvas.Width / 2.0);
            Canvas.SetTop(markerCanvas, screenPosition.Y - markerCanvas.Height / 2.0);
            Canvas.SetZIndex(markerCanvas, 0);
            markerCanvas.MouseDown += this.ImageOrCanvas_MouseDown;
            markerCanvas.MouseMove += this.MarkableCanvas_MouseMove;
            markerCanvas.MouseUp += this.ImageOrCanvas_MouseUp;
            return markerCanvas;
        }

        private void DrawMarkers(Canvas canvas, Size canvasRenderSize, bool doTransform)
        {
            if (this.Markers != null)
            {
                foreach (Marker marker in this.Markers)
                {
                    Canvas markerCanvas = this.DrawMarker(marker, canvasRenderSize, doTransform);
                    canvas.Children.Add(markerCanvas);
                }
            }
        }

        // Whenever the image size changes, refresh the markers so they appear in the correct place
        private void ImageToDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.RedrawMarkers();
        }

        // On Mouse down, record the location, who sent it, and the time.
        // We will use this information on move and up events to discriminate between 
        // panning/zooming vs. marking. 
        private void ImageOrCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.previousMousePosition = e.GetPosition(this);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.mouseDownLocation = e.GetPosition(this.ImageToDisplay);
                this.mouseDownSender = (UIElement)sender;
                this.mouseDownTime = DateTime.Now;

                // If its more than the given time interval since the last click, then we are on the 2nd click of a potential double click\
                // So reset the time of the first click
                TimeSpan timeSinceLastClick = DateTime.Now - this.mouseDoubleClickTime;
                if (timeSinceLastClick.TotalMilliseconds < System.Windows.Forms.SystemInformation.DoubleClickTime)
                {
                    this.isDoubleClick = true;
                }
                else
                {
                    this.isDoubleClick = false;
                    this.mouseDoubleClickTime = DateTime.Now;
                }   
            }
        }

        // Hide the magnifying glass when the mouse cursor leaves the image
        private void ImageOrCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            this.magnifyingGlass.Hide();
        }

        private void ImageOrCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // Make sure the cursor reverts to the normal arrow cursor
            this.Cursor = Cursors.Arrow;

            // Get the current position
            Point mouseLocation = e.GetPosition(this.ImageToDisplay);

            // Is this the end of a translate operation, or of placing a marker?
            // We decide by checking if the left button has been released, the mouse location is
            // smaller than a given threshold, and less than 200 ms have passed since the original
            // mouse down. i.e., the use has done a rapid click and release on a small location
            if ((e.LeftButton == MouseButtonState.Released) &&
                (sender == this.mouseDownSender) &&
                (this.mouseDownLocation - mouseLocation).Length <= 2.0)
            {
                // If its more than the given time interval since we moused down and since the last click, then we are on the rapid 1st click of a potential double click
                // So reset the time of the first click
                TimeSpan timeSinceDown = DateTime.Now - this.mouseDownTime;
                if (timeSinceDown.TotalMilliseconds < 200 && this.isDoubleClick == false)
                {
                    // Get the current point, and create a marker on it.
                    Point position = e.GetPosition(this.ImageToDisplay);
                    position = Marker.ConvertPointToRatio(position, this.ImageToDisplay.ActualWidth, this.ImageToDisplay.ActualHeight);
                    Marker marker = new Marker(null, position);

                    // don't add marker to the marker list
                    // Main window is responsible for filling in remaining properties and adding it.
                    this.SendMarkerEvent(new MarkerEventArgs(marker, true));
                }
            }
            // Show the magnifying glass if its enables, as it may have been hidden during other mouseDown operations
            if (this.magnifyingGlass.IsEnabled)
            {
                this.magnifyingGlass.Show();
                this.RedrawMagnifyingGlassIfVisible();
            }
        }

        // Use the  mouse wheel to scale the image
        private void ImageOrCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            lock (this)
            {
                // We will scale around the current point
                Point mousePosition = e.GetPosition(this.ImageToDisplay);
                bool zoomIn = e.Delta > 0; // Zooming in if delta is positive, else zooming out
                this.ScaleImage(mousePosition, zoomIn);
            }
        }

        private void ImageToMagnify_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // keep the magnifying glass canvas in sync with the magnified image size
            // this update triggers a call to CanvasToMagnify_SizeChanged
            this.canvasToMagnify.Width = this.ImageToMagnify.ActualWidth;
            this.canvasToMagnify.Height = this.ImageToMagnify.ActualHeight;
        }

        /// <summary>
        /// Zoom in the magnifying glass image  by the amount defined by the property MagnifierZoomDelta
        /// </summary>
        public void MagnifierZoomIn()
        {
            this.MagnifyingGlassZoom -= this.magnifyingGlassZoomStep;
        }

        /// <summary>
        /// Zoom out the magnifying glass image  by the amount defined by the property MagnifierZoomDelta
        /// </summary>
        public void MagnifierZoomOut()
        {
            this.MagnifyingGlassZoom += this.magnifyingGlassZoomStep;
        }

        // If we move the mouse with the left mouse button press, translate the image
        private void MarkableCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // The visibility of the magnifying glass depends on whether the mouse is over the image
            // The magnifying glass is visible only if the current mouse position is over the image. 
            // Note that it uses the actual (transformed) bounds of the image
            Point mousePosition = e.GetPosition(this);
            if (this.magnifyingGlass.IsEnabled)
            {
                Point transformedSize = this.transformGroup.Transform(new Point(this.ImageToDisplay.ActualWidth, this.ImageToDisplay.ActualHeight));
                bool mouseOverImage = (mousePosition.X <= transformedSize.X) && (mousePosition.Y <= transformedSize.Y);
                if (mouseOverImage)
                {
                    this.magnifyingGlass.Show();
                }
                else
                {
                    this.magnifyingGlass.Hide();
                }
            }

            // Calculate how much time has passed since the mouse down event?
            TimeSpan timeSinceDown = DateTime.Now - this.mouseDownTime;

            // If at least WAIT_TIME milliseconds has passed
            if (timeSinceDown >= TimeSpan.FromMilliseconds(100))
            {
                // If the left button is pressed, translate (pan) across the scaled image 
                // We hide the magnifying glass during panning so it won't be distracting.
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    this.magnifyingGlass.Hide();
                    // Translation is possible only if the image isn't already scaled
                    if (this.imageToDisplayScale.ScaleX != 1.0 || this.imageToDisplayScale.ScaleY != 1.0)
                    {
                        this.Cursor = Cursors.ScrollAll;    // Change the cursor to a panning cursor
                        this.TranslateImage(mousePosition);
                    }
                }
                else
                {
                    this.canvasToMagnify.Width = this.ImageToMagnify.ActualWidth;      // Make sure that the canvas is the same size as the image
                    this.canvasToMagnify.Height = this.ImageToMagnify.ActualHeight;

                    // update the magnifying glass
                    this.RedrawMagnifyingGlassIfVisible();
                    // Ensure the cursor is a normal arrow cursor
                    this.Cursor = Cursors.Arrow;
                }

                this.previousMousePosition = mousePosition;
            }
        }

        // if it's < or > key zoom out or in around the mouse point
        private void MarkableCanvas_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Point mousePosition;
            switch (e.Key)
            {
                // zoom in
                case Key.OemPeriod:
                    Rect imageToDisplayBounds = new Rect(0.0, 0.0, this.ImageToDisplay.ActualWidth, this.ImageToDisplay.ActualHeight);
                    mousePosition = Mouse.GetPosition(this.ImageToDisplay);
                    if (imageToDisplayBounds.Contains(mousePosition) == false)
                    {
                        break; // ignore if mouse is not on the image
                    }
                    this.ScaleImage(mousePosition, true);
                    break;
                // zoom out
                case Key.OemComma:
                    mousePosition = Mouse.GetPosition(this.ImageToDisplay);
                    this.ScaleImage(mousePosition, false);
                    break;
                // if the current file's a video allow the user to hit the space bar to start or stop playing the video
                case Key.Space:
                    // This is desirable as the play or pause button doesn't necessarily have focus and it saves the user having to click the button with
                    // the mouse.
                    if (this.VideoToDisplay.TryPlayOrPause() == false)
                    {
                        return;
                    }
                    break;
                default:
                    return;
            }

            e.Handled = true;
        }

        // resize content and update transforms when canvas size changes
        private void MarkableImageCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.ImageToDisplay.Width = this.ActualWidth;
            this.ImageToDisplay.Height = this.ActualHeight;

            this.VideoToDisplay.Width = this.ActualWidth;
            this.VideoToDisplay.Height = this.ActualHeight;

            this.imageToDisplayScale.CenterX = 0.5 * this.ActualWidth;
            this.imageToDisplayScale.CenterY = 0.5 * this.ActualHeight;

            // clear the bookmark (if any) as it will no longer be correct
            // if needed, the bookmark could be rescaled instead
            this.bookmark.Reset();
        }

        // Remove a marker on a right mouse button up event
        private void Marker_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            Canvas canvas = (Canvas)sender;
            Marker marker = (Marker)canvas.Tag;
            this.Markers.Remove(marker);
            this.SendMarkerEvent(new MarkerEventArgs(marker, false));
            this.RedrawMarkers();
        }

        private void RedrawMagnifyingGlassIfVisible()
        {
            this.magnifyingGlass.RedrawIfVisible(NativeMethods.GetCursorPos(this),
                                                 NativeMethods.GetCursorPos(this.ImageToDisplay),
                                                 this.ImageToDisplay.ActualWidth,
                                                 this.ImageToDisplay.ActualHeight,
                                                 this.canvasToMagnify);
        }

        /// <summary>
        /// Remove all and then draw all the markers
        /// </summary>
        private void RedrawMarkers()
        {
            this.RemoveMarkers(this);
            this.RemoveMarkers(this.canvasToMagnify);
            if (this.ImageToDisplay != null)
            {
                this.DrawMarkers(this, this.ImageToDisplay.RenderSize, true);
                this.DrawMarkers(this.canvasToMagnify, this.canvasToMagnify.RenderSize, false);
            }
        }

        // remove all markers from the canvas
        private void RemoveMarkers(Canvas canvas)
        {
            for (int index = canvas.Children.Count - 1; index >= 0; index--)
            {
                if (canvas.Children[index] is Canvas && canvas.Children[index] != this.magnifyingGlass)
                {
                    canvas.Children.RemoveAt(index);
                }
            }
        }

        public void ResetMaximumZoom()
        {
            this.ZoomMaximum = Constant.MarkableCanvas.ImageZoomMaximum;
        }

        // Scale the image around the given image location point, where we are zooming in if
        // zoomIn is true, and zooming out if zoomIn is false
        public void ScaleImage(Point location, bool zoomIn)
        {
            // Get out of here if we are already at our maximum or minimum scaling values 
            // while zooming in or out respectively 
            if ((zoomIn && this.imageToDisplayScale.ScaleX >= this.ZoomMaximum) ||
                (!zoomIn && this.imageToDisplayScale.ScaleX <= Constant.MarkableCanvas.ImageZoomMinimum))
            {
                return;
            }

            // We will scale around the current point
            Point beforeZoom = this.PointFromScreen(this.ImageToDisplay.PointToScreen(location));

            // Calculate the scaling factor during zoom ins or out. Ensure that we keep within our
            // maximum and minimum scaling bounds. 
            if (zoomIn)
            {
                // We are zooming in
                // Calculate the scaling factor
                this.imageToDisplayScale.ScaleX *= Constant.MarkableCanvas.ImageZoomStep;   // Calculate the scaling factor
                this.imageToDisplayScale.ScaleY *= Constant.MarkableCanvas.ImageZoomStep;

                // Make sure we don't scale beyond the maximum scaling factor
                this.imageToDisplayScale.ScaleX = Math.Min(this.ZoomMaximum, this.imageToDisplayScale.ScaleX);
                this.imageToDisplayScale.ScaleY = Math.Min(this.ZoomMaximum, this.imageToDisplayScale.ScaleY);
            }
            else
            {
                // We are zooming out. 
                // Calculate the scaling factor
                this.imageToDisplayScale.ScaleX /= Constant.MarkableCanvas.ImageZoomStep;
                this.imageToDisplayScale.ScaleY /= Constant.MarkableCanvas.ImageZoomStep;

                // Make sure we don't scale beyond the minimum scaling factor
                this.imageToDisplayScale.ScaleX = Math.Max(Constant.MarkableCanvas.ImageZoomMinimum, this.imageToDisplayScale.ScaleX);
                this.imageToDisplayScale.ScaleY = Math.Max(Constant.MarkableCanvas.ImageZoomMinimum, this.imageToDisplayScale.ScaleY);

                // if there is no scaling, reset translations
                if (this.imageToDisplayScale.ScaleX == 1.0 && this.imageToDisplayScale.ScaleY == 1.0)
                {
                    this.imageToDisplayTranslation.X = 0.0;
                    this.imageToDisplayTranslation.Y = 0.0;
                }
            }

            Point afterZoom = this.PointFromScreen(this.ImageToDisplay.PointToScreen(location));

            // Scale the image, and at the same time translate it so that the 
            // point in the image under the cursor stays there
            lock (this.ImageToDisplay)
            {
                double imageWidth = this.ImageToDisplay.Width * this.imageToDisplayScale.ScaleX;
                double imageHeight = this.ImageToDisplay.Height * this.imageToDisplayScale.ScaleY;

                Point center = this.PointFromScreen(this.ImageToDisplay.PointToScreen(
                    new Point(this.ImageToDisplay.Width / 2.0, this.ImageToDisplay.Height / 2.0)));

                double newX = center.X - (afterZoom.X - beforeZoom.X);
                double newY = center.Y - (afterZoom.Y - beforeZoom.Y);

                if (newX - imageWidth / 2.0 >= 0.0)
                {
                    newX = imageWidth / 2.0;
                }
                else if (newX + imageWidth / 2.0 <= this.ActualWidth)
                {
                    newX = this.ActualWidth - imageWidth / 2.0;
                }

                if (newY - imageHeight / 2.0 >= 0.0)
                {
                    newY = imageHeight / 2.0;
                }
                else if (newY + imageHeight / 2.0 <= this.ActualHeight)
                {
                    newY = this.ActualHeight - imageHeight / 2.0;
                }

                this.imageToDisplayTranslation.X += newX - center.X;
                this.imageToDisplayTranslation.Y += newY - center.Y;
            }
            this.RedrawMarkers();
        }
        
        // Save the current zoom / pan levels as a bookmark
        public void SetBookmark()
        {
            // a user may want to flip between completely zoomed out / normal pan settings and a saved zoom / pan setting that focuses in on a particular region
            // To do this, we save / restore the zoom pan settings of a particular view, or return to the default zoom/pan.
            this.bookmark.Set(this.imageToDisplayScale, this.imageToDisplayTranslation);
        }

        /// <summary>
        /// Sets only the display image and leaves markers and the magnifier image unchanged.  Used by the differencing routines to set the difference image.
        /// </summary>
        public void SetDisplayImage(BitmapSource bitmapSource)
        {
            this.ImageToDisplay.Source = bitmapSource;
        }

        /// <summary>
        /// Set a wholly new image.  Clears existing markers and syncs the magnifier image to the display image.
        /// </summary>
        public void SetNewImage(BitmapSource bitmapSource, List<Marker> markers)
        {
            // change to new markres
            this.markers = markers;

            this.ImageToDisplay.Source = bitmapSource;
            // initiate render of magnified image
            // The asynchronous chain behind this is not entirely trivial.  The links are
            //   1) ImageToMagnify_SizeChanged fires and updates canvasToMagnify's size to match
            //   2) CanvasToMagnify_SizeChanged fires and redraws the magnified markers since the cavas size is now known and marker positions can update
            //   3) CanvasToMagnify_SizeChanged initiates a render on the magnifying glass to show the new image and marker positions
            //   4) if it's visible the magnifying glass content updates
            // This synchronization to WPF render opertations is necessary as, despite their appearance, properties like Source, Width, and Height are 
            // asynchronous.  Other approaches therefore tend to be subject to race conditions in render order which hide or misplace markers in the 
            // magnified view and also have a proclivity towards leaving incorrect or stale magnifying glass content on screen.
            // 
            // Another race exists as this.Markers can be set during the above rendering, initiating a second, concurrent marker render.  This is unavoidable
            // due to the need to expose a marker property but is mitigated by accepting new markers through this API and performing the set above as 
            // this.markers rather than this.Markers.
            this.ImageToMagnify.Source = bitmapSource;

            // ensure display image is visible
            this.ImageToDisplay.Visibility = Visibility.Visible;
            // SAULXXX DELETE COMMENTED CODE AFTER TESTING. IF ITS IN THERE, THEN THE MAG GLASS SHOWS UP WHEN WE ARE NAVIGATING
            //  ensure magnifying glass is visible if it's enabled
            //  if (this.MagnifyingGlassEnabled)
            //  {
            //      this.magnifyingGlass.Show();
            //  }

            // ensure any previous video is stopped and hidden
            if (this.VideoToDisplay.Visibility == Visibility.Visible)
            {
                this.VideoToDisplay.Pause();
                this.VideoToDisplay.Visibility = Visibility.Collapsed;
            }
        }

        public void SetNewVideo(FileInfo videoFile, List<Marker> markers)
        {
            if (videoFile.Exists == false)
            {
                this.SetNewImage(Constant.Images.FileNoLongerAvailable.Value, markers);
                return;
            }

            this.markers = markers;
            this.VideoToDisplay.SetSource(new Uri(videoFile.FullName));

            this.ImageToDisplay.Visibility = Visibility.Collapsed;
            this.magnifyingGlass.Hide();
            this.VideoToDisplay.Visibility = Visibility.Visible;
            // leave the magnifying glass's enabled state unchanged so user doesn't have to constantly keep re-enabling it in hybrid image sets
        }

        // Given the mouse location on the image, translate the image
        // This is normally called from a left mouse move event
        private void TranslateImage(Point mousePosition)
        {
            // Get the center point on the image
            Point center = this.PointFromScreen(this.ImageToDisplay.PointToScreen(new Point(this.ImageToDisplay.Width / 2.0, this.ImageToDisplay.Height / 2.0)));

            // Calculate the delta position from the last location relative to the center
            double newX = center.X + mousePosition.X - this.previousMousePosition.X;
            double newY = center.Y + mousePosition.Y - this.previousMousePosition.Y;

            // get the translated image width
            double imageWidth = this.ImageToDisplay.Width * this.imageToDisplayScale.ScaleX;
            double imageHeight = this.ImageToDisplay.Height * this.imageToDisplayScale.ScaleY;

            // Limit the delta position so that the image stays on the screen
            if (newX - imageWidth / 2.0 >= 0.0)
            {
                newX = imageWidth / 2.0;
            }
            else if (newX + imageWidth / 2.0 <= this.ActualWidth)
            {
                newX = this.ActualWidth - imageWidth / 2.0;
            }

            if (newY - imageHeight / 2.0 >= 0.0)
            {
                newY = imageHeight / 2.0;
            }
            else if (newY + imageHeight / 2.0 <= this.ActualHeight)
            {
                newY = this.ActualHeight - imageHeight / 2.0;
            }

            // Translate the canvas and redraw the markers
            this.imageToDisplayTranslation.X += newX - center.X;
            this.imageToDisplayTranslation.Y += newY - center.Y;

            this.RedrawMarkers();
        }

        // Whenever the image size changes, refresh the markers so they appear in the correct place
        private void VideoToDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.RedrawMarkers();
        }

        // Return to the zoomed out level, with no panning
        public void ZoomOutAllTheWay()
        {
            this.imageToDisplayScale.ScaleX = 1.0;
            this.imageToDisplayScale.ScaleY = 1.0;
            this.imageToDisplayTranslation.X = 0.0;
            this.imageToDisplayTranslation.Y = 0.0;
            this.RedrawMarkers();
        }
    }
}
