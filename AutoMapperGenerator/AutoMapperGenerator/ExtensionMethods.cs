using Microsoft.CodeAnalysis;
using System.Linq;

namespace AutoMapperGenerator
{
    static class ExtensionMethods
    {
        public static TypedConstant GetAttributeValueByName(this AttributeData attribute, string name)
        {
            return attribute.NamedArguments.SingleOrDefault(kvp => kvp.Key == name).Value;
        }

        //public static TypedConstant GetConstructorArgumentsByName(this AttributeData attribute, string name)
        //{
        //    return attribute.ConstructorArguments.SingleOrDefault(kvp => kvp.ke)
        //}

        public static string GetAttributeValueByNameAsString(this AttributeData attribute, string name, string placeholder = "null")
        {
            var data = attribute.NamedArguments.SingleOrDefault(kvp => kvp.Key == name).Value;

            return data.Value is null ? placeholder : (string)data.Value;
        }
    }
}
