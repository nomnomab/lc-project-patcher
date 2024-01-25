using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nomnom.LCProjectPatcherScriptCleaner {
    public static class ScriptScrubber {
        // ? most of this is directly from
        // ? https://github.com/EvaisaDev/LethalCompanyProjectCleaner/blob/482b32ce17525512a1103c1e59afa9b33da1f00b/LethalCompanyProjectCleaner/Program.cs#L235
        public static void Scrub(string[] files) {
            foreach (var file in files) {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName == "UnitySourceGeneratedAssemblyMonoScriptTypes_v1") {
                    File.Delete(file);
                    continue;
                }
                
                var text = File.ReadAllText(file);
                if (fileName == "NfgoClient") {
                    // go to line that is public NfgoClient([NotNull] NfgoCommsNetwork network)
                    var index = text.IndexOf("public NfgoClient([NotNull] NfgoCommsNetwork network)", StringComparison.Ordinal);
                    // does it contain base(network)
                    if (text.IndexOf("base(network)", index, StringComparison.Ordinal) == -1) {
                        // add base(network)
                        var indexOfLastParenthesis = text.IndexOf(')', index);
                        text = text.Insert(indexOfLastParenthesis + 1, " : base(network)");
                    }
                }
                
                var tree = CSharpSyntaxTree.ParseText(text);
                var root = tree.GetRoot();
                var methodsToRemove = root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Identifier.Text.StartsWith("__getTypeName") || m.Identifier.Text.StartsWith("__initializeVariables") ||
                                m.Identifier.Text.StartsWith("InitializeRPCS_") || m.Identifier.Text.StartsWith("__rpc_handler_"));

                var newRoot = root.RemoveNodes(methodsToRemove, SyntaxRemoveOptions.KeepNoTrivia);
                var classes = newRoot.DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (var classDeclaration in classes) {
                    // check if the class implements INetworkSerializable or IAsyncStateMachine
                    var implementedInterfaces = classDeclaration.DescendantNodes()
                        .OfType<IdentifierNameSyntax>()
                        .Select(x => x.Identifier.Text);

                    var newClassDeclaration = classDeclaration;

                    if (implementedInterfaces.Contains("INetworkSerializable")) {
                        // check if the class has the NetworkSerialize method
                        var hasNetworkSerialize = classDeclaration.DescendantNodes()
                            .OfType<MethodDeclarationSyntax>()
                            .Any(x => x.Identifier.Text == "NetworkSerialize");

                        if (!hasNetworkSerialize) {
                            // add the NetworkSerialize method
                            var method = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "NetworkSerialize")
                                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                                .WithTypeParameterList(
                                    SyntaxFactory.TypeParameterList(SyntaxFactory.SingletonSeparatedList<TypeParameterSyntax>(SyntaxFactory.TypeParameter("T"))))
                                .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList<ParameterSyntax>(SyntaxFactory
                                    .Parameter(SyntaxFactory.Identifier("serializer")).WithType(SyntaxFactory.IdentifierName("BufferSerializer<T>")))))
                                .WithBody(SyntaxFactory.Block(SyntaxFactory.SingletonList<StatementSyntax>(SyntaxFactory.ThrowStatement(SyntaxFactory
                                    .ObjectCreationExpression(SyntaxFactory.IdentifierName("System.NotImplementedException"))
                                    .WithArgumentList(SyntaxFactory.ArgumentList())))))
                                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

                            method = method.AddConstraintClauses(SyntaxFactory.TypeParameterConstraintClause("T")
                                .WithConstraints(
                                    SyntaxFactory.SingletonSeparatedList<TypeParameterConstraintSyntax>(
                                        SyntaxFactory.TypeConstraint(SyntaxFactory.IdentifierName("IReaderWriter")))));

                            newClassDeclaration = newClassDeclaration.AddMembers(method);
                        }
                    }

                    if (implementedInterfaces.Contains("IAsyncStateMachine")) {
                        // Check if the class has the MoveNext method
                        var hasMoveNext = classDeclaration.DescendantNodes()
                            .OfType<MethodDeclarationSyntax>()
                            .Any(x => x.Identifier.Text == "MoveNext");

                        if (!hasMoveNext) {
                            // Add the MoveNext method
                            var moveNextMethod = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "MoveNext")
                                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                                .WithBody(SyntaxFactory.Block(SyntaxFactory.SingletonList<StatementSyntax>(SyntaxFactory.ThrowStatement(SyntaxFactory
                                    .ObjectCreationExpression(SyntaxFactory.IdentifierName("System.NotImplementedException"))
                                    .WithArgumentList(SyntaxFactory.ArgumentList())))))
                                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

                            newClassDeclaration = newClassDeclaration.AddMembers(moveNextMethod);
                        }

                        // Check if the class has the SetStateMachine method
                        var hasSetStateMachine = classDeclaration.DescendantNodes()
                            .OfType<MethodDeclarationSyntax>()
                            .Any(x => x.Identifier.Text == "SetStateMachine");

                        if (!hasSetStateMachine) {
                            // Add the SetStateMachine method
                            var setStateMachineMethod = SyntaxFactory
                                .MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "SetStateMachine")
                                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                                .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList<ParameterSyntax>(SyntaxFactory
                                    .Parameter(SyntaxFactory.Identifier("stateMachine")).WithType(SyntaxFactory.IdentifierName("IAsyncStateMachine")))))
                                .WithBody(SyntaxFactory.Block(SyntaxFactory.SingletonList<StatementSyntax>(SyntaxFactory.ThrowStatement(SyntaxFactory
                                    .ObjectCreationExpression(SyntaxFactory.IdentifierName("System.NotImplementedException"))
                                    .WithArgumentList(SyntaxFactory.ArgumentList())))))
                                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

                            newClassDeclaration = newClassDeclaration.AddMembers(setStateMachineMethod);
                        }
                    }

                    newRoot = newRoot.ReplaceNode(classDeclaration, newClassDeclaration);
                }

                if (newRoot != null) {
                    var rewriter = new RemoveCtorMethodCalls();
                    newRoot = rewriter.Visit(newRoot);
                }

                var structs = newRoot.DescendantNodes()
                    .OfType<StructDeclarationSyntax>()
                    .ToList(); // Collect all struct declarations into a list

                while (structs.Any(x => x.DescendantNodes().OfType<ConstructorDeclarationSyntax>().ToList().Count > 0)) {
                    foreach (var structDeclaration in structs) {
                        var constructors = structDeclaration.DescendantNodes()
                            .OfType<ConstructorDeclarationSyntax>()
                            .ToList(); // Collect all constructor declarations into a list

                        var currentStruct = structDeclaration; // Keep track of the current struct

                        foreach (var constructor in constructors) {
                            var newStruct = currentStruct.RemoveNode(constructor, SyntaxRemoveOptions.KeepNoTrivia);
                            newRoot = newRoot.ReplaceNode(currentStruct, newStruct);

                            currentStruct = newStruct; // Update the current struct
                        }
                    }

                    structs = newRoot.DescendantNodes()
                        .OfType<StructDeclarationSyntax>()
                        .ToList();
                }
                
                var newCode = newRoot.ToFullString();

                // write the new code back to the file
                File.WriteAllText(file, newCode);
            }
        }
    }

    public class RemoveCtorMethodCalls : CSharpSyntaxRewriter {
        public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node) {
            if (node.Expression is InvocationExpressionSyntax invocation &&
                invocation.Expression.ToString().Contains("ctor")) {
                // If the expression is a method call containing "ctor", remove it.
                return null;
            } else {
                // Otherwise, keep the original node.
                return base.VisitExpressionStatement(node);
            }
        }
    }
}
