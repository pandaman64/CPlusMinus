using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AST;

namespace ASTAnalyser
{
    class Compiler
    {
        static void Main(string[] args)
        {
            /*const string source = "4>3";
            var node = Parser.runCompareParser(source);
            var visitor = new Visitor("CPMAssembly");
            node.PrettyPrint(visitor);*/
            const string defaultSource =
@"namespace TestProgram{
    class Klass{
        static let namako:System.Int32 = 2;
        static let hoge:System.Int32 = 6;
        static fn main():System.Int32 = {
            namako <- 42;
            hoge <- 52;
            return (1 >< 1) || (2 - 2 < 0);
        }
    }
}";
            var source = args.Any() ? File.ReadAllText(args[0]) : defaultSource;
            var lines = source.Split('\n').Select((str, i) => new {number = i + 1, str});
            foreach (var line in lines)
            {
                Console.WriteLine("{0:00} {1}",line.number,line.str);
            }
            var node = Parser.runParser(source) as Node;
            var visitor = new Visitor("CPMAssembly");
            node.PrettyPrint(visitor);
            node.Generate(visitor);
            visitor.Generator.Builder.Save("out.exe");
            Console.WriteLine("compile: finished.");

            Console.WriteLine("running...");
            var proc = Process.Start("out.exe");
            proc.WaitForExit(10*1000);
            Console.WriteLine("Exit code is:{0}",proc.ExitCode);
        }
    }
}
