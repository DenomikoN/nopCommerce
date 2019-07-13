using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Widgets.YandexMetrics.Models;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Widgets.YandexMetrics.Controllers
{
    public class WidgetsYandexMetricsController : BasePluginController
    {
        private const string ORDER_ALREADY_PROCESSED_ATTRIBUTE_NAME = "YandexMetrics.OrderAlreadyProcessed";
        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly IOrderService _orderService;
        private readonly ILogger _logger;
        private readonly ICategoryService _categoryService;
        private readonly IProductAttributeParser _productAttributeParser;
        private readonly ILocalizationService _localizationService;
        private readonly IGenericAttributeService _genericAttributeService;

        private static CultureInfo _serializeCulture = new CultureInfo("en-US");

        public WidgetsYandexMetricsController(IWorkContext workContext,
            IStoreContext storeContext, 
            IStoreService storeService,
            ISettingService settingService, 
            IOrderService orderService, 
            ILogger logger, 
            ICategoryService categoryService,
            IProductAttributeParser productAttributeParser,
            ILocalizationService localizationService,
            IGenericAttributeService genericAttributeService)
        {
            this._workContext = workContext;
            this._storeContext = storeContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._orderService = orderService;
            this._logger = logger;
            this._categoryService = categoryService;
            this._productAttributeParser = productAttributeParser;
            this._localizationService = localizationService;
            this._genericAttributeService = genericAttributeService;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var settings = _settingService.LoadSetting<YandexMetricsSettings>(storeScope);
            var model = new ConfigurationModel();
            model.MetricId = settings.MetricId;
            model.TrackingScript = settings.TrackingScript;
            model.EcommerceScript = settings.EcommerceScript;
            model.EcommerceDetailScript = settings.EcommerceDetailScript;
            model.IncludingTax = settings.IncludingTax;
            model.ZoneId = settings.WidgetZone;
            model.AvailableZones.Add(new SelectListItem() { Text = "Before body end html tag", Value = "body_end_html_tag_before" });
            model.AvailableZones.Add(new SelectListItem() { Text = "Head html tag", Value = "head_html_tag" });

            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.MetricId_OverrideForStore = _settingService.SettingExists(settings, x => x.MetricId, storeScope);
                model.TrackingScript_OverrideForStore = _settingService.SettingExists(settings, x => x.TrackingScript, storeScope);
                model.EcommerceScript_OverrideForStore = _settingService.SettingExists(settings, x => x.EcommerceScript, storeScope);
                model.EcommerceDetailScript_OverrideForStore = _settingService.SettingExists(settings, x => x.EcommerceDetailScript, storeScope);
                model.IncludingTax_OverrideForStore = _settingService.SettingExists(settings, x => x.IncludingTax, storeScope);
                model.ZoneId_OverrideForStore = _settingService.SettingExists(settings, x => x.WidgetZone, storeScope);
            }

            return View("~/Plugins/Widgets.YandexMetrics/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var settings = _settingService.LoadSetting<YandexMetricsSettings>(storeScope);
            settings.MetricId = model.MetricId;
            settings.TrackingScript = model.TrackingScript;
            settings.EcommerceScript = model.EcommerceScript;
            settings.EcommerceDetailScript = model.EcommerceDetailScript;
            settings.IncludingTax = model.IncludingTax;
            settings.WidgetZone = model.ZoneId;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(settings, x => x.MetricId, model.MetricId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(settings, x => x.TrackingScript, model.TrackingScript_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(settings, x => x.EcommerceScript, model.EcommerceScript_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(settings, x => x.EcommerceDetailScript, model.EcommerceDetailScript_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(settings, x => x.IncludingTax, model.IncludingTax_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(settings, x => x.WidgetZone, model.ZoneId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(settings, x => x.Timestamp, false, storeScope, false);
            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PublicInfo(string widgetZone, object additionalData = null)
        {
            string globalScript = "";
            var routeData = ((System.Web.UI.Page)this.HttpContext.CurrentHandler).RouteData;

            try
            {
                var controller = routeData.Values["controller"];
                var action = routeData.Values["action"];

                if (controller == null || action == null)
                    return Content("");

                //Special case, if we are in last step of checkout, we can use order total for conversion value
                if (controller.ToString().Equals("checkout", StringComparison.InvariantCultureIgnoreCase) &&
                    action.ToString().Equals("completed", StringComparison.InvariantCultureIgnoreCase))
                {
                    var lastOrder = GetLastOrder();
                    globalScript += GetMetricScript(lastOrder);
                }
                else
                {
                    globalScript += GetMetricScript(null);
                }
            }
            catch (Exception ex)
            {
                _logger.InsertLog(Core.Domain.Logging.LogLevel.Error, "Error creating scripts for YM ecommerce tracking", ex.ToString());
            }
            return Content(globalScript);
        }

        private Order GetLastOrder()
        {
            var order = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1).FirstOrDefault();
            return order;
        }

        static Regex _templateRegEx = new Regex("{{(\\S+)}}",RegexOptions.Compiled|RegexOptions.CultureInvariant);
        static HashSet<string> _usedKeys = new HashSet<string>();
        static object _lock = new object();
        static string _timestamp = null;
        private static HashSet<string> GetUsedKeys(YandexMetricsSettings settings)
        {
            if(_timestamp != settings.Timestamp)
            {
                lock (_lock)
                {
                    if (_timestamp != settings.Timestamp)
                    {
                        var newKeys = new HashSet<string>();
                        foreach (Match itemMatch in _templateRegEx.Matches(settings.TrackingScript))
                        {
                            newKeys.Add(itemMatch.Groups[1].Value);
                        }
                        foreach (Match itemMatch in _templateRegEx.Matches(settings.EcommerceScript))
                        {
                            newKeys.Add(itemMatch.Groups[1].Value);
                        }
                        foreach (Match itemMatch in _templateRegEx.Matches(settings.EcommerceDetailScript))
                        {
                            newKeys.Add(itemMatch.Groups[1].Value);
                        }
                        _usedKeys = newKeys;
                    }
                }
            }
            return _usedKeys;
        }


        private string GetMetricScript(Order order)
        {
            var settings = _settingService.LoadSetting<YandexMetricsSettings>(_storeContext.CurrentStore.Id);
            var keys = GetUsedKeys(settings);
            var resultScript = new StringBuilder(settings.TrackingScript + "\n");

            _logger.InsertLog(Core.Domain.Logging.LogLevel.Debug, "Result script before", resultScript.ToString());

            PreProcess(ref resultScript, BuildGlobalDictionary(settings), keys);

            _logger.InsertLog(Core.Domain.Logging.LogLevel.Debug, "Result script after", resultScript.ToString());

            //ensure that ecommerce tracking code is rendered only once (avoid duplicated data in Google Analytics)
            if (order != null && !order.GetAttribute<bool>(ORDER_ALREADY_PROCESSED_ATTRIBUTE_NAME))
            {
                var analyticsEcommerceScript = new StringBuilder(settings.EcommerceScript + "\n");
                PreProcess(ref analyticsEcommerceScript, BuildOrderDictionary(settings, order), keys);

                var itemDetailsStringBuilder = new StringBuilder();
                var totalItems = order.OrderItems.Count;
                int currentItem = 0;
                foreach (var item in order.OrderItems)
                {
                    currentItem++;
                    var itemDetailScript = new StringBuilder(settings.EcommerceDetailScript);
                    PreProcess(ref itemDetailScript, BuildOrderItemDictionary(settings, item), keys);

                    if (totalItems == currentItem)
                    {
                        itemDetailsStringBuilder.AppendLine(itemDetailScript.ToString());
                    }
                    else
                    {
                        itemDetailsStringBuilder.Append(itemDetailScript.ToString());
                        itemDetailsStringBuilder.AppendLine(",");
                    }
                }

                analyticsEcommerceScript.Replace("<ITEM_DETAILS>", itemDetailsStringBuilder.ToString());
                resultScript.Replace("<ECOMMERCE>", analyticsEcommerceScript.ToString());

                _genericAttributeService.SaveAttribute(order, ORDER_ALREADY_PROCESSED_ATTRIBUTE_NAME, true);
            }
            else
            {
                resultScript.Replace("<ECOMMERCE>", "");
            }

            return resultScript.ToString();
        }
        

        private static void PreProcess(ref StringBuilder resultScript, Dictionary<string, Func<string>> dictionary, HashSet<string> usedKeys)
        {
            foreach (var key in dictionary.Keys.Intersect(usedKeys))
            {
                resultScript.Replace("{{" + key + "}}", dictionary[key].Invoke());
            }
        }

        private string FixIllegalJavaScriptChars(string text)
        {
            if (String.IsNullOrEmpty(text))
                return text;

            //replace ' with \' (http://stackoverflow.com/questions/4292761/need-to-url-encode-labels-when-tracking-events-with-google-analytics)
            text = text.Replace("'", "\\'");
            return text;
        }


        private static Dictionary<string, Func<string>> BuildGlobalDictionary(YandexMetricsSettings settings)
        {
            return new Dictionary<string, Func<string>> {
                { "MetricId", () => settings.MetricId }
            };
        }

        private Dictionary<string, Func<string>> BuildOrderDictionary(YandexMetricsSettings settings, Order order)
        {
            return new Dictionary<string, Func<string>> {
                { "OrderId", () => order.Id.ToString() },
                { "Site", () => _storeContext.CurrentStore.Url.Replace("http://", "").Replace("/", "") },
                { "Total", () => order.OrderTotal.ToString("0.00", _serializeCulture) },
                { "Tax", () => order.OrderTax.ToString("0.00", _serializeCulture) },
                { "Ship", () => (settings.IncludingTax ? order.OrderShippingInclTax : order.OrderShippingExclTax).ToString("0.00", _serializeCulture) },
                { "City", () => order.BillingAddress == null ? "" : FixIllegalJavaScriptChars(order.BillingAddress.City) },
                { "StateProvince", () => order.BillingAddress == null || order.BillingAddress.StateProvince == null ? "" : FixIllegalJavaScriptChars(order.BillingAddress.StateProvince.Name) },
                { "Country", () => order.BillingAddress == null || order.BillingAddress.Country == null ? "" : FixIllegalJavaScriptChars(order.BillingAddress.Country.Name) }
            };
        }

        private Dictionary<string, Func<string>> BuildOrderItemDictionary(YandexMetricsSettings settings, OrderItem orderItem)
        {
            return new Dictionary<string, Func<string>> {
                { "ItemId", () => orderItem.Id.ToString() },
                { "ProductSku", () => FixIllegalJavaScriptChars(orderItem.Product.FormatSku(orderItem.AttributesXml, _productAttributeParser)) },
                { "ProductName", () => FixIllegalJavaScriptChars(orderItem.Product.Name) },
                { "CategoryName", () => {
                                            string category = "";
                                            var defaultProductCategory = _categoryService.GetProductCategoriesByProductId(orderItem.ProductId).FirstOrDefault();
                                            if (defaultProductCategory != null)
                                                category = defaultProductCategory.Category.Name;
                                            return FixIllegalJavaScriptChars(category);
                                        }
                },
                { "UnitPrice", () => {
                                    var unitPrice = settings.IncludingTax ? orderItem.UnitPriceInclTax : orderItem.UnitPriceExclTax;
                                    return unitPrice.ToString("0.00", _serializeCulture);
                                }
                },
                { "Quantity", () => orderItem.Quantity.ToString() }
            };
        }

    }
}