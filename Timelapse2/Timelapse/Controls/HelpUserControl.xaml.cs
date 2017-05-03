using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Resources;

namespace Timelapse.Controls
{
    /// <summary>
    /// Create a control - a grid - that contains a flow document with all the help information
    /// </summary>
    public partial class HelpUserControl : UserControl
    {
        // Substitute for parameter passing: 
        // The HelpFileProperty/ HelpFile lets us specify the location of the helpfile resource in the XAML
        // The Xaml using this user control should contain something like HelpFile="pack://application:,,/Resources/TimelapseHelp.rtf"
        public static readonly DependencyProperty HelpFileProperty =
            DependencyProperty.Register("HelpFile", typeof(string), typeof(HelpUserControl));
        public string HelpFile
        {
            get { return this.GetValue(HelpFileProperty) as string; }
            set { this.SetValue(HelpFileProperty, value); }
        }

        public HelpUserControl()
        {
            this.InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.CreateFlowDocument();
        }

        // Create a flow document containing the contents of the resource specified in HelpFile
        private void CreateFlowDocument()
        {
            FlowDocument flowDocument = new FlowDocument();
            try
            {
                // create a string containing the help text from the rtf help file
                StreamResourceInfo sri = Application.GetResourceStream(new Uri(this.HelpFile));
                StreamReader reader = new StreamReader(sri.Stream);
                string helpText = reader.ReadToEnd();

                // Write the help text to a stream
                MemoryStream stream = new MemoryStream();    
                StreamWriter writer = new StreamWriter(stream);
                writer.Write(helpText);
                writer.Flush();

                // Load the entire text into the Flow Document
                TextRange textRange = new TextRange(flowDocument.ContentStart, flowDocument.ContentEnd);
                textRange.Load(stream, DataFormats.Rtf);
            }
            catch
            {
                // We couldn't get the help file. Display a generic message instead
                flowDocument.FontFamily = new FontFamily("Segui UI");
                flowDocument.FontSize = 14;
                Paragraph p1 = new Paragraph();
                p1.Inlines.Add("Brief instructions are currently unavailable.");
                p1.Inlines.Add(Environment.NewLine + Environment.NewLine);
                p1.Inlines.Add("If you need help, please download and read the ");
                Hyperlink h1 = new Hyperlink();
                h1.Inlines.Add("Timelapse Tutorial Manual");
                h1.NavigateUri = Constant.UserManualAddress;
                h1.RequestNavigate += this.Link_RequestNavigate;
                p1.Inlines.Add(h1);
                flowDocument.Blocks.Add(p1);
            }
            // Add the document to the FlowDocumentScollViewer, converting hyperlinks to active links
            this.SubscribeToAllHyperlinks(flowDocument);
            this.ScrollViewer.Document = flowDocument;
        }

        #region Activate all hyperlinks in the flow document
        private void SubscribeToAllHyperlinks(FlowDocument flowDocument)
        {
            var hyperlinks = GetVisuals(flowDocument).OfType<Hyperlink>();
            foreach (var link in hyperlinks)
            {
                link.RequestNavigate += new System.Windows.Navigation.RequestNavigateEventHandler(this.Link_RequestNavigate);
            }
        }

        private static IEnumerable<DependencyObject> GetVisuals(DependencyObject root)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                yield return child;
                foreach (var descendants in GetVisuals(child))
                {
                    yield return descendants;
                }
            }
        }

        // Load the Uri provided in a web browser  
        private void Link_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
        #endregion
    }
}
