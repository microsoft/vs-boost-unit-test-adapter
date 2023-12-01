// (C) Copyright ETAS 2015.
// Distributed under the Boost Software License, Version 1.0.
// (See accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)

// This file has been modified by Microsoft on 8/2017.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoostTestAdapter;
using BoostTestAdapter.Discoverers;
using BoostTestAdapter.Settings;
using BoostTestAdapter.Utility.VisualStudio;
using BoostTestAdapterNunit.Fakes;
using BoostTestAdapterNunit.Utility;
using NUnit.Framework;
using BoostTestAdapter.Boost.Runner;
using FakeItEasy;
using BoostTestAdapter.Utility.ExecutionContext;
using System;

namespace BoostTestAdapterNunit
{
    [TestFixture]
    internal class BoostTestDiscovererTest
    {
        /// <summary>
        /// The scope of this test is to check that if the Discoverer is given multiple project,
        /// method DiscoverTests splits appropriately the sources of type exe and of type dll in exe sources and dll sources
        /// and dispatches the discovery accordingly.
        /// </summary>
        [Test]
        public void Sink_ShouldContainTestForAllSupportedTypeOfSources()
        {
            var sources = new[]
            {
                "ListContentSupport" + BoostTestDiscoverer.ExeExtension,
                "DllProject1" + BoostTestDiscoverer.DllExtension,
                "DllProject2" + BoostTestDiscoverer.DllExtension,
            };

            var context = new DefaultTestContext();
            var logger = new ConsoleMessageLogger();
            var sink = new DefaultTestCaseDiscoverySink();

            var discoveryVerifier = A.Fake<IDiscoveryVerifier>();
            A.CallTo(() => discoveryVerifier.FileExists(A<string>._)).Returns(true);
            A.CallTo(() => discoveryVerifier.IsFileZoneMyComputer(A<string>._)).Returns(true);

            context.RegisterSettingProvider(BoostTestAdapterSettings.XmlRootName, new BoostTestAdapterSettingsProvider());
            context.LoadEmbeddedSettings("BoostTestAdapterNunit.Resources.Settings.externalTestRunner.runsettings");

            var boostTestDiscovererFactory = new StubBoostTestDiscovererFactory();

            var boostTestDiscoverer = new BoostTestDiscoverer(boostTestDiscovererFactory);
            boostTestDiscoverer.DiscoverTests(sources, context, logger, sink, discoveryVerifier);

            Assert.That(sink.Tests, Is.Not.Empty);

            // tests are found in the using the fake debughelper
            Assert.That(sink.Tests.Count(x => x.Source == "ListContentSupport" + BoostTestDiscoverer.ExeExtension), Is.EqualTo(8));
          
            // the external runner does NOT support the two dll projects
            Assert.That(sink.Tests.Any(x => x.Source == "DllProject1" + BoostTestDiscoverer.DllExtension), Is.False);
            Assert.That(sink.Tests.Any(x => x.Source == "DllProject2" + BoostTestDiscoverer.DllExtension), Is.False);
        }

        [Test]
        public void DiscoveryShouldSkipNonExistingSources()
        {
            var sources = new[]
            {
                "Exists1" + BoostTestDiscoverer.ExeExtension,
                "DoesNotExist" + BoostTestDiscoverer.DllExtension,
                "Exists2" + BoostTestDiscoverer.DllExtension,
            };

            var context = new DefaultTestContext();
            var logger = new ConsoleMessageLogger();
            var sink = new DefaultTestCaseDiscoverySink();

            var discoveryVerifier = A.Fake<IDiscoveryVerifier>();
            A.CallTo(() => discoveryVerifier.FileExists(A<string>._)).ReturnsLazily((string source) => source.StartsWith("Exists"));
            A.CallTo(() => discoveryVerifier.IsFileZoneMyComputer(A<string>._)).Returns(true);

            var boostTestDiscovererFactory = A.Fake<IBoostTestDiscovererFactory>();

            var boostTestDiscoverer = new BoostTestDiscoverer(boostTestDiscovererFactory);
            boostTestDiscoverer.DiscoverTests(sources, context, logger, sink, discoveryVerifier);

            A.CallTo(() => boostTestDiscovererFactory.GetDiscoverers(
                A<IReadOnlyCollection<string>>.That.Contains(sources[0]), A<BoostTestAdapterSettings>._)).MustHaveHappened();
            A.CallTo(() => boostTestDiscovererFactory.GetDiscoverers(
                A<IReadOnlyCollection<string>>.That.Contains(sources[1]), A<BoostTestAdapterSettings>._)).MustNotHaveHappened();
            A.CallTo(() => boostTestDiscovererFactory.GetDiscoverers(
                A<IReadOnlyCollection<string>>.That.Contains(sources[2]), A<BoostTestAdapterSettings>._)).MustHaveHappened();
        }

