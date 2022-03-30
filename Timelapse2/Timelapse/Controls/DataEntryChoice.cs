﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Controls
{
    // A FixedChoice comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - a combobox (containing the content) at the given width
    public class DataEntryChoice : DataEntryControl<ComboBox, Label>
    {
        #region Public properties
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

        /// <summary>Gets or sets the content of the choice.</summary>
        public override string Content
        {
            get { return this.ContentControl.Text; }
        }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set
            {
                if (Util.GlobalReferences.TimelapseState.IsViewOnly)
                {
                    this.ContentControl.IsReadOnly = true;
                    this.ContentControl.IsHitTestVisible = false;
                }
                else
                {
                    this.ContentControl.IsReadOnly = value;
                }
            }
        }
        #endregion

        #region Constructor
        public DataEntryChoice(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyleEnum.ChoiceComboBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // The behaviour of the combo box
            this.ContentControl.Focusable = true;
            this.ContentControl.IsEditable = false;
            this.ContentControl.IsTextSearchEnabled = true;

            // Callback used to allow Enter to select the highlit item
            this.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;

            // Add items to the combo box. If we have an  EmptyChoiceItem, then  add an 'empty string' to the end 
            // Check the arguments for null 
            List<string> choiceList;
            bool includesEmptyChoice;
            if (control == null)
            {
                // this should not happen
                TracePrint.PrintStackTrace(1);
                choiceList = new List<string>();
                includesEmptyChoice = true;
            }
            else
            {
                choiceList = control.GetChoices(out includesEmptyChoice);
            }
            ComboBoxItem cbi;
            foreach (string choice in choiceList)
            {
                cbi = new ComboBoxItem()
                {
                    Content = choice
                };
                this.ContentControl.Items.Add(cbi);
            }
            if (includesEmptyChoice)
            {
                // put empty choice at the beginning of the control below a separator for visual clarity
                cbi = new ComboBoxItem()
                {
                    Content = String.Empty
                };
                this.ContentControl.Items.Insert(0, cbi);
            }
            // We include an invisible ellipsis menu item. This allows us to display an ellipsis in the combo box text field
            // when multiple images with different values are selected
            cbi = new ComboBoxItem()
            {
                Content = Constant.Unicode.Ellipsis
            };
            this.ContentControl.Items.Insert(0, cbi);
            ((ComboBoxItem)this.ContentControl.Items[0]).Visibility = System.Windows.Visibility.Collapsed;
            this.ContentControl.SelectedIndex = 1;
        }
        #endregion

        #region Event Handlers
        // Users may want to use the text search facility on the combobox, where they type the first letter and then enter
        // For some reason, it wasn't working on pressing 'enter' so this handler does that.
        // Whenever a return or enter is detected on the combobox, it finds the highlight item (i.e., that is highlit from the text search)
        // and sets the combobox to that value.
        private void ContentCtl_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            if (keyEvent.Key == Key.Right || keyEvent.Key == Key.Left || keyEvent.Key == Key.PageUp || keyEvent.Key == Key.PageDown)
            {
                // the right/left arrow keys normally cycle through the menu items.
                // However, we want to retain the arrow keys - as well as the PageUp/Down keys - for cycling through the image.
                // So we mark the event as handled, and we cycle through the images anyways.
                // Note that redirecting the event to the main window, while prefered, won't work
                // as the main window ignores the arrow keys if the focus is set to a control.
                keyEvent.Handled = true;
                GlobalReferences.MainWindow.Handle_PreviewKeyDown(keyEvent, true);
            }
            else if (keyEvent.Key == Key.Return || keyEvent.Key == Key.Enter)
            {
                ComboBox comboBox = sender as ComboBox;
                for (int i = 0; i < comboBox.Items.Count; i++)
                {
                    ComboBoxItem comboBoxItem = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(i);
                    if (comboBoxItem != null && comboBoxItem.IsHighlighted)
                    {
                        comboBox.SelectedValue = comboBoxItem.Content.ToString();
                    }
                }
            }
            else if (keyEvent.Key == Key.Up || keyEvent.Key == Key.Down || keyEvent.Key == Key.Home)
            {
                // Because we have inserted an invisible ellipses into the list, we have to skip over it when a 
                // user navigates the combobox with the keyboard using the arrow keys
                ComboBox comboBox = sender as ComboBox;

                if (keyEvent.Key == Key.Up && (comboBox.SelectedIndex == 1 || comboBox.SelectedIndex == -1))
                {
                    // If the user tries to navigate to the ellipsis at the beginning of the list, keep it on the first valid item
                    if (comboBox.SelectedIndex == -1)
                    {
                        comboBox.SelectedIndex = 1;
                    }
                    keyEvent.Handled = true;
                }
                else if (keyEvent.Key == Key.Down && (comboBox.SelectedIndex == comboBox.Items.Count - 1 || comboBox.SelectedIndex == -1))
                {
                    // If the user tries to navigate beyond the end of the list, keep it on the last valid item
                    // But the -1 should only be triggered to go back to the beginning
                    if (comboBox.SelectedIndex == -1)
                    {
                        comboBox.SelectedIndex = 1;
                    }
                    keyEvent.Handled = true;
                }
                else if (keyEvent.Key == Key.Home)
                {
                    // Key.Home - go to the first item.
                    comboBox.SelectedIndex = 1;
                    keyEvent.Handled = true;
                }
            }
        }
        #endregion

        #region Setting Content and Tooltip
        // Set the Control's Content and Tooltip to the provided value
        public override void SetContentAndTooltip(string value)
        {
            // If the value is null, an ellipsis will be drawn in the checkbox (see Checkbox style)
            // Used to signify the indeterminate state in no or multiple selections in the overview.
            if (value == null)
            {
                this.ContentControl.Text = Constant.Unicode.Ellipsis;
                this.ContentControl.ToolTip = "Select an item to change the " + this.Label + " for all selected images";
                return;
            }
            // For some reason, the empty item was not setting the selected index to the item with the blank entry. 
            // This is needed to set it explicitly.
            if (string.IsNullOrEmpty(value))
            {
                this.ContentControl.SelectedIndex = 1;
            }

            this.ContentControl.Text = value;
            this.ContentControl.ToolTip = String.IsNullOrEmpty(value) ? "Blank entry" : value;
        }
        #endregion

        #region Hiding, visual Effects and Popup Previews

        public void HideItems(List<String> itemsToHide)
        {
            if (this.ContentControl == null || this.ContentControl.Items == null || itemsToHide == null)
            {
                return;
            }
            foreach (ComboBoxItem cbi in this.ContentControl.Items)
            {
                if (itemsToHide.Contains((string)cbi.Content))
                {
                    cbi.Height = 0;
                }
            }
        }

        // Flash the content area of the control
        public override void FlashContentControl()
        {
            Border contentHost = (Border)this.ContentControl.Template.FindName("PART_Border", this.ContentControl);
            if (contentHost != null)
            {
                TextBlock tb = VisualChildren.GetVisualChild<TextBlock>(contentHost);
                if (tb != null)
                {
                    tb.Background = new SolidColorBrush(Colors.White);
                    tb.Background.BeginAnimation(SolidColorBrush.ColorProperty, this.GetColorAnimation());
                }
            }
        }

        public override void ShowPreviewControlValue(string value)
        {
            // Create the popup overlay
            if (this.PopupPreview == null)
            {
                // We want to expose the arrow on the choice menu, so subtract its width and move the horizontal offset over
                double arrowWidth = 20;
                double width = this.ContentControl.Width - arrowWidth;
                double horizontalOffset = -arrowWidth / 2;

                // Padding is used to align the text so it begins at the same spot as the control's text
                Thickness padding = new Thickness(5.5, 6.5, 0, 0);

                this.PopupPreview = this.CreatePopupPreview(this.ContentControl, padding, width, horizontalOffset);
            }
            // Show the popup
            this.ShowPopupPreview(value);
        }

        public override void HidePreviewControlValue()
        {
            this.HidePopupPreview();
        }

        public override void FlashPreviewControlValue()
        {
            this.FlashPopupPreview();
        }
        #endregion
    }
}
