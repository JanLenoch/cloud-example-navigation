using KenticoCloud.Delivery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using NavigationMenusMvc.Helpers;
using NavigationMenusMvc.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NavigationMenusMvc.Controllers
{
    public class BlogController : BaseController
    {
        private const string DEFAULT_VIEW = "Default";
        private const string TYPE_NAME = "article";
        private const string ELEMENT_NAME = "elements.post_date";
        private readonly INavigationProvider _navigationProvider;

        public BlogController(IDeliveryClient deliveryClient, IMemoryCache memoryCache, INavigationProvider navigationProvider) : base(deliveryClient, memoryCache)
        {
            _navigationProvider = navigationProvider ?? throw new ArgumentNullException(nameof(navigationProvider));
        }

        public async Task<ActionResult> Index(int? year, int? month)
        {
            List<IQueryParameter> filters = new List<IQueryParameter>();

            filters.AddRange(new IQueryParameter[]
            {
                new EqualsFilter("system.type", TYPE_NAME),
                new DepthParameter(0),
                new OrderParameter(ELEMENT_NAME)
            });

            if (year.HasValue && !month.HasValue)
            {
                filters.Add(new RangeFilter(ELEMENT_NAME, $"{year}-01", $"{year + 1}-01"));
            }
            else if (year.HasValue && month.HasValue)
            {
                filters.Add(new RangeFilter(ELEMENT_NAME, $"{year}-{GetMonthFormatted(month.Value)}", $"{year}-{GetMonthFormatted(month.Value + 1)}"));
            }

            var pageBodyTask = _deliveryClient.GetItemsAsync<Article>(filters);
            var navigationTask = _navigationProvider.GetOrCreateCachedNavigationAsync();

            await Task.WhenAll(pageBodyTask, navigationTask);

            var pageViewModel = new PageViewModel
            {
                Navigation = navigationTask.Result,
                Body = pageBodyTask.Result.Items
            };

            return View(DEFAULT_VIEW, pageViewModel);
        }

        private string GetMonthFormatted(int month)
        {
            return (month < 10) ? "0" + month.ToString() : month.ToString();
        }
    }
}
