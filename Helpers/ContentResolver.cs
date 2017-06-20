using KenticoCloud.Delivery;
using Microsoft.Extensions.Options;
using NavigationMenusMvc.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace NavigationMenusMvc.Helpers
{
    public class ContentResolver : IContentResolver
    {
        #region "Fields"
        
        private readonly INavigationProvider _navigationProvider;
        private readonly string _navigationCodename;
        private readonly int _maxDepth;
        private readonly int _rootLevel;
        private readonly string _homepageToken;

        #endregion
        
        #region "Constructors"

        public ContentResolver(IOptions<ContentResolverOptions> options, INavigationProvider navigationProvider)
        {
            if (options.Value.NavigationCodename == null)
            {
                throw new ArgumentNullException(nameof(options.Value.NavigationCodename));
            }
            else if (options.Value.NavigationCodename.Equals(string.Empty))
            {
                throw new ArgumentOutOfRangeException(nameof(options.Value.NavigationCodename), $"The '{nameof(options.Value.NavigationCodename)}' parameter must not be an empty string.");
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

            if (!options.Value.RootLevel.HasValue)
            {
                throw new ArgumentNullException(nameof(options.Value.RootLevel));
            }
            else if (options.Value.RootLevel.Value < 0)
            {
                // TODO Add constructor description.
                throw new ArgumentOutOfRangeException(nameof(options.Value.RootLevel), $"The {nameof(options.Value.RootLevel)} parameter must be 0 or higher.");
            }

            if (options.Value.HomepageToken == null)
            {
                throw new ArgumentNullException(nameof(options.Value.HomepageToken));
            }
            else if (options.Value.HomepageToken.Equals(string.Empty))
            {
                throw new ArgumentOutOfRangeException(nameof(options.Value.HomepageToken), $"The '{nameof(options.Value.HomepageToken)}' parameter must not be an empty string.");
            }

            _navigationProvider = navigationProvider ?? throw new ArgumentNullException(nameof(navigationProvider));
            _navigationCodename = options.Value.NavigationCodename;
            _maxDepth = options.Value.MaxDepth.Value;
            _rootLevel = options.Value.RootLevel.Value;
            _homepageToken = options.Value.HomepageToken;
        }

        #endregion

        #region "Public methods"

        /// <summary>
        /// Resolves the relative URL path into <see cref="ContentResolverResults"/> containing either the codenames of content items, or a redirect URL.
        /// </summary>
        /// <param name="urlPath">The relative URL from the HTTP request</param>
        /// <returns>The <see cref="ContentResolverResults"/>. If Found is true and the RedirectUrl isn't empty, then it means a local redirect to a static content URL.</returns>
        public async Task<ContentResolverResults> ResolveRelativeUrlPath(string urlPath, string navigationCodeName = null, int? maxDepth = null)
        {
            string cn = navigationCodeName ?? _navigationCodename;
            int d = maxDepth.HasValue ? maxDepth.Value : _maxDepth;

            // Get the 'Navigation' item with depth=deepest menu in the app.
            var navigationItem = await _navigationProvider.GetOrCreateCachedNavigationAsync(cn, d);

            // Strip the trailing slash and split.
            string[] urlSlugs = NavigationProvider.GetUrlSlugs(urlPath);

            // Recursively iterate over modular content and match the URL slugs for the each recursion level.
            return await ProcessUrlLevelAsync(urlSlugs, navigationItem, _rootLevel, navigationItem.ViewName);
        }

        /// <summary>
        /// Gets the codenames of <see cref="IEnumerable{Object}"/> content items using <see cref="System.Reflection"/>.
        /// </summary>
        /// <param name="contentItems">The shallow content items to be fetched again using their codenames</param>
        /// <returns>The codenames</returns>
        public static IEnumerable<string> GetContentItemCodenames(IEnumerable<object> contentItems)
        {
            if (contentItems == null)
            {
                throw new ArgumentNullException(nameof(contentItems));
            }

            var codenames = new List<string>();

            foreach (var item in contentItems)
            {
                ContentItemSystemAttributes system = item.GetType().GetTypeInfo().GetProperty("System", typeof(ContentItemSystemAttributes)).GetValue(item) as ContentItemSystemAttributes;
                codenames.Add(system.Codename);
            }

            return codenames;
        }

        #endregion

        #region "Private methods"

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
            if (currentItem == null)
            {
                throw new ArgumentNullException(nameof(currentItem));
            }

            if (currentItem.ContentItems != null && currentItem.ContentItems.Any())
            {
                if (redirected)
                {
                    // Get complete URL and return 301. No direct rendering (not SEO-friendly).
                    return new ContentResolverResults
                    {
                        Found = true,

                        // Allowing client code to decide the final shape of URL, hence no leading slash character.
                        RedirectUrl = $"{currentItem.UrlPath}"
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
            else if (currentItem.RedirectToItem != null && currentItem.RedirectToItem.Any())
            {
                var redirectItem = currentItem.RedirectToItem.FirstOrDefault();

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
            else if (!string.IsNullOrEmpty(currentItem.RedirectToUrl))
            {
                return new ContentResolverResults
                {
                    // Setting Found to false is a sign of a non-local redirect.
                    RedirectUrl = currentItem.RedirectToUrl
                };
            }
            else
            {
                // Dtto.
                return new ContentResolverResults();
            }
        }

        #endregion
    }
}
