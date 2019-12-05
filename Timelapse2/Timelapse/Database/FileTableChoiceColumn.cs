using System;
using System.Collections.Generic;
using Timelapse.Util;

namespace Timelapse.Database
{
    public class FileTableChoiceColumn : FileTableColumn
    {
        private readonly List<string> choices;
        private readonly string defaultValue;

        public FileTableChoiceColumn(ControlRow control)
            : base(control)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));

            this.choices = control.GetChoices(false);
            this.defaultValue = control.DefaultValue;
        }

        public override bool IsContentValid(string value)
        {
            // the editor doesn't currently enforce the default value is one of the choices, so accept it as valid independently
            if (value == this.defaultValue)
            {
                return true;
            }
            return this.choices.Contains(value);
        }
    }
}
