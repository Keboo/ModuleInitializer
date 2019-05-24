using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodDefinition = Mono.Cecil.MethodDefinition;
using ModuleDefinition = Mono.Cecil.ModuleDefinition;


namespace ModuleInit
{
    class Program
    {
        /// <summary>
        /// Create a module initializer in an assembly that contains a global::Module.Init() method.
        /// </summary>
        /// <param name="targetAssembly">The assembly to create the module initializer.</param>
        static void Main(FileInfo targetAssembly)
        {
            //https://www.coengoedegebure.com/module-initializers-in-dotnet/
            Console.WriteLine($"Processing {targetAssembly}");
            using (var symbolStream = GetSymbolInformation(targetAssembly.FullName, out ISymbolReaderProvider reader,
                out ISymbolWriterProvider writer))
            {
                var module = ModuleDefinition.ReadModule(targetAssembly.FullName, new ReaderParameters
                {
                    ReadSymbols = symbolStream != null || reader is EmbeddedPortablePdbReaderProvider,
                    SymbolReaderProvider = reader,
                    SymbolStream = symbolStream,
                    InMemory = true,
                });

                var type = module.GetType("Module");
                if (type == null)
                {
                    Console.WriteLine("Could not find global::Module class");
                    return;
                }

                var initMethod = type.GetMethods().FirstOrDefault(x => x.Name == "Init");
                if (initMethod == null)
                {
                    Console.WriteLine("Could not find Init() method on global::Module class");
                    return;
                }


                var assemblyModuleClass = module.GetType("<Module>");
                if (assemblyModuleClass == null)
                {
                    Console.WriteLine("Could not find <Module> class");
                    return;
                }

                const MethodAttributes Attributes =
                    MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
                var cctor = new MethodDefinition(".cctor", Attributes, module.ImportReference(typeof(void)));
                var il = cctor.Body.GetILProcessor();
                il.Append(il.Create(OpCodes.Call, initMethod));
                il.Append(il.Create(OpCodes.Ret));

                assemblyModuleClass.Methods.Add(cctor);

                module.Write(targetAssembly.FullName, new WriterParameters
                {
                    WriteSymbols = writer != null,
                    SymbolWriterProvider = writer,
                });
                Console.WriteLine($"Wrote updated assembly '{targetAssembly.FullName}'");
            }
        }

        //stripped down version of: https://github.com/Keboo/AutoDI/blob/master/AutoDI.Build/AssemblyRewriteTask.cs
        private static Stream GetSymbolInformation(string assemblyFile, out ISymbolReaderProvider symbolReaderProvider,
            out ISymbolWriterProvider symbolWriterProvider)
        {
            string pdbPath = FindPdbPath();

            if (pdbPath != null)
            {
                symbolReaderProvider = new PdbReaderProvider();
                symbolWriterProvider = new PdbWriterProvider();
                string tempPath = pdbPath + ".tmp";
                File.Copy(pdbPath, tempPath, true);
                return new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }

            symbolReaderProvider = null;
            symbolWriterProvider = null;
            return null;

            string FindPdbPath()
            {
                string path = Path.ChangeExtension(assemblyFile, "pdb");
                if (File.Exists(path))
                {
                    return path;
                }
                return null;
            }
        }
    }
}
