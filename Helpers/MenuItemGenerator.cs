using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using KenticoCloud.Delivery;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NavigationMenusMvc.Models;

namespace NavigationMenusMvc.Helpers
{

    public class MenuItemGenerator : IMenuItemGenerator
    {
        #region "Fields"

        IDeliveryClient _client;
        IMemoryCache _cache;
        private readonly int _navigationCacheExpirationMinutes;
        private Dictionary<string, Func<NavigationItem, string, Task<NavigationItem>>> _startingUrls = new Dictionary<string, Func<NavigationItem, string, Task<NavigationItem>>>();

        #endregion

        #region "Constructors"

        /// <summary>
        /// Constructs a new <see cref="MenuItemGenerator"/>.
        /// </summary>
        /// <param name="options">Environment settings</param>
        /// <param name="client">A client to communicate with the Delivery/Preview API</param>
        /// <param name="cache">The in-memory cache. The NavigationCacheExpirationMinutes value must be a positive number.</param>
        public MenuItemGenerator(IOptions<NavigationOptions> options, IDeliveryClient client, IMemoryCache cache)
        {
            if (!options.Value.NavigationCacheExpirationMinutes.HasValue)
            {
                throw new ArgumentNullException(nameof(options.Value.NavigationCacheExpirationMinutes));
            }
            else if (options.Value.NavigationCacheExpirationMinutes.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.Value.NavigationCacheExpirationMinutes), $"The {nameof(options.Value.NavigationCacheExpirationMinutes)} parameter must be greater than zero.");
            }

