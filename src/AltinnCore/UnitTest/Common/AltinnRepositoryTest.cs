using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AltinnCore.Common.Factories.ModelFactory;
using AltinnCore.Common.Services.Interfaces;
using AltinnCore.ServiceLibrary.ServiceMetadata;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace AltinnCore.UnitTest.Common
{
    /// <summary>
    ///  xxxx
    /// </summary>
    public class AltinnRepositoryTest
    {
        /// <summary>
        ///  jkjlkjkljlkj
        /// </summary>
        /// [Fact]
        public async void DownloadServicesAsync()
        {
            AltinnServiceRepository repositoryClient = new AltinnServiceRepository();

            List<AltinnResource> resources = await AltinnServiceRepository.GetResourcesAsync();

            Assert.Equal(1129, resources.Count);
        }

        /// <summary>
        ///  read and save
        /// </summary>
        /// [Fact]
        public async void ReadAllAsync()
        {
            List<string> schemaUrls = await AltinnServiceRepository.ReadAllSchemaUrls();

            var json = JsonConvert.SerializeObject(schemaUrls);

            File.WriteAllText("altinn-xsds.json", json);

            foreach (string schemaUrl in schemaUrls)
            {
                var webClient = new WebClient();
                string fileName = schemaUrl.Replace("https://www.altinn.no/api/metadata/formtask", "schema");
                fileName = fileName.Replace("/", "_");
                fileName = fileName.Replace("_xsd", ".xsd");
                
                webClient.DownloadFile(schemaUrl, fileName);
            }
        }

        private ILogger _logger = new LoggerFactory().CreateLogger("error");

        /// <summary>
        /// Test converting all provided XSDs with SeresXSdParser
        /// </summary>
        [Fact]
        public void ReadAllXsdsWithSeresXSDParser()
        {
            int failCount = 0;

            string[] files = Directory.GetFiles("D:/tmp/altinn-schemas", "*.xsd", SearchOption.AllDirectories);
            
            foreach (string file in files)
            {
                var seresParser = GetParser();
                Debug.WriteLine("Converting file " + file + " to Json Instance Model");

                try
                {                                                                               
                    XDocument mainXsd = GetDocument(file);
                    ServiceMetadata serviceMetadata = seresParser.ParseXsdToServiceMetadata("123", "service", mainXsd, null);
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Failed converting file " + file + ": " + e.Message);
                    failCount++;
                }
            }

            Debug.WriteLine("Total schemas in error: " + failCount);
            Assert.Equal(0, failCount);
        }

        [Fact]
        private void ReadOneXSD()
        {
            string file = "D:/tmp/altinn-schemas/schema_1121_2_forms_1362_10746.xsd";
            XDocument xsd = GetDocument(file);
            SeresXsdParser seresParser = GetParser();

            var serviceMetadata = seresParser.ParseXsdToServiceMetadata("123", "service", xsd, null);
            string metadataAsJson = Newtonsoft.Json.JsonConvert.SerializeObject(serviceMetadata);

            File.WriteAllText("test.json", metadataAsJson);
        }

        private XDocument GetDocument(string file)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;

            var doc = XmlReader.Create(file, settings);
            return XDocument.Load(doc);
        }

        private SeresXsdParser GetParser()
        {
            Mock<IRepository> moqRepository = new Mock<IRepository>();

            return new SeresXsdParser(moqRepository.Object);
        }                
    }
}
