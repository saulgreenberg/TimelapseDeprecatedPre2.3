using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.Enums;

namespace Timelapse.Controls
{
    // A note lays out a stack panel containing
    // - a label containing the descriptive label) 
    // - an editable textbox (containing the content) at the given width
    public class DataEntryNote : DataEntryControl<AutocompleteTextBox, Label>
    {
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft
        {
            get { return this.ContentControl.PointToScreen(new Point(0, 0)); }
        }
        public override UIElement GetContentControl
        {
            get { return this.ContentControl; }
        }

        public override bool IsContentControlEnabled
        {
            get { return this.ContentControl.IsEnabled; }
        }

        /// <summary>Gets  the content of the note</summary>
        public override string Content
        {
            get { return this.ContentControl.Text; }
        }

        public bool ContentChanged { get; set; }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public DataEntryNote(ControlRow control, List<string> autocompletions, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyleEnum.NoteTextBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // Now configure the various elements
            this.ContentControl.Autocompletions = autocompletions;
            this.ContentChanged = false;
        }
        public override void SetContentAndTooltip(string value)
        {
            // If the value is null, an ellipsis will be drawn in the checkbox (see Checkbox style)
            // Used to signify the indeterminate state in no or multiple selections in the overview.
            if (value == null)
            {
                this.ContentControl.Text = Constant.Unicode.Ellipsis;
                this.ContentControl.ToolTip = "Edit to change the " + this.Label + " for all selected images";
                return;
            }

            // Otherwise, the note will be set to the provided value 
            // If the value to be empty, we just make it the same as the tooltip so something meaningful is displayed..
            this.ContentChanged = this.ContentControl.Text != value;
            this.ContentControl.Text = value;
            this.ContentControl.ToolTip = String.IsNullOrEmpty(value) ? value : this.LabelControl.ToolTip;
        }

        #region Visual Effects and Popup Previews
        // Flash the ContentControl background
        public override void FlashContentControlValue()
        {
            //base.FlashContentControl();
            //Border border = (Border)this.ContentControl.Template.FindName("checkBoxBorder", this.ContentControl);
            //if (border != null)
            //{
                this.ContentControl.Background.BeginAnimation(SolidColorBrush.ColorProperty, base.GetColorAnimation());
            //}
        }

        public override void FlashPreviewControlValue()
        {
            this.FlashPopupPreview();
        }

        public override void ShowPreviewControlValue(string value)
        {
            // Create the popup overlay
            if (this.PopupPreview == null)
            {
                // No adjustment is needed as the popup is directly over the entire note control
                double horizontalOffset = 0;

                // Padding is used to align the text so it begins at the same spot as the control's text
                Thickness padding = new Thickness(7, 5.5, 0, 0);

                this.PopupPreview = this.CreatePopupPreview(this.ContentControl, padding, this.ContentControl.Width, horizontalOffset);
            }
            // Show the popup
            this.ShowPopupPreview(value);
        }
        public override void HidePreviewControlValue()
        {
            this.HidePopupPreview();
        }

 
        #endregion
  
    }
}