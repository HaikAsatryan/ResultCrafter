using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ResultCrafter.Tests")]
// The FluentValidation endpoint filter reuses FieldKeyNaming. Its package depends on ResultCrafter.AspNetCore at
// ">= its own version", so an app may update ResultCrafter.AspNetCore alone: never change or remove a FieldKeyNaming
// member within a major version, or that app gets a MissingMethodException on its first failed validation.
[assembly: InternalsVisibleTo("ResultCrafter.AspNetCore.FluentValidation")]
