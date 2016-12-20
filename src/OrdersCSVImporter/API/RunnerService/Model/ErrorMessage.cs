namespace OrdersCSVImporter.API.RunnerService.Model
{
    /// <summary>
    /// Error object to return for the user
    /// </summary>
    public class ErrorMessage
    {
        /// <summary>
        /// Error Message 
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Initializes object
        /// </summary>
        public ErrorMessage()
        {
            Message = "";
        }

        /// <summary>
        /// Initializes object with message
        /// </summary>
        /// <param name="msg"></param>
        public ErrorMessage(string msg)
        {
            Message = msg;
        }
    }
}
