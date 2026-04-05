using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using QwenCode.App.Compatibility;
using QwenCode.App.Models;

namespace QwenCode.App.Tools;

/// <summary>
/// Represents the Roslyn Lsp Tool Service
/// </summary>
public sealed class RoslynLspToolService : ILspToolService
{
    private static readonly string[] IgnoredDirectories = [".git", "node_modules", "bin", "obj", ".electron", "dist"];
    private static readonly IReadOnlyList<MetadataReference> MetadataReferences = BuildMetadataReferences();

    /// <summary>
    /// Executes async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="approvalState">The approval state</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to native tool execution result</returns>
    public async Task<NativeToolExecutionResult> ExecuteAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken = default)
    {
        if (runtimeProfile.FolderTrustEnabled && !runtimeProfile.IsWorkspaceTrusted)
        {
            return Error("Workspace is not trusted. LSP operations are unavailable.", runtimeProfile.ProjectRoot, approvalState);
        }

        var operation = TryGetRequiredString(arguments, "operation");
        if (string.IsNullOrWhiteSpace(operation))
        {
            return Error("Parameter 'operation' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        try
        {
            var context = await BuildContextAsync(runtimeProfile.ProjectRoot, cancellationToken);
            return operation switch
            {
                "documentSymbol" => await ExecuteDocumentSymbolAsync(context, runtimeProfile.ProjectRoot, arguments, approvalState, cancellationToken),
                "workspaceSymbol" => ExecuteWorkspaceSymbol(context, runtimeProfile.ProjectRoot, arguments, approvalState),
                "hover" => await ExecuteHoverAsync(context, runtimeProfile.ProjectRoot, arguments, approvalState, cancellationToken),
                "goToDefinition" => await ExecuteDefinitionAsync(context, runtimeProfile.ProjectRoot, arguments, approvalState, cancellationToken),
                "goToImplementation" => await ExecuteImplementationAsync(context, runtimeProfile.ProjectRoot, arguments, approvalState, cancellationToken),
                "findReferences" => await ExecuteReferencesAsync(context, runtimeProfile.ProjectRoot, arguments, approvalState, cancellationToken),
                "diagnostics" => await ExecuteDiagnosticsAsync(context, runtimeProfile.ProjectRoot, arguments, approvalState, cancellationToken),
                "workspaceDiagnostics" => await ExecuteWorkspaceDiagnosticsAsync(context, runtimeProfile.ProjectRoot, approvalState, cancellationToken),
                "prepareCallHierarchy" => await ExecutePrepareCallHierarchyAsync(context, runtimeProfile.ProjectRoot, arguments, approvalState, cancellationToken),
                "incomingCalls" => await ExecuteIncomingCallsAsync(context, runtimeProfile.ProjectRoot, arguments, approvalState, cancellationToken),
                "outgoingCalls" => await ExecuteOutgoingCallsAsync(context, runtimeProfile.ProjectRoot, arguments, approvalState, cancellationToken),
                "codeActions" => await ExecuteCodeActionsAsync(context, runtimeProfile.ProjectRoot, arguments, approvalState, cancellationToken),
                _ => Error($"Unsupported LSP operation: {operation}", runtimeProfile.ProjectRoot, approvalState)
            };
        }
        catch (Exception exception)
        {
            return Error($"LSP {operation} failed: {exception.Message}", runtimeProfile.ProjectRoot, approvalState);
        }
    }

    private static async Task<NativeToolExecutionResult> ExecuteDocumentSymbolAsync(
        RoslynWorkspaceContext context,
        string workspaceRoot,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var filePath = ResolveRequiredFilePath(arguments, workspaceRoot, "documentSymbol");
        var document = context.FindDocument(filePath)
            ?? throw new InvalidOperationException("File is not part of the native Roslyn workspace.");
        var root = await document.GetSyntaxRootAsync(cancellationToken)
            ?? throw new InvalidOperationException("Failed to parse the requested source file.");
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken)
            ?? throw new InvalidOperationException("Failed to create a semantic model for the requested source file.");

        var symbols = root.DescendantNodes()
            .Select(node => semanticModel.GetDeclaredSymbol(node, cancellationToken))
            .Where(static symbol => symbol is not null)
            .Where(static symbol => symbol!.Locations.Any(static location => location.IsInSource))
            .Distinct(SymbolEqualityComparer.Default)
            .OrderBy(symbol => symbol!.Locations.First(static location => location.IsInSource).GetLineSpan().StartLinePosition.Line)
            .Take(Math.Max(1, TryGetInt(arguments, "limit") ?? 100))
            .Select(symbol => FormatSymbolLine(symbol!, workspaceRoot))
            .ToArray();

        if (symbols.Length == 0)
        {
            return Success("No document symbols found.", filePath, approvalState);
        }

        return Success($"Document symbols for {GetRelativePath(workspaceRoot, filePath)}:{Environment.NewLine}{string.Join(Environment.NewLine, symbols)}", filePath, approvalState);
    }

    private static NativeToolExecutionResult ExecuteWorkspaceSymbol(
        RoslynWorkspaceContext context,
        string workspaceRoot,
        JsonElement arguments,
        string approvalState)
    {
        var query = TryGetRequiredString(arguments, "query");
        if (string.IsNullOrWhiteSpace(query))
        {
            return Error("query is required for workspaceSymbol.", workspaceRoot, approvalState);
        }

        var limit = Math.Max(1, TryGetInt(arguments, "limit") ?? 50);
        var matches = context.AllDeclaredSymbols
            .Where(symbol => symbol.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(symbol => FormatSymbolLine(symbol, workspaceRoot))
            .ToArray();

        if (matches.Length == 0)
        {
            return Success($"No workspace symbols found for \"{query}\".", workspaceRoot, approvalState);
        }

        return Success($"Workspace symbols for \"{query}\":{Environment.NewLine}{string.Join(Environment.NewLine, matches)}", workspaceRoot, approvalState);
    }

    private static async Task<NativeToolExecutionResult> ExecuteHoverAsync(
        RoslynWorkspaceContext context,
        string workspaceRoot,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var target = await ResolveSymbolAsync(context, workspaceRoot, arguments, "hover", cancellationToken);
        if (target.ErrorMessage is not null)
        {
            return Error(target.ErrorMessage, target.WorkingDirectory, approvalState);
        }

        var symbol = target.Symbol!;
        var documentation = symbol.GetDocumentationCommentXml();
        var builder = new StringBuilder();
        builder.AppendLine(symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        builder.AppendLine($"Kind: {symbol.Kind}");

        var location = symbol.Locations.FirstOrDefault(static item => item.IsInSource);
        if (location is not null)
        {
            builder.AppendLine($"Location: {FormatLocation(location, workspaceRoot)}");
        }

        if (!string.IsNullOrWhiteSpace(documentation))
        {
            builder.AppendLine();
            builder.AppendLine(documentation.Trim());
        }

        return Success(builder.ToString().Trim(), target.WorkingDirectory, approvalState);
    }

    private static async Task<NativeToolExecutionResult> ExecuteDefinitionAsync(
        RoslynWorkspaceContext context,
        string workspaceRoot,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var target = await ResolveSymbolAsync(context, workspaceRoot, arguments, "goToDefinition", cancellationToken);
        if (target.ErrorMessage is not null)
        {
            return Error(target.ErrorMessage, target.WorkingDirectory, approvalState);
        }

        var definitions = target.Symbol!.Locations
            .Where(static location => location.IsInSource)
            .Select(location => FormatLocation(location, workspaceRoot))
            .Distinct(StringComparer.Ordinal)
            .Take(Math.Max(1, TryGetInt(arguments, "limit") ?? 20))
            .ToArray();

        if (definitions.Length == 0)
        {
            return Success("No definitions found for the requested symbol.", target.WorkingDirectory, approvalState);
        }

        return Success($"Definitions:{Environment.NewLine}{string.Join(Environment.NewLine, definitions.Select((item, index) => $"{index + 1}. {item}"))}", target.WorkingDirectory, approvalState);
    }

    private static async Task<NativeToolExecutionResult> ExecuteImplementationAsync(
        RoslynWorkspaceContext context,
        string workspaceRoot,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var target = await ResolveSymbolAsync(context, workspaceRoot, arguments, "goToImplementation", cancellationToken);
        if (target.ErrorMessage is not null)
        {
            return Error(target.ErrorMessage, target.WorkingDirectory, approvalState);
        }

        var implementations = await SymbolFinder.FindImplementationsAsync(
            target.Symbol!,
            context.Solution,
            cancellationToken: cancellationToken);
        var lines = implementations
            .SelectMany(static symbol => symbol.Locations.Where(static location => location.IsInSource))
            .Select(location => FormatLocation(location, workspaceRoot))
            .Distinct(StringComparer.Ordinal)
            .Take(Math.Max(1, TryGetInt(arguments, "limit") ?? 20))
            .ToArray();

        if (lines.Length == 0)
        {
            return Success("No implementations found for the requested symbol.", target.WorkingDirectory, approvalState);
        }

        return Success($"Implementations:{Environment.NewLine}{string.Join(Environment.NewLine, lines.Select((item, index) => $"{index + 1}. {item}"))}", target.WorkingDirectory, approvalState);
    }

    private static async Task<NativeToolExecutionResult> ExecuteReferencesAsync(
        RoslynWorkspaceContext context,
        string workspaceRoot,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var target = await ResolveSymbolAsync(context, workspaceRoot, arguments, "findReferences", cancellationToken);
        if (target.ErrorMessage is not null)
        {
            return Error(target.ErrorMessage, target.WorkingDirectory, approvalState);
        }

        var includeDeclaration = TryGetBool(arguments, "includeDeclaration") ?? false;
        var limit = Math.Max(1, TryGetInt(arguments, "limit") ?? 50);
        var references = await SymbolFinder.FindReferencesAsync(
            target.Symbol!,
            context.Solution,
            cancellationToken: cancellationToken);

        var lines = new List<string>();
        foreach (var reference in references)
        {
            if (includeDeclaration)
            {
                foreach (var definition in reference.Definition.Locations.Where(static location => location.IsInSource))
                {
                    lines.Add($"definition: {FormatLocation(definition, workspaceRoot)}");
                    if (lines.Count >= limit)
                    {
                        return Success($"References:{Environment.NewLine}{string.Join(Environment.NewLine, lines.Select((item, index) => $"{index + 1}. {item}"))}", target.WorkingDirectory, approvalState);
                    }
                }
            }

            foreach (var location in reference.Locations)
            {
                lines.Add(FormatReferenceLocation(location.Location, workspaceRoot));
                if (lines.Count >= limit)
                {
                    return Success($"References:{Environment.NewLine}{string.Join(Environment.NewLine, lines.Select((item, index) => $"{index + 1}. {item}"))}", target.WorkingDirectory, approvalState);
                }
            }
        }

        if (lines.Count == 0)
        {
            return Success("No references found for the requested symbol.", target.WorkingDirectory, approvalState);
        }

        return Success($"References:{Environment.NewLine}{string.Join(Environment.NewLine, lines.Select((item, index) => $"{index + 1}. {item}"))}", target.WorkingDirectory, approvalState);
    }

    private static async Task<NativeToolExecutionResult> ExecuteDiagnosticsAsync(
        RoslynWorkspaceContext context,
        string workspaceRoot,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var filePath = ResolveRequiredFilePath(arguments, workspaceRoot, "diagnostics");
        var document = context.FindDocument(filePath)
            ?? throw new InvalidOperationException("File is not part of the native Roslyn workspace.");
        var diagnostics = await CollectDiagnosticsAsync(context.Project, cancellationToken);
        var lines = diagnostics
            .Where(item => string.Equals(item.Location.GetLineSpan().Path, filePath, StringComparison.OrdinalIgnoreCase))
            .Select(diagnostic => FormatDiagnostic(diagnostic, workspaceRoot))
            .ToArray();

        if (lines.Length == 0)
        {
            return Success($"No diagnostics for {GetRelativePath(workspaceRoot, filePath)}.", filePath, approvalState);
        }

        return Success($"Diagnostics for {GetRelativePath(workspaceRoot, filePath)}:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}", filePath, approvalState);
    }

    private static async Task<NativeToolExecutionResult> ExecuteWorkspaceDiagnosticsAsync(
        RoslynWorkspaceContext context,
        string workspaceRoot,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var diagnostics = await CollectDiagnosticsAsync(context.Project, cancellationToken);
        var lines = diagnostics
            .Where(static diagnostic => diagnostic.Location.IsInSource)
            .Take(100)
            .Select(diagnostic => FormatDiagnostic(diagnostic, workspaceRoot))
            .ToArray();

        if (lines.Length == 0)
        {
            return Success("No workspace diagnostics found.", workspaceRoot, approvalState);
        }

        return Success($"Workspace diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}", workspaceRoot, approvalState);
    }

    private static async Task<NativeToolExecutionResult> ExecutePrepareCallHierarchyAsync(
        RoslynWorkspaceContext context,
        string workspaceRoot,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var target = await ResolveSymbolAsync(context, workspaceRoot, arguments, "prepareCallHierarchy", cancellationToken);
        if (target.ErrorMessage is not null)
        {
            return Error(target.ErrorMessage, target.WorkingDirectory, approvalState);
        }

        var symbol = target.Symbol!;
        var builder = new StringBuilder();
        builder.AppendLine("Call hierarchy item:");
        builder.AppendLine($"Name: {symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}");
        builder.AppendLine($"Kind: {symbol.Kind}");

        if (symbol.ContainingType is not null)
        {
            builder.AppendLine($"Container: {symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}");
        }
        else if (symbol.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace)
        {
            builder.AppendLine($"Container: {containingNamespace.ToDisplayString()}");
        }

        var sourceLocations = symbol.Locations
            .Where(static location => location.IsInSource)
            .Select(location => FormatLocation(location, workspaceRoot))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (sourceLocations.Length > 0)
        {
            builder.AppendLine($"Location: {sourceLocations[0]}");
        }

        if (symbol is IMethodSymbol { ContainingType.TypeKind: TypeKind.Interface } interfaceMethod)
        {
            var implementations = await SymbolFinder.FindImplementationsAsync(interfaceMethod, context.Solution, cancellationToken: cancellationToken);
            var implementationLines = implementations
                .SelectMany(static implementation => implementation.Locations.Where(static location => location.IsInSource))
                .Select(location => FormatLocation(location, workspaceRoot))
                .Distinct(StringComparer.Ordinal)
                .Take(10)
                .ToArray();

            if (implementationLines.Length > 0)
            {
                builder.AppendLine("Implementations:");
                foreach (var line in implementationLines)
                {
                    builder.AppendLine($"- {line}");
                }
            }
        }

        return Success(builder.ToString().Trim(), target.WorkingDirectory, approvalState);
    }

    private static async Task<NativeToolExecutionResult> ExecuteIncomingCallsAsync(
        RoslynWorkspaceContext context,
        string workspaceRoot,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var target = await ResolveSymbolAsync(context, workspaceRoot, arguments, "incomingCalls", cancellationToken);
        if (target.ErrorMessage is not null)
        {
            return Error(target.ErrorMessage, target.WorkingDirectory, approvalState);
        }

        var limit = Math.Max(1, TryGetInt(arguments, "limit") ?? 20);
        var callers = await FindIncomingCallersAsync(context, target.Symbol!, workspaceRoot, cancellationToken);
        if (callers.Count == 0)
        {
            return Success("No incoming calls found for the requested symbol.", target.WorkingDirectory, approvalState);
        }

        var lines = callers
            .Take(limit)
            .Select((entry, index) => $"{index + 1}. {FormatCallHierarchyEntry(entry)}")
            .ToArray();

        return Success($"Incoming calls:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}", target.WorkingDirectory, approvalState);
    }

    private static async Task<NativeToolExecutionResult> ExecuteOutgoingCallsAsync(
        RoslynWorkspaceContext context,
        string workspaceRoot,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var target = await ResolveSymbolAsync(context, workspaceRoot, arguments, "outgoingCalls", cancellationToken);
        if (target.ErrorMessage is not null)
        {
            return Error(target.ErrorMessage, target.WorkingDirectory, approvalState);
        }

        var limit = Math.Max(1, TryGetInt(arguments, "limit") ?? 20);
        var callees = await FindOutgoingCallsAsync(context, target.Symbol!, workspaceRoot, cancellationToken);
        if (callees.Count == 0)
        {
            return Success("No outgoing calls found for the requested symbol.", target.WorkingDirectory, approvalState);
        }

        var lines = callees
            .Take(limit)
            .Select((entry, index) => $"{index + 1}. {FormatCallHierarchyEntry(entry)}")
            .ToArray();

        return Success($"Outgoing calls:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}", target.WorkingDirectory, approvalState);
    }

    private static async Task<NativeToolExecutionResult> ExecuteCodeActionsAsync(
        RoslynWorkspaceContext context,
        string workspaceRoot,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var filePath = ResolveRequiredFilePath(arguments, workspaceRoot, "codeActions");
        var line = TryGetInt(arguments, "line");
        var character = TryGetInt(arguments, "character");
        var diagnostics = await CollectDiagnosticsAsync(context.Project, cancellationToken);

        var fileDiagnostics = diagnostics
            .Where(item => string.Equals(item.Location.GetLineSpan().Path, filePath, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var relevantDiagnostics = fileDiagnostics
            .Where(item => MatchesSelection(item, line, character))
            .ToArray();

        if (relevantDiagnostics.Length == 0)
        {
            relevantDiagnostics = fileDiagnostics
                .OrderBy(item => item.Location.GetLineSpan().StartLinePosition.Line)
                .Take(10)
                .ToArray();
        }

        if (relevantDiagnostics.Length == 0)
        {
            return Success($"No code actions available for {GetRelativePath(workspaceRoot, filePath)}.", filePath, approvalState);
        }

        var suggestions = relevantDiagnostics
            .SelectMany(diagnostic => BuildCodeActionSuggestions(diagnostic, workspaceRoot))
            .Distinct(StringComparer.Ordinal)
            .Take(20)
            .ToArray();

        if (suggestions.Length == 0)
        {
            return Success($"No code actions available for {GetRelativePath(workspaceRoot, filePath)}.", filePath, approvalState);
        }

        return Success($"Code actions:{Environment.NewLine}{string.Join(Environment.NewLine, suggestions.Select((item, index) => $"{index + 1}. {item}"))}", filePath, approvalState);
    }

    private static async Task<IReadOnlyList<Diagnostic>> CollectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken)
            ?? throw new InvalidOperationException("Failed to build a Roslyn compilation for the workspace.");
        return compilation.GetDiagnostics(cancellationToken)
            .Where(static diagnostic => diagnostic.Location.IsInSource)
            .OrderBy(diagnostic => diagnostic.Location.GetLineSpan().Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line)
            .ToArray();
    }

    private static async Task<ResolvedSymbolTarget> ResolveSymbolAsync(
        RoslynWorkspaceContext context,
        string workspaceRoot,
        JsonElement arguments,
        string operation,
        CancellationToken cancellationToken)
    {
        var filePath = ResolveRequiredFilePath(arguments, workspaceRoot, operation);
        var line = TryGetInt(arguments, "line");
        if (line is null or <= 0)
        {
            return new ResolvedSymbolTarget(filePath, null, $"line is required for {operation}.");
        }

        var document = context.FindDocument(filePath);
        if (document is null)
        {
            return new ResolvedSymbolTarget(filePath, null, "File is not part of the native Roslyn workspace.");
        }

        var sourceText = await document.GetTextAsync(cancellationToken)
            ?? throw new InvalidOperationException("Failed to load source text.");
        var lineIndex = line.Value - 1;
        if (lineIndex < 0 || lineIndex >= sourceText.Lines.Count)
        {
            return new ResolvedSymbolTarget(filePath, null, "line is outside the document range.");
        }

        var character = Math.Max(1, TryGetInt(arguments, "character") ?? 1) - 1;
        var sourceLine = sourceText.Lines[lineIndex];
        var position = Math.Min(sourceLine.End, sourceLine.Start + character);

        var root = await document.GetSyntaxRootAsync(cancellationToken)
            ?? throw new InvalidOperationException("Failed to parse the requested source file.");
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken)
            ?? throw new InvalidOperationException("Failed to create a semantic model for the requested source file.");
        var token = root.FindToken(position);
        var node = token.Parent;

        while (node is not null)
        {
            var symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken)
                ?? semanticModel.GetSymbolInfo(node, cancellationToken).Symbol
                ?? semanticModel.GetTypeInfo(node, cancellationToken).Type;
            if (symbol is not null)
            {
                return new ResolvedSymbolTarget(filePath, symbol, null);
            }

            node = node.Parent;
        }

        return new ResolvedSymbolTarget(filePath, null, "No symbol found at the requested location.");
    }

    private static async Task<IReadOnlyList<CallHierarchyEntry>> FindIncomingCallersAsync(
        RoslynWorkspaceContext context,
        ISymbol symbol,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        var references = await SymbolFinder.FindReferencesAsync(symbol, context.Solution, cancellationToken: cancellationToken);
        var groupedCallers = new Dictionary<ISymbol, HashSet<string>>(SymbolEqualityComparer.Default);

        foreach (var reference in references)
        {
            foreach (var referenceLocation in reference.Locations)
            {
                var caller = await ResolveContainingCallableSymbolAsync(context.Solution, referenceLocation.Location, cancellationToken);
                if (caller is null)
                {
                    continue;
                }

                var formattedLocation = FormatReferenceLocation(referenceLocation.Location, workspaceRoot);
                if (!groupedCallers.TryGetValue(caller, out var locations))
                {
                    locations = new HashSet<string>(StringComparer.Ordinal);
                    groupedCallers[caller] = locations;
                }

                locations.Add(formattedLocation);
            }
        }

        return groupedCallers
            .OrderBy(static item => item.Key.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static item => new CallHierarchyEntry(item.Key, item.Value.OrderBy(static location => location, StringComparer.Ordinal).ToArray()))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CallHierarchyEntry>> FindOutgoingCallsAsync(
        RoslynWorkspaceContext context,
        ISymbol symbol,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        var groupedCallees = new Dictionary<ISymbol, HashSet<string>>(SymbolEqualityComparer.Default);

        foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
        {
            var node = await syntaxReference.GetSyntaxAsync(cancellationToken);
            var document = context.Solution.GetDocument(node.SyntaxTree);
            if (document is null)
            {
                continue;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel is null)
            {
                continue;
            }

            foreach (var descendant in node.DescendantNodesAndSelf())
            {
                IOperation? operation = descendant switch
                {
                    InvocationExpressionSyntax => semanticModel.GetOperation(descendant, cancellationToken),
                    ObjectCreationExpressionSyntax => semanticModel.GetOperation(descendant, cancellationToken),
                    ImplicitObjectCreationExpressionSyntax => semanticModel.GetOperation(descendant, cancellationToken),
                    ConstructorInitializerSyntax => semanticModel.GetOperation(descendant, cancellationToken),
                    _ => null
                };

                var targetSymbol = operation switch
                {
                    IInvocationOperation invocation => (ISymbol?)invocation.TargetMethod,
                    IObjectCreationOperation objectCreation => objectCreation.Constructor is not null ? (ISymbol?)objectCreation.Constructor : objectCreation.Type,
                    _ => null as ISymbol
                };

                if (targetSymbol is null)
                {
                    continue;
                }

                var normalizedSymbol = targetSymbol.OriginalDefinition;
                var formattedLocation = FormatLocation(descendant.GetLocation(), workspaceRoot);
                if (!groupedCallees.TryGetValue(normalizedSymbol, out var locations))
                {
                    locations = new HashSet<string>(StringComparer.Ordinal);
                    groupedCallees[normalizedSymbol] = locations;
                }

                locations.Add(formattedLocation);
            }
        }

        return groupedCallees
            .OrderBy(static item => item.Key.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static item => new CallHierarchyEntry(item.Key, item.Value.OrderBy(static location => location, StringComparer.Ordinal).ToArray()))
            .ToArray();
    }

    private static async Task<ISymbol?> ResolveContainingCallableSymbolAsync(
        Solution solution,
        Location location,
        CancellationToken cancellationToken)
    {
        if (!location.IsInSource || location.SourceTree is null)
        {
            return null;
        }

        var document = solution.GetDocument(location.SourceTree);
        if (document is null)
        {
            return null;
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (root is null || semanticModel is null)
        {
            return null;
        }

        var node = root.FindToken(location.SourceSpan.Start).Parent;
        while (node is not null)
        {
            var symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
            if (symbol is IMethodSymbol or IPropertySymbol or IEventSymbol or INamedTypeSymbol)
            {
                return symbol;
            }

            node = node.Parent;
        }

        return null;
    }

    private static bool MatchesSelection(Diagnostic diagnostic, int? line, int? character)
    {
        if (line is null)
        {
            return true;
        }

        var lineSpan = diagnostic.Location.GetLineSpan();
        var requestedLine = line.Value - 1;
        if (requestedLine < lineSpan.StartLinePosition.Line || requestedLine > lineSpan.EndLinePosition.Line)
        {
            return false;
        }

        if (character is null)
        {
            return true;
        }

        var requestedCharacter = character.Value - 1;
        if (requestedLine == lineSpan.StartLinePosition.Line && requestedCharacter < lineSpan.StartLinePosition.Character)
        {
            return false;
        }

        if (requestedLine == lineSpan.EndLinePosition.Line && requestedCharacter > lineSpan.EndLinePosition.Character)
        {
            return false;
        }

        return true;
    }

    private static IReadOnlyList<string> BuildCodeActionSuggestions(Diagnostic diagnostic, string workspaceRoot)
    {
        var location = FormatDiagnosticLocation(diagnostic, workspaceRoot);
        var suggestions = new List<string>();

        foreach (var title in GetSuggestedCodeActionTitles(diagnostic))
        {
            suggestions.Add($"{title} ({diagnostic.Id}) at {location}");
        }

        return suggestions;
    }

    private static IReadOnlyList<string> GetSuggestedCodeActionTitles(Diagnostic diagnostic) =>
        diagnostic.Id switch
        {
            "CS0103" => ["Declare the missing symbol or fix the identifier name"],
            "CS0161" => ["Add a return statement for every code path"],
            "CS0234" or "CS0246" => ["Add or correct the using directive or assembly reference"],
            "CS0535" => ["Implement the missing interface members"],
            "CS1002" => ["Insert the missing semicolon"],
            "CS1501" or "CS1503" or "CS7036" => ["Adjust the arguments to match an available overload"],
            "CS1525" => ["Fix the invalid expression near this location"],
            "CS8618" => ["Initialize the non-nullable member or mark it as nullable"],
            _ => [$"Review and fix diagnostic: {diagnostic.GetMessage()}"]
        };

    private static string FormatDiagnosticLocation(Diagnostic diagnostic, string workspaceRoot)
    {
        var lineSpan = diagnostic.Location.GetLineSpan();
        return $"{GetRelativePath(workspaceRoot, lineSpan.Path)}:{lineSpan.StartLinePosition.Line + 1}:{lineSpan.StartLinePosition.Character + 1}";
    }

    private static string FormatCallHierarchyEntry(CallHierarchyEntry entry)
    {
        var location = entry.Locations[0];
        var suffix = entry.Locations.Count > 1 ? $" (+{entry.Locations.Count - 1} more)" : string.Empty;
        return $"{entry.Symbol.Kind}: {entry.Symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} at {location}{suffix}";
    }

    private static string ResolveRequiredFilePath(JsonElement arguments, string workspaceRoot, string operation)
    {
        var filePath = TryGetRequiredString(arguments, "filePath");
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException($"filePath is required for {operation}.");
        }

        var resolved = Path.IsPathRooted(filePath)
            ? Path.GetFullPath(filePath)
            : Path.GetFullPath(Path.Combine(workspaceRoot, filePath));
        if (!File.Exists(resolved))
        {
            throw new InvalidOperationException("Requested source file does not exist.");
        }

        if (!string.Equals(Path.GetExtension(resolved), ".cs", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The native C# LSP host currently supports only .cs files.");
        }

        return resolved;
    }

    private static async Task<RoslynWorkspaceContext> BuildContextAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        var filePaths = Directory.EnumerateFiles(workspaceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !IsIgnoredPath(path))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId("native-lsp");
        var solution = workspace.CurrentSolution.AddProject(
            ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "native-lsp",
                "native-lsp",
                LanguageNames.CSharp,
                metadataReferences: MetadataReferences,
                parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));

        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var documentId = DocumentId.CreateNewId(projectId, debugName: filePath);
            var sourceText = SourceText.From(await File.ReadAllTextAsync(filePath, cancellationToken), Encoding.UTF8);
            solution = solution.AddDocument(documentId, Path.GetFileName(filePath), sourceText, filePath: filePath);
        }

        workspace.TryApplyChanges(solution);
        var project = workspace.CurrentSolution.GetProject(projectId)
            ?? throw new InvalidOperationException("Failed to initialize the Roslyn LSP workspace.");
        var declaredSymbols = await CollectDeclaredSymbolsAsync(project, cancellationToken);
        return new RoslynWorkspaceContext(workspace, project, declaredSymbols);
    }

    private static async Task<IReadOnlyList<ISymbol>> CollectDeclaredSymbolsAsync(Project project, CancellationToken cancellationToken)
    {
        var symbols = new List<ISymbol>();
        foreach (var document in project.Documents)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (root is null || semanticModel is null)
            {
                continue;
            }

            foreach (var node in root.DescendantNodes())
            {
                var symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
                if (symbol is not null && symbol.Locations.Any(static location => location.IsInSource))
                {
                    symbols.Add(symbol);
                }
            }
        }

        return symbols
            .Distinct(SymbolEqualityComparer.Default)
            .OrderBy(static symbol => symbol.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<MetadataReference> BuildMetadataReferences()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            return
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Task).Assembly.Location)
            ];
        }

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToArray();
    }

    private static bool IsIgnoredPath(string path)
    {
        var directory = Path.GetDirectoryName(path);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var name = Path.GetFileName(directory);
            if (IgnoredDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return false;
    }

    private static string FormatSymbolLine(ISymbol symbol, string workspaceRoot)
    {
        var location = symbol.Locations.FirstOrDefault(static item => item.IsInSource);
        var line = location is null ? string.Empty : $" at {FormatLocation(location, workspaceRoot)}";
        return $"{symbol.Kind}: {symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}{line}";
    }

    private static string FormatLocation(Location location, string workspaceRoot)
    {
        var lineSpan = location.GetLineSpan();
        var relativePath = GetRelativePath(workspaceRoot, lineSpan.Path);
        var line = lineSpan.StartLinePosition.Line + 1;
        var character = lineSpan.StartLinePosition.Character + 1;
        return $"{relativePath}:{line}:{character}";
    }

    private static string FormatReferenceLocation(Location location, string workspaceRoot)
    {
        var lineSpan = location.GetLineSpan();
        var relativePath = GetRelativePath(workspaceRoot, lineSpan.Path);
        var line = lineSpan.StartLinePosition.Line + 1;
        var character = lineSpan.StartLinePosition.Character + 1;
        return $"{relativePath}:{line}:{character}";
    }

    private static string FormatDiagnostic(Diagnostic diagnostic, string workspaceRoot)
    {
        var lineSpan = diagnostic.Location.GetLineSpan();
        var relativePath = GetRelativePath(workspaceRoot, lineSpan.Path);
        var line = lineSpan.StartLinePosition.Line + 1;
        var character = lineSpan.StartLinePosition.Character + 1;
        return $"{diagnostic.Severity}: {diagnostic.Id} at {relativePath}:{line}:{character} - {diagnostic.GetMessage()}";
    }

    private static string GetRelativePath(string workspaceRoot, string path)
    {
        try
        {
            return Path.GetRelativePath(workspaceRoot, path).Replace('\\', '/');
        }
        catch
        {
            return path.Replace('\\', '/');
        }
    }

    private static string? TryGetRequiredString(JsonElement arguments, string propertyName) =>
        arguments.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? TryGetInt(JsonElement arguments, string propertyName) =>
        arguments.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : null;

    private static bool? TryGetBool(JsonElement arguments, string propertyName) =>
        arguments.TryGetProperty(propertyName, out var property) && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : null;

    private static NativeToolExecutionResult Success(string output, string workingDirectory, string approvalState) =>
        new()
        {
            ToolName = "lsp",
            Status = "completed",
            ApprovalState = approvalState,
            WorkingDirectory = workingDirectory,
            Output = output,
            ChangedFiles = []
        };

    private static NativeToolExecutionResult Error(string message, string workingDirectory, string approvalState) =>
        new()
        {
            ToolName = "lsp",
            Status = "error",
            ApprovalState = approvalState,
            WorkingDirectory = workingDirectory,
            ErrorMessage = message,
            ChangedFiles = []
        };

    private sealed record RoslynWorkspaceContext(
        AdhocWorkspace Workspace,
        Project Project,
        IReadOnlyList<ISymbol> AllDeclaredSymbols)
    {
        /// <summary>
        /// Gets the solution
        /// </summary>
        public Solution Solution => Workspace.CurrentSolution;

        /// <summary>
        /// Executes find document
        /// </summary>
        /// <param name="filePath">The file path</param>
        /// <returns>The resulting document?</returns>
        public Document? FindDocument(string filePath) =>
            Project.Documents.FirstOrDefault(document =>
                string.Equals(document.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record ResolvedSymbolTarget(
        string WorkingDirectory,
        ISymbol? Symbol,
        string? ErrorMessage);

    private sealed record CallHierarchyEntry(
        ISymbol Symbol,
        IReadOnlyList<string> Locations);
}
