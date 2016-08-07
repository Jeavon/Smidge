﻿using System;
using System.Collections.Generic;
using Smidge.Models;
using System.Linq;
using Smidge.Options;

namespace Smidge.FileProcessors
{
    /// <summary>
    /// Defines the default pre-processor pipelines used
    /// </summary>
    public class PreProcessPipelineFactory
    {
        private readonly IEnumerable<IPreProcessor> _allProcessors;

        public PreProcessPipelineFactory(IEnumerable<IPreProcessor> allProcessors)
        {     
            _allProcessors = allProcessors;
        }        

        /// <summary>
        /// Returns a pipeline with the specified types in order
        /// </summary>
        /// <param name="preProcessorTypes"></param>
        /// <returns></returns>
        public PreProcessPipeline GetPipeline(params Type[] preProcessorTypes)
        {
            var processors = new List<IPreProcessor>();
            foreach (var type in preProcessorTypes)
            {
                processors.Add(_allProcessors.Single(x => x.GetType() == type));
            }
            return new PreProcessPipeline(processors);
        }

        public virtual PreProcessPipeline GetDefault(WebFileType fileType)
        {
            switch (fileType)
            {
                case WebFileType.Js:
                    return new PreProcessPipeline(new IPreProcessor[]
                    {
                        _allProcessors.OfType<UglifyNodeMinifier>().Single()
                        //_allProcessors.OfType<JsMinifier>().Single()
                    });
                case WebFileType.Css:
                default:
                    return new PreProcessPipeline(new IPreProcessor[]
                    {
                        _allProcessors.OfType<CssImportProcessor>().Single(),
                        _allProcessors.OfType<CssUrlProcessor>().Single(),
                        _allProcessors.OfType<CssMinifier>().Single()
                    });
            }
        }
    }
}