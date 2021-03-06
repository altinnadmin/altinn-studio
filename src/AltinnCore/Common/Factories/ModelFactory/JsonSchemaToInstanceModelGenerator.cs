using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Manatee.Json;
using Manatee.Json.Schema;

namespace AltinnCore.Common.Factories.ModelFactory
{
    /// <summary>
    ///     Utility class for converting JSON Schema to a Json Instance model
    /// </summary>
    public class JsonSchemaToInstanceModelGenerator
    {
        private const string XmlSchemaNamespace = "http://www.w3.org/2001/XMLSchema";
        private const int MagicNumberMaxOccurs = 99999;

        private Dictionary<string, JsonSchema> definitions = new Dictionary<string, JsonSchema>();
        private ISet<string> visitedTypes = new HashSet<string>();
        private JsonObject instanceModel = new JsonObject();
        private JsonObject elements = new JsonObject();
        private JsonSchema jsonSchema;

        /// <summary>
        ///  Initializes a new instance of the <see cref="JsonSchemaToInstanceModelGenerator"/> class.
        ///  Creates an initial JSON Instance Model. Assumes top object has properties and that there are multiple definitions.
        ///  <see cref="cref="getInstanceModel"> to get the model </see>"/>
        /// </summary>
        /// <param name="organizationName">The organisation name</param>
        /// <param name="serviceName">Service name</param>
        /// <param name="jsonSchema">The Json Schema to generate the instance model from</param>
        public JsonSchemaToInstanceModelGenerator(string organizationName, string serviceName, JsonSchema jsonSchema)
        {
            instanceModel.Add("Org", organizationName);
            instanceModel.Add("Service", serviceName);
            instanceModel.Add("Elements", elements);

            this.jsonSchema = jsonSchema;

            foreach (KeyValuePair<string, JsonSchema> def in GetterExtensions.Definitions(jsonSchema))
            {
                definitions.Add(def.Key, def.Value);
            }

            GenerateInitialReferences();
        }

        /// <summary>
        ///   Returns the current instance model of the Schema.
        /// </summary>
        /// <returns>A Json Object which represents the instance model of the schema</returns>
        ///
        public JsonObject GetInstanceModel()
        {
            return instanceModel;
        }

        private JsonObject GenerateInitialReferences()
        {                                
            string title = GetterExtensions.Title(jsonSchema);

            if (GetterExtensions.Properties(jsonSchema) == null)
            {
                throw new ApplicationException("Cannot read top level object. Did not find any properties");
            }

            // Handle all properties
            foreach (KeyValuePair<string, JsonSchema> def in GetterExtensions.Properties(jsonSchema))
            {
                TraverseModell(string.Empty, title, def.Key, def.Value, IsRequired(def.Key, jsonSchema));          
            }

            return instanceModel;
        }

        /// <summary>
        ///  Expands an element with a given path if there are no other subtrees below the path and the path has an expandable type.
        /// </summary>
        /// <param name="path">the path to expand</param>
        /// <returns>instance modell with new expanded elements</returns>
        public JsonObject ExpandPath(string path)
        {          
            JsonObject startPoint = instanceModel.TryGetObject("Elements").TryGetObject(path);
            if (startPoint == null)
            {
                throw new ApplicationException("Path does not exist in instance model");
            }

            string typeName = startPoint.TryGetString("TypeName");
            string type = startPoint.TryGetString("Type");

            if (typeName == null)
            {
                throw new ApplicationException("Path cannot be expanded");
            }

            JsonSchema jsonSchema = definitions.GetValueOrDefault(typeName);
            if (jsonSchema == null || !string.Equals("Group", type))
            {
                throw new ApplicationException("Path cannot be expanded since type is not a group: " + typeName);
            }

            // Only handle properties below path                
            try
            {
                foreach (KeyValuePair<string, JsonSchema> def in GetterExtensions.Properties(jsonSchema))
                {
                    TraverseModell(path, typeName, def.Key, def.Value, false);
                }
            }
            catch (Exception e)
            {
                throw new ApplicationException("Path already expanded or failure in expanding code", e);
            }

            return instanceModel;   
        }

