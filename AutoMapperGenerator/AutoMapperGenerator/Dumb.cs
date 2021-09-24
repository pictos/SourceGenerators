using System;
using System.Collections.Generic;
using System.Text;


namespace AutoMapperGenerator.Test
{
    public class Funcionario
    {

    }
}


namespace AutoMapperGenerator
{
    class Dumb
    {
        [PropertyName("bla", typeof(Dumb2))]
        public int MyProperty { get; set; }
    }

    class Dumb2
    {
        // AutoMapperGenerator.Test.Funcionario
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public sealed class PropertyNameAttribute : System.Attribute
    {
        public string PropertyName { get; }
        public Type MapperTo { get; }
        public PropertyNameAttribute(string name, Type type)
        {
            PropertyName = name;
            MapperTo = type;
        }
    }
}
