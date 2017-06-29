using KenticoCloud.Delivery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using NavigationMenusMvc.Helpers;
using NavigationMenusMvc.Models;
using SimpleMvcSitemap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NavigationMenusMvc.Controllers
{
    public class SiteMapController : BaseController
    {
        private readonly INavigationProvider _navigationProvider;

        public SiteMapController(IDeliveryClient deliveryClient, IMemoryCache memoryCache, INavigationProvider navigationProvider) : base(deliveryClient, memoryCache)
        {
            _navigationProvider = navigationProvider ?? throw new ArgumentNullException(nameof(navigationProvider));
        }

        public async Task<ActionResult> Index()
        {
            var navigation = await _navigationProvider.GetOrCreateCachedNavigationAsync();

            if (navigation != null)
            {
                var flatNavigation = NavigationProvider.GetNavigationItemsFlat(navigation).ToList();
                Dictionary<NavigationItem, List<string>> codenames = new Dictionary<NavigationItem, List<string>>();

                foreach (var item in flatNavigation)
                {
                    codenames.Add(item, ContentResolver.GetContentItemCodenames(item.ContentItems).ToList());
                }

                var response = await _deliveryClient.GetItemsAsync(new InFilter("system.codename", codenames.SelectMany(ni => ni.Value).ToArray()));
                var nodes = new List<SitemapNode>();

                foreach (var item in codenames)
                {
                    var lastModifiedContentItem = response.Items.Where(ci => item.Value.Contains(ci.System.Codename)).OrderByDescending(ci => ci.System.LastModified).FirstOrDefault();

                    if (lastModifiedContentItem != null)
                    {
                        nodes.Add(new SitemapNode(item.Key.UrlPath)
                        {
                            LastModificationDate = lastModifiedContentItem.System.LastModified
                        });
                    }
                }

                return new SitemapProvider().CreateSitemap(new SitemapModel(nodes)); 
            }
            else
            {
                return NotFound();
            }
        }
    }
}
