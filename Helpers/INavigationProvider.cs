using NavigationMenusMvc.Models;
using System.Threading.Tasks;

namespace NavigationMenusMvc.Helpers
{
    public interface INavigationProvider
    {
        Task<NavigationItem> GetNavigationItemsAsync(string navigationCodeName = null, int? maxDepth = null);
        Task<NavigationItem> GetOrCreateCachedNavigationAsync(string navigationCodeName = null, int? maxDepth = null);
    }
}