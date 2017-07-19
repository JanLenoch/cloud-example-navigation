using System.Threading.Tasks;
using NavigationMenusMvc.Models;

namespace NavigationMenusMvc.Helpers
{
    public interface INavigationProvider
    {
        Task<NavigationItem> GetNavigationAsync(string navigationCodeName = null, int? maxDepth = null);
    }
}