using Audio.Controllers;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5012");

// Add services
builder.Services.AddSingleton<AudioCaptureService>();
builder.Services.AddSingleton<AudioDeviceService>();

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

// Map all API endpoints
AudioAPI.MapEndpoints(app);

app.Run();