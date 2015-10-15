using System;
using System.Collections.Generic;
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
            /*string source = @"
namespace TestProgram{
    class CPMProgram{
        static void Main(){
            Console.WriteLine(""Hello,World!"");
        }
    }
}
            ";*/
            const string source = 
@"namespace CPMProgram{
    class Klass{
        let num:System.Int = 5;
        fn discount(val:System.Double) = {
            if(val){
                return 0.01;
            }
            else{
                return 0.02;
            }
        }
        fn price(val:System.Double) = {
            let dc:double = discount(val);
            return num * val * (1 - dc);
        }
    }
}";
            var lineNumber = 1;
            var lines = source.Split('\n');
            foreach (var line in lines)
            {
                Console.WriteLine("{0} {1}",lineNumber++,line);
            }
            try
            {
                var node = Parser.runParser(source) as Node;
                var visitor = new Visitor();
                node.Visit(visitor);
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
