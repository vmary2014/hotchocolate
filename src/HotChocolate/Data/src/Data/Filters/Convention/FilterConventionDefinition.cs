using System;
using System.Collections.Generic;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Utilities;

namespace HotChocolate.Data.Filters;

public class FilterConventionDefinition : IHasScope
{
    public static readonly string DefaultArgumentName = "where";
    private string _argumentName = DefaultArgumentName;


    public string? Scope { get; set; }

    public string ArgumentName
    {
        get => _argumentName;
        set => _argumentName = value.EnsureGraphQLName();
    }

    public Type? Provider { get; set; }

    public IFilterProvider? ProviderInstance { get; set; }

    public List<FilterOperationConventionDefinition> Operations { get; } = new();

    public IDictionary<Type, Type> Bindings { get; } = new Dictionary<Type, Type>();

    public IDictionary<ITypeReference, List<ConfigureFilterInputType>> Configurations { get; } =
        new Dictionary<ITypeReference, List<ConfigureFilterInputType>>(
            TypeReferenceComparer.Default);

    public List<IFilterProviderExtension> ProviderExtensions { get; } = new();

    public List<Type> ProviderExtensionsTypes { get; } = new();

    public bool UseOr { get; set; } = true;

    public bool UseAnd { get; set; } = true;
}
