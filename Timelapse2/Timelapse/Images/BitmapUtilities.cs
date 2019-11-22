using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Timelapse.Images
{
    public static class BitmapUtilities
    {
        // TODO Check this, as we changed the bitmap cache options from OnDemand to OnLoad to ensure file deletions would work
        public static BitmapSource GetBitmapFromFile(string path, Nullable<int> desiredWidth = null, BitmapCacheOption bitmapCacheOption = BitmapCacheOption.OnLoad)
        {
            Uri uri = new Uri(path);
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = bitmapCacheOption;
            if (desiredWidth != null)
            {
                bitmap.DecodePixelWidth = desiredWidth.Value;
            }
            bitmap.UriSource = uri;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        // Return true only if the file exists and we can actually create a bitmap image from it
        public static bool IsBitmapFileDisplayable(string path)
        {
            if (File.Exists(path) == false)
            {
                return false;
            }  
            try
            {
                // we assume its a valid path
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.DecodePixelWidth = 1; // We try to generate a trivial thumbnail, as that suffices to know if this is a valid jpg;
                bitmap.DecodePixelHeight = 1; // We try to generate a trivial thumbnail, as that suffices to know if this is a valid jpg;
                // TODO Check this, as we changed the bitmap cache options from Default to OnLoad to ensure file deletions would work
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // TODO Check this, as we changed the bitmap cache options from OnDemand to OnLoad to ensure file deletions would work
        public static BitmapSource GetBitmapFromFileWithPlayButton(string path, Nullable<int> desiredWidth = null, BitmapCacheOption bitmapCacheOption = BitmapCacheOption.OnLoad)
        {
            BitmapSource bmp = BitmapUtilities.GetBitmapFromFile(path, desiredWidth, bitmapCacheOption);
            RenderTargetBitmap target = new RenderTargetBitmap(bmp.PixelWidth, bmp.PixelHeight, bmp.DpiX, bmp.DpiY, PixelFormats.Pbgra32);
            DrawingVisual visual = new DrawingVisual();

            using (DrawingContext r = visual.RenderOpen())
            {
                float radius = 20;

                // We will draw based on the center of the bitmap
                Point center = new Point(bmp.Width / 2, bmp.Height / 2);
                PointCollection trianglePoints = GetTriangleVerticesInscribedInCircle(center, radius);

                // Construct the triangle
                StreamGeometry triangle = new StreamGeometry();
                using (StreamGeometryContext geometryContext = triangle.Open())
                {
                    geometryContext.BeginFigure(trianglePoints[0], true, true);
                    PointCollection points = new PointCollection
                                             {
                                                trianglePoints[1],
                                                trianglePoints[2]
                                             };
                    geometryContext.PolyLineTo(points, true, true);
                }

                // Define the translucent bruches for the triangle an circle
                SolidColorBrush triangleBrush = new SolidColorBrush(Colors.LightBlue)
                {
                    Opacity = 0.5
                };

                SolidColorBrush circleBrush = new SolidColorBrush(Colors.White)
                {
                    Opacity = 0.5
                };

                // Draw everything
                r.DrawImage(bmp, new Rect(0, 0, bmp.Width, bmp.Height));
                r.DrawGeometry(triangleBrush, null, triangle);
                r.DrawEllipse(circleBrush, null, center, radius + 5, radius + 5);
            }
            target.Render(visual);
            return target;
        }

        // Return  3 points (vertices) that inscribe a triangle into the circle defined by a center point and a radius, 
        private static PointCollection GetTriangleVerticesInscribedInCircle(Point center, float radius)
        {
            PointCollection points = new PointCollection();
            for (int i = 0; i < 3; i++)
            {
                Point v = new Point
                {
                    X = center.X + radius * (float)Math.Cos(i * 2 * Math.PI / 3),
                    Y = center.Y + radius * (float)Math.Sin(i * 2 * Math.PI / 3)
                };
                points.Add(v);
            }
            return points;
        }
    }
}
