// (C) Copyright ETAS 2015.
// Distributed under the Boost Software License, Version 1.0.
// (See accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)

// This file has been modified by Microsoft on 5/2018.

using BoostTestAdapter.Boost.Runner;
using BoostTestAdapter.Settings;
using BoostTestShared;
using System;
using System.IO;

namespace BoostTestAdapter.Utility
{
    public static class BoostTestRunnerCommandLineArgsEx
    {
        /// <summary>
        /// Allows specification of an environment via a line separated string
        /// </summary>
        /// <param name="args">The arguments to populate</param>
        /// <param name="environment">The line separated environment string</param>
        public static void SetEnvironment(this BoostTestRunnerCommandLineArgs args, string environment)
        {
            Code.Require(args, "args");

            if (!string.IsNullOrEmpty(environment))
            {
                foreach (string entry in environment.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] keyValuePair = entry.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if ((keyValuePair != null) && (keyValuePair.Length == 2))
                    {
                        args.Environment[keyValuePair[0]] = keyValuePair[1];
                    }
                }
            }
        }

        /// <summary>
        /// Sets the working environment (i.e. WorkingDirectory and Environment) properties of the command line arguments
        /// based on the provided details
        /// </summary>
        /// <param name="args">The arguments which to set</param>
        /// <param name="source">The base source which will be executed</param>
        /// <param name="settings">The BoostTestAdapterSettings which are currently applied</param>
        /// <param name="packageService">The current BoostTestPackageService (if available)</param>
        public static void SetWorkingEnvironment(this BoostTestRunnerCommandLineArgs args, string source, BoostTestAdapterSettings settings, IBoostTestPackageServiceWrapper packageService)
        {
            Code.Require(args, "args");
            Code.Require(source, "source");
            Code.Require(settings, "settings");

            // Default working directory
            args.WorkingDirectory = Path.GetDirectoryName(source);

            if (settings.TestProperties != null)
            {
                var normalizedSource = source.Replace('/', '\\');

                foreach (var test in settings.TestProperties)
                {
                    var normalizedCommand = test.Command.Replace('/', '\\');
                    if (normalizedCommand.Equals(normalizedSource, StringComparison.OrdinalIgnoreCase))
                    {
                        var environment = string.Empty;
                        foreach (var v in test.Environment)
                        {
                            environment += $"{v.Name}={v.Value}\n";
                        }

                        args.SetEnvironment(environment);
                        args.WorkingDirectory = test.WorkingDirectory;
                        break;
                    }
                }
            }

            // Working directory extracted from test settings
            if (!string.IsNullOrEmpty(settings.WorkingDirectory) && Directory.Exists(settings.WorkingDirectory))
            {
                args.WorkingDirectory = settings.WorkingDirectory;
            }
            
            // Visual Studio configuration (if available) has higher priority over settings
            var debuggingProperties = packageService?.Service.GetDebuggingPropertiesAsync(source).Result;
            if (debuggingProperties != null)
            {
                args.WorkingDirectory = debuggingProperties.WorkingDirectory ?? args.WorkingDirectory;
                args.SetEnvironment(debuggingProperties.Environment);
            }
            else
            {
                Logger.Warn(Resources.DebuggingPropertiesNotFound, source);
            }

            // Enforce Windows style backward slashes
            args.WorkingDirectory = args.WorkingDirectory.Replace('/', '\\');
        }
    }
}
