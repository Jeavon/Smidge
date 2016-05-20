﻿using Microsoft.AspNetCore.Mvc.Rendering;
using Smidge.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Smidge.CompositeFiles;
using Smidge.FileProcessors;

namespace Smidge
{
    /// <summary>
    /// Used in views to register and render dependencies
    /// </summary>
    public class SmidgeHelper : ISmidgeRequire
    {
        private readonly DynamicallyRegisteredWebFiles _dynamicallyRegisteredWebFiles;
        private readonly PreProcessManager _fileManager;
        private readonly FileSystemHelper _fileSystemHelper;
        private readonly BundleManager _bundleManager;
        private readonly IHttpContextAccessor _http;
        private readonly RequestParts _requestParts;
        private readonly FileBatcher _fileBatcher;
        private readonly PreProcessPipelineFactory _processorFactory;
        private readonly IUrlManager _urlManager;


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dynamicallyRegisteredWebFiles"></param>
        /// <param name="fileManager"></param>
        /// <param name="fileSystemHelper"></param>
        /// <param name="hasher"></param>
        /// <param name="bundleManager"></param>
        /// <param name="http"></param>
        /// <param name="processorFactory"></param>
        /// <param name="urlManager"></param>
        public SmidgeHelper(
            DynamicallyRegisteredWebFiles dynamicallyRegisteredWebFiles,
            PreProcessManager fileManager,
            FileSystemHelper fileSystemHelper,
            IHasher hasher,
            BundleManager bundleManager,
            IHttpContextAccessor http,
            PreProcessPipelineFactory processorFactory,
            IUrlManager urlManager)
        {
            _processorFactory = processorFactory;
            _urlManager = urlManager;
            _bundleManager = bundleManager;
            _http = http;
            _fileManager = fileManager;
            _dynamicallyRegisteredWebFiles = dynamicallyRegisteredWebFiles;
            _fileSystemHelper = fileSystemHelper;
            _requestParts = new RequestParts(http.HttpContext.Request);

            _fileBatcher = new FileBatcher(_fileSystemHelper, _requestParts, hasher);
        }

        public async Task<HtmlString> JsHereAsync(string bundleName, bool debug = false)
        {
            var urls = await GenerateJsUrlsAsync(bundleName, debug);
            var result = new StringBuilder();

            foreach (var url in urls)
            {
                result.AppendFormat("<script src='{0}' type='text/javascript'></script>", url);
            }
            return new HtmlString(result.ToString());
        }

        public async Task<HtmlString> CssHereAsync(string bundleName, bool debug = false)
        {
            var urls = await GenerateCssUrlsAsync(bundleName, debug);
            var result = new StringBuilder();

            foreach (var url in urls)
            {
                result.AppendFormat("<link href='{0}' rel='stylesheet' type='text/css'/>", url);
            }
            return new HtmlString(result.ToString());
        }

        /// <summary>
        /// Renders the JS tags
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// TODO: Once the tags are rendered the collection on the context is cleared. Therefore if this method is called multiple times it will 
        /// render anything that has been registered as 'pending' but has not been rendered.
        /// </remarks>
        public async Task<HtmlString> JsHereAsync(PreProcessPipeline pipeline = null, bool debug = false)
        {
            var result = new StringBuilder();
            var urls = await GenerateJsUrlsAsync(pipeline, debug);
            foreach (var url in urls)
            {
                result.AppendFormat("<script src='{0}' type='text/javascript'></script>", url);
            }
            return new HtmlString(result.ToString());
        }

        /// <summary>
        /// Renders the CSS tags
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// TODO: Once the tags are rendered the collection on the context is cleared. Therefore if this method is called multiple times it will 
        /// render anything that has been registered as 'pending' but has not been rendered.
        /// </remarks>
        public async Task<HtmlString> CssHereAsync(PreProcessPipeline pipeline = null, bool debug = false)
        {
            var result = new StringBuilder();
            var urls = await GenerateCssUrlsAsync(pipeline, debug);
            foreach (var url in urls)
            {
                result.AppendFormat("<link href='{0}' rel='stylesheet' type='text/css'/>", url);
            }
            return new HtmlString(result.ToString());
        }

        /// <summary>
        /// Generates the list of URLs to render based on what is dynamically registered
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<string>> GenerateJsUrlsAsync(PreProcessPipeline pipeline = null, bool debug = false)
        {
            return await GenerateUrlsAsync(_dynamicallyRegisteredWebFiles.JavaScriptFiles, WebFileType.Js, pipeline, debug);
        }

        public async Task<IEnumerable<string>> GenerateJsUrlsAsync(string bundleName, bool debug = false)
        {
            return await GenerateUrlsAsync(bundleName, ".js", debug);
        }

        /// <summary>
        /// Generates the list of URLs to render based on what is dynamically registered
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<string>> GenerateCssUrlsAsync(PreProcessPipeline pipeline = null, bool debug = false)
        {
            return await GenerateUrlsAsync(_dynamicallyRegisteredWebFiles.CssFiles, WebFileType.Css, pipeline, debug);
        }

        public async Task<IEnumerable<string>> GenerateCssUrlsAsync(string bundleName, bool debug = false)
        {
            return await GenerateUrlsAsync(bundleName, ".css", debug);
        }

