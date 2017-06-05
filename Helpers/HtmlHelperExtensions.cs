﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using KenticoCloud.Delivery;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Html;
using System.IO;
using System.Text.Encodings.Web;

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
