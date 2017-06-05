using System.Collections.Generic;
using System.Linq;
using KenticoCloud.Delivery;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using SimpleMvcSitemap;
using Microsoft.Extensions.Caching.Memory;

namespace NavigationMenusMvc.Controllers
{
    public class SiteMapController : BaseController
    {
        public SiteMapController(IDeliveryClient deliveryClient, IMemoryCache memoryCache): base(deliveryClient, memoryCache)
        {
        }

        public async Task<ActionResult> Index()
        {
            // TODO: The different system types which should be included in the sitemap should be specified in the InFilter params
            var parameters = new List<IQueryParameter>
            {
                new DepthParameter(0),
                new InFilter("system.type", "article", "cafe"),
            };

            var response = await _deliveryClient.GetItemsAsync(parameters);

            var nodes = response.Items.Select(item => new SitemapNode(GetPageUrl(item.System))
                {
                    LastModificationDate = item.System.LastModified
                })
                .ToList();

            return new SitemapProvider().CreateSitemap(new SitemapModel(nodes));
        }

        private static string GetPageUrl(ContentItemSystemAttributes system)
        {
            // TODO: The URL generation logic should be adjusted to match your website
            var url = string.Empty;

            if(system.SitemapLocation.Any())
            {
                url = $"/{system.SitemapLocation[0]}";
            }

            url = $"{url}/{system.Codename.Replace("_", "-").TrimEnd('-')}";

            return url;
        }
    }
}
