using NavigationMenusMvc.Models;
using System.Threading.Tasks;

namespace NavigationMenusMvc.Helpers
{
    public interface IContentResolver
    {
        Task<IContentResolverResults> ResolveRelativeUrlPathAsync(string urlPath, string navigationCodeName = null, int? maxDepth = null);
    }
}