using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;

namespace AST
{
    static class TypeResolver
    {
        static bool IsIntegral(this Type type)
        {
            return typeof (long).IsAssignableFrom(type);
        }

        static bool IsFloat(this Type type)
        {
            return typeof (double).IsAssignableFrom(type);
        }

        public static Type CommonType(Type t1, Type t2)
        {
            if (t1.IsAssignableFrom(t2))
            {
                return t1;
            }
            else if (t2.IsAssignableFrom(t2))
            {
                return t2;
            }
            else
            {
                if (t1.IsIntegral() && t2.IsFloat())
                {
                    return typeof(long);
                }
                else if (t1.IsFloat() && t2.IsIntegral())
                {
                    return typeof (double);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }
    }
    public interface Node
    {
        void PrettyPrint(Visitor visitor);
        void Generate(Visitor visitor);
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

        public class CodeGenerator
        {
            public AssemblyBuilder Builder { get; }
            public ModuleBuilder CurrentModule { get; }
            public TypeBuilder CurrentType { get; set; }
            public MethodBuilder CurrentMethod { get; set; }

            public CodeGenerator(string name)
            {
                Builder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), 
                    AssemblyBuilderAccess.Save);
                CurrentModule = Builder.DefineDynamicModule("CPMModule", "CPMModule.dll", true);
            }

            public Type ResolveType(Identifier identifier)
            {
                //Console.WriteLine("ResolveType(Identifier) must be correctly implemented");
                var typeName = identifier.Name;
                return Type.GetType(typeName);
            }

            public Type ResolveType(List<Identifier> identifiers)
            {
                //Console.WriteLine("ResolveType(List<Identifier>) must be correctly implemented");
                var typeName = String.Join(".",identifiers.Select(id => id.Name));
                return Type.GetType(typeName);
            }

            public Type ResolveType(Expression expr)
            {
                return expr.ResolveType();
            }
        }

        public PrettyPrinter Printer { get; }
        public CodeGenerator Generator { get; }

        public Visitor(PrettyPrinter p,string name)
        {
            Printer = p;
            Generator = new CodeGenerator(name);
        }
        public Visitor(string name) : this(new PrettyPrinter("  "),name) { }
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
         
        public void PrettyPrint(Visitor visitor)
        {
            visitor.Printer.Write("namespace ");
            name.PrettyPrint(visitor);
            visitor.Printer.WriteLine("{\nclasses:");
            visitor.Printer.PushIndent();
            foreach (var klass in classes)
            {
                klass.PrettyPrint(visitor);
            }
            visitor.Printer.PopIndent();
            visitor.Printer.WriteLine("}");
        }

        public void Generate(Visitor visitor)
        {
            //var generator = visitor.Generator;
            foreach (var @class in classes)
            {
                @class.Generate(visitor);
            }
        }
    }

    public class ClassDeclaration : Declaration
    {
        private Identifier name;
        private List<VariableDeclaration> fields;
        private List<MethodDeclaration> methods;

        public ClassDeclaration(Identifier n, IEnumerable<VariableDeclaration> v, IEnumerable<MethodDeclaration> m)
        {
            name = n;
            fields = v.ToList();
            methods = m.ToList();
        }

        public void PrettyPrint(Visitor visitor)
        {
            visitor.Printer.Write("class ");
            name.PrettyPrint(visitor);
            visitor.Printer.WriteLine("{");
            visitor.Printer.WriteLine("fields:");
            visitor.Printer.PushIndent();
            foreach (var variable in fields)
            {
                variable.PrettyPrint(visitor);
            }
            visitor.Printer.WriteLine();
            visitor.Printer.PopIndent();
            visitor.Printer.WriteLine("methods:");
            visitor.Printer.PushIndent();
            foreach (var method in methods)
            {
                method.PrettyPrint(visitor);
            }
            visitor.Printer.PopIndent();
            visitor.Printer.WriteLine("}");
        }

        public void Generate(Visitor visitor)
        {
            var generator = visitor.Generator;
            var module = generator.CurrentModule;
            var type = generator.CurrentType = module.DefineType(name.Name);
            foreach (var field in fields)
            {
                var fieldName = field.Name.Name;
                var fieldType = visitor.Generator.ResolveType(field.VariableType);
                type.DefineField(fieldName, fieldType,FieldAttributes.Public);
            }
            foreach (var method in methods)
            {
                method.Generate(visitor);
            }
            type.CreateType();
            generator.CurrentType = null;
        }
    }

