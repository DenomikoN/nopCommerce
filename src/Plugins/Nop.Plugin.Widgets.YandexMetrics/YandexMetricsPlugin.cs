using System.Collections.Generic;
using System.Web.Routing;
using Nop.Core.Plugins;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;

namespace Nop.Plugin.Widgets.YandexMetrics
{
    /// <summary>
    /// Live person provider
    /// </summary>
    public class YandexMetricsPlugin : BasePlugin, IWidgetPlugin
    {
        private readonly ISettingService _settingService;
        private readonly YandexMetricsSettings _yandexMetricsSettings;

        public YandexMetricsPlugin(ISettingService settingService, YandexMetricsSettings yandexMetricsSettings)
        {
            this._settingService = settingService;
            this._yandexMetricsSettings = yandexMetricsSettings;
        }

        /// <summary>
        /// Gets widget zones where this widget should be rendered
        /// </summary>
        /// <returns>Widget zones</returns>
        public IList<string> GetWidgetZones()
        {
            return !string.IsNullOrWhiteSpace(_yandexMetricsSettings.WidgetZone)
                       ? new List<string>() { _yandexMetricsSettings.WidgetZone }
                       : new List<string>() { "body_end_html_tag_before" };
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "WidgetsYandexMetrics";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Widgets.YandexMetrics.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for displaying widget
        /// </summary>
        /// <param name="widgetZone">Widget zone where it's displayed</param>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetDisplayWidgetRoute(string widgetZone, out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PublicInfo";
            controllerName = "WidgetsYandexMetrics";
            routeValues = new RouteValueDictionary
            {
                {"Namespaces", "Nop.Plugin.Widgets.YandexMetrics.Controllers"},
                {"area", null},
                {"widgetZone", widgetZone}
            };
        }

        /// <summary>
        /// Install plugin
        /// </summary>
        public override void Install()
        {
            var settings = new YandexMetricsSettings
            {
                MetricId = "0000000-0",
                TrackingScript = 
                    @"<!-- Yandex.Metrika counter -->
                    <script>
                        window.dataLayer = window.dataLayer || [];
                        (function(m,e,t,r,i,k,a){m[i]=m[i]||function(){(m[i].a=m[i].a||[]).push(arguments)};
                        m[i].l=1*new Date();k=e.createElement(t),a=e.getElementsByTagName(t)[0],k.async=1,k.src=r,a.parentNode.insertBefore(k,a)}) (window, document, 'script', 'https://mc.yandex.ru/metrika/tag.js', 'ym');
                        ym({{MetricId}}, 'init', {
                            clickmap:true,
                            trackLinks: true,
                            accurateTrackBounce: true,
                            webvisor: true,
                            ecommerce: 'dataLayer'
                        });
                        <ECOMMERCE>
                    </ script >
                    <noscript><div><img src='https://mc.yandex.ru/watch/{METRICID}' style='position:absolute; left:-9999px;' alt='' /></div></noscript>
                    <!-- /Yandex.Metrika counter -->",
                EcommerceScript = 
                    @"dataLayer.push({
                        'ecommerce':{
                            'purchase', {
                                'id': '{{OrderId}}',
                                'revenue': '{{Total}}',
                                'shipping': '{{Ship}}',
                                'products': [
                                    <ITEM_DETAILS>
                                ]
                            }
                        }
                    });",
                EcommerceDetailScript = 
                    @"{
                        'id': '{{ItemId}}',
                        'name': '{{ProductName}}',
                        'sku': '{{ProductSku}}',
                        'category': '{{CategoryName}}',
                        'price': '{{UnitPrice}}',
                        'quantity': '{{Quantity}}'
                    }"
            };
            _settingService.SaveSetting(settings);

            this.AddOrUpdatePluginLocaleResource("Plugins.Widgets.YandexMetrics.MetricId", "MetricId");
            this.AddOrUpdatePluginLocaleResource("Plugins.Widgets.YandexMetrics.MetricId.Hint", "Enter Yandex Metric ID.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Widgets.YandexMetrics.TrackingScript", "Tracking code with <ECOMMERCE> line");
            this.AddOrUpdatePluginLocaleResource("Plugins.Widgets.YandexMetrics.TrackingScript.Hint", "Paste the tracking code generated by Metrics here. {{MetricId}} and <ECOMMERCE> will be dynamically replaced.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Widgets.YandexMetrics.EcommerceScript", "Tracking code for <ECOMMERCE> part, with {DETAILS> line");
            this.AddOrUpdatePluginLocaleResource("Plugins.Widgets.YandexMetrics.EcommerceScript.Hint", "Paste the tracking code generated by Metrics here.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Widgets.YandexMetrics.EcommerceDetailScript", "Tracking code for {DETAILS} part");
            this.AddOrUpdatePluginLocaleResource("Plugins.Widgets.YandexMetrics.EcommerceDetailScript.Hint", "Paste the tracking code generated by Google analytics here.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Widgets.YandexMetrics.IncludingTax", "Include tax");
            this.AddOrUpdatePluginLocaleResource("Plugins.Widgets.YandexMetrics.IncludingTax.Hint", "Check to include tax when generating tracking code for <ECOMMERCE> part.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Widgets.YandexMetrics.Instructions", "<p>It keeps track of statistics about the visitors and ecommerce conversion on your website.<br /></li><li>Copy the Tracking ID into the 'ID' box below</li><li>Click the 'Save' button below and metrics will be integrated into your store</li></ul><br /></p>");
            this.AddOrUpdatePluginLocaleResource("Plugins.Widgets.YandexMetrics.Note", "<p><em>Please note that <ECOMMERCE> line works only when you have \"Disable order completed page\" order setting unticked.</em></p>");

            base.Install();
        }

        /// <summary>
        /// Uninstall plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<YandexMetricsSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Widgets.GoogleAnalytics.MetricId");
            this.DeletePluginLocaleResource("Plugins.Widgets.GoogleAnalytics.MetricId.Hint");
            this.DeletePluginLocaleResource("Plugins.Widgets.GoogleAnalytics.TrackingScript");
            this.DeletePluginLocaleResource("Plugins.Widgets.GoogleAnalytics.TrackingScript.Hint");
            this.DeletePluginLocaleResource("Plugins.Widgets.GoogleAnalytics.EcommerceScript");
            this.DeletePluginLocaleResource("Plugins.Widgets.GoogleAnalytics.EcommerceScript.Hint");
            this.DeletePluginLocaleResource("Plugins.Widgets.GoogleAnalytics.EcommerceDetailScript");
            this.DeletePluginLocaleResource("Plugins.Widgets.GoogleAnalytics.EcommerceDetailScript.Hint");
            this.DeletePluginLocaleResource("Plugins.Widgets.GoogleAnalytics.IncludingTax");
            this.DeletePluginLocaleResource("Plugins.Widgets.GoogleAnalytics.IncludingTax.Hint");
            this.DeletePluginLocaleResource("Plugins.Widgets.GoogleAnalytics.Instructions");
            this.DeletePluginLocaleResource("Plugins.Widgets.GoogleAnalytics.Note");

            base.Uninstall();
        }
    }
}
