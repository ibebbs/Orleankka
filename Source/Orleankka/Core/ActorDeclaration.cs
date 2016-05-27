using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Orleankka.Core
{
    class ActorDeclaration
    {
        public static IEnumerable<ActorType> Generate(IEnumerable<Assembly> assemblies)
        {
            var declarations = assemblies.SelectMany(Scan).ToArray();
            var source = Generate(declarations);

            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Select(x => x.IsDynamic ? null : MetadataReference.CreateFromFile(x.Location))
                .Where(x => x != null)
                .ToArray();

            var assemblyName = Path.GetRandomFileName();
            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var output = AppDomain.CurrentDomain.BaseDirectory;

            Assembly asm;
            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);
                if (!result.Success)
                {
                    var failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);
                    throw new Exception("Bad code.\n\n" + string.Join("\n", failures));
                }

                ms.Seek(0, SeekOrigin.Begin);
                asm = Assembly.Load(ms.ToArray());
            }

            return declarations.Select(x => x.Bind(asm));
        }

        static string Generate(IEnumerable<ActorDeclaration> declarations)
        {
            var sb = new StringBuilder(@"
                 using Orleankka;
                 using Orleankka.Core;
                 using Orleankka.Core.Endpoints;
            ");

            foreach (var declaration in declarations)
                sb.AppendLine(declaration.Generate());

            return sb.ToString();
        }

        static IEnumerable<ActorDeclaration> Scan(Assembly assembly) => assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Actor).IsAssignableFrom(type))
            .Select(actor => new {actor, code = ActorTypeCode.Of(actor)})
            .Select(x => new ActorDeclaration(x.code, x.actor));

        static readonly string[] separator = {".", "+"};

        readonly string code;
        readonly Type actor;
        readonly string clazz;
        readonly IList<string> namespaces;

        ActorDeclaration(string code, Type actor)
        {
            this.code = code;
            this.actor = actor;

            var path = code.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            clazz = path.Last();

            namespaces = path.TakeWhile(x => x != clazz).ToList();
            namespaces.Insert(0, "Fun");
        }

        string Generate()
        {
            var src = new StringBuilder($"namespace {string.Join(".", namespaces)}");
            src.AppendLine("{");
            src.AppendLine($"public interface I{clazz} : global::Orleankka.Core.Endpoints.IActorEndpoint {{}}");
            src.AppendLine($"public class {clazz} : global::Orleankka.Core.ActorEndpoint, I{clazz} {{}}");
            src.AppendLine("}");
            return src.ToString();
        }

        ActorType Bind(Assembly asm)
        {
            var fullName = string.Join(".", new List<string>(namespaces) { $"I{clazz}" });
            var @interface = asm.GetType(fullName);
            return ActorType.From(code, @interface, actor);
        }
    }
}