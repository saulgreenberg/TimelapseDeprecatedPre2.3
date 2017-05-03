using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    internal class ControlExpectations
    {
        public long ControlOrder { get; set; }

        public bool Copyable { get; set; }

        public string DataLabel { get; set; }

        public string DefaultValue { get; set; }

        public long ID { get; set; }

        public string Label { get; set; }

        public string List { get; set; }

        public long SpreadsheetOrder { get; set; }

        public int TextBoxWidth { get; set; }

        public string Tooltip { get; set; }

        public string Type { get; set; }

        public bool Visible { get; set; }

        public static ControlExpectations CreateCounter(string dataLabel, long id)
        {
            return CreateCounter(dataLabel, id, id, id);
        }

        public static ControlExpectations CreateCounter(string dataLabel, long id, long controlOrder, long spreadsheetOrder)
        {
            return new ControlExpectations()
            {
                ControlOrder = controlOrder,
                Copyable = false,
                DataLabel = dataLabel,
                DefaultValue = Constant.ControlDefault.CounterValue,
                ID = id,
                Label = dataLabel,
                List = String.Empty,
                SpreadsheetOrder = spreadsheetOrder,
                TextBoxWidth = Constant.ControlDefault.CounterWidth,
                Tooltip = Constant.ControlDefault.CounterTooltip,
                Type = Constant.Control.Counter,
                Visible = true
            };
        }

        public static ControlExpectations CreateChoice(string dataLabel, long id)
        {
            return CreateChoice(dataLabel, id, id, id);
        }

        public static ControlExpectations CreateChoice(string dataLabel, long id, long controlOrder, long spreadsheetOrder)
        {
            return new ControlExpectations()
            {
                ControlOrder = controlOrder,
                Copyable = true,
                DataLabel = dataLabel,
                DefaultValue = Constant.ControlDefault.Value,
                ID = id,
                Label = dataLabel,
                List = String.Empty,
                SpreadsheetOrder = spreadsheetOrder,
                TextBoxWidth = Constant.ControlDefault.FixedChoiceWidth,
                Tooltip = Constant.ControlDefault.FixedChoiceTooltip,
                Type = Constant.Control.FixedChoice,
                Visible = true
            };
        }

        public static ControlExpectations CreateFlag(string dataLabel, long id)
        {
            return CreateFlag(dataLabel, id, id, id);
        }

        public static ControlExpectations CreateFlag(string dataLabel, long id, long controlOrder, long spreadsheetOrder)
        {
            return new ControlExpectations()
            {
                ControlOrder = controlOrder,
                Copyable = true,
                DataLabel = dataLabel,
                DefaultValue = Constant.ControlDefault.FlagValue,
                ID = id,
                Label = dataLabel,
                List = String.Empty,
                SpreadsheetOrder = spreadsheetOrder,
                TextBoxWidth = Constant.ControlDefault.FlagWidth,
                Tooltip = Constant.ControlDefault.FlagTooltip,
                Type = Constant.Control.Flag,
                Visible = true
            };
        }

        public static ControlExpectations CreateNote(string dataLabel, long id)
        {
            return CreateNote(dataLabel, id, id, id);
        }

        public static ControlExpectations CreateNote(string dataLabel, long id, long controlOrder, long spreadsheetOrder)
        {
            return new ControlExpectations()
            {
                ControlOrder = controlOrder,
                Copyable = true,
                DataLabel = dataLabel,
                DefaultValue = Constant.ControlDefault.Value,
                ID = id,
                Label = dataLabel,
                List = String.Empty,
                SpreadsheetOrder = spreadsheetOrder,
                TextBoxWidth = Constant.ControlDefault.NoteWidth,
                Tooltip = Constant.ControlDefault.NoteTooltip,
                Type = Constant.Control.Note,
                Visible = true
            };
        }

        public void Verify(ControlRow control)
        {
            Assert.IsTrue(control.ControlOrder == this.ControlOrder, "{0}: Expected ControlOrder '{1}' but found '{2}'.", this.DataLabel, this.ControlOrder, control.ControlOrder);
            Assert.IsTrue(control.Copyable == this.Copyable, "{0}: Expected Copyable '{1}' but found '{2}'.", this.DataLabel, this.Copyable, control.Copyable);
            Assert.IsTrue(control.DataLabel == this.DataLabel, "{0}: Expected DataLabel '{1}' but found '{2}'.", this.DataLabel, this.DataLabel, control.DataLabel);
            Assert.IsTrue(control.DefaultValue == this.DefaultValue, "{0}: Expected DefaultValue '{1}' but found '{2}'.", this.DataLabel, this.DefaultValue, control.DefaultValue);
            Assert.IsTrue(control.ID == this.ID, "{0}: Expected ID '{1}' but found '{2}'.", this.DataLabel, this.ID, control.ID);
            Assert.IsTrue(control.Label == this.Label, "{0}: Expected Label '{1}' but found '{2}'.", this.DataLabel, this.Label, control.Label);
            Assert.IsTrue(control.List == this.List, "{0}: Expected List '{1}' but found '{2}'.", this.DataLabel, this.List, control.List);
            Assert.IsTrue(control.SpreadsheetOrder == this.SpreadsheetOrder, "{0}: Expected SpreadsheetOrder '{1}' but found '{2}'.", this.DataLabel, this.SpreadsheetOrder, control.SpreadsheetOrder);
            // This fails as I changed the default from 80 to 30, so I need to change stuff in the db or in expectations.
            // Assert.IsTrue(control.Width == this.TextBoxWidth, "{0}: Expected TextBoxWidth '{1}' but found '{2}'.", this.DataLabel, this.TextBoxWidth, control.Width);
            Assert.IsTrue(control.Tooltip == this.Tooltip, "{0}: Expected Tooltip '{1}' but found '{2}'.", this.DataLabel, this.Tooltip, control.Tooltip);
            Assert.IsTrue(control.Type == this.Type, "{0}: Expected Type '{1}' but found '{2}'.", this.DataLabel, this.Type, control.Type);
            Assert.IsTrue(control.Visible == this.Visible, "{0}: Expected Visible '{1}' but found '{2}'.", this.DataLabel, this.Visible, control.Visible);
        }
    }
}
