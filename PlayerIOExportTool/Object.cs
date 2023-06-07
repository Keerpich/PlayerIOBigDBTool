using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using PlayerIOClient;
using DreamTeam.Tson;

namespace PlayerIOExportTool
{
    public abstract class Object
    {
        #region FromDictionary
        public void Deserialize(Dictionary<string, object> values)
        {
            SetValues(values);
        }
        
        private void SetValues(Dictionary<string, object> dataPairs)
        {
            foreach (var data in dataPairs)
            {
                var fieldPath = (data.Key.Split(".")).ToList();
                fieldPath.ForEach(s => s.Trim());

                try
                {
                    SetValue(this, fieldPath, data.Value);
                }
                catch (FieldNotFoundException exception)
                {
                    string exceptionMessage = $"Field \"{exception.FieldName}\" is invalid or missing in path \"{data.Key}\"";
                    throw new Exception(exceptionMessage, exception);
                }
                catch (InvalidArrayIndexException exception)
                {
                    throw new Exception(
                        $"Array index \"{exception.Index}\" is invalid in path \"{data.Key}\". Please use them in incremental order.");
                }
                catch (InvalidDataTypeException exception)
                {
                    throw new Exception(
                        $"Value \"{data.Value}\" for {exception.FieldName} in path \"{data.Key}\" does not match the expected type {exception.ExpectedType}");
                }
            }
        }

        private void SetArrayValue(object obj,  List<string> fieldPath, object value)
        {
            int index = Int32.Parse(fieldPath[0]);
            
            IList listObj = obj as IList;

            var elementType = obj.GetType().GetTypeInfo().GenericTypeArguments[0];

            if (fieldPath.Count == 1)
            {
                try
                {
                    if (Nullable.GetUnderlyingType(elementType) != null)
                    {
                        elementType = Nullable.GetUnderlyingType(elementType);
                    }
                    
                    object castedValue = Convert.ChangeType(value, elementType);
                    
                    if (index == listObj.Count)
                    {
                        listObj.Add(castedValue);

                    }
                    else
                    {
                        listObj[index] = castedValue;
                    }
                    
                }
                catch (InvalidCastException ex)
                {
                    throw new InvalidDataTypeException(fieldPath[0], elementType, ex);
                }
            }
            else
            {
                var newPath = fieldPath.GetRange(1, fieldPath.Count - 1);

                try
                {
                    if (index == listObj.Count)
                    {
                        listObj.Add(Activator.CreateInstance(elementType));
                    }
                    
                    SetValue(listObj[index], newPath, value);
                }
                catch (ArgumentOutOfRangeException e)
                {
                    throw new InvalidArrayIndexException(index, e);
                }
            }
        }

        private void SetSingleValue(object obj, List<string> fieldPath, object value)
        {
            var property = obj.GetType().GetProperty(fieldPath[0]);
            
            if (fieldPath.Count == 1)
            {
                try
                {
                    Type type = property.PropertyType;
                    if (Nullable.GetUnderlyingType(type) != null)
                    {
                        type = Nullable.GetUnderlyingType(type);
                    }

                    property.SetValue(obj, Convert.ChangeType(value, type));
                }
                catch (NullReferenceException ex)
                {
                    throw new FieldNotFoundException(fieldPath[0], ex);
                }
                catch (FormatException ex)
                {
                    throw new InvalidDataTypeException(fieldPath[0], property.PropertyType, ex);
                }
                catch (InvalidCastException ex)
                {
                    throw new InvalidDataTypeException(fieldPath[0], property.PropertyType, ex);
                }
            }
            else
            {
                try
                {
                    if (property.GetValue(obj) == null)
                    {
                        property.SetValue(obj, Activator.CreateInstance(property.PropertyType));
                    }
                }
                catch (NullReferenceException ex)
                {
                    throw new FieldNotFoundException(fieldPath[0], ex);
                }
                catch (FormatException ex)
                {
                    throw new InvalidDataTypeException(fieldPath[0], property.PropertyType, ex);
                }
                catch (InvalidCastException ex)
                {
                    throw new InvalidDataTypeException(fieldPath[0], property.PropertyType, ex);
                }

                var newObj = property.GetValue(obj);
                var newPath = fieldPath.GetRange(1, fieldPath.Count - 1);
                SetValue(newObj, newPath, value);
            }
        }
        
