using KenticoCloud.Delivery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace NavigationMenusMvc.Controllers
{
    public class BaseController : Controller
    {
        protected readonly IMemoryCache _cache;
        protected readonly IDeliveryClient _deliveryClient;

        public BaseController(IDeliveryClient deliveryClient, IMemoryCache memoryCache)
        {
            _deliveryClient = deliveryClient;
            _cache = memoryCache;
        }
    }
}
