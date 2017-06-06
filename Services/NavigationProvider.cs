using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NavigationMenusMvc.Models;
using KenticoCloud.Delivery;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http;

namespace NavigationMenusMvc.Services
{
    public static class NavigationProvider
    {
        public async static Task<NavigationItem> GetNavigationItemsAsync(IDeliveryClient client, string navigationCodeName, int depth)
        {
            var response = await client.GetItemsAsync<object>(
                new EqualsFilter("system.type", "navigation_item"),
                new EqualsFilter("system.codename", navigationCodeName),
                new LimitParameter(1),
                new DepthParameter(depth)
            );

            return response.Items.Cast<NavigationItem>().FirstOrDefault();
        }

        public static async Task<NavigationItem> GetOrCreateCachedNavigationAsync(string navigationCodeName, int depth, IDeliveryClient client, IMemoryCache cache, int cacheExpirationMinutes, string rootToken, string homepageToken)
        {
            return await cache.GetOrCreate("navigationWithUrlPaths", async entry =>
            {
                var navigation = await GetNavigationItemsAsync(client, navigationCodeName, depth);

                // Decorate 'navigation' with UrlPath properties and store it in the cache (independent from CachedDeliveryClient).
                AddUrlPaths(navigation, string.Empty, null, rootToken, homepageToken);
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(cacheExpirationMinutes);

                return navigation;
            });
        }

        public static void AddUrlPaths(NavigationItem navigation, string pathStub, List<NavigationItem> parentItems, string rootToken, string homepageToken)
        {
            if (parentItems == null)
            {
                parentItems = new List<NavigationItem>();
            }

            // Check for infinite loops.
            if (!parentItems.Contains(navigation))
            {
                if (navigation.UrlSlug != rootToken && navigation.UrlSlug != homepageToken)
                {
                    navigation.UrlPath = !string.IsNullOrEmpty(pathStub) ? $"{pathStub}/{navigation.UrlSlug}" : navigation.UrlSlug;
                }
                else
                {
                    navigation.UrlPath = string.Empty;
                }

                parentItems.Add(navigation);
                Parallel.ForEach(navigation.ChildNavigationItems, currentChild => AddUrlPaths(currentChild, navigation.UrlPath, parentItems, rootToken, homepageToken));
            }
        }

        public static string GetRelativeUrlPath(HttpContext context, string originalRelativeUrl)
        {
            string[] urlSlugs = GetUrlSlugs(context.Request.Path);
            string returnUrl = null;

            for (int i = 0; i < urlSlugs.Count() - 2; i++)
            {
                returnUrl += "../";
            }

            returnUrl += originalRelativeUrl;

            return returnUrl;
        }

        public static string[] GetUrlSlugs(string urlPath)
        {
            return urlPath != null ? urlPath.TrimEnd("/".ToCharArray()).Split("/".ToCharArray()) : new string[] { string.Empty };
        }
    }
}
