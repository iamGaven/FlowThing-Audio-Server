using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace AudioCaptureAPI
{
    public class AudioCaptureService
    {
        private MMDevice _selectedDevice;
        private WasapiLoopbackCapture _capture;
        private readonly ConcurrentBag<WebSocket> _connectedClients = new ConcurrentBag<WebSocket>();
        private bool _isCapturing = false;
        private readonly object _lock = new object();

        // Downsampling settings
        private const int DOWNSAMPLE_FACTOR = 4; // Send every 4th sample (reduces data by 75%)

        public void SelectDevice(string deviceId)
        {
            lock (_lock)
            {
                // Stop any existing capture
                if (_isCapturing)
                {
                    StopCapture();
                }

                var enumerator = new MMDeviceEnumerator();

                // Search through ALL devices (both render and capture)
                var allDevices = enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active);
                foreach (var device in allDevices)
                {
                    if (device.ID == deviceId)
                    {
                        _selectedDevice = device;
                        Console.WriteLine($"Selected device: {device.FriendlyName} (Type: {device.DataFlow})");
                        return;
                    }
                }

                throw new Exception($"Device with ID {deviceId} not found");
            }
        }

        public void StartCapture()
        {
            lock (_lock)
            {
                if (_selectedDevice == null)
                {
                    throw new Exception("No device selected. Please select a device first using /api/devices/select");
                }

                if (_isCapturing)
                {
                    throw new Exception("Capture is already running");
                }

                // Create the capture instance with the selected device
                _capture = new WasapiLoopbackCapture(_selectedDevice);

                Console.WriteLine($"Starting capture on: {_selectedDevice.FriendlyName}");
                Console.WriteLine($"Capture format: {_capture.WaveFormat.SampleRate}Hz, {_capture.WaveFormat.BitsPerSample}bit, {_capture.WaveFormat.Channels}ch");
                Console.WriteLine($"Downsampling factor: {DOWNSAMPLE_FACTOR}x (effective rate: {_capture.WaveFormat.SampleRate / DOWNSAMPLE_FACTOR}Hz)");

                // Handle incoming audio data
                _capture.DataAvailable += OnDataAvailable;

                // Handle recording stopped
                _capture.RecordingStopped += (s, e) =>
                {
                    Console.WriteLine("Recording stopped");
                    _isCapturing = false;
                };

                _capture.StartRecording();
                _isCapturing = true;

                Console.WriteLine("Capture started successfully");
            }
        }

        public void StopCapture()
        {
            lock (_lock)
            {
                if (_capture != null && _isCapturing)
                {
                    _capture.StopRecording();
                    _capture.Dispose();
                    _capture = null;
                    _isCapturing = false;
                    Console.WriteLine("Capture stopped");
                }
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            // Downsample audio data before broadcasting
            byte[] downsampledData = DownsampleAudio(e.Buffer, e.BytesRecorded);
            BroadcastAudioData(downsampledData, downsampledData.Length);
        }

        private byte[] DownsampleAudio(byte[] buffer, int bytesRecorded)
        {
            if (_capture == null) return buffer;

            int bytesPerSample = _capture.WaveFormat.BitsPerSample / 8;
            int channels = _capture.WaveFormat.Channels;
            int bytesPerFrame = bytesPerSample * channels; // One frame = all channels for one sample point

            int totalFrames = bytesRecorded / bytesPerFrame;
            int downsampledFrames = totalFrames / DOWNSAMPLE_FACTOR;
            int downsampledBytes = downsampledFrames * bytesPerFrame;

            byte[] downsampled = new byte[downsampledBytes];
            int writePos = 0;

            // Take every Nth frame
            for (int i = 0; i < totalFrames; i += DOWNSAMPLE_FACTOR)
            {
                int readPos = i * bytesPerFrame;

                // Copy entire frame (all channels)
                if (readPos + bytesPerFrame <= bytesRecorded && writePos + bytesPerFrame <= downsampledBytes)
                {
                    Array.Copy(buffer, readPos, downsampled, writePos, bytesPerFrame);
                    writePos += bytesPerFrame;
                }
            }

            return downsampled;
        }

        private async void BroadcastAudioData(byte[] buffer, int bytesRecorded)
        {
            var audioData = new byte[bytesRecorded];
            Array.Copy(buffer, audioData, bytesRecorded);

            foreach (var client in _connectedClients)
            {
                if (client.State == WebSocketState.Open)
                {
                    try
                    {
                        await client.SendAsync(
                            new ArraySegment<byte>(audioData),
                            WebSocketMessageType.Binary,
                            true,
                            CancellationToken.None
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending to client: {ex.Message}");
                    }
                }
            }
        }

        public async Task HandleWebSocketConnection(WebSocket webSocket)
        {
            _connectedClients.Add(webSocket);
            Console.WriteLine($"WebSocket client connected. Total clients: {_connectedClients.Count}");

            // Send the wave format information first (with downsampled sample rate)
            if (_capture != null)
            {
                var formatInfo = System.Text.Json.JsonSerializer.Serialize(new
                {
                    sampleRate = _capture.WaveFormat.SampleRate / DOWNSAMPLE_FACTOR, // Effective sample rate after downsampling
                    bitsPerSample = _capture.WaveFormat.BitsPerSample,
                    channels = _capture.WaveFormat.Channels,
                    encoding = _capture.WaveFormat.Encoding.ToString(),
                    downsampleFactor = DOWNSAMPLE_FACTOR,
                    originalSampleRate = _capture.WaveFormat.SampleRate
                });

                var formatBytes = System.Text.Encoding.UTF8.GetBytes(formatInfo);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(formatBytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }

            var buffer = new byte[1024 * 4];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Closing",
                            CancellationToken.None
                        );
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("WebSocket client disconnected");
            }
        }

        public object GetStatus()
        {
            return new
            {
                isCapturing = _isCapturing,
                selectedDevice = _selectedDevice != null ? new
                {
                    name = _selectedDevice.FriendlyName,
                    id = _selectedDevice.ID
                } : null,
                connectedClients = _connectedClients.Count,
                waveFormat = _capture != null ? new
                {
                    originalSampleRate = _capture.WaveFormat.SampleRate,
                    effectiveSampleRate = _capture.WaveFormat.SampleRate / DOWNSAMPLE_FACTOR,
                    bitsPerSample = _capture.WaveFormat.BitsPerSample,
                    channels = _capture.WaveFormat.Channels,
                    encoding = _capture.WaveFormat.Encoding.ToString(),
                    downsampleFactor = DOWNSAMPLE_FACTOR
                } : null
            };
        }
    }
}