using System;
using System.Collections.Generic;
//using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Timelapse.Images
{
    static class BitmapUtilities
    {
        static public BitmapSource GetBitmapFromFile(string path, Nullable<int> desiredWidth = null, BitmapCacheOption bitmapCacheOption = BitmapCacheOption.OnDemand)
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

        public static BitmapSource GetBitmapFromFileWithPlayButton(string path, Nullable<int> desiredWidth = null, BitmapCacheOption bitmapCacheOption = BitmapCacheOption.OnDemand)
        {
            BitmapSource bmp = BitmapUtilities.GetBitmapFromFile(path, desiredWidth, bitmapCacheOption);
            RenderTargetBitmap target = new RenderTargetBitmap(bmp.PixelWidth, bmp.PixelHeight, bmp.DpiX, bmp.DpiY, PixelFormats.Pbgra32);
            DrawingVisual visual = new DrawingVisual();

            using (DrawingContext r = visual.RenderOpen())
            {
                float radius = 20;

                // We will draw based on the center of the bitmap
                Point center = new Point(bmp.Width/2, bmp.Height/2);
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
                SolidColorBrush triangleBrush = new SolidColorBrush(Colors.LightBlue);
                triangleBrush.Opacity = 0.5;
                SolidColorBrush circleBrush = new SolidColorBrush(Colors.White);
                circleBrush.Opacity = 0.5;

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
                Point v = new Point();
                v.X = center.X + radius * (float)Math.Cos(i * 2 * Math.PI / 3);
                v.Y = center.Y + radius * (float)Math.Sin(i * 2 * Math.PI / 3);
                points.Add(v);
            }
            return points;
        }
    }
}
