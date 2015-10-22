using System;
using System.Collections.Generic;
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
            const string defaultSource =
@"namespace TestProgram{
    class Klass{
        static let namako:System.Int32 = 2;
        static fn price(num:System.Int32):System.Int32 = {
            let unit_price:System.Int32 = 5;
            let shipping:System.Int32 = num * 2;
            return num * unit_price + shipping;
        }
        static fn main():System.Int32 = {
            namako <- 42;
            return namako;
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
            //node.PrettyPrint(visitor);
            node.Generate(visitor);
            visitor.Generator.Builder.Save("out.exe");
            Console.WriteLine("compile: finished.");
        }
    }
}
