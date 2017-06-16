using System.Threading.Tasks;
using NavigationMenusMvc.Models;

namespace NavigationMenusMvc.Helpers
{
    public interface IContentResolver
    {
        Task<ContentResolverResults> ResolveRelativeUrlPath(string urlPath);
    }
}