        /// <summary>
        ///  Removes all elements (subtree) of an existing element  the instance model, but not the element itself.
        /// </summary>
        /// <param name="path">the path that should no longer have any children</param>
        /// <returns>the instance model without the subtree</returns>
        public JsonObject RemovePath(string path)
        {
            JsonObject startPoint = instanceModel.TryGetObject("Elements").TryGetObject(path);
            if (startPoint == null)
            {
                throw new ApplicationException("Path does not exist in instance model");
            }

            List<string> pathsToRemove = new List<string>();
            foreach (string pathName in elements.Keys)
            {
                if (pathName.StartsWith(path) && pathName.Length > path.Length)
                {
                    pathsToRemove.Add(pathName);
                }
            }

            if (pathsToRemove.Count == 0)
            {
                throw new ApplicationException("Nothing to remove, no subelements found");
            }

            foreach (string pathName in pathsToRemove)
            {
                elements.Remove(pathName);
            }

            return instanceModel;
        }

        private bool IsRequired(string propertyName, JsonSchema parentType)
        {
            List<string> requiredProperties = GetterExtensions.Required(parentType);

            if (requiredProperties != null && requiredProperties.Contains(propertyName))
            {
                return true;
            }

            return false;
        }

        private string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return SeresXsdParser.SanitizeName(name);
        }

        private void TraverseModell(string parentPath, string parentTypeName, string propertyName, JsonSchema propertyType, bool isRequired)
        {
            string sanitizedPropertyName = SanitizeName(propertyName);
            string path = string.IsNullOrEmpty(parentPath) ? string.Empty : parentPath + ".";
            path += sanitizedPropertyName;

            string minItems = "0";
            string maxItems = "1";

            TypeKeyword type = propertyType.Get<TypeKeyword>();

            if (type != null && type.Value == JsonSchemaType.Array)
            {
                List<JsonSchema> items = GetterExtensions.Items(propertyType);
                path += "[*]";
                FollowRef(path, items[0]); // TODO fix multiple item types. It now uses only the first

                double? minItemsValue = GetterExtensions.MinItems(propertyType);
                double? maxItemsValue = GetterExtensions.MaxItems(propertyType);

                if (minItemsValue.HasValue)
                {
                    minItems = minItemsValue.ToString();
                }

                maxItems = "*";
                if (maxItemsValue.HasValue && maxItemsValue.Value != MagicNumberMaxOccurs)
                {
                    maxItems = maxItemsValue.ToString();
                }
            }
            else
            {
                FollowRef(path, propertyType);
                if (isRequired)
                {
                    minItems = "1";
                }
            }
                                     
            JsonObject result = new JsonObject();

            string inputType = "Field";
            string xsdType = propertyType.OtherData.TryGetString("@xsdType");

            result.Add("ID", path);

            string parentElement = ExtractParent(path);
            result.Add("ParentElement", parentElement);

            string typeName = ExtractTypeNameFromSchema(propertyType);
            string xsdValueType = FollowValueType(propertyType);
            if (xsdValueType == null)
            {
                result.Add("TypeName", SanitizeName(typeName));
            }
            else
            {
                result.Add("TypeName", null);
                if (typeName != null)
                {
                    typeName = xsdValueType;
                }
            }

            result.Add("Name", sanitizedPropertyName);

            string fixedValue = null;
            JsonValue fixedValueJson = GetterExtensions.Const(propertyType);
            if (fixedValueJson != null)
            {
                fixedValue = fixedValueJson.String;
            }

            if (xsdType != null && xsdType.Equals("XmlAttribute"))
            {
                inputType = "Attribute";
            }
            else if ((type == null && xsdValueType == null) || (type != null && (type.Value == JsonSchemaType.Object || type.Value == JsonSchemaType.Array)))
            {
                inputType = "Group";
            }

            if (inputType.Equals("Group") || !string.IsNullOrEmpty(fixedValue))
            {
                result.Add("DataBindingName", null);
            }
            else 
            {
                result.Add("DataBindingName", path);
            }

            result.Add("XPath", "/" + path.Replace(".", "/"));

            result.Add("Restrictions", ExtractRestrictions(xsdValueType, propertyType));
            result.Add("Choices", null); // ??
            
            result.Add("Type", inputType);
            
            result.Add("XsdValueType", xsdValueType);

            result.Add("Texts", new JsonObject()); // TODO
            result.Add("CustomProperties", new JsonObject()); // ??

            result.Add("MaxOccurs", maxItems.Equals("*") ? MagicNumberMaxOccurs : int.Parse(maxItems));
            result.Add("MinOccurs", int.Parse(minItems));

            result.Add("XName", propertyName);
                        
            if (fixedValue != null)
            {
                result.Add("FixedValue", fixedValue);
            }
                        
            string jsonSchemaPointer = "#/properties/" + propertyName;
            if (parentElement != null) 
            {
                 jsonSchemaPointer = "#/definitions/" + parentTypeName + "/properties/" + propertyName;
            }

            result.Add("JsonSchemaPointer", jsonSchemaPointer);

            string cardinality = "[" + minItems + ".." + maxItems + "]";
            string displayString = RemoveLastStar(path) + " : " + cardinality + " " + SanitizeName(typeName);
            result.Add("DisplayString", displayString);

            // TODO, add texts, ..., XmlSchemaReference
            elements.Add(path, result);
        }

