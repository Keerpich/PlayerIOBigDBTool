using System.Collections.Generic;
using System.Linq;

namespace PlayerIOExportTool.Rules
{
    public class TableMappingMappingRule : AMappingRule
    {
        private string sourceTable;
        private string sourcePath;
        
        /// <summary>
        /// Initialize a rule
        /// </summary>
        /// <param name="checkedField">Path to the field to be checked. This must include the table name.</param>
        /// <param name="sourceField">Path to the field that contains the valid values. This must include the table name.</param>
        public TableMappingMappingRule(string checkedField, string sourceField) 
            : base(checkedField)
        {
            var sourceTableDelimiterIndex = sourceField.IndexOf('.');
            sourceTable = sourceField.Substring(0, sourceTableDelimiterIndex);
            sourcePath = sourceField.Substring(sourceTableDelimiterIndex + 1);
        }

        /// <summary>
        /// Run the rule check on a set of data
        /// </summary>
        /// <param name="allEntries">Set of data that includes both the checked objects and the objects that give the valid values</param>
        /// <exception cref="RuleFailedException">Check has failed</exception>
        public override void Check(Dictionary<string, List<object>> allEntries)
        {
            var checkedEntries = allEntries[checkedTable];
            var sourceEntries = allEntries[sourceTable];

            var validValues = sourceEntries.Select(entry => ((Object)entry).GetValue(sourcePath).ToString()).ToList();

            foreach (var checkedEntry in checkedEntries)
            {
                Check(checkedEntry, checkedPathSplit, validValues);
            }
        }

        protected override void HandleRuleFail(object obj)
        {
            throw new RuleFailedException($"Value {obj} from {checkedTable}.{checkedPath} was not found in {sourceTable}.{sourcePath}");
        }
    }
}