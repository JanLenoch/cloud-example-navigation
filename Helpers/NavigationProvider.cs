using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KenticoCloud.Delivery;
using Microsoft.Extensions.Caching.Memory;
using NavigationMenusMvc.Models;

namespace NavigationMenusMvc.Helpers
{
    public class NavigationProvider : INavigationProvider
    {
        #region "Constants"

        private const string ITEM_TYPE = "navigation_item";
        private const string NAVIGATION_CACHE_KEY = "navigationWithUrlPaths";

        #endregion

        #region "Fields"

        private readonly IDeliveryClient _client;
        private readonly IMemoryCache _cache;
        private readonly string _navigationCodename;
        private readonly int _maxDepth;
        private readonly int _navigationCacheExpirationMinutes;
        private readonly string _rootToken;
        private readonly string _homepageToken;

        #endregion

        #region "Constructors"

        public NavigationProvider(IDeliveryClient client, IMemoryCache cache, string navigationCodename, int maxDepth, int navigationCacheExpirationMinutes, string rootToken, string homepageToken)
        {
            if (string.IsNullOrEmpty(navigationCodename))
            {
                throw new ArgumentNullException(nameof(navigationCodename));
            }

            if (string.IsNullOrEmpty(rootToken))
            {
                throw new ArgumentNullException(nameof(rootToken));
            }

            if (string.IsNullOrEmpty(homepageToken))
            {
                throw new ArgumentNullException(nameof(homepageToken));
            }

            _client = client ?? throw new ArgumentNullException(nameof(client));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _navigationCodename = navigationCodename;
            _maxDepth = maxDepth;
            _navigationCacheExpirationMinutes = navigationCacheExpirationMinutes;
            _rootToken = rootToken;
            _homepageToken = homepageToken;
        }

        #endregion

        #region "Public methods"

        public async Task<NavigationItem> GetNavigationItemsAsync()
        {
            var response = await _client.GetItemsAsync<object>(
                new EqualsFilter("system.type", ITEM_TYPE),
                new EqualsFilter("system.codename", _navigationCodename),
                new LimitParameter(1),
                new DepthParameter(_maxDepth)
            );

            return response.Items.Cast<NavigationItem>().FirstOrDefault();
        }

        public async Task<NavigationItem> GetOrCreateCachedNavigationAsync()
        {
            return await _cache.GetOrCreate(NAVIGATION_CACHE_KEY, async entry =>
            {
                var navigation = await GetNavigationItemsAsync();

                // Decorate 'navigation' with UrlPath, RedirectPath, Parent, and AllParents properties..
                DecorateItems(navigation, null, navigation, string.Empty, null, _rootToken, _homepageToken);

                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_navigationCacheExpirationMinutes);

                return navigation;
            });
        }

        public static string[] GetUrlSlugs(string urlPath)
        {
            return urlPath != null ? urlPath.TrimEnd("/".ToCharArray()).Split("/".ToCharArray()) : new string[] { string.Empty };
        }

        #endregion

        #region "Private methods"

        private void DecorateItems(NavigationItem rootItem, NavigationItem parentItem, NavigationItem currentItem, string pathStub, List<NavigationItem> parentItems, string rootToken, string homepageToken)
        {
            if (parentItems == null)
            {
                parentItems = new List<NavigationItem>();
            }

            // Check for infinite loops.
            if (!parentItems.Contains(currentItem))
            {
                AddUrlPath(currentItem, pathStub);
                var redirect = currentItem.LocalRedirect.FirstOrDefault();

                if (redirect != null)
                {
                    currentItem.RedirectPath = AddRedirectPath(rootItem, redirect);
                }

                currentItem.Parent = parentItem;
                currentItem.AllParents = parentItems;
                parentItems.Add(currentItem);

                // Spawn a tree of recursions running in parallel.
                // TODO Wait for all AddUrlPath runs to complete, then AddRedirectPath ...
                Parallel.ForEach(currentItem.ChildNavigationItems, currentChild => DecorateItems(rootItem, currentItem, currentChild, currentItem.UrlPath, parentItems, rootToken, homepageToken));
            }
        }

        private void AddUrlPath(NavigationItem cachedNavigation, string pathStub)
        {
            if (cachedNavigation.UrlSlug != _rootToken && cachedNavigation.UrlSlug != _homepageToken)
            {
                cachedNavigation.UrlPath = !string.IsNullOrEmpty(pathStub) ? $"{pathStub}/{cachedNavigation.UrlSlug}" : cachedNavigation.UrlSlug;
            }
            else
            {
                cachedNavigation.UrlPath = string.Empty;
            }
        }

        private string AddRedirectPath(NavigationItem cachedNavigation, NavigationItem itemToLocate)
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
                    results.Add(AddRedirectPath(childItem, itemToLocate));
                }

                // No heuristics here, just the first occurrence.
                return results.FirstOrDefault(r => !string.IsNullOrEmpty(r));
            }
        }

        #endregion
    }
}
