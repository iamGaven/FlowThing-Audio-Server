namespace Audio.Models
{

    /// <summary>
    /// Response model for general status messages.
    /// </summary>
    public class SelectDeviceResponse
    {
        /// <summary>
        /// Gets or sets the response message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ID of the selected device.
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;
    }
}
