using System;
using System.Collections;
using System.Collections.Generic;

namespace PlayerIOExportTool.Rules
{
    public class IntervalRule : ARule
    {
        private int min;
        private int max;
        
        public IntervalRule(string checkedField, int min, int max) : base(checkedField)
        {
            this.min = min;
            this.max = max;
        }

        public override void Check(Dictionary<string, List<object>> allEntries)
        {
            var checkedEntries = allEntries[checkedTable];

            foreach (var checkedEntry in checkedEntries)
            {
                Check(checkedEntry, checkedPathSplit);
            }
        }

        private void Check(object obj, List<string> path)
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
                    Check(element, path);
                }
            }
            else if (path.Count == 0)
            {
                int value = (Int32) obj;
                if (value < min || value > max)
                {
                    throw new RuleFailedException(
                        $"Value {obj} from {checkedTable}.{checkedPath} is not in interval [{min}, {max}]");
                }
            }
            else
            {
                var newPath = path.GetRange(1, path.Count - 1);
                var newObj = ((Object) obj).GetValue(path[0]);

                Check(newObj, newPath);
            }
        }
    }
}