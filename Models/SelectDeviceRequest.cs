namespace Audio.Models
{
    /// <summary>
    /// Request model for selecting an audio device.
    /// </summary>
    public class SelectDeviceRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier of the device to select.
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;
    }

}
