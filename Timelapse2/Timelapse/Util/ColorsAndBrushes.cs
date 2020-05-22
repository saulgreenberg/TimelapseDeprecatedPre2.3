using System.Windows.Media;

namespace Timelapse.Util
{
    public static class ColorsAndBrushes
    {
        /// <summary
        /// Create a brush with the given opacity and color as hex e.g., 255, #FF00BFFF
        /// Opacity should be between 0 - 255 if provided, otherwise defaults to 255 (no transparency)
        public static SolidColorBrush SolidColorBrushFromColor(string colorAsHex, int opacity = 255)
        {
            SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorAsHex));
            brush.Opacity = opacity;
            return brush;
        }

        public static SolidColorBrush SolidColorBrushFromColor(Color color, int opacity = 255)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Opacity = opacity;
            return brush;
        }

    }
}
