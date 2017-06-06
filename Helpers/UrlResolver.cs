using KenticoCloud.Delivery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using NavigationMenusMvc.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NavigationMenusMvc.Helpers
{
    public static class UrlResolver
    {
        public static async Task<ActionResult> ResolveRelativeUrlPath(string urlPath, string navigationCodeName, int baseLevel, int maxDepth, IDeliveryClient client, IMemoryCache cache, int navigationCacheExpirationMinutes, string rootToken, string homepageToken, Func<NavigationItem, NavigationItem, string, bool, Task<ActionResult>> contentResolver)
        {
            // Get the 'Navigation' item with depth=deepest menu in the app.
            var navigationItem = await NavigationProvider.GetOrCreateCachedNavigationAsync(navigationCodeName, maxDepth, client, cache, navigationCacheExpirationMinutes, rootToken, homepageToken);

            // Strip the trailing slash and split.
            string[] urlSlugs = NavigationProvider.GetUrlSlugs(urlPath);

            // Recursively iterate over modular content and match the URL slugs for the each recursion level.
            try
            {
                return await ProcessUrlLevelAsync(urlSlugs, navigationItem, baseLevel, navigationItem.ViewName, navigationItem, baseLevel, homepageToken, contentResolver);
            }
            catch (Exception ex)
            {
                return new ContentResult
                {
                    Content = $"There was an error while resolving the URL. Check if your URL was correct and try again. Details: {ex.Message}",
                    StatusCode = 500
                };
            }
        }

        private static async Task<ActionResult> ProcessUrlLevelAsync(string[] urlSlugs, NavigationItem currentLevelItem, int level, string viewName, NavigationItem rootItem, int baseLevel, string homepageToken, Func<NavigationItem, NavigationItem, string, bool, Task<ActionResult>> contentResolver)
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

            if (level < baseLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level), "The 'level' must be greater or equal to zero.");
            }

            // No need to replace with ROOT_TOKEN, we're checking the incoming URL.
            string currentSlug = urlSlugs[level] == string.Empty ? homepageToken : urlSlugs[level];

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
                    return await contentResolver(matchingChild, matchingChild, viewName, false);
                }
                else
                {
                    int newLevel = level + 1;

                    // Dig through the incoming URL.
                    return await ProcessUrlLevelAsync(urlSlugs, matchingChild, newLevel, viewName, rootItem, baseLevel, homepageToken, contentResolver);
                }
            }
            else
            {
                return new NotFoundResult();
            }
        }
    }
}
