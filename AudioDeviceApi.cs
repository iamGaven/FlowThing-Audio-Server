using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;

namespace AudioDeviceApi
{
    public class AudioDeviceService
    {
        private MMDeviceEnumerator _enumerator;

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

    public class AudioDeviceInfo
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Id { get; set; }
        public string State { get; set; }
    }
}