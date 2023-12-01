// (C) Copyright ETAS 2015.
// Distributed under the Boost Software License, Version 1.0.
// (See accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)

// This file has been modified by Microsoft on 8/2017.

using BoostTestAdapter.Discoverers;
using BoostTestAdapter.Settings;
using BoostTestAdapter.Utility;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System;
using System.Collections.Generic;

namespace BoostTestAdapter
{
    [FileExtension(DllExtension)]
    [FileExtension(ExeExtension)]
    [DefaultExecutorUri(BoostTestExecutor.ExecutorUriString)]
    public class BoostTestDiscoverer : ITestDiscoverer
    {
        #region Constants

        public const string DllExtension = ".dll";
        public const string ExeExtension = ".exe";

        #endregion Constants

        #region Constructors

        /// <summary>
        /// Default constructor. The default IBoostTestDiscovererFactory implementation is provided.
        /// </summary>
        public BoostTestDiscoverer()
            :this(new BoostTestDiscovererFactory())
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="boostTestDiscovererFactory">A custom IBoostTestDiscovererFactory implementation.</param>
        public BoostTestDiscoverer(IBoostTestDiscovererFactory boostTestDiscovererFactory)
        {
            _boostTestDiscovererFactory = boostTestDiscovererFactory;
        }

        #endregion
        
        #region Members

        private readonly IBoostTestDiscovererFactory _boostTestDiscovererFactory;

        #endregion
        
        #region ITestDiscoverer

        /// <summary>
        /// Method called by Visual Studio (discovered via reflection) for test enumeration
        /// </summary>
        /// <param name="sources">path, target name and target extensions to discover</param>
        /// <param name="discoveryContext">discovery context settings</param>
        /// <param name="logger"></param>
        /// <param name="discoverySink">Unit test framework Sink</param>
        /// <remarks>Entry point of the discovery procedure</remarks>
        public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger,
            ITestCaseDiscoverySink discoverySink)
        {
            DiscoverTests(sources, discoveryContext, logger, discoverySink, new DefaultDiscoveryVerifier());
        }

        #endregion ITestDiscoverer

        /// <summary>
        /// DiscoverTests hook exposed for tests.
        /// </summary>
        public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger,
            ITestCaseDiscoverySink discoverySink, IDiscoveryVerifier discoveryVerifier)
        {
#if DEBUG && LAUNCH_DEBUGGER
            System.Diagnostics.Debugger.Launch();
#endif

            if (sources == null)
                return;

            Logger.Initialize(logger);

            DiscoverTests(sources, discoveryContext, discoverySink, discoveryVerifier);

            Logger.Shutdown();
        }

        /// <summary>
        /// Method called by BoostTestExecutor for test enumeration
        /// </summary>
        /// <param name="sources">path, target name and target extensions to discover</param>
        /// <param name="discoveryContext">discovery context settings</param>
        /// <param name="discoverySink">Unit test framework Sink</param>
        /// <remarks>This method assumes that the Logger singleton is maintained by the caller.</remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext,
            ITestCaseDiscoverySink discoverySink, IDiscoveryVerifier discoveryVerifier)
        {
            if (sources == null)
                return;

            BoostTestAdapterSettings settings = BoostTestAdapterSettingsProvider.GetSettings(discoveryContext);

            try
            {
                // Filter out any non-interesting, non-existant, or untrusted sources.
                List<string> filteredSources = new List<string>();
                bool sourceFilterAvailable = !TestSourceFilter.IsNullOrEmpty(settings.Filters);
                foreach (var source in sources)
                {
                    if (sourceFilterAvailable && !settings.Filters.ShouldInclude(source))
                    {
                        // Skip silently.
                    }
                    else if (!discoveryVerifier.FileExists(source))
                    {
                        Logger.Warn(Resources.FileNotFound, source);
                    }
                    else if (!discoveryVerifier.IsFileZoneMyComputer(source))
                    {
                        Logger.Error(Resources.BadFileZone, source);
                    }
                    else
                    {
                        filteredSources.Add(source);
                    }
                }

                var results = _boostTestDiscovererFactory.GetDiscoverers(filteredSources, settings);
                if (results == null)
                    return;

                // Test discovery
                foreach (var discoverer in results)
                {
                    if (discoverer.Sources.Count > 0)
                    {
                        Logger.Info(Resources.Discovering, discoverer.Discoverer.GetType().Name, string.Join(", ", discoverer.Sources));
                        discoverer.Discoverer.DiscoverTests(discoverer.Sources, discoveryContext, discoverySink);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, Resources.DiscoveryException, ex.Message, ex.HResult);
            }
        }
    }
}
