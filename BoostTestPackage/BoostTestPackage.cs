// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.Threading;

namespace BoostTestPackage
{
    /// <summary>
    /// Implementation of AsyncPackage for Boost Tests.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideAutoLoad(UIContextGuid, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideUIContextRule(UIContextGuid, "Test Adapter for Boost.Test", "VCProject & TestExplorer",
        new string[] { "VCProject", "TestExplorer" },
        new string[] { VSConstants.UICONTEXT.VCProject_string, TestExplorerContextGuid })]
    [Guid(PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [SuppressMessage("Microsoft.Naming", "CA1724:TypeNamesShouldNotMatchNamespaces")]
    public sealed class BoostTestPackage : AsyncPackage, IDisposable
    {
        public const string PackageGuidString = "6e0d1c7a-ef6a-4442-b8ce-9a58b21c3239";
        private const string UIContextGuid = "3c4b983b-4670-40a8-9e2f-e7b457498c12";
        private const string TestExplorerContextGuid = "ec25b527-d893-4ec0-a814-d2c9f1782997";

        private BoostTestPackageServiceHost _serviceHost;

        #region AsyncPackage

        /// <summary>
        /// Initialize the package
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        protected override System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            _serviceHost = new BoostTestPackageServiceHost();
            try
            {
                _serviceHost.Open();
            }
            catch (CommunicationException)
            {
                _serviceHost.Abort();
                _serviceHost = null;
            }

            return base.InitializeAsync(cancellationToken, progress);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Dispose the package
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    // Cannot simply to ".?" because Code Analysis does not understand that.
                    if (_serviceHost != null)
                    {
                        _serviceHost.Close();
                    }
                }
                catch (CommunicationException)
                {
                    _serviceHost.Abort();
                }
            }
            base.Dispose(disposing);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
