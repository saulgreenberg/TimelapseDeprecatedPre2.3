﻿using System;
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
        MultiplImageView,
        SingleImageView,
    }
    //public enum ControlsEnableState
    //{
    //    NotInOverview_EnableAllAndSetToCurrentImage,
    //    OverviewNoneSelected_DisableAndBlankAll,
    //    OverviewOneSelected_EnableAllAndSetToSelectedImageButCopyPreviousDisabled,
    //    OverviewMultiplSelected_DisableAndBlankStockControlsEnableOthers,
    //}

    /// <summary>
    /// This class generates controls based upon the information passed into it from the data grid templateTable
    /// </summary>
    public partial class DataEntryControls : UserControl
    {
        public List<DataEntryControl> Controls { get; private set; }
        public Dictionary<string, DataEntryControl> ControlsByDataLabel { get; private set; }

        public Button CopyPreviousValuesButton = null;

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
                    DataEntryNote noteControl = new DataEntryNote(control, autocompletions, this);
                    noteControl.ContentReadOnly = readOnly;

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
            
            foreach (DataEntryControl control in this.Controls)
            {
                // File, Folder and Relative Path
                if (control is DataEntryNote &&
                    (control.DataLabel == Constant.DatabaseColumn.File ||
                     control.DataLabel == Constant.DatabaseColumn.Folder ||
                     control.DataLabel == Constant.DatabaseColumn.RelativePath))
                {
                    DataEntryNote note = (DataEntryNote)control;
                    this.dataEntryHandler.IsProgrammaticControlUpdate = true;
                    if (controlsToEnable == ControlsEnableState.SingleImageView)
                    {
                        // Enable and show its contents
                        note.IsEnabled = true;
                        note.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(note.DataLabel));
                    }
                    else
                    {
                        // Disable and hide its contents
                        note.IsEnabled = false;
                        note.SetContentAndTooltip("");
                    }
                    this.dataEntryHandler.IsProgrammaticControlUpdate = false;

                    //note.ContentControl.Foreground = controlsToEnable == ControlsEnableState.NotInOverview_EnableAllAndSetToCurrentImage ? Brushes.Black : note.ContentControl.Background;
                }

                // DateTime
                else if (control is DataEntryDateTime)
                {
                    DataEntryDateTime datetime = (DataEntryDateTime)control;
                    datetime.IsEnabled = (controlsToEnable == ControlsEnableState.SingleImageView);
                    //datetime.ContentControl.Foreground = controlsToEnable == ControlsEnableState.NotInOverview_EnableAllAndSetToCurrentImage ? Brushes.Black : datetime.ContentControl.Background;
                }

                // UTC Offset
                else if (control is DataEntryUtcOffset)
                {
                    DataEntryUtcOffset utcOffset = (DataEntryUtcOffset)control;
                    utcOffset.IsEnabled = (controlsToEnable == ControlsEnableState.SingleImageView);
                    //utcOffset.ContentControl.Foreground = controlsToEnable == ControlsEnableState.NotInOverview_EnableAllAndSetToCurrentImage ? Brushes.Black : utcOffset.ContentControl.Background;

                }

                // ImageQuality
                else if (control is DataEntryChoice &&
                    (control.DataLabel == Constant.DatabaseColumn.ImageQuality))
                {
                    DataEntryChoice imageQuality = (DataEntryChoice)control;
                    imageQuality.IsEnabled = (controlsToEnable == ControlsEnableState.SingleImageView);
                    //imageQuality.ContentControl.Foreground = controlsToEnable == ControlsEnableState.NotInOverview_EnableAllAndSetToCurrentImage ? Brushes.Black : imageQuality.ContentControl.Background;
                }

                // Notes
                else if (control is DataEntryNote)
                {
                    DataEntryNote note = (DataEntryNote)control;
                    if (controlsToEnable == ControlsEnableState.SingleImageView )
                    {
                        // Single image view, so enable and show its contents
                        note.IsEnabled = true;
                        note.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(note.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        this.dataEntryHandler.IsProgrammaticControlUpdate = true;
                        if (imagesSelected <= 0)
                        {
                            // No images selected, so disable and clear the note
                            note.IsEnabled = false;
                            note.SetContentAndTooltip("");
                        }
                        else 
                        {
                            // At least one image is selected, so show enable it and show its value
                            note.IsEnabled = true;
                            note.SetContentAndTooltip(this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(note.DataLabel));
                        }
                        this.dataEntryHandler.IsProgrammaticControlUpdate = false;
                    }
                    
                }

                // Choices
                else if (control is DataEntryChoice)
                {
                    DataEntryChoice choice = (DataEntryChoice)control;
                    choice.IsEnabled = (controlsToEnable == ControlsEnableState.SingleImageView);
                }

                // Counters
                else if (control is DataEntryCounter)
                {
                    DataEntryCounter counter = (DataEntryCounter)control;
                    counter.IsEnabled = (controlsToEnable == ControlsEnableState.SingleImageView);
                }

                // Flags
                else if (control is DataEntryFlag)
                {
                    DataEntryFlag flag = (DataEntryFlag)control;
                    flag.IsEnabled = (controlsToEnable == ControlsEnableState.SingleImageView);
                }
            }
        }

        //public void SetEnableState(ControlsEnableState controlsToEnable, int imagesSelected)
        //{
        //    // Enable the Copy Previous button only when in the single image view, otherwise disable
        //    if (this.CopyPreviousValuesButton != null)
        //    {
        //        this.CopyPreviousValuesButton.IsEnabled = (controlsToEnable == ControlsEnableState.SingleImageView) ? true : false;
        //    }

        //    foreach (DataEntryControl control in this.Controls)
        //    {
        //        // File, Folder and Relative Path
        //        if (control is DataEntryNote &&
        //            (control.DataLabel == Constant.DatabaseColumn.File ||
        //             control.DataLabel == Constant.DatabaseColumn.Folder ||
        //             control.DataLabel == Constant.DatabaseColumn.RelativePath))
        //        {
        //            DataEntryNote note = (DataEntryNote)control;
        //            this.dataEntryHandler.IsProgrammaticControlUpdate = true;
        //            if (controlsToEnable == ControlsEnableState.NotInOverview_EnableAllAndSetToCurrentImage)
        //            {
        //                // Enable and show its contents
        //                note.IsEnabled = true;
        //                note.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(note.DataLabel));
        //            }
        //            else
        //            {
        //                // Disable and hide its contents
        //                note.IsEnabled = false;
        //                note.SetContentAndTooltip("");
        //            }
        //            this.dataEntryHandler.IsProgrammaticControlUpdate = false;

        //            //note.ContentControl.Foreground = controlsToEnable == ControlsEnableState.NotInOverview_EnableAllAndSetToCurrentImage ? Brushes.Black : note.ContentControl.Background;
        //        }

        //        // DateTime
        //        else if (control is DataEntryDateTime)
        //        {
        //            DataEntryDateTime datetime = (DataEntryDateTime)control;
        //            datetime.IsEnabled = (controlsToEnable == ControlsEnableState.NotInOverview_EnableAllAndSetToCurrentImage);
        //            datetime.ContentControl.Foreground = controlsToEnable == ControlsEnableState.NotInOverview_EnableAllAndSetToCurrentImage ? Brushes.Black : datetime.ContentControl.Background;
        //        }

        //        // UTC Offset
        //        else if (control is DataEntryUtcOffset)
        //        {
        //            DataEntryUtcOffset utcOffset = (DataEntryUtcOffset)control;
        //            utcOffset.IsEnabled = (controlsToEnable == ControlsEnableState.NotInOverview_EnableAllAndSetToCurrentImage);
        //            utcOffset.ContentControl.Foreground = controlsToEnable == ControlsEnableState.NotInOverview_EnableAllAndSetToCurrentImage ? Brushes.Black : utcOffset.ContentControl.Background;

        //        }

        //        // ImageQuality
        //        else if (control is DataEntryChoice &&
        //            (control.DataLabel == Constant.DatabaseColumn.ImageQuality))
        //        {
        //            DataEntryChoice imageQuality = (DataEntryChoice)control;
        //            imageQuality.IsEnabled = (controlsToEnable == ControlsEnableState.NotInOverview_EnableAllAndSetToCurrentImage);
        //            imageQuality.ContentControl.Foreground = controlsToEnable == ControlsEnableState.NotInOverview_EnableAllAndSetToCurrentImage ? Brushes.Black : imageQuality.ContentControl.Background;
        //        }

        //        // Notes
        //        else if (control is DataEntryNote)
        //        {
        //            DataEntryNote note = (DataEntryNote)control;
        //            //note.IsEnabled = (controlsToEnable != ControlsEnableState.OverviewNoneSelected_DisableAndBlankAll);
        //            this.dataEntryHandler.IsProgrammaticControlUpdate = true;
        //            //if (controlsToEnable != ControlsEnableState.OverviewNoneSelected_DisableAndBlankAll)
        //            if (controlsToEnable == ControlsEnableState.OverviewOneSelected_EnableAllAndSetToSelectedImageButCopyPreviousDisabled)
        //            {
        //                // Enable and show its contents
        //                note.IsEnabled = true;
        //                note.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(note.DataLabel));
        //            }
        //            else
        //            {
        //                // Disable and hide its contents
        //                note.IsEnabled = false;
        //                string value = this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(note.DataLabel);
        //                System.Diagnostics.Debug.Print(note.DataLabel + " " + value);
        //                note.SetContentAndTooltip(value);
        //                // note.SetContentAndTooltip(this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(note.DataLabel));
        //                ;
        //                //note.SetContentAndTooltip(""); // Set it to whatever is selected if they have a common value, or none.
        //            }
        //            this.dataEntryHandler.IsProgrammaticControlUpdate = false;
        //        }

        //        // Choices
        //        else if (control is DataEntryChoice)
        //        {
        //            DataEntryChoice choice = (DataEntryChoice)control;
        //            choice.IsEnabled = (controlsToEnable != ControlsEnableState.OverviewNoneSelected_DisableAndBlankAll);
        //        }

        //        // Counters
        //        else if (control is DataEntryCounter)
        //        {
        //            DataEntryCounter counter = (DataEntryCounter)control;
        //            counter.IsEnabled = (controlsToEnable != ControlsEnableState.OverviewNoneSelected_DisableAndBlankAll);
        //        }

        //        // Flags
        //        else if (control is DataEntryFlag)
        //        {
        //            DataEntryFlag flag = (DataEntryFlag)control;
        //            flag.IsEnabled = (controlsToEnable != ControlsEnableState.OverviewNoneSelected_DisableAndBlankAll);
        //        }
        //    }
        //}
        public void AddButton(Control button)
        {
            this.ButtonLocation.Child = button;
        }
    }
}