    public class MethodDeclaration : Declaration
    {
        public Identifier Name { get; }
        private List<VariableDeclaration> parameters;
        private Statement body;
        public List<Identifier> ReturnType { get; }

        public MethodDeclaration(Identifier n, IEnumerable<VariableDeclaration> p, Statement b,IEnumerable<Identifier> r)
        {
            Name = n;
            parameters = p.ToList();
            body = b;
            ReturnType = r.ToList();
        }

        public void PrettyPrint(Visitor visitor)
        {
            visitor.Printer.Write("fn ");
            Name.PrettyPrint(visitor);
            visitor.Printer.Write("(");
            if (parameters.Any())
            {
                parameters.First().PrettyPrint(visitor);
                foreach (var param in parameters.Skip(1))
                {
                    visitor.Printer.Write(",");
                    param.PrettyPrint(visitor);
                }
            }
            visitor.Printer.Write("):");

            ReturnType.First().PrettyPrint(visitor);
            foreach (var type in ReturnType.Skip(1))
            {
                Console.Write(".");
                type.PrettyPrint(visitor);
            }

            visitor.Printer.WriteLine(" = ");
            body.PrettyPrint(visitor);
        }

        public void Generate(Visitor visitor)
        {
            
            var type = visitor.Generator.CurrentType;
            var returnType = visitor.Generator.ResolveType(ReturnType);
            var parameterTypes =
                parameters.Select(parameter => visitor.Generator.ResolveType(parameter.VariableType)).ToArray();
            //generate entry point
            if (Name.Name == "main")
            {
                visitor.Generator.CurrentMethod = type.DefineMethod(Name.Name,
                    MethodAttributes.Public | MethodAttributes.Static, returnType,
                    parameterTypes);
            }
            else
            {
                visitor.Generator.CurrentMethod = type.DefineMethod(Name.Name, MethodAttributes.Public, returnType,
                    parameterTypes);
            }
            body.Generate(visitor);
            if (Name.Name == "main")
            {
                visitor.Generator.Builder.SetEntryPoint(visitor.Generator.CurrentMethod);
            }
        }
    }

    public class VariableDeclaration : Declaration
    {
        public Identifier Name { get; }
        public List<Identifier> VariableType { get; }
        public Expression Initializer { get; }

        public VariableDeclaration(Identifier n, IEnumerable<Identifier> t)
        {
            Name = n;
            VariableType = t.ToList();
            Initializer = null;
        }
        public VariableDeclaration(Identifier n, IEnumerable<Identifier> t,Expression init)
        {
            Name = n;
            VariableType = t.ToList();
            Initializer = init;
        }

        public void PrettyPrint(Visitor visitor)
        {
            visitor.Printer.Write("let ");
            Name.PrettyPrint(visitor);
            visitor.Printer.Write(":");
            if (VariableType.Any())
            {
                VariableType.First().PrettyPrint(visitor);
                foreach (var id in VariableType.Skip(1))
                {
                    visitor.Printer.Write(".");
                    id.PrettyPrint(visitor);
                }
            }
            if (Initializer != null)
            {
                visitor.Printer.Write(" = ");
                Initializer.PrettyPrint(visitor);
            }
        }

        public void Generate(Visitor visitor)
        {
            throw new NotImplementedException();
        }
    }

    public interface Statement : Node
    {
    }

    public class ExpressionStatement : Statement
    {
        public Expression Expr { get; }

        public ExpressionStatement(Expression e)
        {
            Expr = e;
        }

        public void PrettyPrint(Visitor visitor)
        {
            Expr.PrettyPrint(visitor);
            visitor.Printer.WriteLine(";");
        }

        public void Generate(Visitor visitor)
        {
            throw new NotImplementedException();
        }
    }

    public class CompoundStatement : Statement
    {
        private List<Statement> statements;

        public CompoundStatement(IEnumerable<Statement> stmts)
        {
            statements = stmts.ToList();
        }


        public void PrettyPrint(Visitor visitor)
        {
            visitor.Printer.WriteLine("{");
            visitor.Printer.PushIndent();
            foreach (var stmt in statements)
            {
                stmt.PrettyPrint(visitor);
            }
            visitor.Printer.PopIndent();
            visitor.Printer.WriteLine("}");
        }

