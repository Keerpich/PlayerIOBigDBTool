using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AngleSharp.Common;
using DreamTeam.Tson;
using PlayerIOClient;

namespace PlayerIOExportTool
{
    public class PullDataRunner
    {
        private Client Client;
        private string OutputDirectory;
        private string TableName;
        private TablesWrapper TablesWrapper;
        private List<ClassBuilder.ClassMember> TableClassMembers;
        private TableLayout.TableLayoutData TableLayoutData;
        
        public PullDataRunner(Client client, string outputDirectory, string tableName, TablesWrapper tablesWrapper,
            List<ClassBuilder.ClassMember> tableClassMembers, TableLayout.TableLayoutData tableLayoutData)
        {
            Client = client;
            OutputDirectory = outputDirectory;
            TableName = tableName;
            TablesWrapper = tablesWrapper;
            TableClassMembers = tableClassMembers;
            TableLayoutData = tableLayoutData;
        }

        public Task<bool> Run()
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            RunRecursively(tcs);
            return tcs.Task;
        }

        private void RunRecursively(TaskCompletionSource<bool> tcs, List<DatabaseObject> allDatabaseObjects = null, object start = null)
        {
            allDatabaseObjects ??= new List<DatabaseObject>();
            
            Client.BigDB.LoadRange(TableName, TableLayoutData.Index, null, start, null, 1000,
                databaseObjects =>
                {
                    if (start != null)
                    {
                        allDatabaseObjects.AddRange(databaseObjects.Skip(1));
                    }
                    else
                    {
                        allDatabaseObjects.AddRange(databaseObjects);
                    }
                    
                    if (databaseObjects.Length == 1000)
                    {
                        object last = databaseObjects.Last().Properties[TableLayoutData.IndexField];
                        RunRecursively(tcs, allDatabaseObjects, last);
                    }
                    else
                    {
                        Dictionary<string, object> keys = new Dictionary<string, object>();

                        foreach (var databaseObject in allDatabaseObjects)
                        {
                            var tsonString = databaseObject.ToString();
                            var objectValues = TsonConvert.DeserializeObject(tsonString);
                            var values = new Dictionary<string, object>();
                            ExtendPathsRecursively(objectValues, values);
                            TablesWrapper.AddEntry(TableName, values);

                            InsertKeys(ref keys, objectValues);
                        }

                        keys = SortKeys(keys, TableClassMembers);
                        var headerList = ConvertToHeaderList(keys);

                        Directory.CreateDirectory(OutputDirectory);
                        string filename = Path.Combine(OutputDirectory, TableName + ".csv");

                        TablesWrapper.ToCSV(TableName, headerList, filename);

                        tcs.SetResult(true);
                    }
                },
                error => { tcs.SetException(error); new Exception($"Couldn't load table {TableName}. Reason: {error.Message}"); });
        }
        
        #region Utils
        
        private static Dictionary<string, object> SortKeys(Dictionary<string, object> keys, List<ClassBuilder.ClassMember> tableClassMembers)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            var names = tableClassMembers.Select(member => member.name);
            var differences = keys.Keys.Except(names);
            if (differences.Any())
            {
                throw new Exception("Layout columns do not match remote columns");
            }

            foreach (var tableClassMember in tableClassMembers)
            {
                string fieldName = tableClassMember.name;

                if (keys.ContainsKey(fieldName))
                {
                    result.Add(fieldName, keys[fieldName]);
                }
            }

            return result;
        }

        private static List<string> ConvertToHeaderList(Dictionary<string, object> keys)
        {
            var extendedPathsDict = new Dictionary<string, object>();
            ExtendPathsRecursively(keys, extendedPathsDict);
            var headerList = extendedPathsDict.Keys.ToList();
            return headerList;
        }

        private static void InsertKeys(ref Dictionary<string, object> destination, Dictionary<string, object> source)
        {
            foreach (var o in source)
            {
                InsertUnique(ref destination, o);
            }
        }

        private static void InsertUnique(ref Dictionary<string, object> destination, KeyValuePair<string, object> element)
        {
            if (!destination.ContainsKey(element.Key))
            {
                destination.Add(element.Key, element.Value);
                return;
            }

            if (element.Value is Dictionary<string, object>)
            {
                var destinationElement = destination[element.Key] as Dictionary<string, object>;
                var elementAsDict = element.Value as Dictionary<string, object>;
                InsertKeys(ref destinationElement, elementAsDict);
                return;
            }

            if (element.Value.GetType().IsArray)
            {
                List<object> destinationList = ConvertToList(destination[element.Key]);
                List<object> elementList = ConvertToList(element.Value);
                
                for (int i = 0; i < elementList.Count; ++i)
                {
                    if (i >= destinationList.Count)
                    {
                        destinationList.Add(elementList[i]);
                    }
                    else if (elementList[i] is Dictionary<string, object>)
                    {
                        var destinationElement = destinationList[i] as Dictionary<string, object>;
                        var elementAsDict = elementList[i] as Dictionary<string, object>;
                        InsertKeys(ref destinationElement, elementAsDict);
                    }
                }

                destination[element.Key] = destinationList.ToArray();
            }
        }

        private static List<object> ConvertToList(object objValue)
        {
            var objType = objValue.GetType();
            
            if ((objType.IsGenericType && (objType.GetGenericTypeDefinition() == typeof(List<>))))
            {
                // Get the generic type definition
                MethodInfo method = typeof(Program).GetMethod("ConvertListOf",
                    BindingFlags.NonPublic | BindingFlags.Static);

                // Build a method with the specific type argument you're interested in
                method = method.MakeGenericMethod(objType.GetGenericArguments()[0]);

                // The "null" is because it's a static method
                object objectList = method.Invoke(null, new[] {objValue});

                return objectList as List<object>;
            }
            else
            {
                return (objValue as IEnumerable<object>).ToList();
            }
        }
        
        private static List<object> ConvertListOf<T>(object list)
        {
            return (list as IEnumerable<T>).Select(x => x as object).ToList();
        }

        private static void ExtendPathsRecursively(Dictionary<string, object> values, Dictionary<string, object> output, string path = "")
        {
            for (int i = 0; i < values.Count; ++i)
            {
                var value = values.GetItemByIndex(i);
                var fullPath = String.IsNullOrEmpty(path) ? value.Key : $"{path}.{value.Key}";

                if (value.Value is Dictionary<string, object>)
                {
                    ExtendPathsRecursively((Dictionary<string, object>) value.Value, output, fullPath);
                }
                else if (value.Value.GetType().IsArray)
                {
                    var arr = (value.Value as object[]);

                    for (int idx = 0; idx < arr.Length; ++idx)
                    {
                        var container = new Dictionary<string, object>();
                        container.Add(idx.ToString(), arr[idx]);
                        
                        ExtendPathsRecursively(container, output, fullPath);
                    }
                }
                else
                {
                    output.Add(fullPath, value.Value);
                }
            }
        }
        
        #endregion
    }
}