using System;

namespace Timelapse.Database
{
    public class ColumnDefinition
    {
        public string DefaultValue { get; private set; }
        public string Name { get; private set; }
        public string Type { get; private set; }

        public ColumnDefinition(string name, string type)
            : this(name, type, null)
        {
        }

        public ColumnDefinition(string name, string type, int defaultValue)
            : this(name, type, defaultValue.ToString())
        {
        }

        public ColumnDefinition(string name, string type, string defaultValue)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentOutOfRangeException(nameof(name));
            }
            if (String.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }

            this.DefaultValue = defaultValue;
            this.Name = name;
            this.Type = type;
        }

        public override string ToString()
        {
            string columnDefinition = String.Format("{0} {1}", this.Name, this.Type);
            if (this.DefaultValue != null)
            {
                columnDefinition += " DEFAULT " + Sql.Quote(this.DefaultValue);
            }
            return columnDefinition;
        }
    }
}
