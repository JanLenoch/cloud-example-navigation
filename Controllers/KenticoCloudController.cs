using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KenticoCloud.Delivery;
using NavigationMenusMvc.Models;
using NavigationMenusMvc.Helpers;
using Microsoft.Extensions.Caching.Memory;
using System.Reflection;

namespace NavigationMenusMvc.Controllers
{
    public class KenticoCloudController : BaseController
    {
        private const string NAVIGATION_CODE_NAME = "navigation";
        private const int BASE_LEVEL = 0;
        private const int MAX_LEVEL_DEPTH = 3;
        private const int NAVIGATION_CACHE_EXPIRATION_MINUTES = 15;
        private const string ROOT_TOKEN = "[root]";
        private const string HOMEPAGE_TOKEN = "[home]";
        private const string DEFAULT_VIEW = "Default";

        protected readonly INavigationProvider _navigationProvider;
        private readonly IContentResolver _contentResolver;

        public KenticoCloudController(IDeliveryClient deliveryClient, IMemoryCache memoryCache, INavigationProvider navigationProvider, IContentResolver contentResolver) : base(deliveryClient, memoryCache)
        {
            _navigationProvider = navigationProvider ?? throw new ArgumentNullException(nameof(navigationProvider));
            _contentResolver = contentResolver ?? throw new ArgumentNullException(nameof(contentResolver));
        }

        public async Task<ActionResult> Index(string urlPath)
        {
            IContentResolverResults results;

            try
            {
                results = await _contentResolver.ResolveRelativeUrlPath(urlPath);
            }
            catch (Exception ex)
            {
                return new ContentResult
                {
                    Content = $"There was an error while resolving the URL. Check if your URL was correct and try again. Details: {ex.Message}",
                    StatusCode = 500
                };
            }

            if (results.Found)
            {
                if (results.ContentItemCodenames != null && results.ContentItemCodenames.Any())
                {
                    return await RenderViewAsync(results.ContentItemCodenames, results.ViewName);
                }
                else if (!string.IsNullOrEmpty(results.RedirectUrl))
                {
                    return LocalRedirectPermanent($"/{results.RedirectUrl}");
                }
            }
            else if (!string.IsNullOrEmpty(results.RedirectUrl))
            {
                return RedirectPermanent(results.RedirectUrl);
            }
            else
            {
                return NotFound();
            }
        }

        private async Task<ViewResult> RenderViewAsync(IEnumerable<string> codenames, string viewName)
        {
            var navigationItemTask = _navigationProvider.GetOrCreateCachedNavigationAsync();

            // Separate request for page body content. Separate caching, separate depth of modular content.
            var bodyResponseTask = _deliveryClient.GetItemsAsync<object>(new InFilter("system.codename", codenames.ToArray()));

            await Task.WhenAll(navigationItemTask, bodyResponseTask);

            var pageViewModel = new PageViewModel
            {
                Navigation = navigationItemTask.Result,
                Body = bodyResponseTask.Result.Items
            };

            return View((string.IsNullOrEmpty(viewName) ? DEFAULT_VIEW : viewName), pageViewModel);
        }
    }
}
