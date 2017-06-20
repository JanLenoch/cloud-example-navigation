using NavigationMenusMvc.Models;
using System.Threading.Tasks;

namespace NavigationMenusMvc.Helpers
{
    public interface IContentResolver
    {
        Task<ContentResolverResults> ResolveRelativeUrlPath(string urlPath, string navigationCodeName = null, int? maxDepth = null);
    }
}