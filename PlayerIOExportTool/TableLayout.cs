using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace PlayerIOExportTool
{
    using TableLine = List<string>;
    using TableLineList = List<List<string>>;
    using ParsedTable = List<List<string>>;
    using ParsedTableDict = Dictionary<string, List<List<string>>>;
    
    //Loads table layout from disk
    public class TableLayout
    {
        public struct TableLayoutData
        {
            public string Name;
            public string Index;
            public string IndexField;
        }
        
        private const int FieldType = 0;
        private const int FieldName = 1;
        private const int DataType = 2;
        private const int ExtraDataStart = 3;

        private const string Column = "column";
        private const string ObjectDefinition = "object_definition";
        private const string ObjectField = "object_field";

        private static readonly string[] ValidFieldTypes = {
            Column, ObjectDefinition, ObjectField,
        };

        private string LayoutFolder;
        private Dictionary<string, TableLayoutData> TableLayoutDataDictionary = new Dictionary<string, TableLayoutData>();
        private ParsedTableDict ParsedTables;

        public void Load(string folder)
        {
            LayoutFolder = folder;
            ParseStringPairsFromCSV(@"tables_indices.csv");

            ParsedTables = ParseTables(GetTableNames());
            ValidateFieldTypeColumn(ParsedTables);
        }

        /// <summary>
        /// Getter for table names
        /// </summary>
        /// <returns>IEnumerable with names of all tables</returns>
        public IEnumerable<string> GetTableNames()
        {
            return from pair in TableLayoutDataDictionary select pair.Key;
        }

        public TableLayoutData GetTableLayoutData(string tableName)
        {
            return TableLayoutDataDictionary[tableName];
        }

        public List<ClassBuilder.ClassMember> GetTableClassMembers(string tableName)
        {
            List<ClassBuilder.ClassMember> classMembers = new List<ClassBuilder.ClassMember>();

            foreach (var line in ParsedTables[tableName])
            {
                if (line[FieldType] != Column)
                    continue;
                
                string name = line[FieldName];
                bool isArray = line[DataType] == "array";
                string type = isArray ? line[ExtraDataStart] : line[DataType];

                classMembers.Add(new ClassBuilder.ClassMember(name, type, isArray));
            }

            return classMembers;
        }

        public List<ClassBuilder.ClassMember> GetObjectFields(string tableName)
        {
            return ParseObjectFields(ParsedTables[tableName]);
        }

        public List<ClassBuilder.ObjectDefinition> GetObjectDefinitions(string tableName)
        {
            List<ClassBuilder.ObjectDefinition> objectDefinitions = new List<ClassBuilder.ObjectDefinition>();
            
            foreach (var line in ParsedTables[tableName])
            {
                if (line[FieldType] == ObjectDefinition)
                {
                    string name = line[FieldName];
                    var objectMembersNames = GetExtraData(line);
                    var objectDefinition = new ClassBuilder.ObjectDefinition(name, objectMembersNames);
                    objectDefinitions.Add(objectDefinition);
                }
            }

            return objectDefinitions;
        }

        private void ParseStringPairsFromCSV(string filename)
        {
            using (var reader = new StreamReader(Path.Combine(LayoutFolder, filename)))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');

                    string name = values[0].Trim();
                    string index = values[1].Trim();
                    string indexField = values[2].Trim();

                    TableLayoutDataDictionary.Add(
                        name, 
                        new TableLayoutData
                        {
                            Name = name,
                            Index = index,
                            IndexField = indexField
                        });
                }
            }
        }

        private ParsedTableDict ParseTables(IEnumerable<string> tableNames)
        {
            ParsedTableDict parsedTables = new ParsedTableDict();

            //parse the layout files
            foreach (var tableName in tableNames)
            {
                using (var reader = new StreamReader(Path.Combine(LayoutFolder, tableName + "_layout.csv")))
                {
                    List<List<string>> lines = new List<List<string>>();
                    
                    reader.ReadLine();

                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var fieldsInLine = line.Split(",").ToList();
                        fieldsInLine.ForEach(s => s.Trim());
                        lines.Add(fieldsInLine);
                    }

                    parsedTables.Add(tableName, lines);
                }
            }

            return parsedTables;
        }


        private void ValidateFieldTypeColumn(ParsedTableDict parsedTables)
        {
            var allInvalidFieldTypes = from allLines in parsedTables.Values
                from line in allLines
                where !ValidFieldTypes.Contains(line[FieldType])
                select line[FieldType];

            bool foundAnyInvalidFieldTypes = allInvalidFieldTypes.Any();

            if (foundAnyInvalidFieldTypes)
            {
                foreach (string invalidType in allInvalidFieldTypes)
                {
                    Console.WriteLine($"[ERROR] Invalid field type: {invalidType}");
                }

                throw new DataException("Found invalid field types. Look above!");
            }
        }

        private List<string> GetExtraData(TableLine line)
        {
            var extraData = line.GetRange(ExtraDataStart, line.Count - ExtraDataStart);
            extraData.RemoveAll(string.IsNullOrWhiteSpace);
            return extraData;
        }

        private List<ClassBuilder.ClassMember> ParseObjectFields(ParsedTable parsedTable)
        {
            var objectFields = new List<ClassBuilder.ClassMember>();

            foreach (var line in parsedTable)
            {
                if (line[FieldType] == ObjectField)
                {
                    string name = line[FieldName];
                    bool isArray = line[DataType] == "array";
                    string type = isArray ? line[ExtraDataStart] : line[DataType];
                    
                    
                    var objectField = new ClassBuilder.ClassMember(name, type, isArray);
                    objectFields.Add(objectField);
                }
            }
        
            return objectFields;
        }
    }
}