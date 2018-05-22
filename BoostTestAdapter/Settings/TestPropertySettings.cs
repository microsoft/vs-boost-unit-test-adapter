// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace BoostTestAdapter.Settings
{
    public static class TestPropertySettingsConstants
    {
        public const string SettingsName = "TestPropertySettingsForBoostAdapter";
    }

    [XmlRoot(TestPropertySettingsConstants.SettingsName)]
    public class TestPropertySettingsContainer : TestRunSettings
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

        public TestPropertySettingsContainer()
            : base(TestPropertySettingsConstants.SettingsName)
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
    [SettingsName(TestPropertySettingsConstants.SettingsName)]
    public class TestPropertySettingsProvider : ISettingsProvider
    {
        public string Name => TestPropertySettingsConstants.SettingsName;

        public TestPropertySettingsContainer TestPropertySettings { get; set; }

        public void Load(XmlReader reader)
        {
            Utility.Code.Require(reader, "reader");

            var schemaSet = new XmlSchemaSet();
            var schemaStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TestPropertySettings.xsd");
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
                        XmlSerializer deserializer = new XmlSerializer(typeof(TestPropertySettingsContainer));
                        this.TestPropertySettings = deserializer.Deserialize(newReader) as TestPropertySettingsContainer;
                    }
                }
                catch (InvalidOperationException e) when (e.InnerException is XmlSchemaValidationException)
                {
                    throw new BoostTestAdapterSettingsProvider.InvalidBoostTestAdapterSettingsException(
                        String.Format(Resources.InvalidPropertyFile, TestPropertySettingsConstants.SettingsName, e.InnerException.Message),
                        e.InnerException);
                }
            }
        }
    }
}
