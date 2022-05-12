using System;
using System.Collections.Generic;
using System.Linq;
using HotChocolate.Language;
using HotChocolate.Language.Rewriters.Contracts;

namespace HotChocolate.Stitching.Types.Rewriters;

public class IgnoreNode<TContext> : Language.Rewriters.SchemaSyntaxRewriter<TContext>
{
    private readonly Dictionary<NameNode, ISyntaxReference> _ignores = new();
    private readonly List<NameNode> _ignoredInterfaces = new();

    public IReadOnlyList<NameNode> IgnoredInterfaces => _ignoredInterfaces.AsReadOnly();

    public IgnoreNode(IList<ISyntaxReference> ignoredNodes)
    {
        foreach (ISyntaxReference ignoredNode in ignoredNodes)
        {
            if (ignoredNode.Parent?.Node is not INamedSyntaxNode namedSyntaxNode)
            {
                throw new NotSupportedException();
            }

            NameNode oldName = namedSyntaxNode.Name;
            _ignores.Add(oldName, ignoredNode);
        }
    }

    protected override ITypeSystemDefinitionNode RewriteTypeDefinition(
        ITypeSystemDefinitionNode node,
        ISyntaxNavigator navigator,
        TContext context)
    {
        if (node is INamedSyntaxNode namedSyntaxNode
            && _ignores.TryGetValue(namedSyntaxNode.Name, out _))
        {
            return RemoveNode(node);
        }

        node = base.RewriteTypeDefinition(node, navigator, context);

        if (node is not ComplexTypeDefinitionNodeBase complexType)
        {
            return node;
        }

        if (complexType.Fields.Any(x => x is not null))
        {
            return node;
        }

        return RemoveNode(node);
    }

    private TNode RemoveNode<TNode>(TNode node)
        where TNode : ISyntaxNode
    {
        if (node is InterfaceTypeDefinitionNode interfaceTypeDefinition)
        {
            _ignoredInterfaces.Add(interfaceTypeDefinition.Name);
        }

        return default!;
    }

    protected override FieldDefinitionNode RewriteFieldDefinition(
        FieldDefinitionNode node,
        ISyntaxNavigator navigator,
        TContext context)
    {
        node = base.RewriteFieldDefinition(node, navigator, context);

        if (node is not INamedSyntaxNode namedSyntaxNode)
        {
            return node;
        }

        NameNode name = namedSyntaxNode.Name;
        if (!_ignores.TryGetValue(name, out ISyntaxReference? syntaxReference))
        {
            return node;
        }

        ITypeDefinitionNode? interfaceTypeDefinitionNode = syntaxReference.GetAncestor<ITypeDefinitionNode>();
        ITypeDefinitionNode? typeDefinitionNode = navigator.GetAncestor<ITypeDefinitionNode>();

        if (SyntaxComparer.BySyntax.Equals(interfaceTypeDefinitionNode?.Name,
                typeDefinitionNode?.Name)
            && interfaceTypeDefinitionNode?.Kind == typeDefinitionNode?.Kind)
        {
            return default!;
        }

        return node;
    }
}
