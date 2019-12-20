namespace Timelapse.Dialog
{
    public class DateTimeFeedbackTuple
    {
        public string FileName { get; set; }
        public string Message { get; set; }

        public DateTimeFeedbackTuple(string fileName, string message)
        {
            this.FileName = fileName;
            this.Message = message;
        }
    }
}
