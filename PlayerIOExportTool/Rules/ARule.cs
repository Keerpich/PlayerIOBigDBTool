using System.Collections.Generic;
using System.Linq;

namespace PlayerIOExportTool.Rules
{
    public abstract class ARule
    {
        protected string checkedTable;
        protected string checkedPath;
        protected List<string> checkedPathSplit;

        public ARule(string checkedField)
        {
            //TODO: Maybe we can/want move some of these initializations later
            var checkedTableDelimiterIndex = checkedField.IndexOf('.');
            checkedTable = checkedField.Substring(0, checkedTableDelimiterIndex);
            checkedPath = checkedField.Substring(checkedTableDelimiterIndex + 1);
            checkedPathSplit = checkedPath.Split(".").ToList();
            checkedPathSplit.ForEach(s => s.Trim());
        }
        
        /// <summary>
        /// Run the rule check on a set of data
        /// </summary>
        /// <param name="allEntries">Set of data that includes both the checked objects and the objects that give the valid values</param>
        /// <exception cref="RuleFailedException">Check has failed</exception>
        public abstract void Check(Dictionary<string, List<object>> allEntries);
    }
}