using Prometheus;
using System.Diagnostics;

namespace OnlyPrometheus;


public class MetricReporter
{
    private readonly ILogger<MetricReporter> _logger;
    private readonly Counter _requestCounter;
    private readonly Histogram _responseTimeHistogram;

    public MetricReporter(ILogger<MetricReporter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _requestCounter = Metrics.CreateCounter("myapp_http_requests",
            "The total number of requests serviced by this API.");

        _responseTimeHistogram = Metrics.CreateHistogram("myapp_request_duration_seconds",
            "The duration in seconds between the response to a request.", new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.01, 2, 10),
                LabelNames = new[] { "path", "method", "status_code", "elapsed_sec" }
            });
    }

    public void RegisterRequest()
    {
        _requestCounter.Inc();
    }

    public void RegisterResponseTime(string? path, string method, int statusCode, TimeSpan elapsed)
    {
        _responseTimeHistogram
            .Labels(path!, method,
            statusCode.ToString(),
            elapsed.TotalSeconds.ToString());
    }
}
public static class RequestMetricMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestMetricTrackMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestMetricMiddleware>();
    }
}

public class RequestMetricMiddleware
{
    private readonly RequestDelegate _request;

    public RequestMetricMiddleware(RequestDelegate request)
    {
        _request = request ?? throw new Exception($"Context is empty; {nameof(request)}");
    }

    public async Task Invoke(HttpContext context, MetricReporter reporter)
    {
        if (context.Request.Path.Value == "/metrics-text" || context.Request.Path.Value == "metrics")
        {
            await _request.Invoke(context);
            return;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            await _request.Invoke(context);
        }
        finally
        {
            sw.Stop();
            reporter.RegisterRequest();

            reporter.RegisterResponseTime(context.Request.Path.Value,
                context.Request.Method,
                context.Response.StatusCode,
                sw.Elapsed);
        }
    }
}