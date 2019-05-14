using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MethodAttributes = Mono.Cecil.MethodAttributes;


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
            var module = ModuleDefinition.ReadModule(targetAssembly.FullName, new ReaderParameters(ReadingMode.Immediate)
            {
                InMemory = true
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

            const MethodAttributes Attributes = MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            var cctor = new MethodDefinition(".cctor", Attributes, module.ImportReference(typeof(void)));
            var il = cctor.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Call, initMethod));
            il.Append(il.Create(OpCodes.Ret));

            assemblyModuleClass.Methods.Add(cctor);

            module.Write(targetAssembly.FullName);
            Console.WriteLine($"Wrote updated assembly '{targetAssembly.FullName}'");
        }
    }
}
