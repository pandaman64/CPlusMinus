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
            node.Item1.PrettyPrint(visitor);*/
            const string defaultSource =
@"namespace TestProgram{
    class Klass{
        static fn main():System.Int32 = {
            let val:System.Int32 = 56;
            if(3 = 5){
            }
            else{
            }
            return val;
        }
    }
}";
            var source = args.Any() ? File.ReadAllText(args[0]) : defaultSource;
            var lines = source.Split('\n').Select((str, i) => new {number = i + 1, str});
            foreach (var line in lines)
            {
                Console.WriteLine("{0:00} {1}",line.number,line.str);
            }
            var result = Parser.runParser(source,false);
            var node = result.Item1;
            var messages = result.Item2;
            Console.WriteLine("messages:");
            foreach (var message in messages.Reverse())
            {
                Console.WriteLine(message);
            }
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
