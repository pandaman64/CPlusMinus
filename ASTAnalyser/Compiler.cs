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
        let num:System.Double = 5.4;
        fn main():System.Int32 = {
            return 2;
        }
    }
}";
            var source = args.Any() ? File.ReadAllText(args[0]) : defaultSource;
            var lines = source.Split('\n').Select((str, i) => new {number = i + 1, str});
            foreach (var line in lines)
            {
                Console.WriteLine("{0} {1}",line.number,line.str);
            }
            try
            {
                var node = Parser.runParser(source) as Node;
                var visitor = new Visitor("CPMAssembly");
                //node.PrettyPrint(visitor);
                node.Generate(visitor);
                visitor.Generator.Builder.Save("out.exe");
                Console.WriteLine("compile: finished.");
            }
            catch (Parser.CompileError e)
            {
                Console.WriteLine(e);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
