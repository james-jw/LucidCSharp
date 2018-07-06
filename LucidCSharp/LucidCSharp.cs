using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace LucidCSharp 
{
    public static class CSharpUtility
    {

        /// <summary>
        /// Complies a string template in the format accepted c# 4.5
        ///
        /// Example:
        /// "Full Name: {first} {last}"
        ///
        /// </summary>
        /// <param name="templateIn"></param>
        /// <returns></returns>
        public static Func<dynamic, dynamic> CompileTemplate(string templateIn)
        {
            var source = $"i => $\"{templateIn.Replace("{", "{i.")}\"";
            return CSharpUtility.CompileLambda<dynamic, dynamic>(source);
        }

        public static IDictionary<string, string> emptyLambdas = new Dictionary<string, string>();
        public static Func<I, O> CompileLambda<I, O>(string lambda)
        {
            return CompileLambda<I, O>(lambda, emptyLambdas);
        }
        public static Func<I, T, O> CompileLambda<I, T, O>(string lambda)
        {
            return CompileLambda<I, T, O>(lambda, emptyLambdas);
        }

        public static Assembly CompileLambdaInternal(string lambda, IEnumerable<KeyValuePair<string, string>> supportLambdas, Type returnType, string[] references, params Type[] parameterTypes)
        {
            var coercedTypes = parameterTypes.Concat(new Type[] { returnType }).Select(t => t.Name == "Object" ? "dynamic" : t.Name);
            var supportLambdaString = supportLambdas == null ? "" : DependentLambdas(lambda, supportLambdas);

            var sources = new string[] { $@"
                using System;
                using System.Linq;
                using System.Collections.Generic;

                namespace InMemory {{
                  public static class DynamicClass {{

                     public static Func<dynamic, string, dynamic> _Trace = (i, m) => i;

                     public static dynamic Trace(dynamic i) {{ return _Trace(i, string.Empty); }}
                     public static dynamic Trace(dynamic i, string m) {{ return _Trace(i, m); }}

                    {supportLambdaString}

                     public static Func<{string.Join(",", coercedTypes)}> mapper = {lambda};
                     public static dynamic executeLambda({String.Join(",", parameterTypes.Select((t, i) => $"dynamic item{i}"))}) {{
                        return mapper({String.Join(",", parameterTypes.Select((t, i) => $"item{i}").ToArray())});
                     }}
                  }}
                }}"
            };

            return CompileAssembly(sources, references?.Select(r => PortableExecutableReference.CreateFromFile(r))?.ToArray());
        }
        public static Func<A, B, C, D, O> CompileLambda<A, B, C, D, O>(string lambda, IEnumerable<KeyValuePair<string, string>> supportLambdas = null, Func<dynamic, string, dynamic> logger = null, string[] references = null)
        {
            var asm = CompileLambdaInternal(lambda, supportLambdas, typeof(O), references, typeof(A), typeof(B), typeof(C), typeof(D));
            SetLogger(asm, logger);
            return RetrieveDelegate<A, B, C, D, O>(asm, "InMemory", "DynamicClass", "executeLambda");
        }

        public static Func<A, B, C, O> CompileLambda<A, B, C, O>(string lambda, IEnumerable<KeyValuePair<string, string>> supportLambdas = null, Func<dynamic, string, dynamic> logger = null, string[] references = null)
        {
            var asm = CompileLambdaInternal(lambda, supportLambdas, typeof(O), references, typeof(A), typeof(B), typeof(C));
            SetLogger(asm, logger);
            return RetrieveDelegate<A, B, C, O>(asm, "InMemory", "DynamicClass", "executeLambda");
        }

        public static Func<A, B, O> CompileLambda<A, B, O>(string lambda, IEnumerable<KeyValuePair<string, string>> supportLambdas = null, Func<dynamic, string, dynamic> logger = null, string[] references = null)
        {
            var asm = CompileLambdaInternal(lambda, supportLambdas, typeof(O), references, typeof(A), typeof(B));
            SetLogger(asm, logger);
            return RetrieveDelegate<A, B, O>(asm, "InMemory", "DynamicClass", "executeLambda");
        }

        public static Func<A, O> CompileLambda<A, O>(string lambda, IEnumerable<KeyValuePair<string, string>> supportLambdas = null, Func<dynamic, string, dynamic> logger = null, string[] references = null)
        {
            var asm = CompileLambdaInternal(lambda, supportLambdas, typeof(O), references, typeof(A));
            SetLogger(asm, logger);
            return RetrieveDelegate<A, O>(asm, "InMemory", "DynamicClass", "executeLambda");
        }
        public static Func<O> CompileLambda<O>(string lambda, IEnumerable<KeyValuePair<string, string>> supportLambdas = null, Func<dynamic, string, dynamic> logger = null, string[] references = null)
        {
            var asm = CompileLambdaInternal(lambda, supportLambdas, typeof(O), references);
            SetLogger(asm, logger);

            return RetrieveDelegate<O>(asm, "InMemory", "DynamicClass", "executeLambda");
        }

        private static void SetLogger(Assembly asm, Func<dynamic, string, dynamic> logger)
        {
            if (logger != null) {
                var clazz = asm.GetType("InMemory.DynamicClass");
                var traceMethod = clazz.GetField("_Trace");
                traceMethod.SetValue(null, logger);
            }
        }

        public static string DependentLambdas(string lambda, IEnumerable<KeyValuePair<string, string>> supportLambdas, string existing = "")
        {
            var dependent = supportLambdas.Where(l => lambda.Contains(l.Key));
            if (!dependent.Any())
                return existing;

            var supportLambdasOut =
                     String.Join(Environment.NewLine, dependent.Select(l => {
                         var parameters = new string[LambdaParameterCount(l.Value) + 1];
                         for(var i = 0; i < parameters.Length; i++) {
                             parameters[i] = "dynamic";
                         }
                         
                         return $"public static Func<{string.Join(",", parameters)}> {l.Key} = {l.Value};";
                     }));

            return DependentLambdas(supportLambdasOut, supportLambdas.Where(l => !dependent.Select(d => d.Key).Contains(l.Key)), supportLambdasOut + Environment.NewLine + existing);
        }

        public static int LambdaParameterCount(string stringValue)
        {
            var parts = stringValue.Split(new string[] { "=>" }, StringSplitOptions.RemoveEmptyEntries);
            var parameters = parts[0];

            return parameters.Trim(' ') == "()" ? 0 : parameters.Split(',').Count();

        }

        /// <summary>
        /// Compiles a lambda expression into a delegate
        /// 
        /// Example lambdas:
        /// i => i + 2
        ///
        /// (item) => {
        ///    item.age += 1;
        ///    return item.age;
        /// }
        /// </summary>
        /// <param name="code">Lambda expression to compile</param>
        /// <returns></returns>
        public static Func<O> RetrieveDelegate<O>(Assembly asm, string nameSpace, string className, string methodName)
        {
            var type = asm.GetType($"{nameSpace}.{className}");
            var method = type.GetMethod(methodName);

            return Expression.Lambda<Func<O>>(Expression.Call(null, method)).Compile();
        }

        public static Func<I, O> RetrieveDelegate<I, O>(Assembly asm, string nameSpace, string className, string methodName)
        {
            var type = asm.GetType($"{nameSpace}.{className}");
            var method = type.GetMethod(methodName);

            var argument = Expression.Parameter(typeof(object), "argument");
            return Expression.Lambda<Func<I, O>>(Expression.Call(null, method, argument), argument).Compile();
        }

        public static Func<I, T, O> RetrieveDelegate<I, T, O>(Assembly asm, string nameSpace, string className, string methodName)
        {
            var type = asm.GetType($"{nameSpace}.{className}");
            var method = type.GetMethod(methodName);

            var argument = Expression.Parameter(typeof(object), "argument");
            var argument2 = Expression.Parameter(typeof(object), "argument2");
            return Expression.Lambda<Func<I, T, O>>(Expression.Call(null, method, argument, argument2), 
                argument, argument2).Compile();
        }

        public static Func<I, T, D, O> RetrieveDelegate<I, T, D, O>(Assembly asm, string nameSpace, string className, string methodName)
        {
            var type = asm.GetType($"{nameSpace}.{className}");
            var method = type.GetMethod(methodName);

            var argument = Expression.Parameter(typeof(object), "argument");
            var argument2 = Expression.Parameter(typeof(object), "argument2");
            var argument3 = Expression.Parameter(typeof(object), "argument3");
            return Expression.Lambda<Func<I, T, D, O>>(Expression.Call(null, method, argument, argument2, argument3),
                argument, argument2, argument3).Compile();
        }

        public static Func<I, T, D, P, O> RetrieveDelegate<I, T, D, P, O>(Assembly asm, string nameSpace, string className, string methodName)
        {
            var type = asm.GetType($"{nameSpace}.{className}");
            var method = type.GetMethod(methodName);

            var argument = Expression.Parameter(typeof(object), "argument");
            var argument2 = Expression.Parameter(typeof(object), "argument2");
            var argument3 = Expression.Parameter(typeof(object), "argument3");
            var argument4 = Expression.Parameter(typeof(object), "argument4");
            return Expression.Lambda<Func<I, T, D, P, O>>(Expression.Call(null, method, argument, argument2, argument3, argument4),
                argument, argument2, argument3, argument4).Compile();
        }

        static PortableExecutableReference[] _coreReferences = new PortableExecutableReference[]
            {
                  MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                  MetadataReference.CreateFromFile(typeof(RuntimeBinderException).Assembly.Location),
                  MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly.Location),
                  MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location)
            };

        public static Assembly CompileAssembly(string[] sources, PortableExecutableReference[] referencesIn = null)
        {
            var references = referencesIn ?? new PortableExecutableReference[0];
            var assemblyFileName = "InMemory" + Guid.NewGuid().ToString().Replace("-", "") + ".dll";

            try {
                var compilation = CSharpCompilation.Create(assemblyFileName,
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                    syntaxTrees: from source in sources
                                 select CSharpSyntaxTree.ParseText(source),
                    references: _coreReferences.Concat(references).ToArray()
                );

                EmitResult emitResult;

                using (var ms = new MemoryStream()) {
                    emitResult = compilation.Emit(ms);
                    if (emitResult.Success) {
                        var asm = Assembly.Load(ms.GetBuffer());
                        return asm;
                    }
                }

                var message = string.Join("\r\n", emitResult.Diagnostics);
                throw new ApplicationException(message);
            }
            catch (ApplicationException e) {
                throw new Exception($"Failed to process expression: {sources[0]} {Environment.NewLine} Error: {e.Message}", e);
            }
        }
        public static Func<dynamic, dynamic> CompileMapExpression(string expression, string method)
        {
            return CompileMapExpression(expression, method, emptyLambdas);
        }

        public static bool IsLambda(string expressionIn)
        {
            return expressionIn?.Contains("=>") == true;
        }

        public static bool IsTemplate(string expressionIn)
        {
            return expressionIn != null && Regex.IsMatch(expressionIn, "[{].*[}]", RegexOptions.Compiled);
        }

        public static Func<dynamic, dynamic> CompileMapExpression(string expression, string method, IEnumerable<KeyValuePair<string, string>> supportLambdas, Func<dynamic, string, dynamic> logger = null, string[] referencesIn = null)
        {
            try {
                if (IsLambda(expression))
                    return CompileLambda<dynamic, dynamic>(expression, supportLambdas, logger, referencesIn);
                else if (IsTemplate(expression))
                    return CompileTemplate(expression);
                else if (expression.EndsWith(".cs")) {
                    var asm = CSharpUtility.CompileAssembly(new string[] { File.ReadAllText($"{System.AppDomain.CurrentDomain.BaseDirectory}{expression}") }, 
                        referencesIn?.Select(r => PortableExecutableReference.CreateFromFile(r)).ToArray()
                    );

                    var path = Regex.Split(expression, "[.]");
                    return RetrieveDelegate<dynamic, dynamic>(asm, path.First(), path.ElementAt(1), method);
                }
                else if (expression.EndsWith(".dll")) {
                    var path = Regex.Split(expression, "[.]");
                    var asm = Assembly.LoadFrom(expression);
                    return RetrieveDelegate<dynamic, dynamic>(asm, path.First(), path.ElementAt(1), method);
                }
                else {
                    return (i) => expression;
                }

            }
            catch (Exception e) {
                throw new Exception($"Unable to compile expression: '{expression}' {Environment.NewLine} {e.Message}", e);
            }

            throw new Exception($"Invalid expression provided: '{expression}'");
        }
        
        /// <summary>
        /// Formats the provided c# lambda code with default formating rules
        /// </summary>
        /// <param name="codeIn">The c# lambda code to format</param>
        /// <returns>The formated lambda code</returns>
        public static string FormatLambdaString(string codeIn)
        {
            var declaration = "var codeInputEquals = ";
            var tree = CSharpSyntaxTree.ParseText(declaration + codeIn);
            var normalized = tree.GetRoot().NormalizeWhitespace();
            return normalized.ToFullString().Replace(declaration, "");
        }
    }
}