        public void Generate(Visitor visitor)
        {
            foreach (var statement in statements)
            {
                statement.Generate(visitor);
            }
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

        public void PrettyPrint(Visitor visitor)
        {
            visitor.Printer.Write("if(");
            condition.PrettyPrint(visitor);
            visitor.Printer.WriteLine(") then");
            thenStatement.PrettyPrint(visitor);
            foreach (var elif in elseIfList)
            {
                visitor.Printer.Write("else if(");
                elif.Item1.PrettyPrint(visitor);
                visitor.Printer.WriteLine(") then");
                elif.Item2.PrettyPrint(visitor);
            }
            visitor.Printer.WriteLine("else");
            elseStatement.PrettyPrint(visitor);
        }

        public void Generate(Visitor visitor)
        {
            throw new NotImplementedException();
        }
    }

    public class ReturnStatement : Statement
    {
        private Expression returnExpression;

        public ReturnStatement(Expression expr)
        {
            returnExpression = expr;
        }

        public void PrettyPrint(Visitor visitor)
        {
            visitor.Printer.Write("return ");
            returnExpression.PrettyPrint(visitor);
        }

        public void Generate(Visitor visitor)
        {
            returnExpression.Generate(visitor);
            visitor.Generator.CurrentMethod.GetILGenerator().Emit(OpCodes.Ret);
        }
    }

    public interface Expression : Node {
        Type ResolveType();
    }
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

        public void PrettyPrint(Visitor visitor)
        {
            visitor.Printer.Write("(");
            lhs.PrettyPrint(visitor);
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
            rhs.PrettyPrint(visitor);
            visitor.Printer.Write(")");
        }

        public void Generate(Visitor visitor)
        {
            lhs.Generate(visitor);
            rhs.Generate(visitor);
            var generator = visitor.Generator.CurrentMethod.GetILGenerator();
            switch (type)
            {
                case ExpressionType.Add:
                    generator.Emit(OpCodes.Add);
                    break;
                case ExpressionType.Subtract:
                    generator.Emit(OpCodes.Div);
                    break;
                case ExpressionType.Multiply:
                    generator.Emit(OpCodes.Mul);
                    break;
                case ExpressionType.Divide:
                    generator.Emit(OpCodes.Div);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public Type ResolveType()
        {
            return TypeResolver.CommonType(lhs.ResolveType(), rhs.ResolveType());
        }
    }

    public class Literal<T> : Expression
    {
        private T value;

        public Literal(T v)
        {
            value = v;
        }

        public void PrettyPrint(Visitor visitor)
        {
            visitor.Printer.Write("<");
            visitor.Printer.Write(typeof(T).Name);
            visitor.Printer.Write("|");
            visitor.Printer.Write(value);
            visitor.Printer.Write(">");
        }

        public void Generate(Visitor visitor)
        {
            var generator = visitor.Generator.CurrentMethod.GetILGenerator();
            if (typeof(T) == typeof(int))
            {
                generator.Emit(OpCodes.Ldc_I4, (int)(object)value);
            }
            else if (typeof (T) == typeof (double))
            {
                generator.Emit(OpCodes.Ldc_R8, (double) (object) value);
            }
            else
            {
                throw new NotImplementedException("Literal not supported");
            }
        }

        public Type ResolveType()
        {
            return typeof (T);
        }
    }

    public class Identifier : Expression
    {
        public string Name { get; }

        public Identifier(string n)
        {
            Name = n;
        }

        public void PrettyPrint(Visitor visitor)
        {
            visitor.Printer.Write("|" + Name + "|");
        }

        public void Generate(Visitor visitor)
        {
            throw new NotImplementedException();
        }

        public Type ResolveType()
        {
            throw new NotImplementedException();
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

        public void PrettyPrint(Visitor visitor)
        {
            callee.PrettyPrint(visitor);
            visitor.Printer.Write("(");
            if (arguments.Any())
            {
                arguments.First().PrettyPrint(visitor);
                foreach (var argument in arguments.Skip(1))
                {
                    visitor.Printer.Write(",");
                    argument.PrettyPrint(visitor);
                }
            }
            visitor.Printer.WriteLine(")");
        }

        public void Generate(Visitor visitor)
        {
            throw new NotImplementedException();
        }

        public Type ResolveType()
        {
            throw new NotImplementedException();
        }
    }
}
