using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KenticoCloud.Delivery;
using NavigationMenusMvc.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Reflection;

namespace NavigationMenusMvc.Controllers
{
    public class KenticoCloudController : BaseController
    {
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
            var navigationItem = await GetNavigationItems("navigation", MAX_LEVEL_DEPTH);

            // Strip the trailing slash and split.
            string[] urlSlugs = urlPath != null ? urlPath.TrimEnd("/".ToCharArray()).Split("/".ToCharArray()) : new string[] { string.Empty };

            // Recursively iterate over modular content and match the URL slug for the current recursion level.
            try
            {
                return await ProcessRouteLevel(urlSlugs, navigationItem, BASE_LEVEL, navigationItem.ViewName, navigationItem);
            }
            catch (Exception ex)
            {
                return Content($"There was an error while resolving the URL. Check if your URL was correct and try again. Details: {ex.Message}");
            }
        }

        private async Task<ActionResult> ProcessRouteLevel(string[] urlSlugs, NavigationItem currentLevelItem, int level, string viewName, NavigationItem rootItem)
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
                    return await ResolveContent(matchingChild, matchingChild, viewName, false, rootItem);
                }
                else
                {
                    int newLevel = level + 1;
                    // Dig through the incoming URL.
                    return await ProcessRouteLevel(urlSlugs, matchingChild, newLevel, viewName, rootItem);
                }
            }
            else
            {
                return new NotFoundResult();
            }
        }

        private async Task<ActionResult> ResolveContent(NavigationItem originalItem, NavigationItem currentItem, string viewName, bool redirected, NavigationItem rootItem)
        {
            if (currentItem.ContentItems != null && currentItem.ContentItems.Any())
            {
                if (redirected)
                {
                    // Get complete URL and return 301. No direct rendering (not SEO-friendly).
                    NavigationItem navigationWithUrlPaths = GetOrCreateCachedNavigation(rootItem);
                    string urlPath = await LocateFirstOccurenceInTree(navigationWithUrlPaths, currentItem);

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
                    return await ResolveContent(originalItem, redirectItem, viewName, true, rootItem);
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

        private async Task<string> LocateFirstOccurenceInTree(NavigationItem cachedNavigation, NavigationItem itemToLocate)
        {
            var match = cachedNavigation.ChildNavigationItems.FirstOrDefault(i => i.System.Codename == itemToLocate.System.Codename);

            if (match != null)
            {
                return match.UrlPath;
            }
            else
            {
                var tasks = new List<Task<string>>();

                foreach (var childItem in cachedNavigation.ChildNavigationItems.Cast<NavigationItem>())
                {
                    tasks.Add(LocateFirstOccurenceInTree(childItem, itemToLocate));
                }

                string[] results = await Task.WhenAll(tasks);

                // No heuristics here, just the first occurence.
                return results.FirstOrDefault(r => !string.IsNullOrEmpty(r));
            }
        }

        private async Task<ViewResult> RenderViewAsync(string viewName, IEnumerable<object> contentItems, NavigationItem navigation)
        {
            NavigationItem navigationWithUrlPaths = GetOrCreateCachedNavigation(navigation);

            var bodyResponse = await _deliveryClient.GetItemsAsync<object>(GetContentItemCodenameFilter(contentItems));

            var pageViewModel = new PageViewModel
            {
                Navigation = navigationWithUrlPaths,
                Body = bodyResponse.Items
            };

            return View(viewName, pageViewModel);
        }

        private NavigationItem GetOrCreateCachedNavigation(NavigationItem navigation)
        {
            return _cache.GetOrCreate("navigationWithUrlPaths", entry =>
            {
                // Decorate 'navigation' with UrlPath properties and store it in the cache (independent from CachedDeliveryClient).
                AddUrlPaths(navigation, string.Empty, null);
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(NAVIGATION_CACHE_EXPIRATION_MINUTES);

                return navigation;
            });
        }

        private void AddUrlPaths(NavigationItem navigation, string pathStub, List<NavigationItem> parentItems)
        {
            if (parentItems == null)
            {
                parentItems = new List<NavigationItem>();
            }

            // Check for infinite loops.
            if (!parentItems.Contains(navigation))
            {
                if (navigation.UrlSlug != ROOT_TOKEN && navigation.UrlSlug != HOMEPAGE_TOKEN)
                {
                    navigation.UrlPath = !string.IsNullOrEmpty(pathStub) ? $"{pathStub}/{navigation.UrlSlug}" : navigation.UrlSlug;
                }
                else
                {
                    navigation.UrlPath = string.Empty;
                }

                parentItems.Add(navigation);
                Parallel.ForEach(navigation.ChildNavigationItems, currentChild => AddUrlPaths(currentChild, navigation.UrlPath, parentItems));
            }
        }

        private InFilter GetContentItemCodenameFilter(IEnumerable<object> items)
        {
            var filterValues = new List<string>();
            //var filters = new List<EqualsFilter>();

            foreach (var item in items)
            {
                ContentItemSystemAttributes system = item.GetType().GetTypeInfo().GetProperty("System", typeof(ContentItemSystemAttributes)).GetValue(item) as ContentItemSystemAttributes;
                filterValues.Add(system.Codename);
                //filters.Add(new EqualsFilter("system.codename", system.Codename));
            }

            return new InFilter("system.codename", filterValues.ToArray());
        }

        private async Task<NavigationItem> GetNavigationItems(string navigationCodeName, int depth)
        {
            var response = await _deliveryClient.GetItemsAsync<object>(
                new EqualsFilter("system.type", "navigation_item"),
                new EqualsFilter("system.codename", navigationCodeName),
                new LimitParameter(1),
                new DepthParameter(depth)
            );

            return response.Items.Cast<NavigationItem>().FirstOrDefault();
        }
    }
}
