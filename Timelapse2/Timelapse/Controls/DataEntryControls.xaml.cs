using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Controls
{
    public enum ControlsToEnable
    {
        All,
        AllButStockControls,
        None
    }
    /// <summary>
    /// This class generates controls based upon the information passed into it from the data grid templateTable
    /// </summary>
    public partial class DataEntryControls : UserControl
    {
        public List<DataEntryControl> Controls { get; private set; }
        public Dictionary<string, DataEntryControl> ControlsByDataLabel { get; private set; }

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
        }

        // Enable or disable the following stock controls: 
        //     File, Folder, RelativePath,  DateTime, UtcOffset, ImageQuality
        // These controls refer to the specifics of a single image. Thus they should be disabled (and are thus not  editable) 
        // when the markable canvas is zoomed out to display multiple images
        public void SetEnableState(ControlsToEnable controlsToEnable)
        {
            foreach (DataEntryControl control in this.Controls)
            {
                if (control is DataEntryNote &&
                    (control.DataLabel == Constant.DatabaseColumn.File ||
                     control.DataLabel == Constant.DatabaseColumn.Folder ||
                     control.DataLabel == Constant.DatabaseColumn.RelativePath))
                {
                    DataEntryNote note = (DataEntryNote)control;
                    note.IsEnabled = (controlsToEnable == ControlsToEnable.All);
                }
                else if (control is DataEntryDateTime)
                {
                    DataEntryDateTime datetime = (DataEntryDateTime)control;
                    datetime.IsEnabled = (controlsToEnable == ControlsToEnable.All);
                }
                else if (control is DataEntryUtcOffset)
                {
                    DataEntryUtcOffset utcOffset = (DataEntryUtcOffset)control;
                    utcOffset.IsEnabled = (controlsToEnable == ControlsToEnable.All);
                }
                else if (control is DataEntryChoice &&
                    (control.DataLabel == Constant.DatabaseColumn.ImageQuality))
                {
                    DataEntryChoice imageQuality = (DataEntryChoice)control;
                    imageQuality.IsEnabled = (controlsToEnable == ControlsToEnable.All);
                }
                else if (control is DataEntryNote)
                {
                    DataEntryNote note = (DataEntryNote)control;
                    note.IsEnabled = (controlsToEnable != ControlsToEnable.None);
                }
                else if (control is DataEntryChoice)
                {
                    DataEntryChoice choice = (DataEntryChoice)control;
                    choice.IsEnabled = (controlsToEnable != ControlsToEnable.None);
                }
                else if (control is DataEntryCounter)
                {
                    DataEntryCounter counter = (DataEntryCounter)control;
                    counter.IsEnabled = (controlsToEnable != ControlsToEnable.None);
                }
            }
        }

        public void AddButton(Control button)
        {
            this.ButtonLocation.Child = button;
        }
    }
}