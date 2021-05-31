using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace RealGoodApps.Companion.Analyzer
{
    /// <summary>
    /// A diagnostic analyzer that implements companion types for methods and classes.
    /// The general idea is to have certain methods that are marked as public, but can only be used from a subset of types.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class Analyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The diagnostic error that is triggered when a method call occurs from a non-companion type.
        /// </summary>
        private static readonly DiagnosticDescriptor CallerNotCompanion =
            new DiagnosticDescriptor(
                "CN0001",
                "Caller Not Companion",
                "The caller of this method is not a companion",
                "Logic",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                "The method you are invoking is only public to companion types. If you would like to invoke it, either do it from a companion type or add the CompanionTypeAttribute to the method in question.");

        /// <summary>
        /// Our analyzer's diagnostic descriptors (which are basically our custom warnings/errors we can trigger).
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(CallerNotCompanion);

        /// <summary>
        /// Initialize our diagnostic analyzer.
        /// The important part here is that we register the appropriate callbacks that are to be executed when certain
        /// types of operations are encountered in analysis.
        /// </summary>
        /// <param name="context">An instance of <see cref="AnalysisContext"/>.</param>
        public override void Initialize(AnalysisContext context)
        {
            // To be honest, I'm not entirely sure what these options do but they were in both of the examples.
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            // Register our callbacks for the code we want to analyze.
            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
            context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
            context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
            context.RegisterOperationAction(AnalyzeObjectCreationReference, OperationKind.ObjectCreation);
        }

        /// <summary>
        /// Analyze a method reference (for example, an action group or a delegate to a method).
        /// </summary>
        /// <param name="context">An instance of <see cref="OperationAnalysisContext"/>.</param>
        private static void AnalyzeMethodReference(OperationAnalysisContext context)
        {
            if (!(context.Operation is IMethodReferenceOperation methodReferenceOperation))
            {
                return;
            }

            PerformInspection(context, methodReferenceOperation.Method, null);
        }

        /// <summary>
        /// Analyze a object creation reference (for example, a constructor).
        /// </summary>
        /// <param name="context">An instance of <see cref="OperationAnalysisContext"/>.</param>
        private static void AnalyzeObjectCreationReference(OperationAnalysisContext context)
        {
            if (!(context.Operation is IObjectCreationOperation objectCreationOperation))
            {
                return;
            }

            PerformInspection(context, objectCreationOperation.Constructor, null);
        }

        /// <summary>
        /// Analyze a property reference (for example, a property getter or setter).
        /// </summary>
        /// <param name="context">An instance of <see cref="OperationAnalysisContext"/>.</param>
        private static void AnalyzePropertyReference(OperationAnalysisContext context)
        {
            if (!(context.Operation is IPropertyReferenceOperation propertyReferenceOperation))
            {
                return;
            }

            var propertyReferenceType = GetPropertyReferenceType(propertyReferenceOperation.Syntax);

            PerformInspection(context, propertyReferenceOperation.Property, propertyReferenceType);
        }

        /// <summary>
        /// Analyze a method invocation.
        /// We only care about operations that are instances of <see cref="IInvocationOperation"/>.
        /// </summary>
        /// <param name="context">An instance of <see cref="OperationAnalysisContext"/>.</param>
        private static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            if (!(context.Operation is IInvocationOperation invocationOperation))
            {
                return;
            }

            PerformInspection(context, invocationOperation.TargetMethod, null);
        }

        /// <summary>
        /// Perform the inspection on a target method or property and throw our error if we are calling a method or property
        /// with one or more companion types from a non-companion type.
        /// </summary>
        /// <param name="context">An instance of <see cref="OperationAnalysisContext"/>.</param>
        /// <param name="targetSymbol">An instance of <see cref="ISymbol"/> representing the target method.</param>
        /// <param name="propertyReferenceType">If the target symbol is a property, this parameter will specify whether it is a getter or a setter (or both).</param>
        private static void PerformInspection(
            OperationAnalysisContext context,
            ISymbol targetSymbol,
            PropertyReferenceType? propertyReferenceType)
        {
            var allCompanionTypeSymbols = GetCompanionTypeSymbolsFromSymbol(
                context,
                targetSymbol,
                propertyReferenceType);

            // If our target method does not have any companion types, it is truly public so we don't need to do anything.
            if (!allCompanionTypeSymbols.Any())
            {
                return;
            }

            // Since we know that there is at least one companion type, we want to restrict any calls that aren't from within a type.
            if (!(context.ContainingSymbol?.ContainingType is ITypeSymbol callerTypeSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    CallerNotCompanion,
                    context.Operation.Syntax.GetLocation()));

                return;
            }

            // If we are calling the target method from the same type that it is defined in, or if we are calling from
            // a companion type then we are fine.
            if (targetSymbol?.ContainingType?.Equals(callerTypeSymbol) == true
                || allCompanionTypeSymbols.Any(companionTypeSymbol =>
                {
                    if (companionTypeSymbol.TypeSymbol != null)
                    {
                        return companionTypeSymbol.TypeSymbol.Equals(callerTypeSymbol);
                    }

                    var callerTypeSymbolFullyQualifiedName = GetFullyQualifiedTypeName(callerTypeSymbol);

                    return !string.IsNullOrWhiteSpace(callerTypeSymbolFullyQualifiedName)
                           && companionTypeSymbol.FullyQualifiedName.Equals(callerTypeSymbolFullyQualifiedName);
                }))
            {
                return;
            }

            // Otherwise, we must be trying to call it from a non-companion type, so throw an error.
            context.ReportDiagnostic(Diagnostic.Create(
                CallerNotCompanion,
                context.Operation.Syntax.GetLocation()));
        }

        /// <summary>
        /// Get the companion types from a method or property symbol by grabbing the target attributes and the class
        /// attributes, combining them, and filtering down on the CompanionTypeAttributes. Then look at the constructor
        /// to locate the type symbol that they refer to, and build a list.
        /// </summary>
        /// <param name="context">An instance of <see cref="OperationAnalysisContext"/>.</param>
        /// <param name="targetSymbol">An instance of <see cref="ISymbol"/> representing the target method or property.</param>
        /// <param name="propertyReferenceType">If the target symbol is a property, this parameter will specify whether it is a getter or a setter (or both).</param>
        /// <returns></returns>
        private static List<CompanionTypeSymbol> GetCompanionTypeSymbolsFromSymbol(
            OperationAnalysisContext context,
            ISymbol targetSymbol,
            PropertyReferenceType? propertyReferenceType)
        {
            const string classNamespacePrefix = "RealGoodApps.Companion.Attributes.";

            var companionTypeAttribute = context.Compilation
                .GetTypeByMetadataName($"{classNamespacePrefix}CompanionTypeAttribute");

            var companionTypeGetterAttribute = context.Compilation
                .GetTypeByMetadataName($"{classNamespacePrefix}CompanionTypeGetterAttribute");

            var companionTypeSetterAttribute = context.Compilation
                .GetTypeByMetadataName($"{classNamespacePrefix}CompanionTypeSetterAttribute");

            return targetSymbol
                .GetAttributes()
                .Concat(targetSymbol.ContainingType?.GetAttributes() ?? Enumerable.Empty<AttributeData>())
                .Where(attribute =>
                {
                    if (attribute.ConstructorArguments.Length <= 0)
                    {
                        return false;
                    }

                    if (attribute.AttributeClass.Equals(companionTypeAttribute))
                    {
                        return true;
                    }

                    if ((propertyReferenceType == PropertyReferenceType.Get ||
                        propertyReferenceType == PropertyReferenceType.GetAndSet) &&
                        attribute.AttributeClass.Equals(companionTypeGetterAttribute))
                    {
                        return true;
                    }

                    if ((propertyReferenceType == PropertyReferenceType.Set ||
                        propertyReferenceType == PropertyReferenceType.GetAndSet) &&
                        attribute.AttributeClass.Equals(companionTypeSetterAttribute))
                    {
                        return true;
                    }

                    return false;
                })
                .Select(attribute =>
                {
                    var constructorParameterValue = attribute.ConstructorArguments.FirstOrDefault().Value;

                    switch (constructorParameterValue)
                    {
                        case ITypeSymbol typeSymbol:
                            return new CompanionTypeSymbol(
                                null,
                                typeSymbol);
                        case string fullyQualifiedName:
                            return new CompanionTypeSymbol(
                                fullyQualifiedName,
                                null);
                        default:
                            return null;
                    }
                })
                .Where(companionTypeSymbol => companionTypeSymbol != null && (companionTypeSymbol.TypeSymbol != null || !string.IsNullOrWhiteSpace(companionTypeSymbol.FullyQualifiedName)))
                .ToList();
        }

        private class CompanionTypeSymbol
        {
            public CompanionTypeSymbol(string fullyQualifiedName, ITypeSymbol typeSymbol)
            {
                FullyQualifiedName = fullyQualifiedName;
                TypeSymbol = typeSymbol;
            }

            public string FullyQualifiedName { get; }

            public ITypeSymbol TypeSymbol { get; }
        }

        /// <summary>
        /// If we have a property reference, we need to be able to quickly determine if it is a getter or a setter.
        /// Credit goes to: https://github.com/dotnet/roslyn/issues/15527#issuecomment-455799409.
        /// </summary>
        /// <param name="node">The syntax node being analyzed.</param>
        /// <returns>An enumeration of <see cref="PropertyReferenceType"/>.</returns>
        private static PropertyReferenceType GetPropertyReferenceType(SyntaxNode node)
        {
            var kind = node.Parent.Kind();

            if (kind == SyntaxKind.PostIncrementExpression ||
                kind == SyntaxKind.PostDecrementExpression ||
                kind == SyntaxKind.PreIncrementExpression ||
                kind == SyntaxKind.PreDecrementExpression)
            {
                return PropertyReferenceType.GetAndSet;
            }

            if (node.Parent is AssignmentExpressionSyntax assignment)
            {
                if (assignment.Left == node)
                {
                    return kind == SyntaxKind.SimpleAssignmentExpression
                        ? PropertyReferenceType.Set
                        : PropertyReferenceType.GetAndSet;
                }
            }
            else if (node.Parent is MemberAccessExpressionSyntax m)
            {
                if (m.Name == node)
                {
                    return GetPropertyReferenceType(node.Parent);
                }
            }

            return PropertyReferenceType.Get;
        }

        /// <summary>
        /// If we want to get the fully qualified name of a type symbol, this method can quickly determine it.
        /// For example, if the type symbol was a class named Bar inside a namespace of RealGoodApps.Foo, this
        /// method would return "RealGoodApps.Foo.Bar".
        /// Credit goes to: https://stackoverflow.com/a/23314956.
        /// </summary>
        /// <param name="typeSymbol">An instance of <see cref="ITypeSymbol" />.</param>
        /// <returns>The fully qualified name of the type symbol.</returns>
        private static string GetFullyQualifiedTypeName(ISymbol typeSymbol)
        {
            if (typeSymbol == null)
            {
                return null;
            }

            var symbolDisplayFormat = new SymbolDisplayFormat(
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

            var fullyQualifiedName = typeSymbol.ToDisplayString(symbolDisplayFormat);

            return fullyQualifiedName;
        }

        /// <summary>
        /// When working with a property reference, this enumeration allows us to quickly identify if we are working with a
        /// getter or a setter (or both).
        /// </summary>
        private enum PropertyReferenceType
        {
            Get,
            Set,
            GetAndSet
        }
    }
}
