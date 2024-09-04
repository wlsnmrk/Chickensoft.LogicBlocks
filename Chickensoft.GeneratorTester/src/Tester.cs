namespace Chickensoft.GeneratorTester;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public readonly record struct GeneratorOutput(
  IDictionary<string, string> Outputs,
  ImmutableArray<Diagnostic> Diagnostics
);

public static class TestStringExtensions {
  public static string Clean(this string text) => string.Join(
    "\n",
    text.NormalizeLineEndings("\n").Split('\n').Select(line => $"// {line}")
  ).NormalizeLineEndings();
}

public static class Tester {
  public static SemanticModel GetSemanticModel(string code) {
    var tree = CSharpSyntaxTree.ParseText(code);
    var root = tree.GetRoot();

    return CSharpCompilation
      .Create("AssemblyName")
      .AddSyntaxTrees(tree)
      .GetSemanticModel(tree);
  }

  public static GeneratorOutput Generate(
    this IIncrementalGenerator generator, params string[] sources
  ) {
    var syntaxTrees = sources.Select(
      source => CSharpSyntaxTree.ParseText(source)
    );

    var references = AppDomain.CurrentDomain.GetAssemblies()
      .Where(assembly => !assembly.IsDynamic)
      .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
      .Cast<MetadataReference>();

    var compilation = CSharpCompilation.Create(
      assemblyName: "SourceGeneratorTests",
      syntaxTrees: syntaxTrees,
      references: references,
      options: new CSharpCompilationOptions(
        OutputKind.DynamicallyLinkedLibrary
      )
    );

    CSharpGeneratorDriver.Create(generator)
      .RunGeneratorsAndUpdateCompilation(
        compilation,
        out var outputCompilation,
        out var diagnostics
      );

    var outputs = new Dictionary<string, string>();
    foreach (var output in outputCompilation.SyntaxTrees) {
      var text = output.ToString();
      if (text is not null && !sources.Contains(text)) {
        outputs.Add(Path.GetFileName(output.FilePath), text);
      }
    }

    return new GeneratorOutput(
      Outputs: outputs.ToImmutableDictionary(), Diagnostics: diagnostics
    );
  }

  /// <summary>
  /// Parses the given code and returns the first node of the given type within
  /// the syntax tree.
  /// </summary>
  /// <param name="code">Source code string.</param>
  /// <typeparam name="T">Type of the node to find in the tree.</typeparam>
  /// <returns>First matching node within the tree of type
  /// <typeparamref name="T" />.</returns>
  public static T Parse<T>(string code) where T : SyntaxNode
    => (T)CSharpSyntaxTree
      .ParseText(code)
      .GetRoot()
      .DescendantNodes()
      .First(node => node is T);

  public static string LoadFixture(
    string relativePathInProject,
    [CallerFilePath] string? callerFilePath = null
  ) => File.ReadAllText(
    Path.Join(
      Path.GetDirectoryName(callerFilePath),
      relativePathInProject
    )
  );

  public static string CurrentDir(
    string relativePathInProject,
    [CallerFilePath] string? callerFilePath = null
  ) => Path.GetFullPath(Path.Join(
    Path.GetDirectoryName(callerFilePath),
    relativePathInProject
  ));
}

public static class StringExtensions {
  public static string NormalizeLineEndings(
    this string text,
    string? newLine = null
  ) {
    newLine ??= Environment.NewLine;
    return text
      .Replace("\r\n", "\n")
      .Replace("\r", "\n")
      .Replace("\n", newLine);
  }
}
