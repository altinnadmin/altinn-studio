using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using AltinnCore.Common.Factories.ModelFactory.Manatee.Json;
using Manatee.Json;
using Manatee.Json.Schema;
using Manatee.Json.Serialization;

namespace AltinnCore.Common.Factories.ModelFactory
{
    /// <summary>
    /// For, like, converting Xsd to JsonSchema
    /// </summary>
    public class XsdToJsonSchema
    {
        private XmlReader xsdReader;
        private XmlSchema mainXsd;
        private JsonSchema mainJsonSchema;

        private IDictionary<XmlSchemaObject, XmlQualifiedName> itemNames = new Dictionary<XmlSchemaObject, XmlQualifiedName>();
        private IDictionary<XmlSchemaObject, XmlQualifiedName> typeNames = new Dictionary<XmlSchemaObject, XmlQualifiedName>();

        /// <summary>
        /// Initializes a new instance of the <see cref="XsdToJsonSchema"/> class.
        /// </summary>
        /// <param name="xsdReader">Reader for the XSD to convert</param>
        public XsdToJsonSchema(XmlReader xsdReader)
        {
            this.xsdReader = xsdReader;
        }

        /// <summary>
        /// Perform the actual conversion (to JsonValue)
        /// </summary>
        /// <returns>JsonValue for root of Json Schema representation of schema</returns>
        public JsonValue AsJsonValue()
        {
            return new JsonSerializer().Serialize<JsonSchema>(AsJsonSchema());
        }

