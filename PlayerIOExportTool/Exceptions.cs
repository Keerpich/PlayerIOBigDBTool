namespace PlayerIOExportTool
{
    using System;

    public class FieldNotFoundException : Exception
    {
        public readonly string FieldName;
        
        public FieldNotFoundException(string fieldName, Exception inner)
            : base($"Couldn't find field {fieldName}", inner)
        {
            FieldName = fieldName;
        }
    }

    public class InvalidArrayIndexException : Exception
    {
        public readonly int Index;
        
        public InvalidArrayIndexException(int index, Exception inner)
            : base($"Index {index} is invalid.", inner)
        {
            Index = index;
        }
    }
    
    public class InvalidDataTypeException : Exception
    {
        public readonly string FieldName;
        public readonly Type ExpectedType;
        
        public InvalidDataTypeException(string fieldName, Type type, Exception inner)
            : base($"Value for {fieldName} does not match the expected value type ({type})", inner)
        {
            FieldName = fieldName;
            ExpectedType = type;
        }
    }

    public class RuleFailedException : Exception
    {
        public RuleFailedException(string message)
            : base(message)
        {
        }
    }
}