using System.IO;
using System.Text.Encodings.Web;
using KenticoCloud.Delivery;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace NavigationMenusMvc.Helpers
{
    public static class HtmlHelperExtensions
    {
        public static HtmlString AssetImage(this IHtmlHelper htmlHelper, Asset asset, string title = null, string cssClass = "", int? width = null, int? height = null)
        {
            if (asset == null)
            {
                return HtmlString.Empty;
            }

            var image = new TagBuilder("img");
            image.MergeAttribute("src", asset.Url);
            image.AddCssClass(cssClass);
            string titleToUse = title ?? asset.Name ?? string.Empty;
            image.MergeAttribute("alt", titleToUse);
            image.MergeAttribute("title", titleToUse);

            if (width.HasValue)
            {
                image.MergeAttribute("width", width.ToString());
            }

            if (height.HasValue)
            {
                image.MergeAttribute("height", height.ToString());
            }

            var writer = new StringWriter();
            image.WriteTo(writer, HtmlEncoder.Default);

            return new HtmlString(writer.ToString());
        }
    }
}
