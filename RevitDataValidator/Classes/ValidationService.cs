using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace RevitDataValidator
{
    public class ValidationService
    {
        public IEnumerable<Diagnostic> Execute(string code, out MemoryStream ms)
        {
            try
            {
                var revitPath = Process.GetCurrentProcess().MainModule.FileName;
                var revitFolder = Path.GetDirectoryName(revitPath);
                var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
                var assemblyName = Path.GetRandomFileName();
                var defaultCompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOverflowChecks(true)
                    .WithOptimizationLevel(OptimizationLevel.Release);
                var defaultReferences = new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
                    MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "mscorlib.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Core.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Linq.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(revitFolder, "RevitAPI.dll")),
                };
                var compilation = CSharpCompilation.Create(
                    assemblyName,
                    syntaxTrees: new[] {
                CSharpSyntaxTree.ParseText(code)
                    },
                    references: defaultReferences,
                    options: defaultCompilationOptions);
                ms = new MemoryStream();
                var result = compilation.Emit(ms);
                if (result.Success)
                    return null;
                var failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error);
                return failures;
            }
            catch (Exception ex)
            {
                ms = null;
                return null;
            }
        }
    }
}
