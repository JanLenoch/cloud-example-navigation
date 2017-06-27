using KenticoCloud.Delivery;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NavigationMenusMvc.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace NavigationMenusMvc.Helpers
{

    public class MenuItemGenerator : IMenuItemGenerator
    {
        #region "Fields"

        IDeliveryClient _client;
        IMemoryCache _cache;
        private readonly int _navigationCacheExpirationMinutes;

        #endregion

        #region "Properties"

        public IDictionary<string, Func<INavigationItem, string, Task<INavigationItem>>> WellKnownUrls
        {
            get
            {
                return new Dictionary<string, Func<INavigationItem, string, Task<INavigationItem>>>()
                {
                    { "blog", GenerateBlogYearMonthItems }
                };
            }
        }

        #endregion

        #region "Constructors"

        public MenuItemGenerator(IOptions<NavigationOptions> options, IDeliveryClient client, IMemoryCache cache)
        {
            if (!options.Value.NavigationCacheExpirationMinutes.HasValue)
            {
                throw new ArgumentNullException(nameof(options.Value.NavigationCacheExpirationMinutes));
            }
            else if (options.Value.NavigationCacheExpirationMinutes.Value <= 0)
            {
                // TODO Add constructor description.
                throw new ArgumentOutOfRangeException(nameof(options.Value.NavigationCacheExpirationMinutes), $"The {nameof(options.Value.NavigationCacheExpirationMinutes)} parameter must be greater than zero.");
            }

            _client = client ?? throw new ArgumentNullException(nameof(client));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _navigationCacheExpirationMinutes = options.Value.NavigationCacheExpirationMinutes.Value;
        }

        #endregion

        #region "Public methods"

        public async Task<INavigationItem> TryGenerateItems(INavigationItem sourceItem)
        {
            foreach (var url in WellKnownUrls.Keys)
            {
                sourceItem = await WellKnownUrls[url].Invoke(sourceItem, WellKnownUrls.Keys.FirstOrDefault(k => k.Equals(url, StringComparison.OrdinalIgnoreCase)));
            }

            return sourceItem;
        }

        #endregion

        #region "Private methods"

        private async Task<INavigationItem> GenerateBlogYearMonthItems(INavigationItem originalItem, string wellKnownUrl)
        {
            return await _cache.GetOrCreate($"blogGeneratedMenuItems|{wellKnownUrl}", async entry =>
            {
                var response = await _client.GetItemsAsync<Article>(new EqualsFilter("system.type", "article"), new ElementsParameter(new string[] { "post_date" }));
                var yearsMonths = new ConcurrentDictionary<Tuple<int, int>, string>();

                Parallel.ForEach(response.Items.ToList(), i =>
                {
                    if (i.PostDate.HasValue)
                    {
                        yearsMonths.TryAdd(new Tuple<int, int>(i.PostDate.Value.Year, i.PostDate.Value.Month), $"{Enum.GetName(typeof(Months), i.PostDate.Value.Month)} {i.PostDate.Value.Year}");
                    }
                });

                // Start a recursion for ChildNavigationItems.
                // If Drawer menu were able to render deeply nested menus, I could change the flat: true to false.
                var regeneratedItems = RegenerateItem(originalItem, yearsMonths, new List<INavigationItem>(), wellKnownUrl, flat: true);

                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_navigationCacheExpirationMinutes);

                return regeneratedItems;
            });
        }

        private NavigationItem RegenerateItem(INavigationItem currentItem, ConcurrentDictionary<Tuple<int, int>, string> yearsMonths, List<INavigationItem> parentItems, string wellKnownUrl, bool flat)
        {
            parentItems = parentItems ?? new List<INavigationItem>();
            var newItem = new NavigationItem();

            if (!parentItems.Contains(currentItem))
            {
                newItem.AppearsIn = currentItem.AppearsIn;
                newItem.Title = currentItem.Title;
                newItem.UrlPath = currentItem.UrlPath;
                parentItems.Add(currentItem);

                if (currentItem.UrlPath.Equals(wellKnownUrl, StringComparison.OrdinalIgnoreCase))
                {
                    if (flat)
                    {
                        var items = new List<NavigationItem>();
                        items.AddRange(yearsMonths.OrderBy(k => k.Key, new YearMonthComparer()).Select(i => GenerateYearMonthItem(i.Key.Item1, i.Key.Item2, i.Value, wellKnownUrl, currentItem.AppearsIn)));
                        newItem.ChildNavigationItems = items.ToList();
                    }
                    else
                    {
                        var yearItems = new List<NavigationItem>();

                        foreach (var year in yearsMonths.Distinct(new YearEqualityComparer()))
                        {
                            var yearItem = GenerateYearItem(year.Key.Item1, wellKnownUrl, currentItem.AppearsIn);
                            yearItems.Add(yearItem);
                            var monthItems = new List<NavigationItem>();

                            foreach (var month in yearsMonths.Keys.Where(k => k.Item1 == year.Key.Item1).OrderBy(k => k.Item2))
                            {
                                monthItems.Add(GenerateMonthItem(year.Key.Item1, month.Item2, wellKnownUrl, currentItem.AppearsIn));
                            }

                            yearItem.ChildNavigationItems = monthItems;
                        }

                        newItem.ChildNavigationItems = yearItems;
                    }
                }
                else
                {
                    newItem.ChildNavigationItems = currentItem.ChildNavigationItems.Select(i => RegenerateItem(i, yearsMonths, parentItems, wellKnownUrl, flat)).ToList();
                }

                return newItem;
            }

            return null;
        }

        private static NavigationItem GenerateYearMonthItem(int year, int month, string title, string wellKnownUrl, IEnumerable<MultipleChoiceOption> appearsIn)
        {
            return new NavigationItem
            {
                AppearsIn = appearsIn,
                Title = title,
                UrlPath = ConcatenateUrl(wellKnownUrl, year, month)
            };
        }

        private static NavigationItem GenerateYearItem(int year, string wellKnownUrl, IEnumerable<MultipleChoiceOption> appearsIn)
        {
            return new NavigationItem
            {
                AppearsIn = appearsIn,
                Title = year.ToString(),
                UrlPath = ConcatenateUrl(wellKnownUrl, year, null)
            };
        }

        private static NavigationItem GenerateMonthItem(int year, int month, string wellKnownUrl, IEnumerable<MultipleChoiceOption> appearsIn)
        {
            return new NavigationItem
            {
                AppearsIn = appearsIn,
                Title = Enum.GetName(typeof(Months), month),
                UrlPath = ConcatenateUrl(wellKnownUrl, year, month)
            };
        }

        private static string ConcatenateUrl(string wellKnownUrl, int year, int? month)
        {
            wellKnownUrl += (wellKnownUrl.EndsWith("/") ? year.ToString() : $"/{year}");

            return (month.HasValue) ? wellKnownUrl += $"/{month}" : wellKnownUrl;
        }

        #endregion
    }
}