        /// <summary>
        /// Generates the URLs a given bundle
        /// </summary>
        /// <param name="bundleName"></param>
        /// <param name="fileExt"></param>
        /// <param name="debug"></param>
        /// <returns></returns>
        private async Task<IEnumerable<string>> GenerateUrlsAsync(string bundleName, string fileExt, bool debug)
        {
            var result = new List<string>();
            var bundleExists = _bundleManager.Exists(bundleName);
            if (!bundleExists)
            {
                throw new BundleNotFoundException(bundleName);
            }

            if (debug)
            {
                var urls = new List<string>();
                var files = _bundleManager.GetFiles(bundleName, _requestParts);
                foreach (var d in files)
                {
                    urls.Add(d.FilePath);
                }

                foreach (var url in urls)
                {
                    result.Add(url);
                }
                return result;
            }
            else
            {
                var compression = _http.HttpContext.Request.GetClientCompression();
                var url = _urlManager.GetUrl(bundleName, fileExt, _http.HttpContext.Request);

                //now we need to determine if these files have already been minified
                var compositeFilePath = _fileSystemHelper.GetCurrentCompositeFilePath(compression, bundleName);
                if (!File.Exists(compositeFilePath))
                {
                    var files = _bundleManager.GetFiles(bundleName, _requestParts);
                    //we need to do the minify on the original files
                    foreach (var file in files)
                    {
                        await _fileManager.ProcessAndCacheFileAsync(file);
                    }
                }
                result.Add(url);
                return result;
            }
        }

        /// <summary>
        /// Generates teh URLs for a given file set
        /// </summary>
        /// <param name="files"></param>
        /// <param name="fileType"></param>
        /// <param name="pipeline"></param>
        /// <param name="debug"></param>
        /// <returns></returns>
        private async Task<IEnumerable<string>> GenerateUrlsAsync(
            IEnumerable<IWebFile> files,
            WebFileType fileType,
            PreProcessPipeline pipeline = null,
            bool debug = false)
        {
            var result = new List<string>();

            var orderedSet = new OrderedFileSet(files,
                _fileSystemHelper, _requestParts,
                pipeline ?? _processorFactory.GetDefault(fileType), 
                _processorFactory.FileProcessingConventions);
            var orderedFiles = orderedSet.GetOrderedFileSet();

            if (debug)
            {
                return orderedFiles.Select(x => x.FilePath);
            }
            else
            {
                var compression = _http.HttpContext.Request.GetClientCompression();
                
                //Get the file collection used to create the composite URLs and the external requests
                var fileBatches = _fileBatcher.GetCompositeFileCollectionForUrlGeneration(orderedFiles);

                foreach (var batch in fileBatches)
                {
                    //if it's external, the rule is that a WebFileBatch can only contain a single external file
                    // it's path will be normalized as an external url so we just use it
                    if (batch.IsExternal)
                    {
                        result.Add(batch.Single().Original.FilePath);
                    }
                    else
                    {
                        //Get the URLs for the batch, this could be more than one resulting URL depending on how many
                        // files are in the batch and the max url length
                        var compositeUrls = _urlManager.GetUrls(batch.Select(x => x.Hashed), fileType == WebFileType.Css ? ".css" : ".js", _http.HttpContext.Request);

                        foreach (var u in compositeUrls)
                        {
                            //now we need to determine if these files have already been minified
                            var compositeFilePath = _fileSystemHelper.GetCurrentCompositeFilePath(compression, u.Key);
                            if (!File.Exists(compositeFilePath))
                            {
                                //need to process/minify these files - need to use their original paths of course
                                foreach (var file in batch.Select(x => x.Original))
                                {
                                    await _fileManager.ProcessAndCacheFileAsync(file);
                                }
                            }
                            result.Add(u.Url);
                        }
                    }
                }
            }

            return result;

        }

        public ISmidgeRequire RequiresJs(JavaScriptFile file)
        {
            _dynamicallyRegisteredWebFiles.Files.Add(file);
            return this;
        }

        public ISmidgeRequire RequiresJs(params string[] paths)
        {
            foreach (var path in paths)
            {
                RequiresJs(new JavaScriptFile(path));
            }
            return this;
        }

        public ISmidgeRequire RequiresCss(CssFile file)
        {
            _dynamicallyRegisteredWebFiles.Files.Add(file);
            return this;
        }

        public ISmidgeRequire RequiresCss(params string[] paths)
        {
            foreach (var path in paths)
            {
                RequiresCss(new CssFile(path));
            }
            return this;
        }

        /// <summary>
        /// Creates a new bundle and returns a bundle context to add files to it
        /// </summary>
        /// <param name="bundleName"></param>
        /// <returns></returns>
        /// <remarks>
        /// The bundle is write once - so if it already exists, a noop context is returned that does nothing
        /// </remarks>
        public ISmidgeRequire CreateJsBundle(string bundleName)
        {
            if (string.IsNullOrWhiteSpace(bundleName)) throw new ArgumentNullException(nameof(bundleName));

            if (_bundleManager.Exists(bundleName)) return new NoopBundleContext();

            return new SmidgeBundleContext(bundleName, _bundleManager, WebFileType.Js);
        }

        /// <summary>
        /// Creates a new bundle and returns a bundle context to add files to it
        /// </summary>
        /// <param name="bundleName"></param>
        /// <returns></returns>
        /// <remarks>
        /// The bundle is write once - so if it already exists, a noop context is returned that does nothing
        /// </remarks>
        public ISmidgeRequire CreateCssBundle(string bundleName)
        {
            if (string.IsNullOrWhiteSpace(bundleName)) throw new ArgumentNullException(nameof(bundleName));

            if (_bundleManager.Exists(bundleName)) return new NoopBundleContext();

            return new SmidgeBundleContext(bundleName, _bundleManager, WebFileType.Css);
        }
    }
}