        private string ExtractTypeNameFromSchema(JsonSchema jSchema)
        {
            string reference = GetterExtensions.Ref(jSchema);
            if (reference != null)
            {
                return ExtractTypeNameFromDefinitionReference(reference);
            }

            TypeKeyword type = jSchema.Get<TypeKeyword>();

            if (type != null)
            {
                if (type.Value == JsonSchemaType.Array)
                {
                    List<JsonSchema> items = GetterExtensions.Items(jSchema);

                    return ExtractTypeNameFromSchema(items[0]);
                }

                if (type.Value == JsonSchemaType.Object)
                {
                    return GetterExtensions.Title(jSchema);
                }

                return type.Value.ToString();
            }

            return null;
        }

        private string ExtractParent(string path)
        {
            if (path == null)
            {
                return null;
            }

            int lastPointIndex = path.LastIndexOf('.');

            if (lastPointIndex > 0)
            {
                return path.Substring(0, lastPointIndex);
            }

            return null;
        }

        private JsonObject ExtractRestrictions(string typeName, JsonSchema jSchema)
        {
            var restriction = new JsonObject();

            if (typeName == null)
            {
                return restriction;
            }

            string reference = GetterExtensions.Ref(jSchema);            
            if (reference != null)
            {
                JsonSchema nextSchema = definitions.GetValueOrDefault(ExtractTypeNameFromDefinitionReference(reference));

                jSchema = nextSchema;
            }

            switch (typeName)
            {
                case "string":
                    {                        
                        AddRestrictionValue(restriction, "minLength", GetterExtensions.MinLength(jSchema));
                        AddRestrictionValue(restriction, "maxLength", GetterExtensions.MaxLength(jSchema));

                        Regex pattern = GetterExtensions.Pattern(jSchema);
                        if (pattern != null)
                        {
                            var pat = new JsonObject();
                            pat.Add("Value", pattern.ToString());

                            restriction.Add("pattern", pat);
                        }

                        break;
                    }

                case "decimal":
                case "positiveInteger":
                case "number":
                    {
                        AddRestrictionValue(restriction, "minimum", GetterExtensions.Minimum(jSchema));
                        AddRestrictionValue(restriction, "maximum", GetterExtensions.Maximum(jSchema));
                        AddRestrictionValue(restriction, "exclusiveMinimum", GetterExtensions.ExclusiveMinimum(jSchema));
                        AddRestrictionValue(restriction, "exclusiveMaximum", GetterExtensions.ExclusiveMaximum(jSchema));

                        break;
                    }
            }

            return restriction;
        }

