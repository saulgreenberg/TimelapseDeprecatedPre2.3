namespace Timelapse.Data.FileData
{
    using Timelapse.Common;

    /// <summary>
    /// Represents a non-standard property of a <see cref="FileModel"/>. This is used to support user configured meta-data associated with the base file model.
    /// </summary>
    public class UserDefinedFileData
    {
        /// <summary>
        /// Gets or sets the type of control.
        /// </summary>
        public ControlType ControlType { get; set; }

        /// <summary>
        /// Gets or sets the name of the user defined property.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the default value for
        /// </summary>
        /// <remarks>TODO: Does this need to be more dynamic and include a value data type and store the value as an object?</remarks>
        public string Value { get; set; }
    }
}