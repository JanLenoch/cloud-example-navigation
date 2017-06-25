using KenticoCloud.Delivery;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NavigationMenusMvc.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        public NavigationProvider(IOptions<NavigationOptions> options, IDeliveryClient client, IMemoryCache cache)
        {
            if (options.Value.NavigationCodename == null)
            {
                throw new ArgumentNullException(nameof(options.Value.NavigationCodename));
            }
            else if (options.Value.NavigationCodename.Equals(string.Empty))
            {
                throw new ArgumentOutOfRangeException(nameof(options.Value.NavigationCodename), $"The {nameof(options.Value.NavigationCodename)} parameter must not be an empty string.");
            }

            if (!options.Value.MaxDepth.HasValue)
            {
                throw new ArgumentNullException(nameof(options.Value.MaxDepth));
            }
            else if (options.Value.MaxDepth.Value < 2)
            {
                // TODO Add constructor description.
                throw new ArgumentOutOfRangeException(nameof(options.Value.MaxDepth), $"The {nameof(options.Value.MaxDepth)} parameter must be 2 or higher.");
            }

            if (!options.Value.NavigationCacheExpirationMinutes.HasValue)
            {
                throw new ArgumentNullException(nameof(options.Value.NavigationCacheExpirationMinutes));
            }
            else if (options.Value.NavigationCacheExpirationMinutes.Value <= 0)
            {
                // TODO Add constructor description.
                throw new ArgumentOutOfRangeException(nameof(options.Value.NavigationCacheExpirationMinutes), $"The {nameof(options.Value.NavigationCacheExpirationMinutes)} parameter must be greater than zero.");
            }

            if (options.Value.RootToken == null)
            {
                throw new ArgumentNullException(nameof(options.Value.RootToken));
            }
            else if (options.Value.RootToken.Equals(string.Empty))
            {
                throw new ArgumentOutOfRangeException(nameof(options.Value.RootToken), $"The {nameof(options.Value.RootToken)} parameter must not be an empty string.");
            }

            if (string.IsNullOrEmpty(options.Value.HomepageToken))
            {
                throw new ArgumentNullException(nameof(options.Value.HomepageToken));
            }
            else if (options.Value.HomepageToken.Equals(string.Empty))
            {
                throw new ArgumentOutOfRangeException(nameof(options.Value.HomepageToken), $"The {nameof(options.Value.HomepageToken)} parameter must not be an empty string.");
            }

            _client = client ?? throw new ArgumentNullException(nameof(client));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _navigationCodename = options.Value.NavigationCodename;
            _maxDepth = options.Value.MaxDepth.Value;
            _navigationCacheExpirationMinutes = options.Value.NavigationCacheExpirationMinutes.Value;
            _rootToken = options.Value.RootToken;
            _homepageToken = options.Value.HomepageToken;
        }

        #endregion

        #region "Public methods"

        /// <summary>
        /// Requests the root <see cref="NavigationItem"/> item off of the Delivery/Preview API endpoint.
        /// </summary>
        /// <param name="navigationCodeName">The explicit codename of the root item. If <see langword="null" />, the value supplied in the constructor is taken.</param>
        /// <param name="maxDepth">The explicit maximum depth of the hierarchy to be fetched</param>
        /// <returns>The root item of either the explicit codename, or default codename</returns>
        public async Task<NavigationItem> GetNavigationItemsAsync(string navigationCodeName = null, int? maxDepth = null)
        {
            string cn = navigationCodeName ?? _navigationCodename;
            int d = maxDepth.HasValue ? maxDepth.Value : _maxDepth;

            var response = await _client.GetItemsAsync<NavigationItem>(
                new EqualsFilter("system.type", ITEM_TYPE),
                new EqualsFilter("system.codename", cn),
                new LimitParameter(1),
                new DepthParameter(d)
            );

            return response.Items.FirstOrDefault();
        }

        /// <summary>
        /// Gets the root <see cref="NavigationItem"/> item either off of the <see cref="IMemoryCache"/> or the Delivery/Preview API endpoint.
        /// </summary>
        /// <param name="navigationCodeName">The explicit codename of the root item. If <see langword="null" />, the value supplied in the constructor is taken.</param>
        /// <param name="maxDepth">The explicit maximum depth of the hierarchy to be fetched</param>
        /// <returns>The root item of either the explicit codename, or default codename</returns>
        public async Task<NavigationItem> GetOrCreateCachedNavigationAsync(string navigationCodeName = null, int? maxDepth = null)
        {
            string cn = navigationCodeName ?? _navigationCodename;
            int d = maxDepth.HasValue ? maxDepth.Value : _maxDepth;

            return await _cache.GetOrCreate(NAVIGATION_CACHE_KEY, async entry =>
            {
                var navigation = await GetNavigationItemsAsync(cn, d);

                // Add UrlPath property values first.
                AddUrlPaths(new List<NavigationItem>(), navigation, string.Empty);

                // UrlPath needed, hence a separate iteration.
                DecorateItems(navigation, null, new List<NavigationItem>(), navigation);

                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_navigationCacheExpirationMinutes);

                return navigation;
            });
        }

        /// <summary>
        /// Flattens the hierarchical <see cref="NavigationItem"/> item and its children to a flat collection.
        /// </summary>
        /// <param name="navigation">The hierarchical <see cref="NavigationItem"/></param>
        /// <returns>The flattened collection</returns>
        public static IEnumerable<NavigationItem> GetNavigationItemsFlat(NavigationItem navigation)
        {
            var outputList = new List<NavigationItem>();
            FlattenLevelToList(new List<NavigationItem>(), navigation, outputList);

            return outputList;
        }

        /// <summary>
        /// Splits the <paramref name="urlPath"/> relative URL by slash characters into an array.
        /// </summary>
        /// <param name="urlPath">The full relative URL</param>
        /// <returns>The array of URL slugs</returns>
        public static string[] GetUrlSlugs(string urlPath)
        {
            return urlPath != null ? urlPath.TrimEnd("/".ToCharArray()).Split("/".ToCharArray()) : new string[] { string.Empty };
        }

        #endregion

        #region "Private methods"

        private static void FlattenLevelToList(List<NavigationItem> allParents, NavigationItem currentItem, List<NavigationItem> outputList)
        {
            if (allParents == null)
            {
                throw new ArgumentNullException(nameof(allParents));
            }

            if (currentItem == null)
            {
                throw new ArgumentNullException(nameof(currentItem));
            }

            if (outputList == null)
            {
                throw new ArgumentNullException(nameof(outputList));
            }

            outputList = outputList ?? new List<NavigationItem>();
            outputList.AddRange(currentItem.ChildNavigationItems);

            // Check for infinite loops.
            if (!allParents.Contains(currentItem))
            {
                var nextAllParents = new List<NavigationItem>(allParents);
                nextAllParents.Add(currentItem);

                // Spawn a tree of recursions running in parallel.
                Parallel.ForEach(currentItem.ChildNavigationItems, currentChild => FlattenLevelToList(nextAllParents, currentChild, outputList));
            }
        }

        private void AddUrlPaths(List<NavigationItem> allParents, NavigationItem currentItem, string pathStub)
        {
            if (allParents == null)
            {
                throw new ArgumentNullException(nameof(allParents));
            }

            if (currentItem == null)
            {
                throw new ArgumentNullException(nameof(currentItem));
            }

            // Check for infinite loops.
            if (!allParents.Contains(currentItem))
            {
                AddUrlPath(currentItem, pathStub);
                var nextAllParents = new List<NavigationItem>(allParents);
                nextAllParents.Add(currentItem);

                // Spawn a tree of recursions running in parallel.
                Parallel.ForEach(currentItem.ChildNavigationItems, currentChild => AddUrlPaths(nextAllParents, currentChild, currentItem.UrlPath));
            }
        }

        private void DecorateItems(NavigationItem cachedNavigation, NavigationItem parentItem, List<NavigationItem> allParents, NavigationItem currentItem)
        {
            if (currentItem == null)
            {
                throw new ArgumentNullException(nameof(currentItem));
            }

            // Check for infinite loops.
            if (!allParents.Contains(currentItem))
            {
                var redirect = currentItem.RedirectToItem.FirstOrDefault();

                if (redirect != null)
                {
                    currentItem.RedirectPath = AddRedirectPath(cachedNavigation, redirect);
                }

                currentItem.Parent = parentItem;
                currentItem.AllParents = allParents;
                var nextAllParents = new List<NavigationItem>(allParents);
                nextAllParents.Add(currentItem);
                
                // Spawn a tree of recursions running in parallel.
                Parallel.ForEach(currentItem.ChildNavigationItems, currentChild => DecorateItems(cachedNavigation, currentItem, nextAllParents, currentChild));
            }
        }

        private void AddUrlPath(NavigationItem cachedNavigation, string pathStub)
        {
            if (cachedNavigation == null)
            {
                throw new ArgumentNullException(nameof(cachedNavigation));
            }

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
            if (cachedNavigation == null)
            {
                throw new ArgumentNullException(nameof(cachedNavigation));
            }

            if (itemToLocate == null)
            {
                throw new ArgumentNullException(nameof(itemToLocate));
            }

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
