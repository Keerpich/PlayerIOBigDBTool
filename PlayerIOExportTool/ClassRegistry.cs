using System;
using System.Linq;
using System.Collections.Generic;

namespace PlayerIOExportTool
{
    public class ClassRegistry
    {
        private static readonly string[] PlayerIoBasicTypes = {"int", "float", "string", "bool", "uint", "datetime", "double"}; 
        private static readonly System.Type[] DotNetBasicTypes = new [] {typeof(int?), typeof(float?), typeof(System.String), typeof(bool?), typeof(uint?), typeof(DateTime), typeof(double?)};

        private List<(string, Type)> TypesMapping;

        public static bool IsBasicType(string playerIOType)
        {
            return PlayerIoBasicTypes.Contains(playerIOType);
        }

        public static bool IsBasicType(Type dotNetType)
        {
            return DotNetBasicTypes.Contains(dotNetType);
        }

        public ClassRegistry()
        {
            if (PlayerIoBasicTypes.Length != DotNetBasicTypes.Length)
                throw new Exception("Check ClassRegistry basic type lists!");
            
            TypesMapping = new List<(string, Type)>();

            for (int i = 0; i < PlayerIoBasicTypes.Length; ++i)
            {
                TypesMapping.Add((PlayerIoBasicTypes[i], DotNetBasicTypes[i]));
            }
        }

        public void AddType(string playerIOType, Type dotNetType)
        {
            TypesMapping.Add((playerIOType, dotNetType));
        }

        public bool IsDefined(string playerIoType)
        {
            return TypesMapping.FindIndex(pair => pair.Item1 == playerIoType) >= 0;
        }
        
        public string GetPlayerIOType(string dotNetType)
        {
            return GetPlayerIOType(Type.GetType(dotNetType, true));
        }

        public string GetPlayerIOType(Type dotNetType)
        {
            return (from map in TypesMapping where map.Item2 == dotNetType select map.Item1).First();
        }

        public Type GetDotNetType(string playerIOType)
        {
            return (from map in TypesMapping where map.Item1 == playerIOType select map.Item2).First();
        }
    }
}