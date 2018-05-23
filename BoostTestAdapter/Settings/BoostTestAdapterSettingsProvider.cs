// (C) Copyright ETAS 2015.
// Distributed under the Boost Software License, Version 1.0.
// (See accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)

// This file has been modified by Microsoft on 5/2018.

using System;
using System.ComponentModel.Composition;
using System.Configuration;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace BoostTestAdapter.Settings
{
    /// <summary>
    /// A Visual Studio ISettingsProvider implementation. Provisions BoostTestAdapterSettings.
    /// </summary>
    [Export(typeof(ISettingsProvider))]
    [SettingsName(BoostTestAdapterSettings.XmlRootName)]
    public class BoostTestAdapterSettingsProvider : ISettingsProvider
    {
        #region Constructors

        public BoostTestAdapterSettingsProvider()
        {
            this.Settings = new BoostTestAdapterSettings();
        }

        #endregion Constructors

        #region Properties

        /// <summary>
        /// Reference to the recently loaded settings. May be null if no settings were specified or the settings failed to load.
        /// </summary>
        public BoostTestAdapterSettings Settings { get; private set; }

        #endregion Properties

        #region ISettingsProvider

        public void Load(XmlReader reader)
        {
            Utility.Code.Require(reader, "reader");

            var schemaSet = new XmlSchemaSet();
            var schemaStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BoostTestAdapterSettings.xsd");
            schemaSet.Add(null, XmlReader.Create(schemaStream));

            var settings = new XmlReaderSettings
            {
                Schemas = schemaSet,
                ValidationType = ValidationType.Schema,
                ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
            };

            settings.ValidationEventHandler += (object o, ValidationEventArgs e) => throw e.Exception;

            // NOTE This method gets called if the settings name matches the node name as expected.
            using (var newReader = XmlReader.Create(reader, settings))
            {
                try
                {
                    if (newReader.Read() && newReader.Name.Equals(BoostTestAdapterSettings.XmlRootName))
                    {
                        XmlSerializer deserializer = new XmlSerializer(typeof(BoostTestAdapterSettings));
                        this.Settings = deserializer.Deserialize(newReader) as BoostTestAdapterSettings;
                    }
                }
                catch (InvalidOperationException e) when (e.InnerException is XmlSchemaValidationException)
                {
                    throw new InvalidBoostTestAdapterSettingsException(String.Format(Resources.InvalidPropertyFile, BoostTestAdapterSettings.XmlRootName, e.InnerException.Message), e.InnerException);
                }
            }
        }

        #endregion ISettingsProvider

        /// <summary>
        /// Builds a BoostTestAdapterSettings structure based on the information located within the IDiscoveryContext instance.
        /// </summary>
        /// <param name="context">The discovery context instance</param>
        /// <returns>A BoostTestRunnerSettings instance based on the information identified via the provided IDiscoveryContext instance.</returns>
        public static BoostTestAdapterSettings GetSettings(IDiscoveryContext context)
        {
            Utility.Code.Require(context, "context");

            BoostTestAdapterSettings settings = new BoostTestAdapterSettings();

            BoostTestAdapterSettingsProvider provider = (context.RunSettings == null) ? null : context.RunSettings.GetSettings(BoostTestAdapterSettings.XmlRootName) as BoostTestAdapterSettingsProvider;

            if (provider != null)
            {
                settings = provider.Settings;
            }

            RunSettingsProvider runSettings = (context.RunSettings == null) ? null : context.RunSettings.GetSettings(BoostTestSettingsConstants.InternalSettingsName) as RunSettingsProvider;

            if (runSettings != null)
            {
                settings.ParentVSProcessId = runSettings.VSProcessId;
            }

            TestPropertySettingsProvider testPropertySettingsProvider = (context.RunSettings == null) ? null : context.RunSettings.GetSettings(TestPropertySettingsConstants.SettingsName) as TestPropertySettingsProvider;

            if (testPropertySettingsProvider != null)
            {
                settings.TestProperties = testPropertySettingsProvider.TestPropertySettings.Tests;
            }

            // Return defaults
            return settings;
        }

        [Serializable]
        public class InvalidBoostTestAdapterSettingsException : Exception
        {
            public InvalidBoostTestAdapterSettingsException() { }
            public InvalidBoostTestAdapterSettingsException(string message) : base(message) { }
            public InvalidBoostTestAdapterSettingsException(string message, Exception inner) : base(message, inner) { }
            protected InvalidBoostTestAdapterSettingsException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
        }
    }
}