using System;
using System.Diagnostics.CodeAnalysis;

namespace StructGenerator.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
    }

    partial struct Person : IEquatable<Person>
    {
        public bool Equals([AllowNull] Person other)
        {
            throw new NotImplementedException();
        }
    }

    partial struct Dumb : IEquatable<Dumb>
    {
        public int Value { get; }

        public string Text { get; }

        public Dumb(int value, string text)
        {
            Value = value;
            Text = text;
        }

        public bool Equals(Dumb other) =>
            (Value, Text) == (other.Value, other.Text);

        //public override int GetHashCode() => (Value, Text).GetHashCode();

        //public override bool Equals(object obj) =>
        //    (obj is Dumb dumb) && Equals(dumb);

        //public static bool operator ==(Dumb left, Dumb right) => left.Equals(right);

        //public static bool operator !=(Dumb left, Dumb right) => left.Equals(right);
    }

    partial struct PersonWithOutInterface
    {

    }
}
