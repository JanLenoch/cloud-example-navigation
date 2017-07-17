﻿using KenticoCloud.Delivery;
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

            if (options.Value.HomepageToken == null)
            {
                throw new ArgumentNullException(nameof(options.Value.HomepageToken));
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
            int d = maxDepth ?? _maxDepth;

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
            int d = maxDepth ?? _maxDepth;

            return await _cache.GetOrCreate(NAVIGATION_CACHE_KEY, async entry =>
            {
                var navigation = await GetNavigationItemsAsync(cn, d);
                var emptyList = new List<NavigationItem>();

                // Add the UrlPath property values to the navigation items first.
                AddUrlPaths((IList<NavigationItem>)emptyList, navigation, string.Empty);

                emptyList.Clear();

                // Then, add the RedirectPath, Parent, and AllParents property values. UrlPath value is needed for that, hence a separate iteration through the hierarchy.
                AddRedirectPathsAndParents(navigation, (IList<NavigationItem>)emptyList, navigation);

                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_navigationCacheExpirationMinutes);

                return navigation;
            });
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

        private void AddUrlPaths(IList<NavigationItem> processedParents, NavigationItem currentItem, string pathStub)
        {
            if (processedParents == null)
            {
                throw new ArgumentNullException(nameof(processedParents));
            }

            if (currentItem == null)
            {
                throw new ArgumentNullException(nameof(currentItem));
            }

            // Check for infinite loops.
            if (!processedParents.Contains(currentItem))
            {
                AddUrlPath(currentItem, pathStub);
                processedParents.Add(currentItem);

                // Spawn a tree of recursions.
                foreach (var currentChild in currentItem.ChildNavigationItems)
                {
                    AddUrlPaths(processedParents, currentChild, currentItem.UrlPath);
                }
            }
        }

        private void AddRedirectPathsAndParents(NavigationItem cachedNavigation, IList<NavigationItem> processedParents, NavigationItem currentItem)
        {
            if (currentItem == null)
            {
                throw new ArgumentNullException(nameof(currentItem));
            }
            
            // Check for infinite loops.
            if (!processedParents.Contains(currentItem))
            {
                var redirect = currentItem.RedirectToItem.FirstOrDefault();

                if (redirect != null)
                {
                    currentItem.RedirectPath = GetRedirectPath(cachedNavigation, redirect);
                }

                currentItem.Parent = processedParents.Count > 0 ? processedParents.Last() : null;
                currentItem.AllParents = processedParents;
                processedParents.Add(currentItem);

                // Spawn a tree of recursions.
                foreach (var currentChild in currentItem.ChildNavigationItems)
                {
                    AddRedirectPathsAndParents(cachedNavigation, processedParents, currentChild);
                }
            }
        }

        private void AddUrlPath(NavigationItem navigationItem, string pathStub)
        {
            if (navigationItem == null)
            {
                throw new ArgumentNullException(nameof(navigationItem));
            }

            if (navigationItem.UrlSlug != _rootToken && navigationItem.UrlSlug != _homepageToken)
            {
                navigationItem.UrlPath = !string.IsNullOrEmpty(pathStub) ? $"{pathStub}/{navigationItem.UrlSlug}" : navigationItem.UrlSlug;
            }
            else
            {
                navigationItem.UrlPath = string.Empty;
            }
        }

        private string GetRedirectPath(NavigationItem cachedNavigation, NavigationItem itemToLocate)
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
                return cachedNavigation.ChildNavigationItems.Select(i => GetRedirectPath(i, itemToLocate)).FirstOrDefault(r => !string.IsNullOrEmpty(r));
            }
        }

        #endregion
    }
}
