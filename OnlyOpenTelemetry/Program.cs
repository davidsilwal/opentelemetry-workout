using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var appName = builder.Environment.ApplicationName;

builder.Services.AddOpenTelemetryMetrics(builder =>
{
    builder.AddHttpClientInstrumentation();
    builder.AddAspNetCoreInstrumentation();
    builder.AddMeter(appName);
    builder.AddConsoleExporter();

    builder.AddPrometheusExporter(options =>
    {
        options.StartHttpListener = true;
        // Use your endpoint and port here
        options.HttpListenerPrefixes = new string[] { $"http://prometheus:{9090}/" };
        options.ScrapeResponseCacheDurationMilliseconds = 0;
    });

});

builder.Services.AddOpenTelemetryTracing(builder =>
{
    builder.AddHttpClientInstrumentation();
    builder.AddAspNetCoreInstrumentation();
    builder.AddSource(appName);
    builder.AddConsoleExporter();

});

builder.Logging.AddOpenTelemetry(builder =>
{
    builder.IncludeFormattedMessage = true;
    builder.IncludeScopes = true;
    builder.ParseStateValues = true;
    builder.AddConsoleExporter();
});



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

var MyActivitySource = new ActivitySource(appName);

var meter = new Meter("MyApplication");

var counter = meter.CreateCounter<int>("Requests");
var histogram = meter.CreateHistogram<float>("RequestDuration", unit: "ms");
meter.CreateObservableGauge("ThreadCount", () => new[] { new Measurement<int>(ThreadPool.ThreadCount) });

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapGet("/weatherforecast", () =>
{
    using var activity = MyActivitySource.StartActivity("weatherforecast");

    // Measure the number of requests
    counter.Add(1, KeyValuePair.Create<string, object?>("name", "weatherforecast"));

    var stopwatch = Stopwatch.StartNew();

    var forecast = Enumerable.Range(1, 5).Select(index =>
       new WeatherForecast
       (
           DateTime.Now.AddDays(index),
           Random.Shared.Next(-20, 55),
           summaries[Random.Shared.Next(summaries.Length)]
       ))
        .ToArray();
    activity?.SetTag("forecast_count", forecast.Length);


    // Measure the duration in ms of requests and includes the host in the tags
    histogram.Record(stopwatch.ElapsedMilliseconds,
        tag: KeyValuePair.Create<string, object?>("Host", "www.meziantou.net"));

    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

internal record WeatherForecast(DateTime Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}