using System;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NavigationMenusMvc.Models;

namespace NavigationMenusMvc.Helpers
{
    public class StaticContentConstraint : IRouteConstraint
    {
        private readonly IContentResolver _resolver;

        public StaticContentConstraint(IContentResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public bool Match(HttpContext httpContext, IRouter route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (routeKey == null)
            {
                throw new ArgumentNullException(nameof(routeKey));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (values.TryGetValue(routeKey, out object routeValue))
            {
                var parameterValueString = Convert.ToString(routeValue, CultureInfo.InvariantCulture);

                ContentResolverResults results = _resolver.ResolveRelativeUrlPathAsync(parameterValueString).Result;

                return (results != null && results.Found);
            }

            return false;
        }
    }
}
