using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KenticoCloud.Delivery;
using Microsoft.Extensions.Caching.Memory;
using NavigationMenusMvc.Models;

namespace NavigationMenusMvc.Helpers
{
    public class ContentResolver : IContentResolver
    {
        private readonly IDeliveryClient _client;
        private readonly IMemoryCache _cache;
        private readonly INavigationProvider _navigationProvider;
        private readonly string _navigationCodename;
        private readonly int _rootLevel;
        private readonly string _homepageToken;

        public ContentResolver(IDeliveryClient client, IMemoryCache cache, INavigationProvider navigationProvider, string navigationCodename, int rootLevel, string homepageToken)
        {
            if (string.IsNullOrEmpty(navigationCodename))
            {
                throw new ArgumentNullException(nameof(navigationCodename));
            }

            _client = client ?? throw new ArgumentNullException(nameof(client));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _navigationProvider = navigationProvider;
            _navigationCodename = navigationCodename;
            _rootLevel = rootLevel;
            _homepageToken = homepageToken;
        }

        public async Task<ContentResolverResults> ResolveRelativeUrlPath(string urlPath)
        {
            // Get the 'Navigation' item with depth=deepest menu in the app.
            var navigationItem = await _navigationProvider.GetOrCreateCachedNavigationAsync();

            // Strip the trailing slash and split.
            string[] urlSlugs = NavigationProvider.GetUrlSlugs(urlPath);

            // Recursively iterate over modular content and match the URL slugs for the each recursion level.
            return await ProcessUrlLevelAsync(urlSlugs, navigationItem, _rootLevel, navigationItem.ViewName);
        }

        private async Task<ContentResolverResults> ProcessUrlLevelAsync(string[] urlSlugs, NavigationItem currentLevelItem, int currentLevel, string viewName)
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

            if (currentLevel < _rootLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(currentLevel), "The 'level' must be greater or equal to zero.");
            }

            // No need to replace with ROOT_TOKEN, we're checking the incoming URL.
            string currentSlug = urlSlugs[currentLevel] == string.Empty ? _homepageToken : urlSlugs[currentLevel];

            NavigationItem matchingChild = currentLevelItem.ChildNavigationItems.FirstOrDefault(i => i.UrlSlug == currentSlug);
            bool endOfPath = currentLevel == urlSlugs.Count() - 1;

            if (matchingChild != null)
            {
                // Set a new inherited view name for lower nodes in the hierarchy.
                if (!string.IsNullOrEmpty(matchingChild.ViewName))
                {
                    viewName = matchingChild.ViewName;
                }

                if (endOfPath)
                {
                    return await ResolveContentAsync(matchingChild, matchingChild, viewName, false);
                }
                else
                {
                    int newLevel = currentLevel + 1;

                    // Dig through the incoming URL.
                    return await ProcessUrlLevelAsync(urlSlugs, matchingChild, newLevel, viewName);
                }
            }
            else
            {
                // Uninitialized, hence Found = false.
                return new ContentResolverResults();
            }
        }

        private async Task<ContentResolverResults> ResolveContentAsync(NavigationItem originalItem, NavigationItem currentItem, string viewName, bool redirected)
        {
            if (currentItem.ContentItems != null && currentItem.ContentItems.Any())
            {
                if (redirected)
                {
                    // Get complete URL and return 301. No direct rendering (not SEO-friendly).
                    return new ContentResolverResults
                    {
                        Found = true,
                        RedirectUrl = $"/{currentItem.RedirectPath}"
                    };
                }
                else
                {
                    return new ContentResolverResults
                    {
                        Found = true,
                        ContentItemCodenames = GetContentItemCodenames(currentItem.ContentItems),
                        ViewName = viewName
                    };
                }
            }
            else if (currentItem.LocalRedirect != null && currentItem.LocalRedirect.Any())
            {
                var redirectItem = currentItem.LocalRedirect.FirstOrDefault();

                // Check for infinite loops.
                if (!redirectItem.Equals(originalItem))
                {
                    return await ResolveContentAsync(originalItem, redirectItem, viewName, true);
                }
                else
                {
                    // Non-invasive solution. Uninitialized, hence Found = false.
                    return new ContentResolverResults();
                }
            }
            else if (!string.IsNullOrEmpty(currentItem.OtherRedirect))
            {
                return new ContentResolverResults
                {
                    // Setting Found to false is a sign of a non-local redirect.
                    RedirectUrl = currentItem.OtherRedirect
                };
            }
            else
            {
                // Dtto.
                return new ContentResolverResults();
            }
        }

        private List<string> GetContentItemCodenames(IEnumerable<object> contentItems)
        {
            var codenames = new List<string>();

            foreach (var item in contentItems)
            {
                ContentItemSystemAttributes system = item.GetType().GetTypeInfo().GetProperty("System", typeof(ContentItemSystemAttributes)).GetValue(item) as ContentItemSystemAttributes;
                codenames.Add(system.Codename);
            }

            return codenames;
        }
    }
}
