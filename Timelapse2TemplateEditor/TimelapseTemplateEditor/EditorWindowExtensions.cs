using System.Windows;

namespace Timelapse.Editor
{
    public class EditorWindowExtensions
    {
        private static readonly DependencyProperty ChoiceListProperty =
            DependencyProperty.RegisterAttached("ChoiceList", typeof(string), typeof(EditorWindowExtensions), new PropertyMetadata(default(string)));

        public static void SetChoiceList(UIElement element, string value)
        {
            element.SetValue(ChoiceListProperty, value);
        }

        public static string GetChoiceList(UIElement element)
        {
            return (string)element.GetValue(ChoiceListProperty);
        }
    }
}
