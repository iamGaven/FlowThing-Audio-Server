using Audio.Models;
using System.Net.WebSockets;

namespace Audio.Controllers
{
    /// <summary>
    /// Contains all API endpoint mappings for the audio capture application.
    /// </summary>
    public static class AudioAPI
    {
        /// <summary>
        /// Maps all API endpoints to the application.
        /// </summary>
        /// <param name="app">The WebApplication instance to configure.</param>
        public static void MapEndpoints(WebApplication app)
        {
            MapDeviceEndpoints(app);
            MapCaptureEndpoints(app);
            MapWebSocketEndpoint(app);
            MapRootEndpoint(app);
        }

        /// <summary>
        /// Maps device-related endpoints.
        /// </summary>
        /// <param name="app">The WebApplication instance to configure.</param>
        private static void MapDeviceEndpoints(WebApplication app)
        {
            // API endpoint to get audio devices
            app.MapGet("/api/devices", (AudioDeviceService audioService) =>
            {
                try
                {
                    var devices = audioService.GetAllDevices();
                    return Results.Ok(devices);
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            })
            .WithName("GetDevices")
            .WithTags("Devices");

            // API endpoint to select a device for capture
            app.MapPost("/api/devices/select", (SelectDeviceRequest request, AudioCaptureService captureService) =>
            {
                try
                {
                    captureService.SelectDevice(request.DeviceId);
                    return Results.Ok(new SelectDeviceResponse
                    {
                        Message = "Device selected successfully",
                        DeviceId = request.DeviceId
                    });
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            })
            .WithName("SelectDevice")
            .WithTags("Devices");
        }

        /// <summary>
        /// Maps audio capture-related endpoints.
        /// </summary>
        /// <param name="app">The WebApplication instance to configure.</param>
        private static void MapCaptureEndpoints(WebApplication app)
        {
            // API endpoint to start capture
            app.MapPost("/api/capture/start", (AudioCaptureService captureService) =>
            {
                try
                {
                    captureService.StartCapture();
                    return Results.Ok(new StatusResponse { Message = "Capture started" });
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            })
            .WithName("StartCapture")
            .WithTags("Capture");

            // API endpoint to stop capture
            app.MapPost("/api/capture/stop", (AudioCaptureService captureService) =>
            {
                try
                {
                    captureService.StopCapture();
                    return Results.Ok(new StatusResponse { Message = "Capture stopped" });
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            })
            .WithName("StopCapture")
            .WithTags("Capture");

            // API endpoint to get current capture status
            app.MapGet("/api/capture/status", (AudioCaptureService captureService) =>
            {
                var status = captureService.GetStatus();
                return Results.Ok(status);
            })
            .WithName("GetCaptureStatus")
            .WithTags("Capture");
        }

        /// <summary>
        /// Maps the WebSocket endpoint for audio streaming.
        /// </summary>
        /// <param name="app">The WebApplication instance to configure.</param>
        private static void MapWebSocketEndpoint(WebApplication app)
        {
            // WebSocket endpoint for streaming audio
            app.Map("/ws/audio", async (HttpContext context, AudioCaptureService captureService) =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await captureService.HandleWebSocketConnection(webSocket);
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            })
            .ExcludeFromDescription();
        }

        /// <summary>
        /// Maps the root endpoint for API information.
        /// </summary>
        /// <param name="app">The WebApplication instance to configure.</param>
        private static void MapRootEndpoint(WebApplication app)
        {
            // Root endpoint for testing
            app.MapGet("/", () =>
            {
                return Results.Ok(new ApiInfoResponse
                {
                    Message = "Audio Device API is running!",
                    SwaggerUI = "http://localhost:5000/swagger",
                    TestPage = "Open test-api.html in your browser"
                });
            })
            .ExcludeFromDescription();
        }
    }
}