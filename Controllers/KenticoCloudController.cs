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

        public KenticoCloudController(IDeliveryClient deliveryClient, IMemoryCache memoryCache) : base(deliveryClient, memoryCache)
        {

        }

        public async Task<ActionResult> ResolveContent(string urlPath)
        {
            return await UrlResolver.ResolveRelativeUrlPath(urlPath, NAVIGATION_CODE_NAME, BASE_LEVEL, MAX_LEVEL_DEPTH, _deliveryClient, _cache, NAVIGATION_CACHE_EXPIRATION_MINUTES, ROOT_TOKEN, HOMEPAGE_TOKEN, ResolveContentAsync);
        }

        private async Task<ActionResult> ResolveContentAsync(NavigationItem originalItem, NavigationItem currentItem, string viewName, bool redirected)
        {
            if (currentItem.ContentItems != null && currentItem.ContentItems.Any())
            {
                if (redirected)
                {
                    var cachedNavigation = await NavigationProvider.GetOrCreateCachedNavigationAsync(NAVIGATION_CODE_NAME, MAX_LEVEL_DEPTH, _deliveryClient, _cache, NAVIGATION_CACHE_EXPIRATION_MINUTES, ROOT_TOKEN, HOMEPAGE_TOKEN);
                    
                    // Get complete URL and return 301. No direct rendering (not SEO-friendly).
                    string urlPath = await LocateFirstOccurrenceInTreeAsync(cachedNavigation, currentItem);

                    return LocalRedirectPermanent($"/{urlPath}");
                }
                else
                {
                    return await RenderViewAsync(viewName, currentItem.ContentItems);
                }
            }
            else if (currentItem.Redirect != null && currentItem.Redirect.Any())
            {
                var redirectItem = currentItem.Redirect.FirstOrDefault();

                // Check for infinite loops.
                if (!redirectItem.Equals(originalItem))
                {
                    return await ResolveContentAsync(originalItem, redirectItem, viewName, true);
                }
                else
                {
                    // Non-invasive solution.
                    return new NotFoundResult();
                }
            }
            else
            {
                return new NotFoundResult();
            }
        }

        private async Task<string> LocateFirstOccurrenceInTreeAsync(NavigationItem cachedNavigation, NavigationItem itemToLocate)
        {
            if (cachedNavigation.UrlPath == null)
            {
                throw new ArgumentException($"The {nameof(cachedNavigation.UrlPath)} property cannot be null.", nameof(cachedNavigation.UrlPath));
            }

            var match = cachedNavigation.ChildNavigationItems.FirstOrDefault(i => i.System.Codename == itemToLocate.System.Codename);

            if (match != null)
            {
                return match.UrlPath;
            }
            else
            {
                var results = new List<string>();

                foreach (var childItem in cachedNavigation.ChildNavigationItems)
                {
                    results.Add(await LocateFirstOccurrenceInTreeAsync(childItem, itemToLocate));
                }
                
                // No heuristics here, just the first occurrence.
                return results.FirstOrDefault(r => !string.IsNullOrEmpty(r));
            }
        }

        private async Task<ViewResult> RenderViewAsync(string viewName, IEnumerable<object> contentItems)
        {
            var navigationItemTask = NavigationProvider.GetOrCreateCachedNavigationAsync(NAVIGATION_CODE_NAME, MAX_LEVEL_DEPTH, _deliveryClient, _cache, NAVIGATION_CACHE_EXPIRATION_MINUTES, ROOT_TOKEN, HOMEPAGE_TOKEN);

            // Separate request for real content. Separate caching, separate depth of modular content.
            var bodyResponseTask = _deliveryClient.GetItemsAsync<object>(GetContentItemCodenameFilter(contentItems));

            await Task.WhenAll(navigationItemTask, bodyResponseTask);

            var pageViewModel = new PageViewModel
            {
                Navigation = navigationItemTask.Result,
                Body = bodyResponseTask.Result.Items
            };

            return View((string.IsNullOrEmpty(viewName) ? DEFAULT_VIEW : viewName), pageViewModel);
        }

        private InFilter GetContentItemCodenameFilter(IEnumerable<object> items)
        {
            var filterValues = new List<string>();

            foreach (var item in items)
            {
                ContentItemSystemAttributes system = item.GetType().GetTypeInfo().GetProperty("System", typeof(ContentItemSystemAttributes)).GetValue(item) as ContentItemSystemAttributes;
                filterValues.Add(system.Codename);
            }

            return new InFilter("system.codename", filterValues.ToArray());
        }
    }
}
