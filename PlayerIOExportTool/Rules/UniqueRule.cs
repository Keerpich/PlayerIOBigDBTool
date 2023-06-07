using System;
using System.Collections;
using System.Collections.Generic;

namespace PlayerIOExportTool.Rules
{
    public class UniqueRule : ARule
    {        
        public UniqueRule(string checkedField) : base(checkedField)
        {
        }

        public override void Check(Dictionary<string, List<object>> allEntries)
        {            
            var checkedEntries = allEntries[checkedTable];

            foreach (var checkedEntry in checkedEntries)
            {
                List<string> currentValues = new List<string>();
                Check(checkedEntry, checkedPathSplit, currentValues);
            }
        }
        
        private void Check(object obj, List<string> path, List<string> currentValues)
        {
            // Some objects might not have this field
            if (obj == null)
            {
                return;
            }
            
            if (obj is IList)
            {
                foreach (var element in (IList)obj)
                {
                    Check(element, path, currentValues);
                }
            }
            else if (path.Count == 0)
            {
                if (currentValues.Contains(obj.ToString()))
                {
                    throw new RuleFailedException(
                        $"Value {obj} from {checkedTable}.{checkedPath} is present multiple times");
                }
                else
                {
                    currentValues.Add(obj.ToString());
                }
            }
            else
            {
                var newPath = path.GetRange(1, path.Count - 1);
                var newObj = ((Object) obj).GetValue(path[0]);

                Check(newObj, newPath, currentValues);
            }
        }
    }
}