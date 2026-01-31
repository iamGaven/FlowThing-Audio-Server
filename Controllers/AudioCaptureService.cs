using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Audio.Controllers
{
    /// <summary>
    /// Service for capturing audio from system devices and streaming it to connected WebSocket clients.
    /// </summary>
    public class AudioCaptureService
    {
        private MMDevice _selectedDevice;
        private WasapiLoopbackCapture _capture;
        private readonly ConcurrentBag<WebSocket> _connectedClients = new ConcurrentBag<WebSocket>();
        private bool _isCapturing = false;
        private readonly object _lock = new object();

        // Downsampling settings
        private const int DOWNSAMPLE_FACTOR = 4; // Send every 4th sample (reduces data by 75%)

        /// <summary>
        /// Selects an audio device for capture by its device ID.
        /// If capture is currently running, it will be stopped before selecting the new device.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the audio device to select.</param>
        /// <exception cref="Exception">Thrown when the device with the specified ID is not found.</exception>
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

        /// <summary>
        /// Starts capturing audio from the selected device.
        /// The captured audio is downsampled and broadcast to all connected WebSocket clients.
        /// </summary>
        /// <exception cref="Exception">Thrown when no device is selected or capture is already running.</exception>
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

        /// <summary>
        /// Stops the audio capture if it is currently running and disposes of the capture resources.
        /// </summary>
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

        /// <summary>
        /// Event handler called when audio data is available from the capture device.
        /// Downsamples the audio data and broadcasts it to all connected clients.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments containing the audio buffer and byte count.</param>
        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            // Downsample audio data before broadcasting
            byte[] downsampledData = DownsampleAudio(e.Buffer, e.BytesRecorded);
            BroadcastAudioData(downsampledData, downsampledData.Length);
        }

        /// <summary>
        /// Downsamples audio data by taking every Nth frame based on the DOWNSAMPLE_FACTOR.
        /// This reduces the data size by approximately 75% while maintaining audio quality.
        /// </summary>
        /// <param name="buffer">The original audio buffer.</param>
        /// <param name="bytesRecorded">The number of bytes recorded in the buffer.</param>
        /// <returns>A new byte array containing the downsampled audio data.</returns>
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

        /// <summary>
        /// Broadcasts audio data to all connected WebSocket clients asynchronously.
        /// </summary>
        /// <param name="buffer">The audio data buffer to broadcast.</param>
        /// <param name="bytesRecorded">The number of bytes to send from the buffer.</param>
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

        /// <summary>
        /// Handles a WebSocket connection for streaming audio data to a client.
        /// Sends initial wave format information and maintains the connection until closed.
        /// </summary>
        /// <param name="webSocket">The WebSocket connection to handle.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
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

        /// <summary>
        /// Gets the current status of the audio capture service.
        /// </summary>
        /// <returns>
        /// An anonymous object containing capture status, selected device information,
        /// connected client count, and wave format details.
        /// </returns>
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