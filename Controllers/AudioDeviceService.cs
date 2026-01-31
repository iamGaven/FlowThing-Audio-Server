using Audio.Models;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;

namespace Audio.Controllers
{
    /// <summary>
    /// Service for managing and retrieving audio device information.
    /// </summary>
    public class AudioDeviceService
    {
        private MMDeviceEnumerator _enumerator;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioDeviceService"/> class.
        /// Creates an MMDeviceEnumerator for querying audio devices.
        /// </summary>
        /// <exception cref="Exception">Thrown when the MMDeviceEnumerator cannot be created.</exception>
        public AudioDeviceService()
        {
            try
            {
                _enumerator = new MMDeviceEnumerator();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating MMDeviceEnumerator: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Retrieves all active audio devices, categorized by render (output) and capture (input) devices.
        /// </summary>
        /// <returns>
        /// An anonymous object containing RenderDevices and CaptureDevices lists, 
        /// or an error object if retrieval fails.
        /// </returns>
        public object GetAllDevices()
        {
            try
            {
                return new
                {
                    RenderDevices = GetDeviceList(DataFlow.Render),
                    CaptureDevices = GetDeviceList(DataFlow.Capture)
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting devices: {ex.Message}");
                return new
                {
                    Error = ex.Message,
                    RenderDevices = new List<AudioDeviceInfo>(),
                    CaptureDevices = new List<AudioDeviceInfo>()
                };
            }
        }

        /// <summary>
        /// Retrieves a list of active audio devices for the specified data flow direction.
        /// </summary>
        /// <param name="dataFlow">The data flow direction (Render for output devices, Capture for input devices).</param>
        /// <returns>A list of <see cref="AudioDeviceInfo"/> objects representing the active devices.</returns>
        public List<AudioDeviceInfo> GetDeviceList(DataFlow dataFlow)
        {
            var deviceList = new List<AudioDeviceInfo>();
            try
            {
                var devices = _enumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active);
                int index = 0;
                foreach (var device in devices)
                {
                    try
                    {
                        deviceList.Add(new AudioDeviceInfo
                        {
                            Index = index,
                            Name = device.FriendlyName,
                            Id = device.ID,
                            State = device.State.ToString()
                        });
                        index++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading device {index}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enumerating {dataFlow} devices: {ex.Message}");
            }
            return deviceList;
        }
    }

  
}