using System.Threading.Tasks;
using NavigationMenusMvc.Models;

namespace NavigationMenusMvc.Helpers
{
    public interface IContentResolver
    {
        Task<IContentResolverResults> ResolveRelativeUrlPathAsync(string urlPath, string navigationCodeName = null, int? maxDepth = null);
    }
}