        [Test]
        public void DiscoveryShouldSkipUntrustedSources()
        {
            var sources = new[]
            {
                "Untrusted1" + BoostTestDiscoverer.ExeExtension,
                "Trusted" + BoostTestDiscoverer.DllExtension,
                "Untrusted2" + BoostTestDiscoverer.DllExtension,
            };

            var context = new DefaultTestContext();
            var logger = new ConsoleMessageLogger();
            var sink = new DefaultTestCaseDiscoverySink();

            var discoveryVerifier = A.Fake<IDiscoveryVerifier>();
            A.CallTo(() => discoveryVerifier.FileExists(A<string>._)).Returns(true);
            A.CallTo(() => discoveryVerifier.IsFileZoneMyComputer(A<string>._)).ReturnsLazily((string source) => source.StartsWith("Trusted"));

            var boostTestDiscovererFactory = A.Fake<IBoostTestDiscovererFactory>();

            var boostTestDiscoverer = new BoostTestDiscoverer(boostTestDiscovererFactory);
            boostTestDiscoverer.DiscoverTests(sources, context, logger, sink, discoveryVerifier);

            A.CallTo(() => boostTestDiscovererFactory.GetDiscoverers(
                A<IReadOnlyCollection<string>>.That.Contains(sources[0]), A<BoostTestAdapterSettings>._)).MustNotHaveHappened();
            A.CallTo(() => boostTestDiscovererFactory.GetDiscoverers(
                A<IReadOnlyCollection<string>>.That.Contains(sources[1]), A<BoostTestAdapterSettings>._)).MustHaveHappened();
            A.CallTo(() => boostTestDiscovererFactory.GetDiscoverers(
                A<IReadOnlyCollection<string>>.That.Contains(sources[2]), A<BoostTestAdapterSettings>._)).MustNotHaveHappened();
        }
    }

    internal class StubBoostTestDiscovererFactory : IBoostTestDiscovererFactory
    {
        private readonly DummySolution _dummySolution = new DummySolution("ParseSources1" + BoostTestDiscoverer.ExeExtension, "BoostUnitTestSample.cpp");
        
        public IEnumerable<FactoryResult> GetDiscoverers(IReadOnlyCollection<string> sources, BoostTestAdapterSettings settings)
        {
            var tmpSources = new List<string>(sources);
            var discoverers = new List<FactoryResult>();

            // sources that can be run on the external runner
            if (settings.ExternalTestRunner != null)
            {
                var extSources = tmpSources
                    .Where(s => settings.ExternalTestRunner.ExtensionType.IsMatch(Path.GetExtension(s)))
                    .ToList();

                discoverers.Add(new FactoryResult()
                {
                    Discoverer = new ExternalDiscoverer(settings.ExternalTestRunner, _dummySolution.PackageServiceFactory),
                    Sources = extSources
                });

                tmpSources.RemoveAll(s => extSources.Contains(s));
            }

            // sources that support list-content parameter
            var listContentSources = tmpSources
                .Where(s => (s == ("ListContentSupport" + BoostTestDiscoverer.ExeExtension)))
                .ToList();

            if (listContentSources.Count > 0)
            {
                IBoostTestRunnerFactory factory = A.Fake<IBoostTestRunnerFactory>();
                A.CallTo(() => factory.GetRunner(A<string>._, A<BoostTestRunnerFactoryOptions>._)).ReturnsLazily((string source, BoostTestRunnerFactoryOptions options) => new StubListContentRunner(source));

                discoverers.Add(new FactoryResult()
                {
                    Discoverer = new ListContentDiscoverer(factory, _dummySolution.PackageServiceFactory),
                    Sources = listContentSources
                });

                tmpSources.RemoveAll(s => listContentSources.Contains(s));
            }
  
            return discoverers;

        }
    }

    internal class StubListContentRunner : IBoostTestRunner
    {
        public StubListContentRunner(string source)
        {
            this.Source = source;
        }

        public IBoostTestRunnerCapabilities Capabilities { get; } = new BoostTestRunnerCapabilities { ListContent = true, Version = false };
        
        public string Source { get; private set; }

        public int Execute(BoostTestRunnerCommandLineArgs args, BoostTestRunnerSettings settings, IProcessExecutionContext context)
        {
            Copy("BoostTestAdapterNunit.Resources.ListContentDOT.sample.8.list.content.gv", args.StandardErrorFile);
            return 0;
        }

        private void Copy(string embeddedResource, string path)
        {
            using (Stream inStream = TestHelper.LoadEmbeddedResource(embeddedResource))
            using (FileStream outStream = File.Create(path))
            {
                inStream.CopyTo(outStream);
            }
        }
    }
}