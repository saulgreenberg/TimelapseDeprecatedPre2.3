using System;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    internal class DefaultTemplateTableExpectation : TemplateTableExpectation
    {
        public ControlExpectations Counter0 { get; private set; }
        public ControlExpectations Choice0 { get; private set; }
        public ControlExpectations Note0 { get; private set; }
        public ControlExpectations Flag0 { get; private set; }
        public ControlExpectations CounterWithCustomDataLabel { get; private set; }
        public ControlExpectations ChoiceWithCustomDataLabel { get; private set; }
        public ControlExpectations NoteWithCustomDataLabel { get; private set; }
        public ControlExpectations FlagWithCustomDataLabel { get; private set; }
        public ControlExpectations CounterNotVisible { get; private set; }
        public ControlExpectations ChoiceNotVisible { get; private set; }
        public ControlExpectations NoteNotVisible { get; private set; }
        public ControlExpectations FlagNotVisible { get; private set; }
        public ControlExpectations Counter3 { get; private set; }
        public ControlExpectations Choice3 { get; private set; }
        public ControlExpectations Note3 { get; private set; }
        public ControlExpectations Flag3 { get; private set; }

        public DefaultTemplateTableExpectation(Version version)
            : base(version)
        {
            long id = Constant.Control.StandardTypes.Count + 1;
            this.Counter0 = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.Counter0, id++);
            this.Counter0.List = " ";
            this.Choice0 = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.Choice0, id++);
            this.Choice0.DefaultValue = " ";
            this.Choice0.List = "choice a|choice b|choice c";
            this.Note0 = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.Note0, id++);
            this.Note0.DefaultValue = " ";
            this.Note0.List = " ";
            this.Flag0 = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.Flag0, id++);
            this.Flag0.List = " ";
            this.CounterWithCustomDataLabel = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, id++);
            this.CounterWithCustomDataLabel.DefaultValue = "100";
            this.CounterWithCustomDataLabel.Label = "CounterWithCustomLabel";
            this.CounterWithCustomDataLabel.List = " ";
            this.CounterWithCustomDataLabel.TextBoxWidth = 75;
            this.CounterWithCustomDataLabel.Tooltip = "Counter with custom label.";
            this.ChoiceWithCustomDataLabel = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel, id++);
            this.ChoiceWithCustomDataLabel.DefaultValue = " ";
            this.ChoiceWithCustomDataLabel.Label = "ChoiceWithCustomLabel";
            this.ChoiceWithCustomDataLabel.List = " ";
            this.ChoiceWithCustomDataLabel.List = "value|Genus species|Genus species subspecies|with , comma|with ' apostrophe";
            this.ChoiceWithCustomDataLabel.TextBoxWidth = 90;
            this.ChoiceWithCustomDataLabel.Tooltip = "Choice with custom label and some values of interest.";
            this.NoteWithCustomDataLabel = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, id++);
            this.NoteWithCustomDataLabel.DefaultValue = " ";
            this.NoteWithCustomDataLabel.Label = "NoteWithCustomLabel";
            this.NoteWithCustomDataLabel.List = " ";
            this.NoteWithCustomDataLabel.TextBoxWidth = 200;
            this.NoteWithCustomDataLabel.Tooltip = "Note with custom label.";
            this.FlagWithCustomDataLabel = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel, id++);
            this.FlagWithCustomDataLabel.DefaultValue = Constant.Boolean.True;
            this.FlagWithCustomDataLabel.Label = "FlagWithCustomLabel";
            this.FlagWithCustomDataLabel.List = " ";
            this.FlagWithCustomDataLabel.TextBoxWidth = 30;
            this.FlagWithCustomDataLabel.Tooltip = "Flag with custom label.";

            this.CounterNotVisible = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.CounterNotVisible, id++);
            this.CounterNotVisible.DefaultValue = "3";
            this.CounterNotVisible.Label = "InvisibleCounter";
            this.CounterNotVisible.List = " ";
            this.CounterNotVisible.TextBoxWidth = 111;
            this.CounterNotVisible.Tooltip = "Counter which can't be seen.";
            this.CounterNotVisible.Visible = false;
            this.ChoiceNotVisible = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, id++);
            this.ChoiceNotVisible.DefaultValue = " ";
            this.ChoiceNotVisible.Label = "InvisibleChoice";
            this.ChoiceNotVisible.List = "you can't see me";
            this.ChoiceNotVisible.TextBoxWidth = 150;
            this.ChoiceNotVisible.Tooltip = "Choice which can't be seen.";
            this.ChoiceNotVisible.Visible = false;
            this.NoteNotVisible = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.NoteNotVisible, id++);
            this.NoteNotVisible.DefaultValue = " ";
            this.NoteNotVisible.Label = "InvisibleNote";
            this.NoteNotVisible.List = " ";
            this.NoteNotVisible.TextBoxWidth = 32;
            this.NoteNotVisible.Tooltip = "Note which can't be seen.";
            this.NoteNotVisible.Visible = false;
            this.FlagNotVisible = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.FlagNotVisible, id++);
            this.FlagNotVisible.Label = "InvisibleFlag";
            this.FlagNotVisible.List = " ";
            this.FlagNotVisible.TextBoxWidth = 17;
            this.FlagNotVisible.Tooltip = "Flag which can't be seen.";
            this.FlagNotVisible.Visible = false;

            this.Counter3 = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.Counter3, id++);
            this.Counter3.Copyable = true;
            this.Counter3.Label = "CopyableCounter";
            this.Counter3.List = " ";
            this.Counter3.Tooltip = "Counter which can be copied (rather than the default of not being copyable).";
            this.Choice3 = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.Choice3, id++);
            this.Choice3.Copyable = false;
            this.Choice3.DefaultValue = " ";
            this.Choice3.Label = "NotCopyableChoice";
            this.Choice3.List = " ";
            this.Choice3.Tooltip = "Choice which can't be copied (rather than the default of copyable).";
            this.Note3 = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.Note3, id++);
            this.Note3.Copyable = false;
            this.Note3.DefaultValue = " ";
            this.Note3.Label = "NotCopyableNote";
            this.Note3.List = " ";
            this.Note3.Tooltip = "Note which can't be copied (rather than the default of copyable).";
            this.Flag3 = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.Flag3, id++);
            this.Flag3.Copyable = false;
            this.Flag3.Label = "NotCopyableFlag";
            this.Flag3.List = " ";
            this.Flag3.Tooltip = "Flag which can't be copied (rather than the default of copyable).";
        }

        public override void Verify(TemplateDatabase templateDatabase)
        {
            base.Verify(templateDatabase);

            int rowIndex = Constant.Control.StandardTypes.Count;
            this.Counter0.Verify(templateDatabase.Controls[rowIndex++]);
            this.Choice0.Verify(templateDatabase.Controls[rowIndex++]);
            this.Note0.Verify(templateDatabase.Controls[rowIndex++]);
            this.Flag0.Verify(templateDatabase.Controls[rowIndex++]);
            this.CounterWithCustomDataLabel.Verify(templateDatabase.Controls[rowIndex++]);
            this.ChoiceWithCustomDataLabel.Verify(templateDatabase.Controls[rowIndex++]);
            this.NoteWithCustomDataLabel.Verify(templateDatabase.Controls[rowIndex++]);
            this.FlagWithCustomDataLabel.Verify(templateDatabase.Controls[rowIndex++]);
            this.CounterNotVisible.Verify(templateDatabase.Controls[rowIndex++]);
            this.ChoiceNotVisible.Verify(templateDatabase.Controls[rowIndex++]);
            this.NoteNotVisible.Verify(templateDatabase.Controls[rowIndex++]);
            this.FlagNotVisible.Verify(templateDatabase.Controls[rowIndex++]);
            this.Counter3.Verify(templateDatabase.Controls[rowIndex++]);
            this.Choice3.Verify(templateDatabase.Controls[rowIndex++]);
            this.Note3.Verify(templateDatabase.Controls[rowIndex++]);
            this.Flag3.Verify(templateDatabase.Controls[rowIndex++]);
        }
    }
}
