> [!Warning]
> This library is in EXPERIMENT status

# Nameof

A C# source generator that extends the built-in `nameof` operator to support non-accessible types and members from current and referenced assemblies.

## Why?

The built-in `nameof` operator in C# only works with accessible symbols. This limitation exists by design to prevent accidental dependencies on private or internal members, which could lead to fragile code.

However, there are some scenarios where referring to non-accessible members names can be useful, such as:
- Mapping EF Core navigation properties to private fields
- Using reflection to access private members

## Examples

### 1. Public class, non-accessible members

Target type:
```csharp
namespace SomeNamespace;

public class SomeClass
{
    private int _privateField;
    protected string ProtectedProperty { get; set; }
    internal event EventHandler InternalEvent;
    protected internal void ProtectedInternalMethod() { ... }
}
```

Nameof usage:
```csharp
using Nameof;
using SomeNamespace;

[assembly: GenerateNameof<SomeClass>]

Console.WriteLine(nameof<SomeClass>._privateField);           // Output: "_privateField"
Console.WriteLine(nameof<SomeClass>.ProtectedProperty);       // Output: "ProtectedProperty"
Console.WriteLine(nameof<SomeClass>.InternalEvent);           // Output: "InternalEvent"
Console.WriteLine(nameof<SomeClass>.ProtectedInternalMethod); // Output: "ProtectedInternalMethod"
```

<details>
  <summary>Auto-generated code</summary>

  ```csharp
  namespace Nameof
  {  
      internal static class nameof<T>
      {
      }
  }

  namespace Nameof
  {  
      [global::System.AttributeUsage(global::System.AttributeTargets.Assembly)]
      internal sealed class GenerateNameofAttribute : global::System.Attribute
      {
          public GenerateNameofAttribute(global::System.Type type)
          {
          }
      }
  }

  namespace SomeNamespace
  {
      internal static class Nameof_SomeNamespace_SomeClass
      {
          extension(global::Nameof.nameof<global::SomeNamespace.SomeClass>)
          {
              public static string _privateField => "_privateField";
              public static string ProtectedProperty => "ProtectedProperty";
              public static string InternalEvent => "InternalEvent";
              public static string ProtectedInternalMethod => "ProtectedInternalMethod";
          }
      }
  }
  ```
</details>

### 2. Non-accessible class, any members

Target type:
```csharp
namespace SomeNamespace

internal class SomeClass
{
    private int _privateField;

    public string PublicProperty { get; set; }
}
```

Nameof usage:
```csharp
using Nameof;
using SomeNamespace;

[GenerateNameof("SomeNamespace.SomeClass")]

Console.WriteLine(nameof(SomeClass));                 // Output: "SomeClass"
Console.WriteLine(nameof<SomeClass>._privateField);   // Output: "_privateField"
Console.WriteLine(nameof<SomeClass>.PublicProperty);  // Output: "PublicProperty"
```

<details>
  <summary>Auto-generated code</summary>
  
  ```csharp
  namespace Nameof
  {  
      internal static class nameof<T>
      {
      }
  }

  namespace Nameof
  {  
      [global::System.AttributeUsage(global::System.AttributeTargets.Assembly)]
      internal sealed class GenerateNameofAttribute : global::System.Attribute
      {
          public GenerateNameofAttribute(string fullTypeName, global::System.Type inAssemblyOf)
          {
          }
      }
  }

  namespace SomeNamespace
  {
      /// <summary>
      /// This class was auto-generated for referencing non-accessible type within nameof expressions.
      /// </summary>
      internal sealed class SomeClass
      {
          private SomeClass()
          {
          }
      }
  }

  namespace SomeNamespace
  {
      internal static class Nameof_SomeNamespace_SomeClass
      {
          extension(global::Nameof.nameof<global::SomeNamespace.SomeClass>)
          {
              public static string _privateField => "_privateField";
              public static string PublicProperty => "PublicProperty";
          }
      }
  }
  ```
</details>
