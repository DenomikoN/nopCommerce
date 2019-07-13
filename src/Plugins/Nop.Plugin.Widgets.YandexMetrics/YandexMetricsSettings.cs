
using Nop.Core.Configuration;

namespace Nop.Plugin.Widgets.YandexMetrics
{
    public class YandexMetricsSettings : ISettings
    {
        public string MetricId { get; set; }
        public string TrackingScript { get; set; }
        public string EcommerceScript { get; set; }
        public string EcommerceDetailScript { get; set; }
        public bool IncludingTax { get; set; }
        public string WidgetZone { get; set; }
        public string Timestamp { get; set; }
    }
}