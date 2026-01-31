namespace Audio.Models
{
    /// <summary>
    /// Represents information about an audio device.
    /// </summary>
    public class AudioDeviceInfo
    {
        /// <summary>
        /// Gets or sets the index of the device in the enumeration.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the friendly name of the audio device.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the audio device.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the current state of the audio device.
        /// </summary>
        public string State { get; set; }
    }
}
