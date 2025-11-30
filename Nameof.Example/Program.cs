using Nameof;

[assembly: GenerateNameof<ConsoleKeyInfo>]
[assembly: GenerateNameof(typeof(SomeType))]
[assembly: GenerateNameof("System.IO.ConsoleStream", inAssemblyOf: typeof(Console))]

Console.WriteLine("=== ConsoleKeyInfo (external public type) ===");
Console.WriteLine("Private fields:");
Console.WriteLine(nameof<ConsoleKeyInfo>._key);
Console.WriteLine(nameof<ConsoleKeyInfo>._keyChar);
Console.WriteLine(nameof<ConsoleKeyInfo>._mods);

Console.WriteLine("\n=== ConsoleStream (external INTERNAL type) ===");
Console.WriteLine("Private fields (via stub + reflection):");
Console.WriteLine(nameof<ConsoleStream>._canRead);
Console.WriteLine(nameof<ConsoleStream>._canWrite);

Console.WriteLine("\n=== SomeType (user-defined internal type) ===");
Console.WriteLine("Private members:");
Console.WriteLine(nameof<SomeType>._someField);
Console.WriteLine(nameof<SomeType>.SomeMethod);
Console.WriteLine(nameof<SomeType>.SomeProperty);

internal class SomeType
{
    private int _someField;
    private void SomeMethod() { }
    private string SomeProperty { get; set; } = "";
}