        /// <summary>
        /// Perform the actual conversion (to JsonSchema)
        /// </summary>
        /// <returns>Json Schema representation of schema</returns>
        public JsonSchema AsJsonSchema()
        {
            // Read XSD
            mainXsd = XmlSchema.Read(xsdReader, ValidationCallback);

            // Set up Json Schema object
            mainJsonSchema = new JsonSchema();
            mainJsonSchema.Schema("http://json-schema.org/schema#");
            mainJsonSchema.Id(Guid.NewGuid().ToString());
            AddTypeObject(mainJsonSchema);

            XmlSchemaObjectEnumerator enumerator = mainXsd.Items.GetEnumerator();
            while (enumerator.MoveNext())
            {
                XmlSchemaObject item = enumerator.Current;

                if (item is XmlSchemaElement)
                {
                    bool isRequired;
                    JsonSchema itemSchema = ParseTopLevelElement((XmlSchemaElement)item, out isRequired);
                    if (itemSchema != null)
                    {
                        mainJsonSchema.Property(GetItemName(item).Name, itemSchema);
                    }
                }
            }

            enumerator = mainXsd.Items.GetEnumerator();
            while (enumerator.MoveNext())
            {
                XmlSchemaObject item = enumerator.Current;

                bool isRequired;
                object parsedObject = Parse(item, out isRequired);
                if (parsedObject == null)
                {
                }
                else if (parsedObject is JsonSchema)
                {
                    JsonSchema parsedSchema = (JsonSchema)parsedObject;
                    if (!(item is XmlSchemaElement) && parsedSchema != null)
                    {
                        AddDefinition(item, parsedSchema);
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            return mainJsonSchema;
        }

        private object Parse(XmlSchemaObject item, out bool isRequired)
        {
            isRequired = false;

            if (item is XmlSchemaElement)
            {
                return ParseElement((XmlSchemaElement)item, out isRequired);
            }
            else if (item is XmlSchemaComplexType)
            {
                return ParseComplexType((XmlSchemaComplexType)item);
            }
            else if (item is XmlSchemaSimpleType)
            {
                JsonSchema simpleTypeSchema = new JsonSchema();
                AppendSimpleType((XmlSchemaSimpleType)item, simpleTypeSchema);
                return simpleTypeSchema;
            }
            else if (item is XmlSchemaAnnotation)
            {
                AppendAnnotation((XmlSchemaAnnotation)item, mainJsonSchema);
                return null;
            }
            else if (item is XmlSchemaGroup || item is XmlSchemaAttributeGroup || item is XmlSchemaAttribute)
            {
                // Do nothing. xsd:group and top-level xsd:attribute is expanded in place
                return null;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private JsonSchema ParseTopLevelElement(XmlSchemaElement item, out bool isRequired)
        {
            return ParseElement(item, true, out isRequired);
        }

        private JsonSchema ParseElement(XmlSchemaElement item, out bool isRequired)
        {
            return ParseElement(item, false, out isRequired);
        }

        private JsonSchema ParseElement(XmlSchemaElement item, bool isFirstPass, out bool isRequired)
        {
            XmlQualifiedName elementName = GetItemName(item); // This will become either .Name of .RefName.Name

            isRequired = false;

            JsonSchema elementSchema = new JsonSchema();

            if (item.Annotation != null)
            {
                AppendAnnotated(item, elementSchema);
            }

            if (item.Constraints.Count > 0)
            {
                throw new NotImplementedException();
            }

            if (item.DefaultValue != null)
            {
                throw new NotImplementedException();
            }

            if (item.ElementSchemaType != null)
            {
                throw new NotImplementedException();
            }

            if (item.ElementType != null)
            {
                throw new NotImplementedException();
            }

            if (item.Final != XmlSchemaDerivationMethod.None)
            {
                throw new NotImplementedException();
            }

            if (item.FixedValue != null)
            {
                throw new NotImplementedException();
            }

            if (item.IsAbstract)
            {
                throw new NotImplementedException();
            }

            if (!item.IsNillable)
            {
                // ToDo
            }

            if (item.MinOccurs >= 1 ||
                (item.MinOccursString != null && Convert.ToUInt32(item.MinOccursString) >= 1))
            {
                isRequired = true;
            }

            if (item.MaxOccursString != null)
            {
                // This is handled when appending type info
            }

            if (!item.RefName.IsEmpty)
            {
                AppendType(FindObject(elementName), item, elementSchema); // Elementname is already set to refName here
            }

            if (item.SchemaType != null)
            {
                if (isFirstPass)
                {
                    AppendType(item, elementSchema);
                }
                else
                {
                    if (item.SchemaType is XmlSchemaComplexType)
                    {
                        XmlQualifiedName complexTypeName = GetItemName(item.SchemaType);
                        JsonSchema complexTypeSchema = ParseComplexType((XmlSchemaComplexType)item.SchemaType);
                        AddDefinition(item.SchemaType, complexTypeSchema);
                        AppendTypeFromNameInternal(complexTypeName, elementSchema);
                    }
                    else if (item.SchemaType is XmlSchemaSimpleType)
                    {
                        XmlQualifiedName simpleTypeName = GetItemName(item.SchemaType);
                        JsonSchema simpleTypeSchema = new JsonSchema();
                        AppendSimpleType((XmlSchemaSimpleType)item.SchemaType, simpleTypeSchema);
                        AddDefinition(item.SchemaType, simpleTypeSchema);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }

            if (!item.SchemaTypeName.IsEmpty)
            {
                AppendType(item, elementSchema);
            }

            return elementSchema;
        }

        private JsonSchema ParseComplexType(XmlSchemaComplexType item)
        {
            JsonSchema complexTypeSchema = new JsonSchema();
            List<XmlQualifiedName> requiredList = new List<XmlQualifiedName>();

            if (item.Annotation != null)
            {
                AppendAnnotated(item, complexTypeSchema);
            }

            if (item.Attributes.Count > 0)
            {
                foreach (XmlSchemaAttribute attribute in item.Attributes)
                {
                    bool isRequired;
                    JsonSchema attributeSchema = ParseAttribute(attribute, out isRequired);
                    if (attributeSchema != null)
                    {
                        XmlQualifiedName name = GetItemName(attribute);
                        complexTypeSchema.Property(name.Name, attributeSchema);
                        if (isRequired)
                        {
                            requiredList.Add(name);
                        }
                    }
                }
            }

            if (item.BaseSchemaType != null)
            {
                throw new NotImplementedException();
            }

            if (item.BaseXmlSchemaType != null)
            {
                throw new NotImplementedException();
            }

            if (item.ContentModel != null)
            {
                if (item.ContentModel is XmlSchemaComplexContent)
                {
                    AppendComplexContent((XmlSchemaComplexContent)item.ContentModel, complexTypeSchema, requiredList);
                }
                else if (item.ContentModel is XmlSchemaSimpleContent)
                {
                    AppendSimpleContent((XmlSchemaSimpleContent)item.ContentModel, complexTypeSchema, requiredList);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            if (item.Particle != null)
            {
                AppendParticle(item.Particle, complexTypeSchema, requiredList);
            }

            if (requiredList.Count > 0)
            {
                complexTypeSchema.Required(RequiredListToArray(requiredList));
            }

            return complexTypeSchema;
        }

        private void AppendSimpleType(XmlSchemaSimpleType item, JsonSchema appendToSchema)
        {
            if (item.Annotation != null)
            {
                AppendAnnotated(item, appendToSchema);
            }

            if (item.BaseSchemaType != null)
            {
                throw new NotImplementedException();
            }

            if (item.BaseXmlSchemaType != null)
            {
                throw new NotImplementedException();
            }

            if (item.Content != null)
            {
                XmlSchemaSimpleTypeContent simpleTypeContent = item.Content;
                if (simpleTypeContent is XmlSchemaSimpleTypeRestriction)
                {
                    XmlSchemaSimpleTypeRestriction simpleTypeRestriction = (XmlSchemaSimpleTypeRestriction)simpleTypeContent;

                    if (simpleTypeRestriction.Facets != null && simpleTypeRestriction.Facets.Count > 0)
                    {
                        List<JsonValue> enumList = new List<JsonValue>();

                        foreach (XmlSchemaFacet facet in simpleTypeRestriction.Facets)
                        {
                            if (facet is XmlSchemaEnumerationFacet)
                            {
                                enumList.Add(new JsonValue(facet.Value));
                            }
                            else if (facet is XmlSchemaMinInclusiveFacet)
                            {
                                try
                                {
                                    SetMinimum(appendToSchema, Convert.ToDouble(facet.Value));
                                }
                                catch (Exception)
                                {
                                }
                            }
                            else if (facet is XmlSchemaMaxInclusiveFacet)
                            {
                                try
                                {
                                    SetMaximum(appendToSchema, Convert.ToDouble(facet.Value));
                                }
                                catch (Exception)
                                {
                                }
                            }
                            else if (facet is XmlSchemaMinLengthFacet)
                            {
                                try
                                {
                                    appendToSchema.MinLength(Convert.ToUInt32(facet.Value));
                                }
                                catch (Exception)
                                {
                                }
                            }
                            else if (facet is XmlSchemaMaxLengthFacet)
                            {
                                try
                                {
                                    appendToSchema.MaxLength(Convert.ToUInt32(facet.Value));
                                }
                                catch (Exception)
                                {
                                }
                            }
                            else if (facet is XmlSchemaLengthFacet || facet is XmlSchemaTotalDigitsFacet)
                            {
                                try
                                {
                                    appendToSchema.MinLength(Convert.ToUInt32(facet.Value));
                                }
                                catch (Exception)
                                {
                                }

                                try
                                {
                                    appendToSchema.MaxLength(Convert.ToUInt32(facet.Value));
                                }
                                catch (Exception)
                                {
                                }
                            }
                            else if (facet is XmlSchemaPatternFacet)
                            {
                                appendToSchema.Pattern(facet.Value);
                            }
                            else if (facet is XmlSchemaFractionDigitsFacet)
                            {
                                // Use pattern?
                            }
                            else if (facet is XmlSchemaMinExclusiveFacet)
                            {
                                try
                                {
                                    appendToSchema.ExclusiveMinimum(Convert.ToDouble(facet.Value));
                                }
                                catch (Exception)
                                {
                                }
                            }
                            else if (facet is XmlSchemaMaxExclusiveFacet)
                            {
                                try
                                {
                                    appendToSchema.ExclusiveMaximum(Convert.ToDouble(facet.Value));
                                }
                                catch (Exception)
                                {
                                }
                            }
                            else
                            {
                                throw new NotImplementedException();
                            }
                        }

                        if (enumList.Count > 0)
                        {
                            appendToSchema.Enum(enumList.ToArray());
                        }
                    }

                    AppendTypeFromSchemaTypeInternal(simpleTypeRestriction.BaseType, simpleTypeRestriction.BaseTypeName, appendToSchema);
                }
                else if (simpleTypeContent is XmlSchemaSimpleTypeList)
                {
                    XmlSchemaSimpleTypeList simpleTypeListItem = (XmlSchemaSimpleTypeList)simpleTypeContent;

                    if (simpleTypeListItem.ItemType != null)
                    {
                        XmlQualifiedName simpleTypeName = GetItemName(simpleTypeListItem.ItemType);
                        JsonSchema simpleTypeSchema = new JsonSchema();
                        AppendSimpleType((XmlSchemaSimpleType)simpleTypeListItem.ItemType, simpleTypeSchema);
                        AddDefinition(simpleTypeListItem.ItemType, simpleTypeSchema);
                    }
                    else if (!simpleTypeListItem.ItemTypeName.IsEmpty)
                    {
                        appendToSchema.Type(JsonSchemaType.Array);
                        appendToSchema.OtherData.Add("@xsdType", new JsonValue("XmlList"));

                        JsonSchema[] itemsSchemas = new JsonSchema[1];
                        itemsSchemas[0] = new JsonSchema();

                        AppendTypeFromSchemaTypeInternal(simpleTypeListItem.ItemType, simpleTypeListItem.ItemTypeName, itemsSchemas[0]);
                        appendToSchema.Items(itemsSchemas);
                    }
                    else
                    {
                        throw new ArgumentException();
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            if (item.Datatype != null)
            {
                throw new NotImplementedException();
            }
        }

        private JsonSchema ParseAttribute(XmlSchemaAttribute attribute, out bool isRequired)
        {
            isRequired = false;
            JsonSchema attributeSchema = new JsonSchema();

            if (attribute != null && !attribute.RefName.IsEmpty)
            {
                XmlSchemaObject refAttribute = FindObject(attribute.RefName);
                if (refAttribute is XmlSchemaAttribute)
                {
                    attributeSchema = ParseAttribute((XmlSchemaAttribute)refAttribute, out isRequired);
                }
                else
                {
                    throw new ArgumentException();
                }
            }

            SetType(attributeSchema, new JsonValue("XmlAttribute"));

            if (attribute.Annotation != null)
            {
                AppendAnnotated(attribute, attributeSchema);
            }

            if (attribute.AttributeSchemaType != null)
            {
                throw new NotImplementedException();
            }

            if (attribute.AttributeType != null)
            {
                throw new NotImplementedException();
            }

            if (attribute.FixedValue != null)
            {
                SetConst(attributeSchema, new JsonValue(attribute.FixedValue));
            }

            if (!attribute.QualifiedName.IsEmpty)
            {
                throw new NotImplementedException();
            }

            if (attribute.SchemaType != null)
            {
                AppendSimpleType(attribute.SchemaType, attributeSchema);
            }

            if (!attribute.SchemaTypeName.IsEmpty)
            {
                AppendTypeFromNameInternal(attribute.SchemaTypeName, attributeSchema);
            }

            isRequired = attribute.Use == XmlSchemaUse.Required;

            return attributeSchema;
        }

        private JsonSchema ParseAny(XmlSchemaAny item, out bool isRequired)
        {
            isRequired = false;

            JsonSchema anySchema = new JsonSchema();
            anySchema.OtherData.Add("@xsdType", new JsonValue("XmlAny"));

            if (item.Annotation != null)
            {
                AppendAnnotated(item, anySchema);
            }

            if (item.MinOccurs >= 1 ||
                (item.MinOccursString != null && Convert.ToUInt32(item.MinOccursString) >= 1))
            {
                isRequired = true;
            }

            if (item.MaxOccursString != null)
            {
                // This is handled when appending type info
            }

            AppendType(item, anySchema);

            return anySchema;
        }

        private JsonSchema ParseParticle(XmlSchemaParticle item, out bool isRequired)
        {
            JsonSchema particleSchema = new JsonSchema();

            isRequired = false;

            if (item is XmlSchemaElement)
            {
                return ParseElement((XmlSchemaElement)item, out isRequired);
            }
            else if (item is XmlSchemaChoice)
            {
                throw new NotImplementedException();
                /*
                List<JsonSchema> oneOfSchemaList = new List<JsonSchema>();
                foreach (XmlSchemaObject choiceItem in ((XmlSchemaChoice)item).Items)
                {
                    JsonSchema oneOfSchema = new JsonSchema();
                    AddType(choiceItem, oneOfSchema);
                    oneOfSchemaList.Add(oneOfSchema);
                }

                resultSchema.OneOf(oneOfSchemaList.ToArray());
                */
            }
            else if (item is XmlSchemaGroupRef)
            {
                throw new NotImplementedException();

                // ExpandAndCopyGroupRef((XmlSchemaGroupRef)item, resultSchema, requiredList);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private void AppendParticle(XmlSchemaParticle particle, JsonSchema appendToSchema, List<XmlQualifiedName> requiredList)
        {
            if (particle == null)
            {
            }
            else if (particle is XmlSchemaSequence)
            {
                foreach (XmlSchemaParticle item in ((XmlSchemaSequence)particle).Items)
                {
                    AppendParticleProperty(item, appendToSchema, requiredList);
                }
            }
            else if (particle is XmlSchemaGroupRef)
            {
                ExpandAndAppendGroupRef((XmlSchemaGroupRef)particle, appendToSchema, requiredList);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private void AppendParticleProperty(XmlSchemaParticle item, JsonSchema appendToSchema, List<XmlQualifiedName> requiredList)
        {
            if (item is XmlSchemaElement)
            {
                XmlQualifiedName elementName = GetItemName(item);
                bool isRequired;
                JsonSchema elementSchema = ParseElement((XmlSchemaElement)item, out isRequired);
                appendToSchema.Property(elementName.Name, elementSchema);

                if (isRequired)
                {
                    requiredList.Add(elementName);
                }
            }
            else if (item is XmlSchemaChoice)
            {
                List<JsonSchema> oneOfSchemaList = new List<JsonSchema>();
                foreach (XmlSchemaObject choiceItem in ((XmlSchemaChoice)item).Items)
                {
                    JsonSchema oneOfSchema = new JsonSchema();
                    AppendType(choiceItem, oneOfSchema);
                    oneOfSchemaList.Add(oneOfSchema);
                }

                appendToSchema.OneOf(oneOfSchemaList.ToArray());
            }
            else if (item is XmlSchemaAny)
            {
                XmlQualifiedName anyName = GetItemName(item);
                bool isRequired;
                JsonSchema anySchema = ParseAny((XmlSchemaAny)item, out isRequired);
                appendToSchema.Property(anyName.Name, anySchema);

                if (isRequired)
                {
                    requiredList.Add(anyName);
                }
            }
            else if (item is XmlSchemaGroupRef)
            {
                ExpandAndAppendGroupRef((XmlSchemaGroupRef)item, appendToSchema, requiredList);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private void AppendAnnotated(XmlSchemaAnnotated annotatedItem, JsonSchema appendToSchema)
        {
            if (annotatedItem != null)
            {
                AppendAnnotation(annotatedItem.Annotation, appendToSchema);
            }
        }

        private void AppendAnnotation(XmlSchemaAnnotation annotationItem, JsonSchema appendToSchema)
        {
            if (annotationItem == null)
            {
                return;
            }

            string s = string.Empty;
            foreach (XmlSchemaDocumentation item in annotationItem.Items)
            {
                foreach (XmlNode markup in item.Markup)
                {
                    if (markup is XmlText)
                    {
                        appendToSchema.Description(((XmlText)markup).Value);
                    }
                    else
                    {
                        XmlQualifiedName markupName = new XmlQualifiedName(markup.LocalName, markup.NamespaceURI);
                        if ("http://www.w3.org/2001/XMLSchema:attribute".Equals(markupName.ToString()))
                        {
                            XmlAttribute name = GetAttribute(markup.Attributes, "name", markup.NamespaceURI);
                            XmlAttribute fixedValue = GetAttribute(markup.Attributes, "fixed", markup.NamespaceURI);

                            appendToSchema.Info(name.Value, fixedValue.Value);
                        }
                        else if ("http://www.brreg.no/or:tekst".Equals(markupName.ToString()))
                        {
                            XmlAttribute teksttype = GetAttribute(markup.Attributes, "teksttype", markup.NamespaceURI);
                            XmlAttribute lang = GetAttribute(markup.Attributes, "lang", markup.NamespaceURI);

                            appendToSchema.Texts(teksttype.Value, lang.Value, markup.InnerText);
                        }
                        else if ("http://www.brreg.no/or:info".Equals(markupName.ToString()))
                        {
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                }
            }
        }

        private string AppendTypeFromSchemaTypeInternal(XmlSchemaType schemaType, XmlQualifiedName schemaTypeName, JsonSchema appendToSchema)
        {
            if (!schemaTypeName.IsEmpty)
            {
                return AppendTypeFromNameInternal(schemaTypeName, appendToSchema);
            }
            else
            {
                return AppendTypeFromNameInternal(GetItemName(schemaType), appendToSchema);
            }
        }

        private string AppendTypeFromNameInternal(XmlQualifiedName qname, JsonSchema appendToSchema)
        {
            string type = (qname == null || qname.IsEmpty) ? null : qname.ToString();
            string name = (qname == null || qname.IsEmpty) ? null : qname.Name;
            if ((type == null || type.Length == 0) && (name == null || name.Length == 0))
            {
                // xs:anyType
                appendToSchema.Type(JsonSchemaType.String);
            }
            else if ("http://www.w3.org/2001/XMLSchema:string".Equals(type)
                     || "http://www.w3.org/2001/XMLSchema:normalizedString".Equals(type)
                     || "http://www.w3.org/2001/XMLSchema:token".Equals(type))
            {
                appendToSchema.Type(JsonSchemaType.String);
            }
            else if ("http://www.w3.org/2001/XMLSchema:boolean".Equals(type))
            {
                appendToSchema.Type(JsonSchemaType.Boolean);
            }
            else if ("http://www.w3.org/2001/XMLSchema:integer".Equals(type))
            {
                appendToSchema.Type(JsonSchemaType.Integer);
            }
            else if ("http://www.w3.org/2001/XMLSchema:positiveInteger".Equals(type)
                     || "http://www.w3.org/2001/XMLSchema:nonNegativeInteger".Equals(type))
            {
                appendToSchema.Type(JsonSchemaType.Integer);
                SetMinimum(appendToSchema, 0);
            }
            else if ("http://www.w3.org/2001/XMLSchema:short".Equals(type))
            {
                appendToSchema.Type(JsonSchemaType.Integer);
                SetMinimum(appendToSchema, short.MinValue);
                SetMaximum(appendToSchema, short.MaxValue);
            }
            else if ("http://www.w3.org/2001/XMLSchema:long".Equals(type))
            {
                appendToSchema.Type(JsonSchemaType.Integer);
                SetMinimum(appendToSchema, long.MinValue);
                SetMaximum(appendToSchema, long.MaxValue);
            }
            else if ("http://www.w3.org/2001/XMLSchema:double".Equals(type)
                     || "http://www.w3.org/2001/XMLSchema:decimal".Equals(type))
            {
                appendToSchema.Type(JsonSchemaType.Number);
            }
            else if ("http://www.w3.org/2001/XMLSchema:date".Equals(type))
            {
                appendToSchema.Type(JsonSchemaType.String);
                appendToSchema.Format(StringFormat.GetFormat("date"));
            }
            else if ("http://www.w3.org/2001/XMLSchema:time".Equals(type))
            {
                appendToSchema.Type(JsonSchemaType.String);
                appendToSchema.Format(StringFormat.GetFormat("time"));
            }
            else if ("http://www.w3.org/2001/XMLSchema:dateTime".Equals(type))
            {
                appendToSchema.Type(JsonSchemaType.String);
                appendToSchema.Format(StringFormat.DateTime);
            }
            else if ("http://www.w3.org/2001/XMLSchema:gYear".Equals(type))
            {
                appendToSchema.Type(JsonSchemaType.String);
                appendToSchema.Format(StringFormat.GetFormat("year"));
            }
            else if ("http://www.w3.org/2001/XMLSchema:gYearMonth".Equals(type))
            {
                appendToSchema.Type(JsonSchemaType.String);
                appendToSchema.Format(StringFormat.GetFormat("year-month"));
            }
            else if ("http://www.w3.org/2001/XMLSchema:base64Binary".Equals(type))
            {
                appendToSchema.Type(JsonSchemaType.String);
                appendToSchema.ContentEncoding("base64");
            }
            else if ("http://www.w3.org/2001/XMLSchema:anyURI".Equals(type))
            {
                appendToSchema.Type(JsonSchemaType.String);
            }
            else if (type.StartsWith("http://www.w3.org/2001/XMLSchema:"))
            {
                throw new NotImplementedException();
            }
            else
            {
                if (name == null)
                {
                    AddTypeObject(appendToSchema);
                }
                else
                {
                    appendToSchema.Ref("#/definitions/" + name);
                    return name;
                }
            }

            return null;
        }

        private void AppendType(XmlSchemaObject item, JsonSchema appendToSchema)
        {
            AppendType(item, null, appendToSchema);
        }

        private void AppendType(XmlSchemaObject item, XmlSchemaObject referencedFromItem, JsonSchema appendToSchema)
        {
            if (item is XmlSchemaElement)
            {
                XmlSchemaElement elementItem = (XmlSchemaElement)item;
                var maxOccurs = referencedFromItem is XmlSchemaParticle ? ((XmlSchemaParticle)referencedFromItem).MaxOccurs : elementItem.MaxOccurs;
                if (maxOccurs > 1)
                {
                    appendToSchema.Type(JsonSchemaType.Array);
                    var minOccurs = referencedFromItem is XmlSchemaParticle ? ((XmlSchemaParticle)referencedFromItem).MinOccurs : elementItem.MinOccurs;
                    var maxOccursString = referencedFromItem is XmlSchemaParticle ? ((XmlSchemaParticle)referencedFromItem).MaxOccursString : elementItem.MaxOccursString;
                    appendToSchema.MinItems(Convert.ToUInt32(minOccurs));
                    if (!"unbounded".Equals(maxOccursString))
                    {
                        appendToSchema.MaxItems(Convert.ToUInt32(maxOccurs));
                    }

                    JsonSchema[] itemsSchemas = new JsonSchema[1];
                    itemsSchemas[0] = new JsonSchema();

                    AppendTypeFromSchemaTypeInternal(elementItem.SchemaType, elementItem.SchemaTypeName, itemsSchemas[0]);
                    appendToSchema.Items(itemsSchemas);
                }
                else
                {
                    AppendTypeFromSchemaTypeInternal(elementItem.SchemaType, elementItem.SchemaTypeName, appendToSchema);
                }
            }
            else if (item is XmlSchemaType)
            {
                throw new NotImplementedException();

                // AddDefinition(resultSchema, ObjectName(item), GenerateSchemaType((XmlSchemaType)item));
                /*
                XmlSchemaType itemSchemaType = (XmlSchemaType)item;
                if (itemSchemaType.BaseSchemaType != null)
                {
                    throw new NotImplementedException();

                    // AddTypeFromSchemaTypeInternal(itemSchemaType.BaseSchemaType, qname, elementName, resultSchema);
                }
                else
                {
                    throw new NotImplementedException();
                }
                */
            }
            else if (item is XmlSchemaSimpleTypeRestriction)
            {
                XmlSchemaSimpleTypeRestriction simpleTypeRestriction = (XmlSchemaSimpleTypeRestriction)item;
                AppendTypeFromSchemaTypeInternal(simpleTypeRestriction.BaseType, simpleTypeRestriction.BaseTypeName, appendToSchema);
            }
            else if (item is XmlSchemaSequence)
            {
                List<XmlQualifiedName> requiredList = new List<XmlQualifiedName>();
                AppendParticle((XmlSchemaSequence)item, appendToSchema, requiredList);

                if (requiredList.Count > 0)
                {
                    appendToSchema.Required(RequiredListToArray(requiredList));
                }
            }
            else if (item is XmlSchemaAny)
            {
                XmlSchemaAny anyItem = (XmlSchemaAny)item;
                var maxOccurs = referencedFromItem is XmlSchemaParticle ? ((XmlSchemaParticle)referencedFromItem).MaxOccurs : anyItem.MaxOccurs;
                if (maxOccurs > 1)
                {
                    appendToSchema.Type(JsonSchemaType.Array);
                    var minOccurs = referencedFromItem is XmlSchemaParticle ? ((XmlSchemaParticle)referencedFromItem).MinOccurs : anyItem.MinOccurs;
                    var maxOccursString = referencedFromItem is XmlSchemaParticle ? ((XmlSchemaParticle)referencedFromItem).MaxOccursString : anyItem.MaxOccursString;
                    appendToSchema.MinItems(Convert.ToUInt32(minOccurs));
                    if (!"unbounded".Equals(maxOccursString))
                    {
                        appendToSchema.MaxItems(Convert.ToUInt32(maxOccurs));
                    }

                    JsonSchema[] itemsSchemas = new JsonSchema[1];
                    itemsSchemas[0] = new JsonSchema();

                    AppendTypeFromSchemaTypeInternal(null, XmlQualifiedName.Empty, itemsSchemas[0]);
                    appendToSchema.Items(itemsSchemas);
                }
                else
                {
                    AppendTypeFromSchemaTypeInternal(null, XmlQualifiedName.Empty, appendToSchema);
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private bool HasDefinition(XmlQualifiedName name)
        {
            return mainJsonSchema.Definitions() != null && mainJsonSchema.Definitions().ContainsKey(name.Name);
        }

        private void AddDefinition(XmlSchemaObject item, JsonSchema definition)
        {
            XmlQualifiedName name = GetItemName(item);
            if (!HasDefinition(name))
            {
                mainJsonSchema.Definition(name.Name, definition);
            }
            else
            {
                throw new XmlSchemaException();
            }
        }

        private void AppendDefinition(JsonSchema appendToSchema, XmlSchemaObject item, JsonSchema definitionSchema)
        {
            if (appendToSchema == mainJsonSchema)
            {
                AddDefinition(item, definitionSchema);
            }
            else
            {
                AddTypeObject(appendToSchema);
                appendToSchema.Property(GetItemName(item).Name, definitionSchema);
            }
        }

        private void ExpandAndAppendGroupRef(XmlSchemaGroupRef groupRefItem, JsonSchema appendToSchema, List<XmlQualifiedName> requiredList)
        {
            XmlSchemaObject groupObject = FindObject(groupRefItem.RefName);
            if (groupObject != null)
            {
                if (groupObject is XmlSchemaGroup)
                {
                    AppendParticle(((XmlSchemaGroup)groupObject).Particle, appendToSchema, requiredList);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        private void ExpandAndAppendAttributeGroupRef(XmlSchemaAttributeGroupRef groupRefItem, JsonSchema appendToSchema, List<XmlQualifiedName> requiredList)
        {
            XmlSchemaObject refItem = FindObject(groupRefItem.RefName);
            if (refItem != null)
            {
                if (refItem is XmlSchemaAttributeGroup)
                {
                    XmlSchemaAttributeGroup attributeGroup = (XmlSchemaAttributeGroup)refItem;

                    if (attributeGroup.Annotation != null)
                    {
                        AppendAnnotated(attributeGroup, appendToSchema);
                    }

                    if (attributeGroup.AnyAttribute != null)
                    {
                        throw new NotImplementedException();
                    }

                    if (attributeGroup.Attributes.Count > 0)
                    {
                        AppendAttributes(attributeGroup.Attributes, appendToSchema, requiredList);
                    }

                    if (attributeGroup.RedefinedAttributeGroup != null)
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        private void AppendComplexContent(XmlSchemaComplexContent item, JsonSchema appendToSchema, List<XmlQualifiedName> requiredList)
        {
            if (item.Annotation != null)
            {
                AppendAnnotated(item, appendToSchema);
            }

            if (item.Content != null)
            {
                AppendContent(item.Content, appendToSchema, requiredList);
            }
        }

        private void AppendSimpleContent(XmlSchemaSimpleContent item, JsonSchema appendToSchema, List<XmlQualifiedName> requiredList)
        {
            if (item.Annotation != null)
            {
                AppendAnnotated(item, appendToSchema);
            }

            if (item.Content != null)
            {
                AppendContent(item.Content, appendToSchema, requiredList);
            }
        }

        private void AppendAttributes(XmlSchemaObjectCollection attributes, JsonSchema appendToSchema, List<XmlQualifiedName> requiredList)
        {
            foreach (var attribute in attributes)
            {
                if (attribute is XmlSchemaAttribute)
                {
                    XmlSchemaAttribute schemaAttribute = (XmlSchemaAttribute)attribute;
                    bool isRequired;
                    JsonSchema attributeSchema = ParseAttribute(schemaAttribute, out isRequired);
                    if (attributeSchema != null)
                    {
                        XmlQualifiedName name = GetItemName(schemaAttribute);
                        appendToSchema.Property(name.Name, attributeSchema);
                        if (isRequired)
                        {
                            requiredList.Add(name);
                        }
                    }
                }
                else if (attribute is XmlSchemaAttributeGroupRef)
                {
                    ExpandAndAppendAttributeGroupRef((XmlSchemaAttributeGroupRef)attribute, appendToSchema, requiredList);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        private void AppendValueAttribute(XmlQualifiedName baseTypeName, JsonSchema appendToSchema, List<XmlQualifiedName> requiredList)
        {
            if (!baseTypeName.IsEmpty)
            {
                JsonSchema valueAttributeSchema = new JsonSchema();
                AppendTypeFromNameInternal(baseTypeName, valueAttributeSchema);
                appendToSchema.Property("value", valueAttributeSchema);
                requiredList.Add(new XmlQualifiedName("value", mainXsd.TargetNamespace));
            }
        }

        private void AppendContent(XmlSchemaContent item, JsonSchema appendToSchema, List<XmlQualifiedName> requiredList)
        {
            if (item is XmlSchemaSimpleContentExtension)
            {
                XmlSchemaSimpleContentExtension contentExtensionItem = (XmlSchemaSimpleContentExtension)item;
                if (contentExtensionItem.Annotation != null)
                {
                    AppendAnnotated(item, appendToSchema);
                }

                if (contentExtensionItem.Attributes.Count > 0)
                {
                    AppendAttributes(contentExtensionItem.Attributes, appendToSchema, requiredList);
                    AppendValueAttribute(contentExtensionItem.BaseTypeName, appendToSchema, requiredList);
                }
                else if (!contentExtensionItem.BaseTypeName.IsEmpty)
                {
                    AppendTypeFromNameInternal(contentExtensionItem.BaseTypeName, appendToSchema);
                }
            }
            else if (item is XmlSchemaComplexContentExtension)
            {
                XmlSchemaComplexContentExtension contentExtensionItem = (XmlSchemaComplexContentExtension)item;

                bool isInherit = !contentExtensionItem.BaseTypeName.IsEmpty;
                List<JsonSchema> allOfList = new List<JsonSchema>();
                JsonSchema definitionSchema = new JsonSchema();

                if (contentExtensionItem.Annotation != null)
                {
                    AppendAnnotated(item, appendToSchema);
                }

                if (contentExtensionItem.Attributes.Count > 0)
                {
                    foreach (XmlSchemaAttribute attribute in contentExtensionItem.Attributes)
                    {
                        bool isRequired;
                        JsonSchema attributeSchema = ParseAttribute(attribute, out isRequired);
                        if (attributeSchema != null)
                        {
                            XmlQualifiedName name = GetItemName(attribute);
                            appendToSchema.Property(name.Name, attributeSchema);
                            if (isRequired)
                            {
                                requiredList.Add(name);
                            }
                        }
                    }
                }

                if (!contentExtensionItem.BaseTypeName.IsEmpty)
                {
                    JsonSchema inheritFromSchema = new JsonSchema();
                    AppendTypeFromNameInternal(contentExtensionItem.BaseTypeName, inheritFromSchema);
                    allOfList.Add(inheritFromSchema);
                }

                if (contentExtensionItem.Particle != null)
                {
                    JsonSchema usingSchema = isInherit ? new JsonSchema() : definitionSchema;
                    List<XmlQualifiedName> particleRequiredList = new List<XmlQualifiedName>();

                    AppendParticle(contentExtensionItem.Particle, usingSchema, particleRequiredList);

                    if (particleRequiredList.Count > 0)
                    {
                        usingSchema.Required(RequiredListToArray(particleRequiredList));
                    }

                    if (isInherit)
                    {
                        allOfList.Add(usingSchema);
                    }
                }

                if (allOfList.Count > 0)
                {
                    AddTypeObject(appendToSchema);
                    appendToSchema.AllOf(allOfList.ToArray());
                }

                if (definitionSchema.Count > 0)
                {
                    AppendDefinition(appendToSchema, contentExtensionItem, definitionSchema);
                }
            }
            else if (item is XmlSchemaSimpleContentRestriction)
            {
                XmlSchemaSimpleContentRestriction contentRestrictionItem = (XmlSchemaSimpleContentRestriction)item;

                if (contentRestrictionItem.BaseType != null)
                {
                    throw new NotImplementedException();
                }

                if (!contentRestrictionItem.BaseTypeName.IsEmpty)
                {
                    XmlSchemaObject baseItem = FindObject(contentRestrictionItem.BaseTypeName);

                    // Todo
                }

                if (contentRestrictionItem.Annotation != null)
                {
                    AppendAnnotated(item, appendToSchema);
                }

                if (contentRestrictionItem.AnyAttribute != null)
                {
                    throw new NotImplementedException();
                }

                if (contentRestrictionItem.Attributes.Count > 0)
                {
                    AppendAttributes(contentRestrictionItem.Attributes, appendToSchema, requiredList);
                }

                if (contentRestrictionItem.Facets.Count > 0)
                {
                    throw new NotImplementedException();
                }
            }
            else if (item is XmlSchemaComplexContentRestriction)
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private void AddTypeObject(JsonSchema appendToSchema)
        {
            if (!appendToSchema.Exists(e => "type".Equals(e.Name)))
            {
                appendToSchema.Type(JsonSchemaType.Object);
            }
        }

        private void SetMinimum(JsonSchema appendToSchema, double value)
        {
            IJsonSchemaKeyword existingItem = appendToSchema.Find(e => "minimum".Equals(e.Name));
            if (existingItem is MinimumKeyword)
            {
                value = Math.Max(value, ((MinimumKeyword)existingItem).Value);
                appendToSchema.RemoveAll(e => "minimum".Equals(e.Name));
            }

            appendToSchema.Minimum(value);
        }

        private void SetMaximum(JsonSchema appendToSchema, double value)
        {
            IJsonSchemaKeyword existingItem = appendToSchema.Find(e => "maximum".Equals(e.Name));
            if (existingItem is MaximumKeyword)
            {
                value = Math.Min(value, ((MaximumKeyword)existingItem).Value);
                appendToSchema.RemoveAll(e => "maximum".Equals(e.Name));
            }

            appendToSchema.Maximum(value);
        }

        private void SetType(JsonSchema appendToSchema, JsonValue value)
        {
            appendToSchema.OtherData.Remove("@xsdType");
            appendToSchema.OtherData.Add("@xsdType", value);
        }

        private void SetConst(JsonSchema appendToSchema, JsonValue value)
        {
            appendToSchema.RemoveAll(e => "const".Equals(e.Name));
            appendToSchema.Const(value);
        }

        private XmlSchemaObject FindObject(XmlQualifiedName name)
        {
            XmlSchemaObjectEnumerator enumerator = mainXsd.Items.GetEnumerator();
            while (enumerator.MoveNext())
            {
                XmlSchemaObject xmlSchemaObject = enumerator.Current;

                XmlQualifiedName objectName = XmlQualifiedName.Empty;
                if (xmlSchemaObject is XmlSchemaGroup)
                {
                    XmlSchemaGroup groupItem = (XmlSchemaGroup)xmlSchemaObject;
                    objectName = QualifiedNameOrName(groupItem.QualifiedName, groupItem.Name, groupItem);
                }
                else if (xmlSchemaObject is XmlSchemaElement)
                {
                    XmlSchemaElement elementItem = (XmlSchemaElement)xmlSchemaObject;
                    objectName = QualifiedNameOrName(elementItem.QualifiedName, elementItem.Name, elementItem);
                }
                else if (xmlSchemaObject is XmlSchemaType)
                {
                    XmlSchemaType typeItem = (XmlSchemaType)xmlSchemaObject;
                    objectName = QualifiedNameOrName(typeItem.QualifiedName, typeItem.Name, typeItem);
                }
                else if (xmlSchemaObject is XmlSchemaAttribute)
                {
                    XmlSchemaAttribute attributeItem = (XmlSchemaAttribute)xmlSchemaObject;
                    objectName = QualifiedNameOrName(attributeItem.QualifiedName, attributeItem.Name, attributeItem);
                }
                else if (xmlSchemaObject is XmlSchemaAttributeGroup)
                {
                    XmlSchemaAttributeGroup groupItem = (XmlSchemaAttributeGroup)xmlSchemaObject;
                    objectName = QualifiedNameOrName(groupItem.QualifiedName, groupItem.Name, groupItem);
                }
                else if (xmlSchemaObject is XmlSchemaAnnotation)
                {
                    // No name
                }
                else
                {
                    throw new NotImplementedException();
                }

                if (name == objectName)
                {
                    return xmlSchemaObject;
                }
            }

            return null;
        }

        private string GetNamespaceForItem(XmlSchemaObject item)
        {
            if (item == null)
            {
                return null;
            }

            if (itemNames.ContainsKey(item))
            {
                return itemNames[item].Namespace;
            }

            XmlQualifiedName xmlQualifiedName = XmlQualifiedName.Empty;

            if (item is XmlSchemaElement)
            {
                xmlQualifiedName = ((XmlSchemaElement)item).QualifiedName;
            }
            else if (item is XmlSchemaAttribute)
            {
                xmlQualifiedName = ((XmlSchemaAttribute)item).QualifiedName;
            }
            else if (item is XmlSchemaType)
            {
                xmlQualifiedName = ((XmlSchemaType)item).QualifiedName;
            }
            else if (item is XmlSchemaGroup)
            {
                xmlQualifiedName = ((XmlSchemaGroup)item).QualifiedName;
            }

            if (!xmlQualifiedName.IsEmpty)
            {
                return xmlQualifiedName.Namespace;
            }
            else if (item.Parent != mainXsd)
            {
                return GetNamespaceForItem(item.Parent);
            }
            else
            {
                return mainXsd.TargetNamespace;
            }
        }

        private XmlQualifiedName QualifiedNameOrName(XmlQualifiedName qualifiedName, string name, XmlSchemaObject item)
        {
            if (!qualifiedName.IsEmpty)
            {
                return qualifiedName;
            }
            else if (name != null)
            {
                return new XmlQualifiedName(name, GetNamespaceForItem(item));
            }
            else
            {
                return XmlQualifiedName.Empty;
            }
        }

        private XmlQualifiedName GetItemName(XmlSchemaObject item)
        {
            if (item == null)
            {
                return XmlQualifiedName.Empty;
            }

            if (itemNames.ContainsKey(item))
            {
                return itemNames[item];
            }

            if (item is XmlSchemaElement)
            {
                XmlSchemaElement elementItem = (XmlSchemaElement)item;
                XmlQualifiedName name = QualifiedNameOrName(elementItem.QualifiedName, elementItem.Name, elementItem);
                if (name.IsEmpty && !elementItem.RefName.IsEmpty)
                {
                    XmlSchemaObject refObject = FindObject(elementItem.RefName);
                    name = GetItemName(refObject);
                }

                return RegisterItemName(item, name);
            }
            else if (item is XmlSchemaAttribute)
            {
                XmlSchemaAttribute attributeItem = (XmlSchemaAttribute)item;
                XmlQualifiedName name = QualifiedNameOrName(attributeItem.QualifiedName, attributeItem.Name, attributeItem);
                if (name.IsEmpty && !attributeItem.RefName.IsEmpty)
                {
                    XmlSchemaObject refObject = FindObject(attributeItem.RefName);
                    name = GetItemName(refObject);
                }

                return RegisterItemName(item, name);
            }
            else if (item is XmlSchemaType)
            {
                XmlSchemaType typeItem = (XmlSchemaType)item;
                XmlQualifiedName name = QualifiedNameOrName(typeItem.QualifiedName, typeItem.Name, typeItem);
                if (name.IsEmpty)
                {
                    name = GetItemName(item.Parent);

                    if (typeNames.Values.Contains(name))
                    {
                        name = GenerateAnonymousTypeName((XmlSchemaType)item);
                    }
                }

                return RegisterItemName(item, name);
            }
            else if (item is XmlSchemaGroup)
            {
                XmlSchemaGroup groupItem = (XmlSchemaGroup)item;
                XmlQualifiedName name = QualifiedNameOrName(groupItem.QualifiedName, groupItem.Name, groupItem);
                return RegisterItemName(item, name);
            }
            else if (item is XmlSchemaAttributeGroup)
            {
                XmlSchemaAttributeGroup attributeGroupItem = (XmlSchemaAttributeGroup)item;
                XmlQualifiedName name = QualifiedNameOrName(attributeGroupItem.QualifiedName, attributeGroupItem.Name, attributeGroupItem);
                return RegisterItemName(item, name);
            }
            else
            {
                return RegisterItemName(item, XmlQualifiedName.Empty);
            }
        }

        private XmlQualifiedName RegisterItemName(XmlSchemaObject item, XmlQualifiedName name)
        {
            if (name.IsEmpty)
            {
                name = GetItemName(item.Parent);
            }

            if (!name.IsEmpty)
            {
                itemNames[item] = name;

                if (item is XmlSchemaType)
                {
                    typeNames[item] = name;
                }
            }
            else
            {
                int d = 0;
            }

            return name;
        }

        private XmlQualifiedName GenerateAnonymousTypeName(XmlSchemaType item)
        {
            if (itemNames.ContainsKey(item))
            {
                return itemNames[item];
            }

            XmlQualifiedName tmpName = QualifiedNameOrName(item.QualifiedName, item.Name, item);
            if (tmpName.IsEmpty)
            {
                tmpName = GetItemName(item.Parent);

                if (tmpName.IsEmpty)
                {
                    throw new XmlSchemaException();
                }
            }

            XmlQualifiedName newName;

            // string typeNamePart = tmpName.Substring(0, 1).ToUpper() + tmpName.Substring(1); //Use this to force first letter uppercase
            string typeNamePart = tmpName.Name;

            int i = 0;
            while (true)
            {
                i++;

                string typeName = typeNamePart;
                if (i > 1)
                {
                    typeName += i.ToString();
                }

                // Is name unused?
                newName = new XmlQualifiedName(typeName, tmpName.Namespace);
                if (!itemNames.Values.Contains(newName) &&
                    !HasDefinition(newName))
                {
                    break;
                }
            }

            return RegisterItemName(item, newName);
        }

        private JsonSchemaType JsonSchemaTypeFromString(string type)
        {
            switch (type.ToLower())
            {
                case "array": return JsonSchemaType.Array;
                case "boolean": return JsonSchemaType.Boolean;
                case "integer": return JsonSchemaType.Integer;
                case "null": return JsonSchemaType.Null;
                case "number": return JsonSchemaType.Number;
                case "object": return JsonSchemaType.Object;
                case "string": return JsonSchemaType.String;
                default: return JsonSchemaType.NotDefined;
            }
        }

        private string[] RequiredListToArray(List<XmlQualifiedName> requiredList)
        {
            List<string> requiredArrayList = new List<string>();
            foreach (XmlQualifiedName name in requiredList)
            {
                requiredArrayList.Add(name.Name);
            }

            return requiredArrayList.ToArray();
        }

        private XmlAttribute GetAttribute(XmlAttributeCollection attributes, string name, string ns)
        {
            XmlAttribute bestMatch = null;
            foreach (XmlAttribute attribute in attributes)
            {
                if (attribute.LocalName.Equals(name))
                {
                    if (attribute.NamespaceURI.Equals(ns))
                    {
                        bestMatch = attribute;
                    }
                    else if (attribute.NamespaceURI == string.Empty && bestMatch == null)
                    {
                        bestMatch = attribute;
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }

            return bestMatch;
        }

        private bool IsTopLevel(XmlSchemaObject item)
        {
            return item.Parent == mainXsd;
        }

        private static void ValidationCallback(object sender, ValidationEventArgs args)
        {
            throw new NotImplementedException();
        }
    }
}
