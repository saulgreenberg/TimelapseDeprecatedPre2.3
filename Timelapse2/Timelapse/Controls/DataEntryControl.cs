﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Timelapse.Database;
using Timelapse.Enums;

namespace Timelapse.Controls
{
    public abstract class DataEntryControl
    {
        /// <summary>Gets the value of the control</summary>
        public abstract string Content { get; }

        /// <summary>Gets or sets a value indicating whether the control's content is user editable</summary>
        public abstract bool ContentReadOnly { get; set; }

        /// <summary>Gets or sets a value indicating whether the control's contents are copyable.</summary>
        public bool Copyable { get; set; }

        /// <summary>Gets the container that holds the control.</summary>
        public StackPanel Container { get; private set; }

        /// <summary>Gets the data label which corresponds to this control.</summary>
        public string DataLabel { get; private set; }

        public abstract IInputElement Focus(DependencyObject focusScope);

        // used to remember and restore state when
        // displayTemporaryContents and RestoreTemporaryContents are used
        protected Popup PopupPreview { get; set; }

        protected DataEntryControl(ControlRow control, DataEntryControls styleProvider)
        {
            // populate properties from database definition of control
            // this.Content and Tooltip can't be set, however, as the caller hasn't instantiated the content control yet
            this.Copyable = control.Copyable;
            this.DataLabel = control.DataLabel;

            // Create the stack panel
            this.Container = new StackPanel();
            Style style = styleProvider.FindResource(Constant.ControlStyle.ContainerStyle) as Style;
            this.Container.Style = style;

            // use the containers's tag to point back to this so event handlers can access the DataEntryControl
            // this is needed by callbacks such as DataEntryHandler.Container_PreviewMouseRightButtonDown() and TimelapseWindow.CounterControl_MouseLeave()
            this.Container.Tag = this;
        }
        public abstract void SetContentAndTooltip(string value);

        // These two methods allow us to temporarily display an arbitrary string value into the data field
        // This should alwasy be followed by restoring the original contents.
        // An example of its use is to show the user what will be placed in the data control if the user continues their action
        // e.g., moving the mouse over a quickpaste or copyprevious buttons will display potential values,
        //       while moving the mouse out of those buttons will restore those values.
        public abstract void ShowPreviewControlValue(string value);
        public abstract void HidePreviewControlValue();
        public abstract void FlashPreviewControlValue();
    }

