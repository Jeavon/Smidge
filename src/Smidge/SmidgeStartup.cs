﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Smidge.CompositeFiles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
//using Microsoft.AspNetCore.NodeServices;
using Smidge.Models;
using Microsoft.Extensions.Options;
using Smidge.Options;
using Smidge.FileProcessors;

[assembly: InternalsVisibleTo("Smidge.Tests")]

namespace Smidge
{
    public static class SmidgeStartup
    {


        public static IServiceCollection AddSmidge(this IServiceCollection services, IConfiguration smidgeConfiguration = null, IFileProvider fileProvider = null)
        {
            //services.AddNodeServices(NodeHostingModel.Http);

            services.AddTransient<IConfigureOptions<SmidgeOptions>, SmidgeOptionsSetup>();
            services.AddTransient<IConfigureOptions<Bundles>, BundlesSetup>();
            services.AddSingleton<PreProcessPipelineFactory>();
            services.AddSingleton<BundleManager>();
            services.AddSingleton<FileSystemHelper>((p) =>
            {
                var hosting = p.GetRequiredService<IHostingEnvironment>();
                var provider = fileProvider ?? hosting.WebRootFileProvider;
                return new FileSystemHelper(p.GetRequiredService<IHostingEnvironment>(), hosting,
                    p.GetRequiredService<ISmidgeConfig>(), p.GetRequiredService<IUrlHelper>(), provider);
            });


            services.AddSingleton<PreProcessManager>();
            services.AddSingleton<ISmidgeConfig>((p) =>
            {
                if (smidgeConfiguration == null)
                {
                    return new SmidgeConfig(p.GetRequiredService<IHostingEnvironment>());
                }
                return new SmidgeConfig(smidgeConfiguration);
            });
            services.AddScoped<DynamicallyRegisteredWebFiles>();
            services.AddScoped<SmidgeHelper>();
            services.AddSingleton<IUrlManager, DefaultUrlManager>();
            services.AddSingleton<IHasher, Crc32Hasher>();

            //pre-processors
            services.AddSingleton<IPreProcessor, JsMinifier>();
            services.AddSingleton<IPreProcessor, CssMinifier>();
            //services.AddSingleton<IPreProcessor, NodeMinifier>();
            services.AddSingleton<IPreProcessor, CssImportProcessor>();
            services.AddSingleton<IPreProcessor, CssUrlProcessor>();
            //conventions
            services.AddSingleton<IFileProcessingConvention, MinifiedFilePathConvention>();

            //Add the controller models as DI services - these get auto created for model binding
            services.AddTransient<BundleModel>();
            services.AddTransient<CompositeFileModel>();

            return services;
        }

        public static void UseSmidge(this IApplicationBuilder app)
        {

            //Create custom route
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    "SmidgeComposite",
                    "sc/{file}",
                    new { controller = "Smidge", action = "Composite" });

                routes.MapRoute(
                    "SmidgeBundle",
                    "sb/{bundle}",
                    new { controller = "Smidge", action = "Bundle" });

            });


        }
    }
}
