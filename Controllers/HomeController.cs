using NavigationMenusMvc.Models;
using KenticoCloud.Delivery;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace NavigationMenusMvc.Controllers
{
    public class HomeController : BaseController
    {
        public HomeController(IDeliveryClient deliveryClient, IMemoryCache memoryCache) : base(deliveryClient, memoryCache)
        {
            
        }

        public async Task<ViewResult> Index()
        {
            var response = await _deliveryClient.GetItemsAsync<Article>(
                new EqualsFilter("system.type", "article"),
                new LimitParameter(3),
                new DepthParameter(0),
                new OrderParameter("elements.post_date")
            );

            return View(response.Items);
        }
    }
}
