using System;

class Module
{
    internal static void Init()
    {
        Console.WriteLine("Init");
    }
}

namespace ModuleInit.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
    }
}
