using System;
using System.Linq;
using System.Windows;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;
namespace Timelapse
{
    // AvalonDock callbacks and methods
    public partial class TimelapseWindow : Window, IDisposable
    {
        // Property changing callback
        public void LayoutAnchorable_PropertyChanging(object sender, System.ComponentModel.PropertyChangingEventArgs e)
        {
            LayoutAnchorable la = sender as LayoutAnchorable;
            if (la.ContentId == "ContentIDDataEntryControlPanel" && (e.PropertyName == Constant.AvalonDock.FloatingWindowFloatingHeightProperty || e.PropertyName == Constant.AvalonDock.FloatingWindowFloatingWidthProperty))
            {
                DockingManager_FloatingDataEntryWindowLimitSize();
            }
            this.FindBoxVisibility(false);
        }

        // Layout Updated 
        private void DockingManager_LayoutUpdated(object sender, EventArgs e)
        {
            if (this.DockingManager.FloatingWindows.Count() > 0)
            {
                this.DockingManager_FloatingDataEntryWindowWindowTopmost(false);
            }
        }

        // Enable or disable floating windows normally always being on top. 
        // Also shows floating windows in the task bar if it can be hidden
        private void DockingManager_FloatingDataEntryWindowWindowTopmost(bool topMost)
        {
            foreach (LayoutFloatingWindowControl floatingWindow in this.DockingManager.FloatingWindows)
            {
                // This checks to see if its the data entry window, which is the only layoutanchorable present.
                // If its not, then the value will be null (i.e., its the DataGrid layoutdocument)
                LayoutAnchorableFloatingWindow model = floatingWindow.Model as LayoutAnchorableFloatingWindow;
                if (model == null)
                {
                    // SAULXXX: Note that the Floating DocumentPane (i.e., the DataGrid) behaviour is not what we want
                    // That is, it always appears topmost. yet if we set it to null, then it disappears behind the main 
                    // window when the mouse is moved over the main window (vs. clicking in it).
                    floatingWindow.Owner = null;
                    floatingWindow.ShowInTaskbar = true;
                    continue;
                }
                floatingWindow.MinHeight = Constant.AvalonDock.FloatingWindowMinimumHeight;
                floatingWindow.MinWidth = Constant.AvalonDock.FloatingWindowMinimumWidth;

                if (topMost)
                {
                    if (floatingWindow.Owner == null)
                    {
                        floatingWindow.Owner = this;
                    }
                }
                else if (floatingWindow.Owner != null)
                {
                    // Set this to null if we want windows NOT to appear always on top, otherwise to true
                    // floatingWindow.Owner = null;
                    floatingWindow.ShowInTaskbar = true;
                }
            }
        }

        // Helper methods - used only by the above methods 

        // When a floating window is resized, limit it to the size of the scrollviewer.
        // SAULXX: Limitatinons, I think it also applies to the instructions and data pane floating windows!
        private void DockingManager_FloatingDataEntryWindowLimitSize()
        {
            foreach (var floatingWindow in this.DockingManager.FloatingWindows)
            {
                // This checks to see if its the data entry window, which is the only layoutanchorable present.
                // If its not, then the value will be null (i.e., its the DataGrid layoutdocument)
                LayoutAnchorableFloatingWindow model = floatingWindow.Model as LayoutAnchorableFloatingWindow;
                if (model == null)
                {
                    continue;
                }
                if (floatingWindow.HasContent)
                {
                    if (floatingWindow.Height > this.DataEntryScrollViewer.ActualHeight)
                    {
                        floatingWindow.Height = this.DataEntryScrollViewer.ActualHeight + Constant.AvalonDock.FloatingWindowLimitSizeHeightCorrection;
                    }
                    if (floatingWindow.Width > this.DataEntryScrollViewer.ActualWidth)
                    {
                        floatingWindow.Width = this.DataEntryScrollViewer.ActualWidth + Constant.AvalonDock.FloatingWindowLimitSizeWidthCorrection;
                    }
                }
            }
        }
    }
}
