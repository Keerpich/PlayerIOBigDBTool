using System.Collections;
using System.Collections.Generic;

namespace PlayerIOExportTool.Rules
{
    /// <summary>
    /// Checks if a field from an object has only values that exist in another objects
    /// </summary>
    public abstract class AMappingRule : ARule
    {
        public AMappingRule(string checkedField) :
            base(checkedField)
        {
        }

        //TODO: Maybe this shouldn't know how to walk through an object. 
        protected void Check(object obj, List<string> path, List<string> validValues)
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
                    Check(element, path, validValues);
                }
            }
            else if (path.Count == 0)
            {
                CheckSingle(obj.ToString(), validValues);
            }
            else
            {
                var newPath = path.GetRange(1, path.Count - 1);
                var newObj = ((Object) obj).GetValue(path[0]);

                Check(newObj, newPath, validValues);
            }
        }

        protected virtual void CheckSingle(string value, List<string> validValues)
        {
            if (!validValues.Contains(value))
            {
                HandleRuleFail(value);
            }
        }

        protected abstract void HandleRuleFail(object obj);
    }
}