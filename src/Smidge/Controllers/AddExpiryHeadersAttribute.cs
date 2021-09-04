﻿using Microsoft.Extensions.DependencyInjection;
using Smidge.Models;
using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;
using Smidge.Hashing;

namespace Smidge.Controllers
{
    /// <summary>
    /// Adds the correct caching expiry headers when the request is not in debug
    /// </summary>
    public sealed class AddExpiryHeadersAttribute : Attribute, IFilterFactory, IOrderedFilter
    {
        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            return new AddExpiryHeaderFilter(
                serviceProvider.GetRequiredService<IHasher>(),
                serviceProvider.GetRequiredService<IBundleManager>());
        }

        public bool IsReusable => true;
        
        public int Order { get; set; }

        public sealed class AddExpiryHeaderFilter : IActionFilter
        {
            private readonly IHasher _hasher;
            private readonly IBundleManager _bundleManager;

            public AddExpiryHeaderFilter(IHasher hasher, IBundleManager bundleManager)
            {
                _hasher = hasher;
                _bundleManager = bundleManager;
            }

            public void OnActionExecuting(ActionExecutingContext context)
            {
                if (context.ActionArguments.Count == 0)
                    return;

                //put the model in the context, we'll resolve that after it's executed
                if (context.ActionArguments.First().Value is RequestModel file)
                    context.HttpContext.Items[nameof(AddExpiryHeadersAttribute)] = file;
            }

            /// <summary>
            /// Adds the expiry headers
            /// </summary>
            /// <param name="context"></param>
            public void OnActionExecuted(ActionExecutedContext context)
            {
                if (context.Exception != null) return;

                //get the model from the items
                if (context.HttpContext.Items.TryGetValue(nameof(AddExpiryHeadersAttribute), out var requestModel) && requestModel is RequestModel file)
                {
                    var enableETag = true;
                    var cacheControlMaxAge = 10 * 24; //10 days

                    //check if it's a bundle (not composite file)
                    if (file is BundleRequestModel bundleRequest && _bundleManager.TryGetValue(bundleRequest.FileKey, out var bundle))
                    {
                        var bundleOptions = bundle.GetBundleOptions(_bundleManager, bundleRequest.Debug);
                        enableETag = bundleOptions.CacheControlOptions.EnableETag;
                        cacheControlMaxAge = bundleOptions.CacheControlOptions.CacheControlMaxAge;
                    }

                    if (enableETag)
                    {
                        var etag = _hasher.Hash(file.FileKey + file.Compression + file.Mime);
                        context.HttpContext.Response.AddETagResponseHeader(etag);
                    }

                    if (cacheControlMaxAge > 0)
                    {
                        context.HttpContext.Response.AddCacheControlResponseHeader(cacheControlMaxAge);
                        context.HttpContext.Response.AddLastModifiedResponseHeader(file);
                        context.HttpContext.Response.AddExpiresResponseHeader(cacheControlMaxAge);
                    }
                }
            }
        }
    }
}