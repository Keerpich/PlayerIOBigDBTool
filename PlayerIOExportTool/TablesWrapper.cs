using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Konsole;
using PlayerIOClient;
using PlayerIOExportTool.Rules;

namespace PlayerIOExportTool
{
    public class TablesWrapper
    {
        private Dictionary<string, Type> tableEntryTypes = new Dictionary<string, Type>();
        private Dictionary<string, List<object>> tableEntries = new Dictionary<string, List<object>>();

        public void CreateTable(string tableName, Type tableType)
        {
            tableEntryTypes.Add(tableName, tableType);
            tableEntries.Add(tableName, new List<object>());
        }

        public List<string> GetTableNames()
        {
            return tableEntries.Keys.ToList();
        }

        public int GetNumberOfEntries(string tableName)
        {
            return tableEntries[tableName].Count();
        }

        public void ReadTable(string tableName)
        {
            using (var reader = new StreamReader(Path.Combine("Push-Data",  tableName + ".csv")))
            {
                string headerLine = reader.ReadLine();
                List<string> headers = headerLine.Split(",").ToList();
                headers.ForEach(s => s.Trim());

                //first line is the header
                int lineNumber = 1;
                
                while (!reader.EndOfStream)
                {
                    ++lineNumber;
                    
                    var line = reader.ReadLine();
                    var entryFields = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

                    for (int i = 0; i < entryFields.Length; ++i)
                    {
                        StringBuilder sb = new StringBuilder(entryFields[i].Trim());
                        
                        //Remove quotes
                        if (sb.Length > 0 && sb[0] == '\"' && sb[^1] == '\"')
                        {
                            sb.Remove(0, 1);
                            sb.Remove(sb.Length - 1, 1);
                        }

                        entryFields[i] = sb.ToString();
                    }

                    var values = new Dictionary<string, object>();

                    for (int i = 0; i < headers.Count; ++i)
                    {
                        if (!String.IsNullOrEmpty(entryFields[i]))
                        {
                            values.Add(headers[i], entryFields[i]);
                        }
                    }

                    try
                    {
                        AddEntry(tableName, values);
                    }
                    catch (Exception exception)
                    {
                        string exceptionMessage = $"{exception.Message} -- At line {lineNumber} in table {tableName}";
                        throw new Exception(exceptionMessage, exception);
                    }
                }
            }
        }

        public void AddEntry(string tableName, Dictionary<string, object> values)
        {
            var entry = ClassBuilder.CreateNewObject(tableEntryTypes[tableName]);
            tableEntries[tableName].Add(entry);

            Object obj = (Object) entry;
            obj.Deserialize(values);
        }

        public async Task PopulateTable(BigDB bigDb, string tableName, ProgressBar progressBar)
        {
            int entriesPushed = 0;
            progressBar.Refresh(0, tableName);
            
            foreach (var entry in tableEntries[tableName])
            {
                Object entryObject = (Object)entry;

                string key = entryObject.GetValue("id").ToString();
                
                var dbo = entryObject.ToDatabaseObject();
                bigDb.CreateObject(tableName, key, dbo,
                    value =>
                    {
                        Interlocked.Increment(ref entriesPushed);
                        progressBar.Refresh(entriesPushed, tableName);
                    },
                    error =>
                    {
                        throw new Exception($"Couldn't populate table {tableName}. Reason: {error.Message}");
                    });
                await Task.Delay(100);
            }

            while (entriesPushed < tableEntries[tableName].Count())
            {
                await Task.Delay(1000);
            }
        }

        public void ToCSV(string tableName, List<string> keys, string filename)
        {
            var entries = tableEntries[tableName];

            using var writer = new StreamWriter(filename);

            writer.WriteLine(string.Join(",", keys));
            
            foreach (var entry in entries)
            {
                var entryObject = (Object) entry;
                var entryFields = entryObject.GetValues(keys);
                var entryFieldsValues = from value in entryFields.Values select $"\"{value}\"";
                var csvString = string.Join(",", entryFieldsValues);
                
                writer.WriteLine(csvString);
            }
        }

        public void RunRule(ARule rule)
        {
            // TODO: Passing tableEntries kind of breaks encapsulation
            rule.Check(tableEntries);
        }
    }
}