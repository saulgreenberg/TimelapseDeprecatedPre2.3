using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Controls
{
    public enum ControlsEnableState
    {
        MultipleImageView,
        SingleImageView,
    }

    /// <summary>
    /// This class generates controls based upon the information passed into it from the data grid templateTable
    /// </summary>
    public partial class DataEntryControls : UserControl
    {
        public List<DataEntryControl> Controls { get; private set; }
        public Dictionary<string, DataEntryControl> ControlsByDataLabel { get; private set; }

        public Button CopyPreviousValuesButton { get; set; }
        private DataEntryHandler dataEntryHandler = null;
        public DataEntryControls()
        {
            this.InitializeComponent();
            this.Controls = new List<DataEntryControl>();
            this.ControlsByDataLabel = new Dictionary<string, DataEntryControl>();
        }

        public void CreateControls(FileDatabase database, DataEntryHandler dataEntryPropagator)
        {
            // Depending on how the user interacts with the file import process image set loading can be aborted after controls are generated and then
            // another image set loaded.  Any existing controls therefore need to be cleared.
            this.Controls.Clear();
            this.ControlsByDataLabel.Clear();
            this.ControlGrid.Inlines.Clear();

            foreach (ControlRow control in database.Controls)
            {
                // no point in generating a control if it doesn't render in the UX
                if (control.Visible == false)
                {
                    continue;
                }

                DataEntryControl controlToAdd;
                if (control.Type == Constant.DatabaseColumn.DateTime)
                {
                    DataEntryDateTime dateTimeControl = new DataEntryDateTime(control, this);
                    controlToAdd = dateTimeControl;
                }
                else if (control.Type == Constant.DatabaseColumn.File ||
                         control.Type == Constant.DatabaseColumn.RelativePath ||
                         control.Type == Constant.DatabaseColumn.Folder ||
                         control.Type == Constant.DatabaseColumn.Date ||
                         control.Type == Constant.DatabaseColumn.Time ||
                         control.Type == Constant.Control.Note)
                {
                    // standard controls rendering as notes aren't editable by the user 
                    List<string> autocompletions = null;
                    bool readOnly = control.Type != Constant.Control.Note;
                    if (readOnly == false)
                    {
                        autocompletions = new List<string>(database.GetDistinctValuesInFileDataColumn(control.DataLabel));
                    }
                    DataEntryNote noteControl = new DataEntryNote(control, autocompletions, this)
                    {
                        ContentReadOnly = readOnly
                    };
                    controlToAdd = noteControl;
                }
                else if (control.Type == Constant.Control.Flag || control.Type == Constant.DatabaseColumn.DeleteFlag)
                {
                    DataEntryFlag flagControl = new DataEntryFlag(control, this);
                    controlToAdd = flagControl;
                }
                else if (control.Type == Constant.Control.Counter)
                {
                    DataEntryCounter counterControl = new DataEntryCounter(control, this);
                    controlToAdd = counterControl;
                }
                else if (control.Type == Constant.Control.FixedChoice || control.Type == Constant.DatabaseColumn.ImageQuality)
                {
                    DataEntryChoice choiceControl = new DataEntryChoice(control, this);
                    controlToAdd = choiceControl;
                }
                else if (control.Type == Constant.DatabaseColumn.UtcOffset)
                {
                    DataEntryUtcOffset utcOffsetControl = new DataEntryUtcOffset(control, this);
                    controlToAdd = utcOffsetControl;
                }
                else
                {
                    Utilities.PrintFailure(String.Format("Unhandled control type {0} in CreateControls.", control.Type));
                    continue;
                }
                this.ControlGrid.Inlines.Add(controlToAdd.Container);
                this.Controls.Add(controlToAdd);
                this.ControlsByDataLabel.Add(control.DataLabel, controlToAdd);
            }
            dataEntryPropagator.SetDataEntryCallbacks(this.ControlsByDataLabel);
            this.dataEntryHandler = dataEntryPropagator;
        }

        // Enable or disable the following stock controls: 
        //     File, Folder, RelativePath,  DateTime, UtcOffset, ImageQuality
        // These controls refer to the specifics of a single image. Thus they should be disabled (and are thus not  editable) 
        // when the markable canvas is zoomed out to display multiple images
        public void SetEnableState(ControlsEnableState controlsToEnable, int imagesSelected)
        {
            // Enable the Copy Previous button only when in the single image view, otherwise disable
            if (this.CopyPreviousValuesButton != null)
            {
                this.CopyPreviousValuesButton.IsEnabled = (controlsToEnable == ControlsEnableState.SingleImageView) ? true : false;
            }

            if (this.dataEntryHandler.ImageCache.Current == null)
            {
                return;
            }
            this.dataEntryHandler.IsProgrammaticControlUpdate = true;
            foreach (DataEntryControl control in this.Controls)
            {
                // File, Folder and Relative Path
                if (control is DataEntryNote &&
                    (control.DataLabel == Constant.DatabaseColumn.File ||
                     control.DataLabel == Constant.DatabaseColumn.Folder ||
                     control.DataLabel == Constant.DatabaseColumn.RelativePath))
                {
                    DataEntryNote note = (DataEntryNote)control;
                    if (controlsToEnable == ControlsEnableState.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        note.IsEnabled = true;
                        note.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(note.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one image is selected, display it as enabled (but not editable) and show its value
                        // Otherwise disable these field as they should not be editable anyways. When no images are selected, clear the fields
                        string contentAndTooltip = (imagesSelected == 0) ? String.Empty : this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(note.DataLabel);
                        note.IsEnabled = (imagesSelected == 1) ? true : false;
                        note.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryDateTime datetime)
                {
                    // DateTime
                    if (controlsToEnable == ControlsEnableState.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        datetime.IsEnabled = true;
                        datetime.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(datetime.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one image is selected, display it as enabled (but not editable) and show its value
                        // Otherwise disable these field as they should not be editable anyways. When no images are selected, clear the fields
                        string contentAndTooltip = (imagesSelected == 0) ? String.Empty : this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(datetime.DataLabel);
                        datetime.IsEnabled = (imagesSelected == 1) ? true : false;
                        datetime.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryUtcOffset utcOffset)
                {
                    // UTC Offset
                    if (controlsToEnable == ControlsEnableState.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        utcOffset.IsEnabled = true;
                        utcOffset.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(utcOffset.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one image is selected, display it as enabled (but not editable) and show its value
                        // Otherwise disable these field as they should not be editable anyways. When no images are selected, clear the fields
                        string contentAndTooltip = (imagesSelected == 0) ? String.Empty : this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(utcOffset.DataLabel);
                        utcOffset.IsEnabled = (imagesSelected == 1) ? true : false;
                        utcOffset.SetContentAndTooltip(contentAndTooltip);

                        //// Multiple images view
                        //if (imagesSelected == 1)
                        //{
                        //    // When one image is selected, enable it and show its value
                        //    utcOffset.IsEnabled = true;
                        //    utcOffset.ContentControl.Foreground = Brushes.Black;
                        //    utcOffset.SetContentAndTooltip(this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(utcOffset.DataLabel));
                        //}
                        //else
                        //{
                        //    // When no images or multiple images are selected, disable and clear the field
                        //    // SAULXXX: UTC CONTROLS DONT ALLOW BLANKS. NOTE THAT WE NEED TO DO THIS BETTER, PERHAPS BY PUTTING A ZERO IN THERE? AS OTHERWISE 
                        //    utcOffset.IsEnabled = false;
                        //    utcOffset.ContentControl.Foreground = utcOffset.ContentControl.Background;
                        //    utcOffset.SetContentAndTooltip(this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(utcOffset.DataLabel));
                        //}
                    }
                }
                else if (control is DataEntryChoice &&
                    (control.DataLabel == Constant.DatabaseColumn.ImageQuality))
                {
                    // ImageQuality
                    DataEntryChoice imageQuality = (DataEntryChoice)control;
                    if (controlsToEnable == ControlsEnableState.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        imageQuality.IsEnabled = true;
                        imageQuality.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(imageQuality.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable, and show its value
                        // When no images are selected, clear the fields
                        string contentAndTooltip = (imagesSelected == 0) ? String.Empty : this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(imageQuality.DataLabel);
                        imageQuality.IsEnabled = (imagesSelected >= 1) ? true : false;
                        imageQuality.SetContentAndTooltip(contentAndTooltip);
                     }
                }
                else if (control is DataEntryNote note)
                {
                    // Notes
                    if (controlsToEnable == ControlsEnableState.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        note.IsEnabled = true;
                        note.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(note.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable, and show its value
                        // When no images are selected, clear the fields
                        //string contentAndTooltip = (imagesSelected == 0) ? String.Empty : this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(note.DataLabel);
                        string contentAndTooltip = this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(note.DataLabel);
                        if (contentAndTooltip == null || imagesSelected == 0)
                        {
                            //contentAndTooltip = "\u2026"; // 
                            contentAndTooltip = "..."; // Ellipsis
                        }
                        note.IsEnabled = (imagesSelected >= 1) ? true : false;
                        note.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryChoice choice)
                {
                    // Choices
                    if (controlsToEnable == ControlsEnableState.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        choice.IsEnabled = true;
                        choice.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(choice.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable, and show its value
                        // When no images are selected, clear the fields
                        string contentAndTooltip = this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(choice.DataLabel);
                        if (contentAndTooltip == null || imagesSelected == 0)
                        {
                            contentAndTooltip = "..."; // Ellipsis
                        }
                        choice.IsEnabled = (imagesSelected >= 1) ? true : false;
                        choice.SetContentAndTooltip(contentAndTooltip);    
                    }
                }
                else if (control is DataEntryCounter counter)
                {
                    // Counters
                    if (controlsToEnable == ControlsEnableState.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        counter.IsEnabled = true;
                        counter.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(counter.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable, and show its value
                        // When no images are selected, clear the fields
                        string contentAndTooltip = (imagesSelected == 0) ? String.Empty : this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(counter.DataLabel);
                        counter.IsEnabled = (imagesSelected >= 1) ? true : false;
                        counter.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryFlag flag)
                {
                    // Flag
                    if (controlsToEnable == ControlsEnableState.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        flag.IsEnabled = true;
                        flag.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(flag.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable, and show its value
                        // When no images are selected, clear the fields
                        string contentAndTooltip = (imagesSelected == 0) ? String.Empty : this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(flag.DataLabel);
                        flag.IsEnabled = (imagesSelected >= 1) ? true : false;
                        flag.SetContentAndTooltip(contentAndTooltip);
                    }
                }
            }
            this.dataEntryHandler.IsProgrammaticControlUpdate = false;
        }
    }
}