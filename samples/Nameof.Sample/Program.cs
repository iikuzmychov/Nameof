using Nameof.Shared;
using Namespace;

//[assembly: GenerateNameof(NameofAccessModifier.Public)]

Console.WriteLine(nameof(SampleType));
Console.WriteLine(nameof<SampleType>._field);
Console.WriteLine(nameof<SampleType>.Property);
Console.WriteLine(nameof<SampleType>.Method);
Console.WriteLine(nameof<SampleType>.Event);

Console.WriteLine();

Console.WriteLine(nameof(SampleType.PublicNestedType));
Console.WriteLine(nameof<SampleType.PublicNestedType>._nestedField);

Console.WriteLine();

Console.WriteLine(nameof<SampleType>.PrivateNestedType);
Console.WriteLine(nameof<SampleType>.PrivateNestedType._nestedField);

Console.WriteLine();

Console.WriteLine(nameof<SampleType>.PrivateNestedType.DoubleNestedType);
Console.WriteLine(nameof<SampleType>.PrivateNestedType.DoubleNestedType._doubleNestedField); // updated to actual field name

Console.WriteLine();

//Console.WriteLine($"{nameof(SampleGenericType<List<int>>)}<List<int>>");
//Console.WriteLine(nameof<SampleGenericType<List<int>>>._field1);
//
//Console.WriteLine();
//
//Console.WriteLine($"{nameof(SampleGenericType<,>)}<int, string>");
//Console.WriteLine(nameof<SampleGenericType<int, string>>._field1);
//Console.WriteLine(nameof<SampleGenericType<int, string>>._field2);
//
//Console.WriteLine();
//
//Console.WriteLine(nameof(SampleGenericType_2));
//Console.WriteLine(nameof<SampleGenericType_2>._field1);
//Console.WriteLine(nameof<SampleGenericType_2>._field2);
//
//Console.WriteLine();
//
//Console.WriteLine(nameof(SampleGenericType));
//Console.WriteLine(nameof<SampleGenericType>._field1);
//Console.WriteLine(nameof<SampleGenericType>._field2);

namespace Namespace
{
    [GenerateNameof]
    internal struct SampleType
    {
        private readonly int _field;
        private event Action Event { add { } remove { } }
        private double Property { get; set; }
        private void Method() { }

        [GenerateNameof]
        internal class PublicNestedType
        {
            private readonly string _nestedField;
        }

        private class PrivateNestedType
        {
            private readonly string _nestedField;

            public static class DoubleNestedType
            {
                private static readonly string _doubleNestedField;
            }
        }
    }

    public class SampleGenericType<T> where T : class, IEnumerable<int>
    {
        private readonly T _field1;
    }

    public class SampleGenericType<T1, T2>
    {
        private readonly T1 _field1;
        private readonly T2 _field2;
    }

    public class SampleGenericType_2
    {
        private readonly int _field1;
        private readonly string _field2;
    }

    public class SampleGenericType
    {
        private readonly int _field1;
        private readonly string _field2;
    }
}
