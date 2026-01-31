namespace Audio.Models
{

    /// <summary>
    /// Response model for general status messages.
    /// </summary>
    public class StatusResponse
    {
        /// <summary>
        /// Gets or sets the status message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
