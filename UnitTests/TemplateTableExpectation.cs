using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.UnitTests
{
    internal class TemplateTableExpectation
    {
        public ControlExpectations File { get; private set; }
        public ControlExpectations RelativePath { get; private set; }
        public ControlExpectations Folder { get; private set; }
        public ControlExpectations Date { get; private set; }
        public ControlExpectations DateTime { get; private set; }
        public ControlExpectations UtcOffset { get; private set; }
        public ControlExpectations Time { get; private set; }
        public ControlExpectations ImageQuality { get; private set; }
        public ControlExpectations DeleteFlag { get; private set; }

        protected TemplateTableExpectation(Version version)
        {
            long id = 1;
            this.File = ControlExpectations.CreateNote(Constant.DatabaseColumn.File, id++);
            this.File.Copyable = false;
            this.File.DefaultValue = Constant.ControlDefault.Value;
            this.File.List = Constant.ControlDefault.Value;
            this.File.TextBoxWidth = Int32.Parse(Constant.ControlDefault.FileWidth);
            this.File.Tooltip = Constant.ControlDefault.FileTooltip;
            this.File.Type = Constant.DatabaseColumn.File;
            this.RelativePath = ControlExpectations.CreateNote(Constant.DatabaseColumn.RelativePath, id++);
            this.RelativePath.Copyable = false;
            this.RelativePath.DefaultValue = Constant.ControlDefault.Value;
            this.RelativePath.List = Constant.ControlDefault.Value;
            this.RelativePath.TextBoxWidth = Int32.Parse(Constant.ControlDefault.RelativePathWidth);
            this.RelativePath.Tooltip = Constant.ControlDefault.RelativePathTooltip;
            this.RelativePath.Type = Constant.DatabaseColumn.RelativePath;
            this.RelativePath.Visible = true;
            this.Folder = ControlExpectations.CreateNote(Constant.DatabaseColumn.Folder, id++);
            this.Folder.Copyable = false;
            this.Folder.DefaultValue = Constant.ControlDefault.Value;
            this.Folder.List = Constant.ControlDefault.Value;
            this.Folder.TextBoxWidth = Int32.Parse(Constant.ControlDefault.FolderWidth);
            this.Folder.Tooltip = Constant.ControlDefault.FolderTooltip;
            this.Folder.Type = Constant.DatabaseColumn.Folder;
            this.DateTime = ControlExpectations.CreateNote(Constant.DatabaseColumn.DateTime, id++);
            this.DateTime.Copyable = false;
            this.DateTime.DefaultValue = DateTimeHandler.ToDatabaseDateTimeString(Constant.ControlDefault.DateTimeValue);
            this.DateTime.List = Constant.ControlDefault.Value;
            this.DateTime.TextBoxWidth = Int32.Parse(Constant.ControlDefault.DateTimeWidth);
            this.DateTime.Tooltip = Constant.ControlDefault.DateTimeTooltip;
            this.DateTime.Type = Constant.DatabaseColumn.DateTime;
            this.UtcOffset = ControlExpectations.CreateNote(Constant.DatabaseColumn.UtcOffset, id++);
            this.UtcOffset.Copyable = false;
            this.UtcOffset.DefaultValue = DateTimeHandler.ToDatabaseUtcOffsetString(Constant.ControlDefault.DateTimeValue.Offset);
            this.UtcOffset.List = Constant.ControlDefault.Value;
            this.UtcOffset.TextBoxWidth = Int32.Parse(Constant.ControlDefault.UtcOffsetWidth);
            this.UtcOffset.Tooltip = Constant.ControlDefault.UtcOffsetTooltip;
            this.UtcOffset.Type = Constant.DatabaseColumn.UtcOffset;
            this.UtcOffset.Visible = false;
            this.Date = ControlExpectations.CreateNote(Constant.DatabaseColumn.Date, id++);
            this.Date.Copyable = false;
            this.Date.DefaultValue = Constant.ControlDefault.Value;
            this.Date.List = Constant.ControlDefault.Value;
            this.Date.TextBoxWidth = Int32.Parse(Constant.ControlDefault.DateWidth);
            this.Date.Tooltip = Constant.ControlDefault.DateTooltip;
            this.Date.Type = Constant.DatabaseColumn.Date;
            this.Date.Visible = false;
            this.Time = ControlExpectations.CreateNote(Constant.DatabaseColumn.Time, id++);
            this.Time.Copyable = false;
            this.Time.DefaultValue = Constant.ControlDefault.Value;
            this.Time.List = Constant.ControlDefault.Value;
            this.Time.TextBoxWidth = Int32.Parse(Constant.ControlDefault.TimeWidth);
            this.Time.Tooltip = Constant.ControlDefault.TimeTooltip;
            this.Time.Type = Constant.DatabaseColumn.Time;
            this.Time.Visible = false;
            this.ImageQuality = ControlExpectations.CreateChoice(Constant.DatabaseColumn.ImageQuality, id++);
            this.ImageQuality.Copyable = false;
            this.ImageQuality.DefaultValue = Constant.ControlDefault.Value;
            this.ImageQuality.List = Constant.ImageQuality.ListOfValues;
            this.ImageQuality.TextBoxWidth = Int32.Parse(Constant.ControlDefault.ImageQualityWidth);
            this.ImageQuality.Tooltip = Constant.ControlDefault.ImageQualityTooltip;
            this.ImageQuality.Type = Constant.DatabaseColumn.ImageQuality;
            this.DeleteFlag = ControlExpectations.CreateFlag(Constant.DatabaseColumn.DeleteFlag, id++);
            this.DeleteFlag.Copyable = false;
            this.DeleteFlag.Label = Constant.ControlDefault.DeleteFlagLabel;
            this.DeleteFlag.List = String.Empty;
            this.DeleteFlag.Tooltip = Constant.ControlDefault.DeleteFlagTooltip;
            this.DeleteFlag.Type = Constant.DatabaseColumn.DeleteFlag;

            if (version < TestConstant.Version2104)
            {
                this.File.DefaultValue = " ";
                this.File.List = " ";
                this.File.Tooltip = "The image file name";

                this.Date.DefaultValue = " ";
                this.Date.List = " ";
                this.Date.TextBoxWidth = 100;
                this.Date.Tooltip = "Date the image was taken";

                this.Folder.DefaultValue = " ";
                this.Folder.List = " ";
                this.Folder.Tooltip = "Name of the folder containing the images";

                this.Time.DefaultValue = " ";
                this.Time.List = " ";
                this.Time.TextBoxWidth = 100;
                this.Time.Tooltip = "Time the image was taken";

                this.ImageQuality.DefaultValue = " ";
                this.ImageQuality.List = "Ok| Dark| Corrupted | Missing";
                this.ImageQuality.TextBoxWidth = 80;
                this.ImageQuality.Tooltip = "System-determined image quality: Ok, dark if mostly black, corrupted if it can not be read";

                this.DeleteFlag.Tooltip = "Mark a file as one to be deleted. You can then confirm deletion through the Edit Menu";
            }
        }

        public virtual void Verify(TemplateDatabase templateDatabase)
        {
            Assert.IsTrue(templateDatabase.Controls.RowCount == TestConstant.DefaultDataColumns.Count - 1);

            int rowIndex = 0;
            this.File.Verify(templateDatabase.Controls[rowIndex++]);
            this.RelativePath.Verify(templateDatabase.Controls[rowIndex++]);
            this.Folder.Verify(templateDatabase.Controls[rowIndex++]);
            this.DateTime.Verify(templateDatabase.Controls[rowIndex++]);
            this.UtcOffset.Verify(templateDatabase.Controls[rowIndex++]);
            this.Date.Verify(templateDatabase.Controls[rowIndex++]);
            this.Time.Verify(templateDatabase.Controls[rowIndex++]);
            this.ImageQuality.Verify(templateDatabase.Controls[rowIndex++]);
            this.DeleteFlag.Verify(templateDatabase.Controls[rowIndex++]);
        }
    }
}
