/*
 * Copyright 2012 © Victor Chekalin
 *
 * THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
 * KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
 * PARTICULAR PURPOSE.
 *
 */

using Autodesk.Revit.DB.ExtensibleStorage;
using RevitDataValidator;
using System;
using System.Reflection;
using VCExtensibleStorageExtension.Attributes;

namespace VCExtensibleStorageExtension
{
    /// <summary>
    /// Create an Autodesk Extensible storage schema from a type
    /// </summary>
    public class SchemaCreator : ISchemaCreator
    {
        private readonly AttributeExtractor<SchemaAttribute> _schemaAttributeExtractor =
            new AttributeExtractor<SchemaAttribute>();

        private readonly AttributeExtractor<FieldAttribute> _fieldAttributeExtractor =
            new AttributeExtractor<FieldAttribute>();

        private readonly IFieldFactory _fieldFactory = new FieldFactory();

        #region Implementation of ISchemaCreator

        public Schema CreateSchema(Type type)
        {
            try
            {
                SchemaAttribute schemaAttribute =
                    _schemaAttributeExtractor.GetAttribute(type);

                // Create a new Schema using SchemaAttribute Properties
                SchemaBuilder schemaBuilder = new SchemaBuilder(schemaAttribute.GUID);
                schemaBuilder.SetSchemaName(schemaAttribute.SchemaName);

                // Set up other schema properties if they exists
                if (!string.IsNullOrEmpty(schemaAttribute.ApplicationGUID))
                    schemaBuilder.SetApplicationGUID(Guid.Parse(schemaAttribute.ApplicationGUID));

                if (!string.IsNullOrEmpty(schemaAttribute.Documentation))
                    schemaBuilder.SetDocumentation(schemaAttribute.Documentation);

                if (schemaAttribute.ReadAccessLevel != default(AccessLevel))
                    schemaBuilder.SetReadAccessLevel(schemaAttribute.ReadAccessLevel);

                if (schemaAttribute.WriteAccessLevel != default(AccessLevel))
                    schemaBuilder.SetWriteAccessLevel(schemaAttribute.WriteAccessLevel);

                if (!string.IsNullOrEmpty(schemaAttribute.VendorId))
                    schemaBuilder.SetVendorId(schemaAttribute.VendorId);

                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                // Iterate all of the RevitEntity Properties
                foreach (var pi in properties)
                {
                    //get the field attribute of public properties
                    var propertyAttributes =
                        pi.GetCustomAttributes(typeof(FieldAttribute), true);

                    // if property does not have a FieldAttribute
                    // skip this property
                    if (propertyAttributes.Length == 0)
                        continue;

                    FieldAttribute fieldAttribute =
                        _fieldAttributeExtractor.GetAttribute(pi);

                    FieldBuilder fieldBuilder =
                        _fieldFactory.CreateField(schemaBuilder, pi);

                    /*
                    //If entity contains field of IRevitEntity
                    //also create a schema and add subSchemaId
                    var iRevitEntity = pi.PropertyType.GetInterface("IRevitEntity");
                    if (iRevitEntity != null)
                    {
                        fieldBuilder = schemaBuilder.AddSimpleField(pi.Name, typeof(Entity));
                        var subSchemaAttribute = _schemaAttributeExtractor.GetAttribute(pi.PropertyType);
                        fieldBuilder.SetSubSchemaGUID(subSchemaAttribute.GUID);
                    }
                    else
                    {
                        fieldBuilder = schemaBuilder.AddSimpleField(pi.Name, pi.PropertyType);
                    }
                    */

                    if (!string.IsNullOrEmpty(fieldAttribute.Documentation))
                        fieldBuilder.SetDocumentation(fieldAttribute.Documentation);
                    if (fieldBuilder.NeedsUnits())
                        fieldBuilder.SetSpec(new Autodesk.Revit.DB.ForgeTypeId(fieldAttribute.SpecTypeId));
                }
                Schema ret = schemaBuilder.Finish();
                return ret;
            }
            catch (Exception ex)
            {
                Utils.Log($"Could not create schema: {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        #endregion Implementation of ISchemaCreator
    }
}