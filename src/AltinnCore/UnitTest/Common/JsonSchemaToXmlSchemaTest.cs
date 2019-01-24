using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using AltinnCore.Common.Factories.ModelFactory;
using AltinnCore.Common.Services.Interfaces;
using AltinnCore.ServiceLibrary.ServiceMetadata;
using Manatee.Json;
using Manatee.Json.Schema;
using Manatee.Json.Serialization;
using Moq;
using NUnit.Framework;
using Xunit;

namespace AltinnCore.UnitTest.Common
{
    /// <summary>
    ///  jo know for test
    /// </summary>
    public class JsonSchemaToXmlSchemaTest
    {
        /// <summary>
        /// Converting edag Json Schema to XSD
        /// </summary>
        [Fact]
        public void ConvertJsonSchemaToXsd()
        {
            var schemaText = File.ReadAllText("Common/edag.schema.json");
            var schemaJson = JsonValue.Parse(schemaText);
            var schema = new JsonSerializer().Deserialize<JsonSchema>(schemaJson);

            JsonSchemaToXsd converter = new JsonSchemaToXsd();

            XmlSchema xmlSchema = converter.CreateXsd(schema);

            SaveXmlSchema(xmlSchema, "edag.generated.xsd");
        }

        /// <summary>
        ///   convert from json schema which is autogenerated with xsdToJsonSchema parser
        /// </summary>
        [Fact]
        public void ConvertFromAutogeneratedJsonSchemaToXsd()
        {
            var schemaText = File.ReadAllText("Common/melding.schema.json");
            var schemaJson = JsonValue.Parse(schemaText);
            var schema = new JsonSerializer().Deserialize<JsonSchema>(schemaJson);

            JsonSchemaToXsd converter = new JsonSchemaToXsd();
            XmlSchema xmlSchema = converter.CreateXsd(schema);

            SaveXmlSchema(xmlSchema, "melding.generated.xsd");
        }

        private static void SaveXmlSchema(XmlSchema xmlSchema, string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);
            XmlTextWriter xwriter = new XmlTextWriter(file, new UTF8Encoding())
            {
                Formatting = Formatting.Indented,
            };
            xmlSchema.Write(xwriter);
        }

        private static bool XmlDiff(XmlSchema schema1, XmlSchema schema2)
        {
            return false;
        }

        private static void SaveJsonSchema(JsonSchema jsonSchema, string filename)
        {
            var serializer = new JsonSerializer();
            var json = serializer.Serialize(jsonSchema);
            File.WriteAllText(filename, json.ToString());
        }

        private static bool JsonDiff(JsonSchema schema1, JsonSchema schema2)
        {
            var jsonSchema1 = new JsonSerializer().Serialize(schema1);
            var jsonSchema2 = new JsonSerializer().Serialize(schema1);

            return jsonSchema1 == jsonSchema2;            
        }

        // [Fact]
        private void FromXsdToJsdAndBack()
        {
            int failCount = 0;
            int equalityCount = 0;

            string[] files = Directory.GetFiles("d:/tmp/altinn-schemas/", "*.xsd", SearchOption.AllDirectories);

            /*string[] files = { "d:/tmp/altinn-schemas/schema_1162_140411_forms_1482_10868.xsd" };*/

            foreach (string filePath in files)
            {
                Debug.WriteLine("Testing file " + filePath);

                JsonSchema jsonSchema1 = null, jsonSchema2 = null;
                XmlSchema xmlSchema2 = null, xmlSchema3 = null;
                string fileName = filePath.Substring(filePath.LastIndexOf("/") + 1);

                try
                {                
                    XsdToJsonSchema converter = new XsdToJsonSchema(new XmlTextReader(filePath), TestLogger.Create<XsdToJsonSchema>());
                    jsonSchema1 = converter.AsJsonSchema();
                    SaveJsonSchema(jsonSchema1, "jsd1_" + fileName);
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Failed XsdToJsd conversion: " + filePath + " Reason: " + e.Message);
                    failCount++;
                }

                xmlSchema2 = ConvertFromJsdToXsd(jsonSchema1, "xsd2_" + fileName);
                jsonSchema2 = ConvertFromXsdToJsd(xmlSchema2, "jsd2_" + fileName);
                xmlSchema3 = ConvertFromJsdToXsd(jsonSchema2, "xsd3_" + fileName);
                     
                try
                {
                    // Assert.True(XmlDiff(xmlSchema2, xmlSchema3), "xsd1 != xsd2");
                    Assert.True(JsonDiff(jsonSchema1, jsonSchema2), "jsd1 != jsd2");
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Failed equality test " + e.Message);
                    equalityCount++;
                }

                Assert.Equal(0, equalityCount);
            }           
        }

        private static JsonSchema ConvertFromXsdToJsd(XmlSchema xmlSchema2, string fileName)
        {
            try
            {
                var xsdConverter2 = new XsdToJsonSchema(xmlSchema2, TestLogger.Create<XsdToJsonSchema>());
                var jsonSchema2 = xsdConverter2.AsJsonSchema();
                SaveJsonSchema(jsonSchema2, fileName);

                return jsonSchema2;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed XsdToJsd conversion: " + fileName + " Reason: " + e.Message);
            }

            return null;
        }

        private static XmlSchema ConvertFromJsdToXsd(JsonSchema jsonSchema1, string fileName)
        {
            try
            {
                XmlSchema xmlSchema2;
                JsonSchemaToXsd jsonConverter1 = new JsonSchemaToXsd();
                xmlSchema2 = jsonConverter1.CreateXsd(jsonSchema1);
                SaveXmlSchema(xmlSchema2, fileName);
                return xmlSchema2;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed JsdToXsd conversion: " + fileName + " Reason: " + e.Message);
            }

            return null;
        }
    }       
 }
