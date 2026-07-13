using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Hosting;
using Microsoft.SourceBrowser.SourceIndexServer.Models;

namespace Microsoft.SourceBrowser.SourceIndexServer
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env)
        {
            Environment = env;
        }

        public IWebHostEnvironment Environment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            RootPath = Path.Combine(Environment.ContentRootPath, "index");

            var subfolder = Path.Combine(RootPath, "index");
            if (File.Exists(Path.Combine(subfolder, "Projects.txt")))
            {
                RootPath = subfolder;
            }

            services.AddSingleton(new Index(RootPath));

            // Everything we serve is generated static text -- HTML source pages, the namespace explorer
            // (multiple MB on large indexes), scripts, and the .txt index stats -- so compressing responses
            // is a large, broad win for download size, which matters most on mobile/cellular. Brotli and
            // gzip are both offered so any browser gets one. The content is public and carries no secrets,
            // so enabling compression over HTTPS poses no BREACH concern. Fastest keeps per-request CPU low
            // (Brotli's Optimal is its slow max-quality mode, which would stall on the multi-MB pages).
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
                {
                    "text/javascript",
                    "image/svg+xml",
                });
            });
            services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
            services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

            services.AddControllersWithViews();
            services.AddRazorPages();
        }

        public string RootPath { get; set; }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Sits ahead of the reference middleware and both static-file handlers so their responses,
            // including the packed reference fragments, are compressed.
            app.UseResponseCompression();

            app.UseDefaultFiles();
            app.UseMiddleware<ReferencePackMiddleware>(RootPath);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(RootPath, ExclusionFilters.Sensitive & ~ExclusionFilters.DotPrefixed),
            });
            app.UseStaticFiles();
            app.UseRouting();


            app.UseEndpoints(endPoints =>
            {
                endPoints.MapRazorPages();
                endPoints.MapControllers();
            });
        }
    }
}