        private void SetValue(object obj, List<string> fieldPath, object value)
        {
            if (obj is IList && obj.GetType().IsGenericType)
            {
                SetArrayValue(obj, fieldPath, value);
            }
            else
            {
                SetSingleValue(obj, fieldPath, value);
            }
        }
        #endregion
        
        #region ToDatabaseObject
        public DatabaseObject ToDatabaseObject()
        {
            string dboString = TsonConvert.SerializeObject(this);
            return DatabaseObject.LoadFromString(dboString);
        }
        #endregion

        #region ToDictionary
        public Dictionary<string, object> GetValues(List<string> headers)
        {
            var result = new Dictionary<string, object>();
            
            foreach (var header in headers)
            {
                var fieldPath = (header.Split(".")).ToList();
                fieldPath.ForEach(s => s.Trim());
                var value = GetValue(this, fieldPath);
                
                result.Add(header, value);
            }

            return result;
        }

        public object GetValue(string path)
        {
            var fieldPath = (path.Split(".")).ToList();
            fieldPath.ForEach(s => s.Trim());
            return GetValue(this, fieldPath);
        }
        
        public object GetValue(List<string> path)
        {
            path.ForEach(s => s.Trim());
            return GetValue(this, path);
        }
        
        private object GetValue(object obj, List<string> fieldPath)
        {
            if (obj is IList && obj.GetType().IsGenericType)
            {
                return GetArrayValue(obj as IList, fieldPath);
            }
            else
            {
                return GetSingleValue(obj, fieldPath);
            }
        }

        private object GetArrayValue(IList obj, List<string> fieldPath)
        {
            int index = Int32.Parse(fieldPath[0]);
            
            // Some of the objects might not have the same amount of skills
            if (index >= obj.Count)
            {
                return null;
            }

            if (fieldPath.Count == 1)
            {
                return obj[index];
            }
            
            var newPath = fieldPath.GetRange(1, fieldPath.Count - 1);
            return GetValue(obj[index], newPath);
        }

        private object GetSingleValue(object obj, List<string> fieldPath)
        {
            var property = obj.GetType().GetProperty(fieldPath[0]);
            
            if (fieldPath.Count == 1)
            {
                return property.GetValue(obj);
            }

            var newObj = property.GetValue(obj);

            // Some of the objects might not have some of the fields
            if (newObj == null)
                return null;
            
            var newPath = fieldPath.GetRange(1, fieldPath.Count - 1);
            return GetValue(newObj, newPath);
        }
        
        #endregion

        #region ToString
        public override string ToString()
        {
            string result = ToString(this, 0);
            return result;
        }
        
        private string ToString(object obj, int depthIndex)
        {
            if (obj == null)
                return "";
            if (ClassRegistry.IsBasicType(obj.GetType()))
                return obj.ToString();

            StringBuilder builder = new StringBuilder();
            
            var childProperties = obj.GetType().GetProperties();
            
            foreach (var childProperty in childProperties)
            {
                if (childProperty.CanWrite == false)
                    continue;

                var newObj = childProperty.GetValue(obj);

                string finalString = "";
                
                if (newObj is IList && newObj.GetType().IsGenericType)
                {
                    var listObj = newObj as IList;
                    
                    for (int i = 0; i < listObj.Count; ++i)
                    {
                        var element = listObj[i];
                        string childString = ToString(element, depthIndex + 1);
                        finalString = Environment.NewLine + "".PadLeft(depthIndex * 4, ' ') + $"{childProperty.Name}.{i}: {childString}";
                        
                        builder.Append(finalString);
                    }

                }
                else
                {
                    string childString = ToString(newObj, depthIndex + 1);
                    finalString = Environment.NewLine + "".PadLeft(depthIndex * 4, ' ') + childProperty.Name + ": " + childString;

                    builder.Append(finalString);
                }
            }

            return builder.ToString();
        }
        #endregion
    }
}