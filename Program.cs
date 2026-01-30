using AudioCaptureAPI;
using AudioDeviceApi;
using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<AudioCaptureService>();

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseStaticFiles();

// Enable Swagger middleware
app.UseSwagger();
app.UseSwaggerUI();

// Enable WebSockets
app.UseWebSockets();

// API endpoint to get audio devices
app.MapGet("/api/devices", () =>
{
    try
    {
        var audioService = new AudioDeviceService();
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

app.Run();

// Request/Response Models - using classes instead of records
public class SelectDeviceRequest
{
    public string DeviceId { get; set; } = string.Empty;
}

public class SelectDeviceResponse
{
    public string Message { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
}

public class StatusResponse
{
    public string Message { get; set; } = string.Empty;
}

public class ApiInfoResponse
{
    public string Message { get; set; } = string.Empty;
    public string SwaggerUI { get; set; } = string.Empty;
    public string TestPage { get; set; } = string.Empty;
}