namespace Timelapse.Dialog
{
    public class DateTimeRereadFeedbackTuple
    {
        public string FileName { get; set; }
        public string Message { get; set; }

        public DateTimeRereadFeedbackTuple(string fileName, string message)
        {
            this.FileName = fileName;
            this.Message = message;
        }
    }
}
