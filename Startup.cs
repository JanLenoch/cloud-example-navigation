using NavigationMenusMvc.Models;
using NavigationMenusMvc.Resolvers;
using NavigationMenusMvc.Services;
using KenticoCloud.Delivery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NavigationMenusMvc.Helpers;

namespace NavigationMenusMvc
{
    public class Startup
    {
        // This constant must match <UserSecretsId> value in NavigationMenusMvc.csproj 
        public const string USER_SECRETS_ID = "NavigationMenusMvc";
        public IConfigurationRoot Configuration { get; }

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddUserSecrets(USER_SECRETS_ID)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Adds services required for using options.
            services.AddOptions();

            // Register the IConfiguration instance which ProjectOptions binds against.
            services.Configure<ProjectOptions>(Configuration);
            services.Configure<NavigationOptions>(Configuration);
            services.Configure<ContentResolverOptions>(Configuration);
            services.AddMemoryCache();
            services.AddMvc();

            services.AddSingleton<IDeliveryClient>(c => new CachedDeliveryClient(c.GetRequiredService<IOptions<ProjectOptions>>(), c.GetRequiredService<IMemoryCache>())
            {
                CodeFirstModelProvider = { TypeProvider = new CustomTypeProvider() },
                ContentLinkUrlResolver = new CustomContentLinkUrlResolver()
            });

            services.AddSingleton<INavigationProvider>(c => new NavigationProvider(c.GetRequiredService<IOptions<NavigationOptions>>(), c.GetRequiredService<IDeliveryClient>(), c.GetRequiredService<IMemoryCache>()));
            services.AddSingleton<IContentResolver>(c => new ContentResolver(c.GetRequiredService<IOptions<ContentResolverOptions>>(), c.GetRequiredService<INavigationProvider>()));
            services.AddSingleton<IMenuItemGenerator>(c => new MenuItemGenerator(c.GetRequiredService<IOptions<NavigationOptions>>(), c.GetRequiredService<IDeliveryClient>(), c.GetRequiredService<IMemoryCache>()));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            // Add console logger with a logging scopes from appsettings.json
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));

            if (env.IsDevelopment())
            {
                loggerFactory.AddDebug();

                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                // Handle unhandled exceptions as Internal Server Errors (500)
                app.UseExceptionHandler("/Error/500");

                // Display friendly error pages for any 
                // non-success case (status code is >= 400 and < 600)
                app.UseStatusCodePagesWithReExecute("/Error/{0}");
            }

            // Add IIS URL Rewrite list
            // See https://docs.microsoft.com/en-us/aspnet/core/fundamentals/url-rewriting
            app.UseRewriter(new RewriteOptions()
                .AddIISUrlRewrite(env.ContentRootFileProvider, "IISUrlRewrite.xml")
            );

            // Enables anything under wwwroot to be served directly (without any permission check).
            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "facetedNavigation",
                    template: "blog/{year?}/{month?}",
                    defaults: new { controller = "Blog", action = "Index" });

                routes.MapRoute(
                    name: "staticContent",
                    template: "{*urlPath}",
                    defaults: new { controller = "StaticContent", action = "Index" },
                    constraints: new { urlPath = new StaticContentConstraint(app.ApplicationServices.GetRequiredService<IContentResolver>()) });

                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
