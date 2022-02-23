// (C) Copyright ETAS 2015.
// Distributed under the Boost Software License, Version 1.0.
// (See accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)

// This file has been modified by Microsoft on 2/2022.

using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Extensions.MSBuild;
using Microsoft.VisualStudio.Workspace.Indexing;
using Microsoft.VisualStudio.Workspace.VSIntegration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DTEProject = EnvDTE.Project;

namespace VisualStudioAdapter
{
    /// <summary>
    /// Base class for DTE2-based Visual Studio instances.
    /// </summary>
    public class VisualStudio : IVisualStudio
    {
        /// <summary>
        /// Constant strings which distinguish Solution item kinds.
        /// </summary>
        private static class EnvDTEProjectKinds
        {
            /// <summary>
            /// Solution folder item kind label
            /// </summary>
            public const string VsProjectKindSolutionFolder = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";

            /// <summary>
            /// C++ project item kind label
            /// </summary>
            public const string VsProjectKindVCpp = "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}";
        }

        private DTE2 _dte;
        private ServiceProvider _serviceProvider;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dte">The DTE2 instance for a running Visual Studio instance</param>
        /// <param name="version">The version identifying the Visual Studio instance</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "dte"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "dte"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "dte")]
        public VisualStudio(DTE2 dte)
        {
            this._dte = dte;
            this._serviceProvider = new ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)_dte);
        }

        #region IVisualStudio

        public async Task<DebuggingProperties> GetDebuggingPropertiesAsync(string binary)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var vsSolution = (IVsSolution)this._serviceProvider.GetService(typeof(SVsSolution));
            if (vsSolution == null)
            {
                return null;
            }

            IEnumHierarchies enumHierarchies;
            Guid guid = Guid.Empty;
            var hr = vsSolution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION, ref guid, out enumHierarchies);
            if (hr != VSConstants.S_OK || enumHierarchies == null)
            {
                return null;
            }

            IVsHierarchy[] hierarchies = new IVsHierarchy[1];
            uint fetched;
            while (enumHierarchies.Next(1, hierarchies, out fetched) == VSConstants.S_OK && fetched > 0)
            {
                var hierarchy = hierarchies[0];
                if (hierarchy == null)
                {
                    continue;
                }

                var debuggingProperties = GetDebugPropsIfMatching(hierarchy, binary);
                if (debuggingProperties != null)
                {
                    return debuggingProperties;
                }
            }

            return null;
        }

        #endregion IVisualStudio

        private static DebuggingProperties GetDebugPropsIfMatching(IVsHierarchy hierarchy, string binary)
        {
            object extObject;
            hierarchy.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_ExtObject, out extObject);
            var dteProject = (DTEProject)extObject;

            if (dteProject.Kind != EnvDTEProjectKinds.VsProjectKindVCpp)
            {
                return null;
            }

            var project = new Project(dteProject);
            var configuration = project.ActiveConfiguration;
            if (!string.Equals(binary, configuration.PrimaryOutput, StringComparison.Ordinal))
            {
                return null;
            }

            return new DebuggingProperties
            {
                Environment = configuration.VSDebugConfiguration.Environment,
                WorkingDirectory = configuration.VSDebugConfiguration.WorkingDirectory
            };
        }
    }
}
