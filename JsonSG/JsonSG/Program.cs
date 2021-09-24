using System;

namespace JsonSG
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var p = new Person { Name = "Pedro", Age = 22 };

            var z = JsonSerializeGenerator.GeneratedSerializer.Serialize(p);

            Console.WriteLine(z);
        }
    }

    [JsonSerializeGenerator.SerializeAttribute]
    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}
