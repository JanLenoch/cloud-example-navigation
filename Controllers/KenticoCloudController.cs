using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KenticoCloud.Delivery;
using NavigationMenusMvc.Models;
using NavigationMenusMvc.Services;
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

        public KenticoCloudController(IDeliveryClient deliveryClient, IMemoryCache memoryCache) : base(deliveryClient, memoryCache)
        {

        }

        public async Task<ActionResult> Route(string urlPath)
        {
            // Get the 'Navigation' item with depth=deepest menu in the app.
            //var navigationItem = await NavigationProvider.GetNavigationItems(_deliveryClient, "navigation", MAX_LEVEL_DEPTH);
            var navigationItem = await NavigationProvider.GetOrCreateCachedNavigationAsync(NAVIGATION_CODE_NAME, MAX_LEVEL_DEPTH, _deliveryClient, _cache, NAVIGATION_CACHE_EXPIRATION_MINUTES, ROOT_TOKEN, HOMEPAGE_TOKEN);

            // Strip the trailing slash and split.
            string[] urlSlugs = NavigationProvider.GetUrlSlugs(urlPath);

            // Recursively iterate over modular content and match the URL slugs for the each recursion level.
            try
            {
                return await ProcessRouteLevelAsync(urlSlugs, navigationItem, BASE_LEVEL, navigationItem.ViewName, navigationItem);
            }
            catch (Exception ex)
            {
                return Content($"There was an error while resolving the URL. Check if your URL was correct and try again. Details: {ex.Message}");
            }
        }

        private async Task<ActionResult> ProcessRouteLevelAsync(string[] urlSlugs, NavigationItem currentLevelItem, int level, string viewName, NavigationItem rootItem)
        {
            if (urlSlugs == null)
            {
                throw new ArgumentNullException(nameof(urlSlugs));
            }

            if (!urlSlugs.Any())
            {
                throw new ArgumentOutOfRangeException(nameof(urlSlugs));
            }

            if (currentLevelItem == null)
            {
                throw new ArgumentNullException(nameof(currentLevelItem));
            }

            if (level < BASE_LEVEL)
            {
                throw new ArgumentOutOfRangeException(nameof(level), "The 'level' must be greater or equal to zero.");
            }

            // No need to replace with ROOT_TOKEN, we're checking the incoming URL.
            string currentSlug = urlSlugs[level] == string.Empty ? HOMEPAGE_TOKEN : urlSlugs[level];

            NavigationItem matchingChild = currentLevelItem.ChildNavigationItems.FirstOrDefault(i => i.UrlSlug == currentSlug);
            bool endOfPath = level == urlSlugs.Count() - 1;

            if (matchingChild != null)
            {
                // Set a new inherited view name for lower nodes in the hierarchy.
                if (!string.IsNullOrEmpty(matchingChild.ViewName))
                {
                    viewName = matchingChild.ViewName;
                }

                if (endOfPath)
                {
                    return await ResolveContentAsync(matchingChild, matchingChild, viewName, false, rootItem);
                }
                else
                {
                    int newLevel = level + 1;

                    // Dig through the incoming URL.
                    return await ProcessRouteLevelAsync(urlSlugs, matchingChild, newLevel, viewName, rootItem);
                }
            }
            else
            {
                return new NotFoundResult();
            }
        }

        private async Task<ActionResult> ResolveContentAsync(NavigationItem originalItem, NavigationItem currentItem, string viewName, bool redirected, NavigationItem rootItem)
        {
            if (currentItem.ContentItems != null && currentItem.ContentItems.Any())
            {
                if (redirected)
                {
                    // Get complete URL and return 301. No direct rendering (not SEO-friendly).
                    string urlPath = await LocateFirstOccurrenceInTreeAsync(rootItem, currentItem);

                    return LocalRedirectPermanent($"/{urlPath}");
                }
                else
                {
                    return await RenderViewAsync(viewName, currentItem.ContentItems, rootItem);
                }
            }
            else if (currentItem.Redirect != null && currentItem.Redirect.Any())
            {
                var redirectItem = currentItem.Redirect.FirstOrDefault();

                // Check for infinite loops.
                if (!redirectItem.Equals(originalItem))
                {
                    return await ResolveContentAsync(originalItem, redirectItem, viewName, true, rootItem);
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

        private async Task<ViewResult> RenderViewAsync(string viewName, IEnumerable<object> contentItems, NavigationItem navigation)
        {
            var bodyResponse = await _deliveryClient.GetItemsAsync<object>(GetContentItemCodenameFilter(contentItems));

            var pageViewModel = new PageViewModel
            {
                Navigation = navigation,
                Body = bodyResponse.Items
            };

            return View(viewName, pageViewModel);
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
