using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AST
{
    public interface Node
    {
        void Visit(Visitor visitor);
    }

    public class Visitor
    {
        public class PrettyPrinter
        {
            private int indent;
            private bool breakLine;
            private readonly string tabString;

            string IndentString
            {
                get
                {
                    StringBuilder builder = new StringBuilder();
                    for (int i = indent; i > 0; i--)
                    {
                        builder.Append(tabString);
                    }
                    return builder.ToString();
                }
            }

            public PrettyPrinter(string ts)
            {
                indent = 0;
                breakLine = false;
                tabString = ts;
            }
            public PrettyPrinter() : this("  ") { }

            public void PushIndent()
            {
                indent++;
            }

            public void PopIndent()
            {
                indent--;
            }

            public void Write(string format, params object[] args)
            {
                if (breakLine)
                {
                    Console.Write(IndentString);
                }
                Console.Write(format, args);
                breakLine = false;
            }

            public void Write(object obj)
            {
                if (breakLine)
                {
                    Console.Write(IndentString);
                }
                Console.Write(obj);
                breakLine = false;
            }

            public void Write(string obj)
            {
                if (breakLine)
                {
                    Console.Write(IndentString);
                }
                Console.Write(obj);
                breakLine = false;
            }

            public void Write()
            {
                if (breakLine)
                {
                    Console.Write(IndentString);
                }
                breakLine = false;
            }

            public void WriteLine(string format, params object[] args)
            {
                if (breakLine)
                {
                    Console.Write(IndentString);
                }
                Console.WriteLine(format, args);
                breakLine = true;
            }

            public void WriteLine(object obj)
            {
                if (breakLine)
                {
                    Console.Write(IndentString);
                }
                Console.WriteLine(obj);
                breakLine = true;
            }

            public void WriteLine(string obj)
            {
                if (breakLine)
                {
                    Console.Write(IndentString);
                }
                Console.WriteLine(obj);
                breakLine = true;
            }

            public void WriteLine()
            {
                if (breakLine)
                {
                    Console.Write(IndentString);
                }
                Console.WriteLine();
                breakLine = true;
            }
        }

        public PrettyPrinter Printer { get; }

        public Visitor(PrettyPrinter p)
        {
            Printer = p;
        }
        public Visitor() : this(new PrettyPrinter("  ")) { }
    }

    public interface Declaration : Statement
    {
    }

    public class NamespaceDeclaration : Declaration
    {
        private Identifier name;
        private List<ClassDeclaration> classes;

        public NamespaceDeclaration(Identifier n,IEnumerable<ClassDeclaration> cs)
        {
            name = n;
            classes = cs.ToList();
        }
         
        public void Visit(Visitor visitor)
        {
            visitor.Printer.Write("namespace ");
            name.Visit(visitor);
            visitor.Printer.WriteLine("{\nclasses:");
            visitor.Printer.PushIndent();
            foreach (var klass in classes)
            {
                klass.Visit(visitor);
            }
            visitor.Printer.PopIndent();
            visitor.Printer.WriteLine("}");
        }
    }

    public class ClassDeclaration : Declaration
    {
        private Identifier name;
        private List<VariableDeclaration> variables;
        private List<MethodDeclaration> methods;

        public ClassDeclaration(Identifier n, IEnumerable<VariableDeclaration> v, IEnumerable<MethodDeclaration> m)
        {
            name = n;
            variables = v.ToList();
            methods = m.ToList();
        }

        public void Visit(Visitor visitor)
        {
            visitor.Printer.Write("class ");
            name.Visit(visitor);
            visitor.Printer.WriteLine("{");
            visitor.Printer.WriteLine("variables:");
            visitor.Printer.PushIndent();
            foreach (var variable in variables)
            {
                variable.Visit(visitor);
            }
            visitor.Printer.WriteLine();
            visitor.Printer.PopIndent();
            visitor.Printer.WriteLine("methods:");
            visitor.Printer.PushIndent();
            foreach (var method in methods)
            {
                method.Visit(visitor);
            }
            visitor.Printer.PopIndent();
            visitor.Printer.WriteLine("}");
        }
    }

    public class MethodDeclaration : Declaration
    {
        private Identifier name;
        private List<VariableDeclaration> parameters;
        private Statement body;

        public MethodDeclaration(Identifier n, IEnumerable<VariableDeclaration> p, Statement b)
        {
            name = n;
            parameters = p.ToList();
            body = b;
        }

        public void Visit(Visitor visitor)
        {
            visitor.Printer.Write("fn ");
            name.Visit(visitor);
            visitor.Printer.Write("(");
            if (parameters.Any())
            {
                parameters.First().Visit(visitor);
                foreach (var param in parameters.Skip(1))
                {
                    visitor.Printer.Write(",");
                    param.Visit(visitor);
                }
            }
            visitor.Printer.WriteLine(") = ");
            body.Visit(visitor);
        }
    }

    public class VariableDeclaration : Declaration
    {
        private Identifier name;
        private List<Identifier> type;
        private Expression initializer;

        public VariableDeclaration(Identifier n, IEnumerable<Identifier> t)
        {
            name = n;
            type = t.ToList();
            initializer = null;
        }
        public VariableDeclaration(Identifier n, IEnumerable<Identifier> t,Expression init)
        {
            name = n;
            type = t.ToList();
            initializer = init;
        }

        public void Visit(Visitor visitor)
        {
            visitor.Printer.Write("let ");
            name.Visit(visitor);
            visitor.Printer.Write(":");
            if (type.Any())
            {
                type.First().Visit(visitor);
                foreach (var id in type.Skip(1))
                {
                    visitor.Printer.Write(".");
                    id.Visit(visitor);
                }
            }
            if (initializer != null)
            {
                visitor.Printer.Write(" = ");
                initializer.Visit(visitor);
            }
        }
    }

    public interface Statement : Node
    {
    }

    public class ExpressionStatement : Statement
    {
        private Expression expression;

        public ExpressionStatement(Expression e)
        {
            expression = e;
        }

        public void Visit(Visitor visitor)
        {
            expression.Visit(visitor);
            visitor.Printer.WriteLine(";");
        }
    }

    public class CompoundStatement : Statement
    {
        private List<Statement> statements;

        public CompoundStatement(IEnumerable<Statement> stmts)
        {
            statements = stmts.ToList();
        }


        public void Visit(Visitor visitor)
        {
            visitor.Printer.WriteLine("{");
            visitor.Printer.PushIndent();
            foreach (var stmt in statements)
            {
                stmt.Visit(visitor);
            }
            visitor.Printer.PopIndent();
            visitor.Printer.WriteLine("}");
        }
    }

    public class IfStatement : Statement
    {
        private Expression condition;
        private Statement thenStatement;
        private Statement elseStatement;
        private List<Tuple<Expression, Statement>> elseIfList;

        public IfStatement(Expression cond, Statement then, Statement else_, IEnumerable<Tuple<Expression, Statement>> elseIf)
        {
            condition = cond;
            thenStatement = then;
            elseStatement = else_;
            elseIfList = elseIf.ToList();
        }

        public void Visit(Visitor visitor)
        {
            visitor.Printer.Write("if(");
            condition.Visit(visitor);
            visitor.Printer.WriteLine(") then");
            thenStatement.Visit(visitor);
            foreach (var elif in elseIfList)
            {
                visitor.Printer.Write("else if(");
                elif.Item1.Visit(visitor);
                visitor.Printer.WriteLine(") then");
                elif.Item2.Visit(visitor);
            }
            visitor.Printer.WriteLine("else");
            elseStatement.Visit(visitor);
        }
    }

    public class ReturnStatement : Statement
    {
        private ExpressionStatement returnExpression;

        public ReturnStatement(ExpressionStatement expr)
        {
            returnExpression = expr;
        }

        public void Visit(Visitor visitor)
        {
            visitor.Printer.Write("return ");
            returnExpression.Visit(visitor);
        }
    }

    public interface Expression : Node { }
    public class BinaryExpression : Expression
    {
        public enum ExpressionType
        {
            Add,
            Subtract,
            Multiply,
            Divide
        }

        private ExpressionType type;
        private Expression lhs, rhs;

        public BinaryExpression(ExpressionType t, Expression l, Expression r)
        {
            type = t;
            lhs = l;
            rhs = r;
        }

        public void Visit(Visitor visitor)
        {
            visitor.Printer.Write("(");
            lhs.Visit(visitor);
            switch (type)
            {
                    case ExpressionType.Add:
                        visitor.Printer.Write("+");
                    break;

                    case ExpressionType.Subtract:
                        visitor.Printer.Write("-");
                    break;

                    case ExpressionType.Multiply:
                        visitor.Printer.Write("*");
                    break;

                    case ExpressionType.Divide:
                        visitor.Printer.Write("/");
                    break;
            }
            rhs.Visit(visitor);
            visitor.Printer.Write(")");
        }
    }

    public class Literal<T> : Expression
    {
        private T value;

        public Literal(T v)
        {
            value = v;
        }

        public void Visit(Visitor visitor)
        {
            visitor.Printer.Write("<");
            visitor.Printer.Write(value);
            visitor.Printer.Write(">");
        }
    }

    public class Identifier : Expression
    {
        private string name;
        public Identifier(string n)
        {
            name = n;
        }

        public void Visit(Visitor visitor)
        {
            visitor.Printer.Write("|"+name+"|");
        }
    }

    public class FunctionCall : Expression
    {
        private Expression callee;
        private List<Expression> arguments;

        public FunctionCall(Expression c, IEnumerable<Expression> args)
        {
            callee = c;
            arguments = args.ToList();
        }

        public void Visit(Visitor visitor)
        {
            callee.Visit(visitor);
            visitor.Printer.Write("(");
            if (arguments.Any())
            {
                arguments.First().Visit(visitor);
                foreach (var argument in arguments.Skip(1))
                {
                    visitor.Printer.Write(",");
                    argument.Visit(visitor);
                }
            }
            visitor.Printer.WriteLine(")");
        }
    }
}
