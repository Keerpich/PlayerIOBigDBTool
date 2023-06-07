using System.Collections.Generic;
using System.Linq;

namespace PlayerIOExportTool.Rules
{
    public class ValuesMappingMappingRule : AMappingRule
    {
        private List<string> validValues;
        
        public ValuesMappingMappingRule(string checkedField, List<string> validValues) :
            base (checkedField)
        {
            this.validValues = validValues;
        }

        public override void Check(Dictionary<string, List<object>> allEntries)
        {
            var checkedEntries = allEntries[checkedTable];

            foreach (var checkedEntry in checkedEntries)
            {
                Check(checkedEntry, checkedPathSplit, validValues);
            }
        }

        protected override void CheckSingle(string value, List<string> validValues)
        {
            List<string> substrings = value.Trim('"').Split(',').ToList();

            foreach (var substring in substrings)
            {
                base.CheckSingle(substring, validValues);
            }
        }

        protected override void HandleRuleFail(object obj)
        {
            throw new RuleFailedException($"Value {obj} from {checkedTable}.{checkedPath} was not found in {string.Join(",", validValues)}");
        }
    }
}