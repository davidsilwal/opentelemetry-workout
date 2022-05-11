using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Formatters.InfluxDB;
using App.Metrics.Formatters.Json;
using AppMetricss;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAppMetricsCollectors();


//var filter = new MetricsFilter().WhereType(MetricType.Timer);
var metrics = AppMetrics.CreateDefaultBuilder()
    .Configuration.Configure(options =>
    {
        options.DefaultContextLabel = builder.Environment.ApplicationName;

        options.WithGlobalTags((globalTags, envInfo) =>
        {
            globalTags.Add("env", envInfo.RunningEnvironment);
            globalTags.Add("machine_name", envInfo.MachineName);
            globalTags.Add("app_name", envInfo.EntryAssemblyName);
            globalTags.Add("app_version", envInfo.EntryAssemblyVersion);
        });
        options.Enabled = true;
        options.ReportingEnabled = true;
    })
    .Report.ToConsole(
        options =>
        {
            //  options.FlushInterval = TimeSpan.FromSeconds(5);
            //    options.Filter = filter;
            options.MetricsOutputFormatter = new MetricsJsonOutputFormatter();
        })
     .Report.ToTextFile(
        options =>
        {
            options.MetricsOutputFormatter = new MetricsJsonOutputFormatter();
            options.AppendMetricsToTextFile = true;
            //   options.Filter = filter;
            //    options.FlushInterval = TimeSpan.FromSeconds(20);
            options.OutputPathAndFileName = "./metrics.txt";
        })
     .Report.ToInfluxDb(
        options =>
        {
            options.InfluxDb.BaseUri = new Uri("http://127.0.0.1:8086");
            options.InfluxDb.Database = "metricsdatabase";
            options.InfluxDb.Consistenency = "consistency";
            options.InfluxDb.UserName = "admin";
            options.InfluxDb.Password = "password";
            options.InfluxDb.RetentionPolicy = "rp";
            options.InfluxDb.CreateDataBaseIfNotExists = true;
            options.HttpPolicy.BackoffPeriod = TimeSpan.FromSeconds(30);
            options.HttpPolicy.FailuresBeforeBackoff = 5;
            options.HttpPolicy.Timeout = TimeSpan.FromSeconds(10);
            options.MetricsOutputFormatter = new MetricsInfluxDbLineProtocolOutputFormatter();
            //    options.Filter = filter;
            //   options.FlushInterval = TimeSpan.FromSeconds(20);
        })
     .OutputMetrics.AsPrometheusPlainText()
     .OutputMetrics.AsPrometheusProtobuf()
    .Build();

builder.Services.AddMetrics(metrics);
builder.Services.AddMetricsTrackingMiddleware();

builder.Host.UseMetricsWebTracking();

builder.WebHost.UseMetrics(options =>
{
    options.EndpointOptions = endpointsOptions =>
    {
        endpointsOptions.MetricsTextEndpointOutputFormatter = metrics.OutputMetricsFormatters.OfType<App.Metrics.Formatters.Prometheus.MetricsPrometheusTextOutputFormatter>().First();
        endpointsOptions.MetricsEndpointOutputFormatter = metrics.OutputMetricsFormatters.OfType<App.Metrics.Formatters.Prometheus.MetricsPrometheusProtobufOutputFormatter>().First();
    };
});
builder.WebHost.UseMetricsWebTracking();
builder.WebHost.UseMetricsEndpoints();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMetricsAllMiddleware();
app.UseMetricsAllEndpoints();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", (IMetrics metrics) =>
{
    metrics.Measure.Counter.Increment(MetricsRegistry.SampleCounter);

    var forecast = Enumerable.Range(1, 5).Select(index =>
       new WeatherForecast
       (
           DateTime.Now.AddDays(index),
           Random.Shared.Next(-20, 55),
           summaries[Random.Shared.Next(summaries.Length)]
       ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

internal record WeatherForecast(DateTime Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}