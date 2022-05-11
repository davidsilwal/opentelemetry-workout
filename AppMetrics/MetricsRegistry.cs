using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Histogram;

namespace AppMetricss
{
    public static class MetricsRegistry
    {
        public static CounterOptions SampleCounter => new CounterOptions
        {
            Name = "GetWeatherForecast Counter",
            MeasurementUnit = Unit.Calls
        };

        public static CounterOptions RequestCounter => new CounterOptions
        {
            Name = "myapp_http_requests",
            MeasurementUnit = Unit.Calls
        };

        public static HistogramOptions Request => new HistogramOptions
        {
            Name = "myapp_request_duration_seconds",

        };
    }
}
