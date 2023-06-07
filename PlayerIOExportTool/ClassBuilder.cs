using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlayerIOExportTool
{
    public static class ClassBuilder
    {
        public class ClassMember
        {
            public string name;
            public string type;
            public bool isArray;

            public ClassMember(string name, string type, bool isArray)
            {
                this.name = name;
                this.type = type;
                this.isArray = isArray;
            }
        }

        public class ObjectDefinition
        {
            public string name;
            public List<string> fields;

            public ObjectDefinition(string name, List<string> fields)
            {
                this.name = name;
                this.fields = fields;
            }
        }
        
        public static void CreateClassesFromTable(string tableName, List<ClassMember> rootObjectMembers, List<ClassMember> objectFields, List<ObjectDefinition> objectDefinitions, out ClassRegistry classRegistry)
        {
            classRegistry = new ClassRegistry();
            CreateUnderlyingClasses(tableName, objectFields, objectDefinitions, classRegistry);
            CreateTableClass(tableName, classRegistry, rootObjectMembers);
        }

        private static void CreateTableClass(string tableName, ClassRegistry classRegistry, List<ClassMember> rootObjectMembers)
        {
            List<Tuple<string, Type>> memberTypes = rootObjectMembers.Select(
                member => GetMemberType(tableName, member, classRegistry)).ToList();
            
            var type = CreateNewClass(tableName, memberTypes);
            classRegistry.AddType(tableName, type);
        }
        
        private static Tuple<string, Type> GetMemberType(string tableName, ClassMember member, ClassRegistry classRegistry)
        {
            Type type;
        
            if (member.isArray)
            {
                string dataTypeName = ClassRegistry.IsBasicType(member.type)
                    ? member.type
                    : tableName + "." + member.type;

                if (!classRegistry.IsDefined(dataTypeName))
                {
                    return null;
                }
        
                var listType = typeof(List<>);
                var constructedListType = listType.MakeGenericType(classRegistry.GetDotNetType(dataTypeName));
        
                type = constructedListType;
            }
            else
            {
                string dataTypeName = ClassRegistry.IsBasicType(member.type)
                    ? member.type
                    : tableName + "." + member.type;
                
                if (!classRegistry.IsDefined(dataTypeName))
                {
                    return null;
                }
        
                type = classRegistry.GetDotNetType(dataTypeName);
            }
        
            return new Tuple<string, Type>(member.name, type);
        }

        private static void CreateUnderlyingClasses(string tableName, List<ClassMember> objectFields, List<ObjectDefinition> objectDefinitions, ClassRegistry classRegistry)
        {
            List<string> skippedFields = new List<string>();
            List<string> skippedFieldsCulprits = new List<string>();
            
            int lastSkippedFields = -1;

            do
            {
                skippedFields.Clear();
                skippedFieldsCulprits.Clear();

                foreach (var objectDefinition in objectDefinitions)
                {
                    var objectName = objectDefinition.name;

                    if (classRegistry.IsDefined(objectName))
                        continue;

                    bool canCreateObject = true;
                    var classFields = new List<Tuple<string, Type>>();

                    foreach (var objectMemberName in objectDefinition.fields)
                    {
                        var fieldType = GetObjectFieldType(tableName, objectFields, objectName, objectMemberName, classRegistry);

                        if (fieldType == null)
                        {
                            skippedFieldsCulprits.Add($"{tableName}.{objectName}.{objectMemberName}");
                            canCreateObject = false;
                            break;
                        }

                        classFields.Add(new Tuple<string, Type>(objectMemberName, fieldType));
                    }

                    if (canCreateObject)
                    {
                        var typeName = $"{tableName}.{objectName}";
                        var type = ClassBuilder.CreateNewClass(typeName, classFields);
                        classRegistry.AddType(typeName, type);
                    }
                    else
                    {
                        skippedFields.Add(objectName);
                    }
                }

                bool couldntCreateNewFields = lastSkippedFields == skippedFields.Count;
                if (couldntCreateNewFields)
                {
                    ThrowObjectCreationError(skippedFields, skippedFieldsCulprits);
                }

                lastSkippedFields = skippedFields.Count;
            } while (skippedFields.Count > 0);
        }
        
        private static Type GetObjectFieldType(string tableName, List<ClassMember> allObjectFields, string objectName, string objectMemberName, ClassRegistry classRegistry)
        {
            var objectMemberFullName = $"{objectName}.{objectMemberName}";
            var objectField = allObjectFields.Find(member => member.name == objectMemberFullName);

            if (objectField == null)
            {
                IEnumerable<string> candidatesNames = allObjectFields.Select(field => $"\t{field.name}");
                string candidatesString = string.Join(Environment.NewLine, candidatesNames);
                throw new Exception(
                    $"In table {tableName}, can't find {objectMemberFullName} in the following fields: {Environment.NewLine}{candidatesString}");
            }
            
            var isMemberDefined = !string.IsNullOrEmpty(objectField.name);

            if (!isMemberDefined)
            {
                return null;
            }

            var memberType = GetMemberType(tableName, objectField, classRegistry)?.Item2;
            
            return memberType;
        }
        
        private static void ThrowObjectCreationError(List<string> skippedFields, List<string> skippedFieldsCulprits)
        {
            StringBuilder stringBuilder = new StringBuilder();
            var skippedFieldsNames = String.Join(", ", skippedFields);

            stringBuilder.Append($"Cannot create object type(s): {skippedFieldsNames} !");
            stringBuilder.Append($"{Environment.NewLine}Culprits:");

            foreach (var culprit in skippedFieldsCulprits)
            {
                stringBuilder.Append($"{Environment.NewLine}{culprit}");
            }

            throw new Exception(stringBuilder.ToString());
        }
        
        public static object CreateNewObject(System.Type type)
        {
            // Get the generic type definition
            MethodInfo method = typeof(ClassBuilder).GetMethod("CreateNewObject", 
                BindingFlags.NonPublic | BindingFlags.Static);
            
            // Build a method with the specific type argument you're interested in
            method = method.MakeGenericMethod(type);
            
            // The "null" is because it's a static method
            return method.Invoke(null, null);

        }

        //This is used by CreateNewObject(System.Type type)
        private static object CreateNewObject<T>()
        {
            return Activator.CreateInstance(typeof(T));
        }

        private static Type CreateNewClass(string typeName, List<Tuple<string, Type>> listOfFields)
        {
            return CompileResultType(typeName, listOfFields);
        }

        private static Type CompileResultType(string typeName, List<Tuple<string, Type>> listOfFields)
        {
            TypeBuilder tb = GetTypeBuilder($"PlayerIOExportTool.{typeName}");
            ConstructorBuilder constructor = tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

            // NOTE: assuming your list contains Field objects with fields FieldName(string) and FieldType(Type)
            foreach (var field in listOfFields)
                CreateProperty(tb, field.Item1, field.Item2);

            Type objectType = tb.CreateType();
            return objectType;
        }

        private static TypeBuilder GetTypeBuilder(string typeSignature)
        {
            var an = new AssemblyName(typeSignature);
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
            TypeBuilder tb = moduleBuilder.DefineType(typeSignature,
                    TypeAttributes.Public |
                    TypeAttributes.Class |
                    TypeAttributes.AutoClass |
                    TypeAttributes.AnsiClass |
                    TypeAttributes.BeforeFieldInit |
                    TypeAttributes.AutoLayout,
                    typeof(PlayerIOExportTool.Object));
            return tb;
        }

        private static void CreateProperty(TypeBuilder tb, string propertyName, Type propertyType)
        {
            FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);

            PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
            MethodBuilder getPropMthdBldr = tb.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, propertyType, Type.EmptyTypes);
            ILGenerator getIl = getPropMthdBldr.GetILGenerator();

            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getIl.Emit(OpCodes.Ret);

            MethodBuilder setPropMthdBldr =
                tb.DefineMethod("set_" + propertyName,
                  MethodAttributes.Public |
                  MethodAttributes.SpecialName |
                  MethodAttributes.HideBySig,
                  null, new[] { propertyType });

            ILGenerator setIl = setPropMthdBldr.GetILGenerator();
            Label modifyProperty = setIl.DefineLabel();
            Label exitSet = setIl.DefineLabel();

            setIl.MarkLabel(modifyProperty);
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, fieldBuilder);

            setIl.Emit(OpCodes.Nop);
            setIl.MarkLabel(exitSet);
            setIl.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getPropMthdBldr);
            propertyBuilder.SetSetMethod(setPropMthdBldr);
        }
    }
}
