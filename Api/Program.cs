using Api;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Prometheus;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<MetricReporter>();

var appName = builder.Environment.ApplicationName;

var oltpUrl = new Uri("http://otel-collector:4317");
// Configure metrics
builder.Services.AddOpenTelemetryMetrics(builder =>
{
    builder.AddHttpClientInstrumentation();
    builder.AddAspNetCoreInstrumentation();
    builder.AddMeter(appName);
    builder.AddOtlpExporter(options => options.Endpoint = oltpUrl);
    builder.AddConsoleExporter();

    builder.AddPrometheusExporter(options =>
    {
        options.StartHttpListener = true;
        // Use your endpoint and port here
        options.HttpListenerPrefixes = new string[] { $"http://prometheus:{9090}/" };
        options.ScrapeResponseCacheDurationMilliseconds = 0;
    });

});

//var filter = new MetricsFilter().WhereType(App.Metrics.MetricType.Timer);
//var metrics = new MetricsBuilder()
//    .Report.ToInfluxDb(
//        options =>
//        {
//            options.InfluxDb.BaseUri = new Uri("http://127.0.0.1:8086");
//            options.InfluxDb.Database = "metricsdatabase";
//            options.InfluxDb.Consistenency = "consistency";
//            options.InfluxDb.UserName = "admin";
//            options.InfluxDb.Password = "password";
//            options.InfluxDb.RetentionPolicy = "rp";
//            options.InfluxDb.CreateDataBaseIfNotExists = true;
//            options.HttpPolicy.BackoffPeriod = TimeSpan.FromSeconds(30);
//            options.HttpPolicy.FailuresBeforeBackoff = 5;
//            options.HttpPolicy.Timeout = TimeSpan.FromSeconds(10);
//            options.MetricsOutputFormatter = new MetricsInfluxDbLineProtocolOutputFormatter();
//            options.Filter = filter;
//            options.FlushInterval = TimeSpan.FromSeconds(20);
//        })

//    .Build();

// Configure tracing
builder.Services.AddOpenTelemetryTracing(builder =>
{
    builder.AddHttpClientInstrumentation();
    builder.AddAspNetCoreInstrumentation();
    builder.AddSource(appName);
    builder.AddOtlpExporter(options => options.Endpoint = oltpUrl);
    builder.AddConsoleExporter();

});


// Configure logging
builder.Logging.AddOpenTelemetry(builder =>
{
    builder.IncludeFormattedMessage = true;
    builder.IncludeScopes = true;
    builder.ParseStateValues = true;
    builder.AddOtlpExporter(options => options.Endpoint = oltpUrl);
    builder.AddConsoleExporter();

});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpLogging();
app.UseMetricServer();
app.UseRequestMetricTrackMiddleware();

app.UseHttpMetrics(options =>
{
    options.AddCustomLabel("host", context => context.Request.Host.Host);
    options.AddCustomLabel("appName", _ => builder.Environment.ApplicationName);

    // Assume there exists a custom route parameter with this name.
    // options.AddRouteParameter("api-version");

});
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};


var MyActivitySource = new ActivitySource(appName);
Counter counter = Metrics.CreateCounter("weatherforecast_visiter", "page_visit_counter");

app.MapGet("/weatherforecast", () =>
{
    counter.Inc();
    // Track work inside of the request
    using var activity = MyActivitySource.StartActivity("Getting forecast");


    var forecast = Enumerable.Range(1, 5).Select(index =>
       new WeatherForecast
       (
           DateTime.Now.AddDays(index),
           Random.Shared.Next(-20, 55),
           summaries[Random.Shared.Next(summaries.Length)]
       ))
        .ToArray();
    activity?.SetTag("forecast_count", forecast.Count());
    return forecast;
})
.WithName("GetWeatherForecast");


app.Run();

internal record WeatherForecast(DateTime Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}