            _client = client ?? throw new ArgumentNullException(nameof(client));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _navigationCacheExpirationMinutes = options.Value.NavigationCacheExpirationMinutes.Value;
            _startingUrls.Add("blog", GenerateNavigationWithBlogItemsAsync);
        }

        #endregion

        #region "Public methods"

        /// <summary>
        /// Wraps all methods that generate additional navigation items.
        /// </summary>
        /// <param name="sourceItem">The original root navigation item</param>
        /// <returns>A copy of the <paramref name="sourceItem"/> with additional items</returns>
        public async Task<NavigationItem> GenerateItemsAsync(NavigationItem sourceItem)
        {
            return await _cache.GetOrCreateAsync("generatedNavigationItems", async entry =>
            {
                foreach (var url in _startingUrls)
                {
                    sourceItem = await url.Value(sourceItem, url.Key);
                }

                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_navigationCacheExpirationMinutes);

                return sourceItem;
            });
        }

        #endregion

        #region "Private methods"

        /// <summary>
        /// Generates a copy of the original <see cref="NavigationItem"/> hierarchy, with added child navigation items under the "/blog" starting item. Generates year and month items, based on "Post date" elements of existing "Article" content items.
        /// </summary>
        /// <param name="originalItem">The original hierarchy</param>
        /// <param name="startingUrl">The starting item URL</param>
        /// <returns>The new hierarchy with Blog child items</returns>
        private async Task<NavigationItem> GenerateNavigationWithBlogItemsAsync(NavigationItem originalItem, string startingUrl)
        {
            var response = await _client.GetItemsAsync<Article>(new EqualsFilter("system.type", "article"), new ElementsParameter("post_date"));

            // The key holds the pair of year and month digits, the value is supposed to hold a friendly name like "October 2014".
            var yearsMonths = new Dictionary<YearMonthPair, string>();

            foreach (var item in response.Items)
            {
                if (item.PostDate.HasValue)
                {
                    try
                    {
                        yearsMonths.Add(new YearMonthPair(item.PostDate.Value.Year, item.PostDate.Value.Month), $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(item.PostDate.Value.Month)} {item.PostDate.Value.Year}");
                    }
                    catch
                    {
                        // Do nothing. Just omit an article falling into the same date range.
                    }
                }
            }

            // If Drawer menu were able to render deeply nested menus, I could change the "flat" to false.
            var regeneratedItems = ProcessLevelForBlog(originalItem, yearsMonths, startingUrl, flat: true, processedParents: new List<NavigationItem>());

            return regeneratedItems;
        }

        /// <summary>
        /// Traverses the original hierarchy, creates a lightweight clone of it (not all original properties), and adds <see cref="NavigationItem"/> items for the Blog section.
        /// </summary>
        /// <param name="currentItem">The root <see cref="NavigationItem"/></param>
        /// <param name="yearsMonths">Dictionary of years and months of existing "Article" content items</param>
        /// <param name="startingUrl">The starting URL where new year and month <see cref="NavigationItem"/> items will be added</param>
        /// <param name="flat">Flag indicating whether flat structure like "October 2014" should be generated</param>
        /// <param name="processedParents">Collection of processed parent navigation items</param>
        /// <returns>A copy of <paramref name="currentItem"/> with generated Blog navigation items</returns>
        private NavigationItem ProcessLevelForBlog(NavigationItem currentItem, IDictionary<YearMonthPair, string> yearsMonths, string startingUrl, bool flat, IList<NavigationItem> processedParents)
        {
            processedParents = processedParents ?? new List<NavigationItem>();
            var newItem = new NavigationItem();

            // Check for infinite loops.
            if (!processedParents.Contains(currentItem))
            {
                // Add only those properties that are needed to render the menu (as opposed to routing the incoming requests).
                newItem.Title = currentItem.Title;
                newItem.UrlPath = currentItem.UrlPath;
                newItem.AllParents = processedParents;

                // Prepare the collection of all parents for the next chunk of child items.
                var nextProcessedParents = new List<NavigationItem>(processedParents);

                nextProcessedParents.Add(currentItem);

                // The "/blog" item is currently being iterated over.
                if (currentItem.UrlPath.Equals(startingUrl, StringComparison.OrdinalIgnoreCase))
                {
                    // Example of a flat variant: "October 2014", "November 2014" etc.
                    if (flat)
                    {
                        var items = new List<NavigationItem>();
                        items.AddRange(yearsMonths.OrderBy(k => k.Key, new YearMonthComparer()).Select(i => GetItem(startingUrl, i.Value, i.Key.Year, i.Key.Month)));
                        newItem.ChildNavigationItems = items.ToList();
                    }
                    // Example of a deep variant:
                    // "2014"
                    //    "10"
                    //    "11"
                    else
                    {
                        var yearItems = new List<NavigationItem>();

                        // Distill years of existing "Article" items.
                        foreach (var year in yearsMonths.Distinct(new YearEqualityComparer()))
                        {
                            var yearItem = GetItem(startingUrl, null, year.Key.Year);
                            yearItems.Add(yearItem);
                            var monthItems = new List<NavigationItem>();

                            // Distill months.
                            foreach (var month in yearsMonths.Keys.Where(k => k.Year == year.Key.Year).OrderBy(k => k.Month))
                            {
                                monthItems.Add(GetItem(startingUrl, null, year.Key.Year, month.Month));
                            }

                            yearItem.ChildNavigationItems = monthItems;
                        }

                        newItem.ChildNavigationItems = yearItems;
                    }
                }
                else
                {
                    newItem.ChildNavigationItems = currentItem.ChildNavigationItems.Select(i => ProcessLevelForBlog(i, yearsMonths, startingUrl, flat, nextProcessedParents)).ToList();
                }

                return newItem;
            }

            return null;
        }

        private static NavigationItem GetItem(string startingUrl, string title, int year, int? month = null)
        {
            string urlPath;

            if (!month.HasValue)
            {
                title = year.ToString();
                urlPath = ConcatenateUrl(startingUrl, year, null);
            }
            else
            {
                if (string.IsNullOrEmpty(title))
                {
                    title = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month.Value);
                }

                urlPath = ConcatenateUrl(startingUrl, year, month.Value);
            }

            return new NavigationItem
            {
                Title = title,
                UrlPath = urlPath
            };
        }

        private static string ConcatenateUrl(string startingUrl, int year, int? month)
        {
            if (startingUrl == null)
            {
                throw new ArgumentNullException(nameof(startingUrl));
            }

            startingUrl += (startingUrl.EndsWith("/") ? year.ToString() : $"/{year}");

            return (month.HasValue) ? startingUrl + $"/{month}" : startingUrl;
        }

        #endregion
    }
}
