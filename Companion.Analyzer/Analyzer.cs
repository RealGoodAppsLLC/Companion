using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
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

            PerformInspection(context, methodReferenceOperation.Method);
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

            PerformInspection(context, invocationOperation.TargetMethod);
        }

        /// <summary>
        /// Perform the inspection on a target method and throw our error if we are calling a method with one or more
        /// companion types from a non-companion type.
        /// </summary>
        /// <param name="context">An instance of <see cref="OperationAnalysisContext"/>.</param>
        /// <param name="targetMethodSymbol">An instance of <see cref="ISymbol"/> representing the target method.</param>
        private static void PerformInspection(
            OperationAnalysisContext context,
            ISymbol targetMethodSymbol)
        {
            var allCompanionTypeSymbols = GetCompanionTypeSymbolsFromMethodSymbol(
                context,
                targetMethodSymbol);

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
            if (targetMethodSymbol?.ContainingType?.Equals(callerTypeSymbol) == true
                || allCompanionTypeSymbols.Any(companionTypeSymbol => companionTypeSymbol.Equals(callerTypeSymbol)))
            {
                return;
            }

            // Otherwise, we must be trying to call it from a non-companion type, so throw an error.
            context.ReportDiagnostic(Diagnostic.Create(
                CallerNotCompanion,
                context.Operation.Syntax.GetLocation()));
        }

        /// <summary>
        /// Get the companion types from a method symbol by grabbing the target method attributes and the class
        /// attributes, combining them, and filtering down on the CompanionTypeAttributes. Then look at the constructor
        /// to locate the type symbol that they refer to, and build a list.
        /// </summary>
        /// <param name="context">An instance of <see cref="OperationAnalysisContext"/>.</param>
        /// <param name="targetMethodSymbol">An instance of <see cref="ISymbol"/> representing the target method.</param>
        /// <returns></returns>
        private static List<ITypeSymbol> GetCompanionTypeSymbolsFromMethodSymbol(
            OperationAnalysisContext context,
            ISymbol targetMethodSymbol)
        {
            var companionTypeAttribute = context.Compilation
                .GetTypeByMetadataName("RealGoodApps.Companion.Attributes.CompanionTypeAttribute");

            return targetMethodSymbol
                .GetAttributes()
                .Concat(targetMethodSymbol.ContainingType?.GetAttributes() ?? Enumerable.Empty<AttributeData>())
                .Where(attribute => attribute.AttributeClass.Equals(companionTypeAttribute)
                                    && attribute.ConstructorArguments.Length > 0)
                .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as ITypeSymbol)
                .Where(typeSymbol => typeSymbol != null)
                .ToList();
        }
    }
}
