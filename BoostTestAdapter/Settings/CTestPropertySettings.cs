// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.Xml.XPath;

namespace BoostTestAdapter.Settings
{
    public static class CTestPropertySettingsConstants
    {
        public const string SettingsName = "CTestProperties";
    }

    [XmlRoot(CTestPropertySettingsConstants.SettingsName)]
    public class CTestPropertySettingsContainer : TestRunSettings
    {
        public class EnvVar
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        public class TestProperties
        {
            public string Name { get; set; }
            public string Command { get; set; }
            public List<EnvVar> Environment { get; set; }
            public string WorkingDirectory { get; set; }
        }

        public CTestPropertySettingsContainer()
            : base(CTestPropertySettingsConstants.SettingsName)
        {
        }

        public List<TestProperties> Tests { get; set; }

        public override XmlElement ToXml()
        {
            var document = new XmlDocument();
            using (XmlWriter writer = document.CreateNavigator().AppendChild())
            {
                new XmlSerializer(typeof(RunSettingsContainer))
                    .Serialize(writer, this);
            }
            return document.DocumentElement;
        }
    }

    [Export(typeof(ISettingsProvider))]
    [SettingsName(CTestPropertySettingsConstants.SettingsName)]
    public class CTestPropertySettingsProvider : ISettingsProvider
    {
        public string Name => CTestPropertySettingsConstants.SettingsName;

        public CTestPropertySettingsContainer CTestProperySettings { get; set; }

        public void Load(XmlReader reader)
        {
            Utility.Code.Require(reader, "reader");

            var schemaSet = new XmlSchemaSet();
            var schemaStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CTestPropertySettings.xsd");
            schemaSet.Add(null, XmlReader.Create(schemaStream));

            var settings = new XmlReaderSettings
            {
                Schemas = schemaSet,
                ValidationType = ValidationType.Schema,
                ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
            };

            settings.ValidationEventHandler += (object o, ValidationEventArgs e) => throw e.Exception;

            using (var newReader = XmlReader.Create(reader, settings))
            {
                try
                {
                    if (newReader.Read() && newReader.Name.Equals(this.Name))
                    {
                        XmlSerializer deserializer = new XmlSerializer(typeof(CTestPropertySettingsContainer));
                        this.CTestProperySettings = deserializer.Deserialize(newReader) as CTestPropertySettingsContainer;
                    }
                }
                catch (InvalidOperationException e) when (e.InnerException is XmlSchemaValidationException)
                {
                    throw new BoostTestAdapterSettingsProvider.InvalidBoostTestAdapterSettingsException(
                        String.Format(Resources.InvalidPropertyFile, CTestPropertySettingsConstants.SettingsName, e.InnerException.Message),
                        e.InnerException);
                }
            }
        }
    }
}
