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
        private readonly IMenuItemGenerator _menuItemGenerator;

        public BlogController(IDeliveryClient deliveryClient, IMemoryCache memoryCache, INavigationProvider navigationProvider, IMenuItemGenerator menuItemGenerator) : base(deliveryClient, memoryCache)
        {
            _navigationProvider = navigationProvider ?? throw new ArgumentNullException(nameof(navigationProvider));
            _menuItemGenerator = menuItemGenerator ?? throw new ArgumentNullException(nameof(menuItemGenerator));
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

            string yearString = null;
            string monthString = null;

            if (year.HasValue && !month.HasValue)
            {
                yearString = $"{year}-01";
                monthString = $"{year + 1}-01";
            }
            else if (year.HasValue && month.HasValue)
            {
                if (month < 12)
                {
                    yearString = $"{year}-{GetMonthFormatted(month.Value)}";
                    monthString = $"{year}-{GetMonthFormatted(month.Value + 1)}";
                }
                else
                {
                    yearString = $"{year}-12";
                    monthString = $"{year + 1}-01";
                }
            }

            if (year.HasValue)
            {
                filters.Add(new RangeFilter(ELEMENT_NAME, yearString, monthString));
            }

            var pageBody = await _deliveryClient.GetItemsAsync<Article>(filters);
            var navigation = await _menuItemGenerator.GenerateItemsAsync(await _navigationProvider.GetNavigationAsync());

            var pageViewModel = new PageViewModel
            {
                Navigation = navigation,
                Body = pageBody.Items
            };

            return View(DEFAULT_VIEW, pageViewModel);
        }

        private string GetMonthFormatted(int month)
        {
            return string.Format("{0:00}", month);
        }
    }
}
