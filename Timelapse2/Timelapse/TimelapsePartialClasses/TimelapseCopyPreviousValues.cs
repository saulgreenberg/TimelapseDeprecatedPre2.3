using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Timelapse.Controls;
using Timelapse.Enums;

namespace Timelapse
{
    public partial class TimelapseWindow : Window, IDisposable
    {
        // The methods below all relate to the CopyPreviousValues button
        #region Callbacks
        // When the the mouse enters or leaves the CopyPreviousValues button 
        // determine if the copyable control should glow, have highlighted previews of the values to be copied, or just be left in its orignal state     
        private void CopyPreviousValues_MouseEnterOrLeave(object sender, MouseEventArgs e)
        {
            CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();
        }

        private void CopyPreviousValues_LostFocus(object sender, RoutedEventArgs e)
        {
            this.CopyPreviousValuesSetGlowAsNeeded();
        }

        // When the CopyPreviousValues button is clicked, or when a space is entered while it is focused,
        // copy the data values from the previous control to the current one
        private void CopyPreviousValues_Click()
        {
            CopyPreviousValuesPasteValues();
            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = pair.Value;
                if (control.Copyable)
                {
                    control.FlashPreviewControlValue();
                }
            }
        }

        // When the CopyPreviousValues button gets a space entered while it is focused,
        // copy the data values from the previous control to the current one
        private void CopyPreviousValues_KeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.Key == Key.Space)
            {
                CopyPreviousValuesPasteValues();
            }
        }
        #endregion

        #region Methods invoked  that actually do the work

        // This should be the only method (aside from the above events) invoked from outside this file
        public void CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded()
        {
            int previousRow = (this.dataHandler == null || this.dataHandler.ImageCache == null) ? -1 : this.dataHandler.ImageCache.CurrentRow - 1;
            this.CopyPreviousValuesButton.IsEnabled = previousRow >= 0 && this.IsDisplayingSingleImage();
            this.CopyPreviousValueSetPreviewsAsNeeded(previousRow);
            this.CopyPreviousValuesSetGlowAsNeeded();
        }
        #endregion

        #region These methods are only accessed from within this file
        // Set the glow highlight on the copyable fields if various conditions are met
        private void CopyPreviousValuesSetGlowAsNeeded()
        {
            if (this.IsDisplayingSingleImage() &&
                this.CopyPreviousValuesButton != null && 
                this.CopyPreviousValuesButton.IsFocused &&
                this.CopyPreviousValuesButton.IsEnabled == true &&
                this.CopyPreviousValuesButton.IsFocused && 
                this.CopyPreviousValuesButton.IsMouseOver == false)
            {
                // Add the glow around the copyable controls
                DropShadowEffect effect = new DropShadowEffect()
                {
                    Color = Colors.LightGreen,
                    Direction = 0,
                    ShadowDepth = 0,
                    BlurRadius = 5,
                    Opacity = 1
                };
                foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
                {
                    DataEntryControl control = (DataEntryControl)pair.Value;
                    if (control.Copyable)
                    {
                        control.Container.Effect = effect;
                    }
                }
            }
            else
            {
                // Remove the glow around the copyable controls
                foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
                {
                    DataEntryControl control = (DataEntryControl)pair.Value;
                    control.Container.ClearValue(Control.EffectProperty);
                }
            }
        }

        // Place highlighted previews of the values to be copied atop the copyable controls
        // e.g., if the mouse is over the CopyPrevious button and we are not on the first row
        private void CopyPreviousValueSetPreviewsAsNeeded(int previousRow)
        {
            if (this.IsDisplayingSingleImage() &&
                this.CopyPreviousValuesButton != null && 
                this.CopyPreviousValuesButton.IsEnabled == true && 
                this.CopyPreviousValuesButton.IsMouseOver && 
                previousRow >= 0)
            {
                // Show the previews on the copyable controls
                foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
                {
                    DataEntryControl control = (DataEntryControl)pair.Value;
                    if (control.Copyable)
                    {
                        string previewValue = this.dataHandler.FileDatabase.Files[previousRow].GetValueDisplayString(control.DataLabel);
                        control.ShowPreviewControlValue(previewValue);
                    }
                }
            }
            else
            {
                // Remove the preview from each control
                foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
                {
                    DataEntryControl control = (DataEntryControl)pair.Value;
                    if (control.Copyable)
                    {
                        control.HidePreviewControlValue();
                    }
                }
            }
        }

        // Paste the data values from the previous copyable controls to the currently displayed controls
        private void CopyPreviousValuesPasteValues()
        {
            int previousRow = this.dataHandler.ImageCache.CurrentRow - 1;

            // This is an unneeded test as the CopyPreviousButton should be disabled if these conditions are met
            if (this.IsDisplayingSingleImage() == false || previousRow < 0)
            {
                return;
            }

            this.FilePlayer_Stop(); // In case the FilePlayer is going
            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = pair.Value;
                if (control.Copyable)
                {
                    control.SetContentAndTooltip(this.dataHandler.FileDatabase.Files[previousRow].GetValueDisplayString(control.DataLabel));
                }
            }
        }
        #endregion
    }
}
