using System;

namespace Zooper.Bee.Generators;

/// <summary>
/// Marks a record class as a workflow payload, enabling automatic generation of
/// dependency tracking, validation, and type-safe property extensions.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class WorkflowPayloadAttribute : Attribute
{
}