        private static void AddRestrictionValue(JsonObject restriction, string name, double? value)
        {
            if (value.HasValue)
            {
                JsonObject len = new JsonObject();
                len.Add("Value", value.ToString());

                restriction.Add(name, len);
            }
        }

        private string RemoveLastStar(string path)
        {
            if (path.EndsWith("[*]"))
            {
                return path.Substring(0, path.Length - 3);
            }

            return path;
        }

        private void FollowRef(string path, JsonSchema jSchema)
        {            
            string reference = GetterExtensions.Ref(jSchema);
            if (reference != null)
            {
                string typeName = ExtractTypeNameFromDefinitionReference(reference);
                JsonSchema schema = definitions.GetValueOrDefault(typeName);
                if (schema != null && GetterExtensions.Properties(schema) != null)
                {
                    if (!visitedTypes.Contains(typeName))
                    {
                        visitedTypes.Add(typeName);
                        foreach (KeyValuePair<string, JsonSchema> def in GetterExtensions.Properties(schema))
                        {
                            TraverseModell(path, typeName, def.Key, def.Value, IsRequired(def.Key, jSchema));
                        }
                    }                    
                }
            }

            // TODO oneOf, allOf, ...
        }

        private string FollowValueType(JsonSchema jSchema)
        {
            TypeKeyword topType = jSchema.Get<TypeKeyword>();
            if (topType != null)
            {
                return HandleJsonTypes(jSchema);
            }

            string reference = GetterExtensions.Ref(jSchema);
            if (reference != null)
            {
                JsonSchema nextSchema = definitions.GetValueOrDefault(ExtractTypeNameFromDefinitionReference(reference));
                if (nextSchema != null)
                {
                    TypeKeyword type = nextSchema.Get<TypeKeyword>();

                    if (type != null)
                    {
                       return HandleJsonTypes(nextSchema);
                    }
                    else
                    {
                        return FollowValueType(nextSchema);
                    }
                }
            }

            return null;
        }

        private string HandleJsonTypes(JsonSchema jSchema)
        {
            TypeKeyword type = jSchema.Get<TypeKeyword>();

            if (type != null)
            {
                switch (type.Value)
                {
                    case JsonSchemaType.String:
                        {
                            FormatKeyword format = jSchema.Get<FormatKeyword>();
                            if (format != null && format.Value != null && !string.IsNullOrEmpty(format.Value.Key))
                            {
                                return HandleFormatTypes(format.Value.Key);
                            }

                            return "string";
                        }
                        
                    case JsonSchemaType.Boolean:
                        return "boolean";
                    case JsonSchemaType.Number:
                        return "decimal";
                    case JsonSchemaType.Integer:
                        {
                            double? minimum = GetterExtensions.Minimum(jSchema);
                            if (minimum == 0.0)
                            {
                                return "positiveInteger";
                            }
                            else
                            {
                                return "integer";
                            }
                        }                        
                }
            }

            return null;
        }

        private string HandleFormatTypes(string format)
        {
            switch (format)
            {
                case "date":
                    return "date";

                case "date-time":
                    return "dateTime";

                case "duration":
                    return "duration";

                case "day":
                    return "gDay";

                case "month":
                    return "gMonth";

                case "month-day":
                    return "gMonthDay";

                case "year":
                    return "gYear";

                case "year-month":
                    return "gYearMonth";

                case "time":
                    return "time";

                case "email":
                    return "string";

                case "uri":
                    return "anyUri";
            }

            return "unknown";
        }

        private string ExtractTypeNameFromDefinitionReference(string reference)
        {
            if (reference != null)
            {
                return reference.Replace("#/definitions/", string.Empty);
            }

            return "Unknown";
        }
    }    
}