    // A generic control comprises a stack panel containing 
    // - a control containing at least a descriptive label 
    // - another control for displaying / entering data at a given width
    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:FileMayOnlyContainASingleClass", Justification = "StyleCop limitation.")]
    public abstract class DataEntryControl<TContent, TLabel> : DataEntryControl
        where TContent : Control, new()
        where TLabel : ContentControl, new()
    {
        public TContent ContentControl { get; private set; }

        /// <summary>Gets the control label's value</summary>
        public string Label
        {
            get { return (string)this.LabelControl.Content; }
        }

        public TLabel LabelControl { get; private set; }

        /// <summary>Gets or sets the width of the content control</summary>
        public int Width
        {
            get { return (int)this.ContentControl.Width; }
            set { this.ContentControl.Width = value; }
        }

        // Sets or gets whether this control is enabled or disabled</summary>
        public bool IsEnabled
        {
            get
            {
                return this.Container.IsEnabled;
            }
            set
            {
                this.ContentControl.IsEnabled = value;
                this.LabelControl.IsEnabled = value;
                this.Container.IsEnabled = value;
                this.ContentControl.Foreground = value ? Brushes.Black : Brushes.DimGray;
            }
        }

        protected DataEntryControl(ControlRow control, DataEntryControls styleProvider, Nullable<ControlContentStyleEnum> contentStyleName, ControlLabelStyleEnum labelStyleName) : 
            base(control, styleProvider)
        {
            this.ContentControl = new TContent()
            {
                IsTabStop = true
            };
            if (contentStyleName.HasValue)
            {
                this.ContentControl.Style = (Style)styleProvider.FindResource(contentStyleName.Value.ToString());
            }
            this.ContentReadOnly = false;
            this.ContentControl.IsEnabled = true;
            this.Width = control.Width;

            // use the content's tag to point back to this so event handlers can access the DataEntryControl as well as just ContentControl
            // the data update callback for each control type in TimelapseWindow, such as NoteControl_TextChanged(), relies on this
            this.ContentControl.Tag = this;

            // Create the label (which is an actual label)
            this.LabelControl = new TLabel()
            {
                Content = control.Label,
                Style = (Style)styleProvider.FindResource(labelStyleName.ToString()),
                ToolTip = control.Tooltip
            };

            // add the label and content to the stack panel
            this.Container.Children.Add(this.LabelControl);
            this.Container.Children.Add(this.ContentControl);
        }

        public override IInputElement Focus(DependencyObject focusScope)
        {
            // request the focus manager figure out how to assign focus within the edit control as not all controls are focusable at their top level
            // This is not reliable at small focus scopes, possibly due to interaction with TimelapseWindow's focus management, but seems reasonably
            // well behaved at application scope.
            FocusManager.SetFocusedElement(focusScope, this.ContentControl);
            return (IInputElement)this.ContentControl;
        }
        protected Popup CreatePopupPreview(Control control, Thickness padding, double width, double horizontalOffset)
        {
            // Creatre a textblock and align it so the text is exactly at the same position as the control's text
            TextBlock popupText = new TextBlock
            {
                Text = String.Empty,
                Width = width,
                Height = control.Height,
                Padding = padding,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Constant.Control.QuickPasteFieldHighlightBrush,
                Foreground = Brushes.Green,
                FontStyle = FontStyles.Italic,
            };

            Border border = new Border
            {
                BorderBrush = Brushes.Green,
                BorderThickness = new Thickness(1),
                Child = popupText,
            };

            Popup popup = new Popup
            {
                Width = width,
                Height = control.Height,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Placement = PlacementMode.Center,
                VerticalOffset = 0,
                HorizontalOffset = horizontalOffset,
                PlacementTarget = control,
                IsOpen = false,
                Child = border
            };
            return popup;
        }

        protected void ShowPopupPreview(string value)
        {
            Border border = (Border)this.PopupPreview.Child;
            TextBlock popupText = (TextBlock)border.Child;
            popupText.Text = value;
            this.PopupPreview.IsOpen = true;
        }

        protected void HidePopupPreview()
        {
            if (this.PopupPreview == null || this.PopupPreview.Child == null)
            {
                // There is no popupPreview being displayed, so there is nothing to hide.
                return;
            }
            Border border = (Border)this.PopupPreview.Child;
            TextBlock popupText = (TextBlock)border.Child;
            popupText.Text = String.Empty;
            this.PopupPreview.IsOpen = false;
        }

        // Create a flash effect for the popup. We use this to signal that the 
        // preview text has been selected
        protected void FlashPopupPreview()
        {
            if (this.PopupPreview == null || this.PopupPreview.Child == null)
            {
                return;
            }

            // Get the TextBlock
            Border border = (Border)this.PopupPreview.Child;
            TextBlock popupText = (TextBlock)border.Child;

            // Revert to normal fontstyle, and set up a
            // timer to change it back to italics after a short duration
            popupText.FontStyle = FontStyles.Normal;
            DispatcherTimer timer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromSeconds(.4),
                Tag = popupText,
            };
            timer.Tick += FlashFontTimer_Tick;

            // Animate the color from white back to its current color
            ColorAnimation animation;
            animation = new ColorAnimation()
            { 
                From = Colors.White,
                AutoReverse = false,
                Duration = new Duration(TimeSpan.FromSeconds(.6)),
                EasingFunction = new ExponentialEase()
                {
                    EasingMode = EasingMode.EaseIn
                },  
            };

            // Get it all going
            popupText.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            timer.Start();
        }
        private void FlashFontTimer_Tick(object sender, EventArgs e)
        {
            DispatcherTimer timer = sender as DispatcherTimer;
            ((TextBlock)timer.Tag).FontStyle = FontStyles.Italic;
            timer.Stop();
        }
    }
}