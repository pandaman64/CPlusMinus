using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime;

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
            public class NameLookupTable<T>
            {
                public NameLookupTable<T> Parent { get; }
                private Dictionary<string,T> Table { get; }

                public NameLookupTable()
                {
                    Parent = null;
                    Table = new Dictionary<string, T>();
                }

                public NameLookupTable(NameLookupTable<T> parent)
                {
                    Parent = parent;
                    Table = new Dictionary<string, T>();
                }

                public NameLookupTable<T> CreateChild()
                {
                    return new NameLookupTable<T>(this);
                }

                public T Lookup(Identifier id)
                {
                    //Console.WriteLine($"Looking for {id.Name}");
                    if (Table.ContainsKey(id.Name))
                    {
                        return Table[id.Name];
                    }
                    else if (Parent != null)
                    {
                        return Parent.Lookup(id);
                    }
                    else
                    {
                        throw new ArgumentException($"Variable {id.Name} not found");
                    }
                }

                public void Add(Identifier id,T expr)
                {
                    //Console.WriteLine($"Added {id.Name}");
                    Table.Add(id.Name,expr);
                }
            }

            public AssemblyBuilder Builder { get; }
            public ModuleBuilder CurrentModule { get; }
            public TypeBuilder CurrentType { get; set; }
            public MethodBuilder CurrentMethod { get; set; }
            public NameLookupTable<Expression> LocalLookupTable { get; set; }
            public NameLookupTable<MethodBuilder> DeclaredMethodBuilders { get; set; } 
            public NameLookupTable<FieldBuilder> DeclaredFieldBuilders { get; set; } 

            public CodeGenerator(string name)
            {
                Builder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), 
                    AssemblyBuilderAccess.Save);
                CurrentModule = Builder.DefineDynamicModule("CPMModule", "CPMModule.dll", true);
                LocalLookupTable = new NameLookupTable<Expression>();
                DeclaredMethodBuilders = new NameLookupTable<MethodBuilder>();
                DeclaredFieldBuilders = new NameLookupTable<FieldBuilder>();
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
                visitor.Generator.LocalLookupTable = visitor.Generator.LocalLookupTable.CreateChild();
                @class.Generate(visitor);
                visitor.Generator.LocalLookupTable = visitor.Generator.LocalLookupTable.Parent;
            }
        }
    }

    public class ClassDeclaration : Declaration
    {
        private Identifier name;
        private List<FieldDeclaration> fields;
        private List<MethodDeclaration> methods;

        public ClassDeclaration(Identifier n, IEnumerable<FieldDeclaration> v, IEnumerable<MethodDeclaration> m)
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

            //make child scope
            generator.DeclaredFieldBuilders = generator.DeclaredFieldBuilders.CreateChild();
            generator.DeclaredMethodBuilders = generator.DeclaredMethodBuilders.CreateChild();

            foreach (var field in fields)
            {
                field.Generate(visitor);
                generator.LocalLookupTable.Add(field.Name, new MemberAccess(field));
            }

            foreach (var method in methods)
            {
                var objectType = visitor.Generator.CurrentType;
                var returnType = visitor.Generator.ResolveType(method.ReturnType);
                var parameterTypes =
                    method.Parameters.Select(parameter => visitor.Generator.ResolveType(parameter.VariableType)).ToArray();
                MethodAttributes attributes = MethodAttributes.Public;
                if (method.IsStatic)
                {
                    attributes |= MethodAttributes.Static;
                }
                var builder = objectType.DefineMethod(method.Name.Name, attributes, returnType,
                    parameterTypes);
                //generate entry point
                if (method.Name.Name == "main")
                {
                    visitor.Generator.Builder.SetEntryPoint(builder);
                }
                generator.LocalLookupTable.Add(method.Name, new MethodAccess(type, method.Name));
                generator.DeclaredMethodBuilders.Add(method.Name,builder);
            }

            foreach (var method in methods)
            {
                generator.CurrentMethod = generator.DeclaredMethodBuilders.Lookup(method.Name);
                generator.LocalLookupTable = generator.LocalLookupTable.CreateChild();
                method.Generate(visitor);
                generator.LocalLookupTable = generator.LocalLookupTable.Parent;
            }

            type.CreateType();

            //retreive parent scope
            generator.DeclaredFieldBuilders = generator.DeclaredFieldBuilders.Parent;
            generator.DeclaredMethodBuilders = generator.DeclaredMethodBuilders.Parent;
            generator.CurrentType = null;
        }
    }

    public class FieldDeclaration : Declaration
    {
        public Identifier Name { get; set; }
        public List<Identifier> Type { get; set; }
        public Expression Initializer { get; set; }
        public bool IsStatic { get; set; }

        public FieldDeclaration(Identifier n, IEnumerable<Identifier> type, Expression init, bool isStatic)
        {
            Name = n;
            Type = type.ToList();
            Initializer = init;
            IsStatic = isStatic;
        }

        public void PrettyPrint(Visitor visitor)
        {
            visitor.Printer.WriteLine("field:{0}",Name);
        }

        public void Generate(Visitor visitor)
        {
            var objectType = visitor.Generator.CurrentType;
            var fieldType = visitor.Generator.ResolveType(Type);
            var attribute = FieldAttributes.Public;
            if (IsStatic)
            {
                attribute |= FieldAttributes.Static;;
            }
            var builder = objectType.DefineField(Name.Name, fieldType, attribute);
            visitor.Generator.DeclaredFieldBuilders.Add(Name, builder);
        }
    }

    public class MethodDeclaration : Declaration
    {
        public Identifier Name { get; }
        public List<VariableDeclaration> Parameters { get; }
        private Statement body;
        public List<Identifier> ReturnType { get; }
        public bool IsStatic { get; }

        public MethodDeclaration(Identifier n, IEnumerable<VariableDeclaration> p, Statement b,IEnumerable<Identifier> r,bool isS)
        {
            Name = n;
            Parameters = p.ToList();
            body = b;
            ReturnType = r.ToList();
            IsStatic = isS || n.Name == "main";
        }

        public void PrettyPrint(Visitor visitor)
        {
            if (IsStatic)
            {
                visitor.Printer.Write("static ");
            }
            visitor.Printer.Write("fn ");
            Name.PrettyPrint(visitor);
            visitor.Printer.Write("(");
            if (Parameters.Any())
            {
                Parameters.First().PrettyPrint(visitor);
                foreach (var param in Parameters.Skip(1))
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
            /*var objectType = visitor.Generator.CurrentType;
            var returnType = visitor.Generator.ResolveType(ReturnType);
            var parameterTypes =
                Parameters.Select(parameter => visitor.Generator.ResolveType(parameter.VariableType)).ToArray();
            //generate entry point
            if (Name.Name == "main")
            {
                visitor.Generator.CurrentMethod = objectType.DefineMethod(Name.Name,
                    MethodAttributes.Public | MethodAttributes.Static, returnType,
                    parameterTypes);
            }
            else
            {
                visitor.Generator.CurrentMethod = objectType.DefineMethod(Name.Name, MethodAttributes.Public, returnType,
                    parameterTypes);
            }*/
            foreach (var parameter in Parameters.Select((p,i) => new {i,p}))
            {
                var genertor = visitor.Generator;
                var parameterType = genertor.ResolveType(parameter.p.VariableType);
                var access = new ParameterAccess(parameter.p.Name, parameterType, (short) parameter.i);
                genertor.LocalLookupTable.Add(parameter.p.Name,access);
            }
            body.Generate(visitor);
            /*if (Name.Name == "main")
            {
                visitor.Generator.Builder.SetEntryPoint(visitor.Generator.CurrentMethod);
            }*/
        }
    }

    public class ParameterDeclaration
    {
        public Identifier Name { get; }
        public List<Identifier> Type { get; }

        public ParameterDeclaration(Identifier n, IEnumerable<Identifier> t)
        {
            Name = n;
            Type = t.ToList();
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
            //treat as local variable
            var generator = visitor.Generator;
            var variable = generator.CurrentMethod.GetILGenerator().DeclareLocal(generator.ResolveType(VariableType));
            generator.LocalLookupTable.Add(Name,new LocalVariableAccess(variable));
            if (Initializer != null)
            {
                Initializer.Generate(visitor);
                var ILGenerator = generator.CurrentMethod.GetILGenerator();
                ILGenerator.Emit(OpCodes.Stloc,variable.LocalIndex);
            }
        }
    }

    public class LocalVariableAccess : Expression
    {
        private LocalBuilder variable;
        private bool isLValue = false;

        public LocalVariableAccess(LocalBuilder var)
        {
            variable = var;
        }

        public void PrettyPrint(Visitor visitor)
        {
            throw new NotImplementedException();
        }

        public void Generate(Visitor visitor)
        {
            visitor.Generator.CurrentMethod.GetILGenerator().Emit(OpCodes.Ldloc,variable.LocalIndex);
        }

        public Type ResolveType()
        {
            return variable.LocalType;
        }

        public bool IsLValue(Visitor visitor)
        {
            return isLValue;
        }

        public Expression AsLValue(Visitor visitor)
        {
            var lv = MemberwiseClone() as LocalVariableAccess;
            lv.isLValue = true;
            return lv;
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
            Expr.Generate(visitor);
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
        bool IsLValue(Visitor visitor);
        Expression AsLValue(Visitor visitor);
    }

    public abstract class Assignable : Expression
    {
        protected bool isLValue;

        public abstract Type ResolveType();

        public bool IsLValue(Visitor visitor)
        {
            return isLValue;
        }

        public Expression AsLValue(Visitor visitor)
        {
            var lvalue = MemberwiseClone() as Assignable;
            lvalue.isLValue = true;
            return lvalue;
        }

        public abstract void PrettyPrint(Visitor visitor);
        public abstract void Generate(Visitor visitor);
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

        public bool IsLValue(Visitor visitor)
        {
            return false;
        }

        public Expression AsLValue(Visitor visitor)
        {
            throw new InvalidOperationException("Binary Expression can't be a lvalue");
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
            else if(typeof(T) == typeof(string))
            {
                generator.Emit(OpCodes.Ldstr, (string) (object) value);
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

        public bool IsLValue(Visitor visitor)
        {
            return false;
        }

        public Expression AsLValue(Visitor visitor)
        {
            throw new InvalidOperationException("Literal can't be a lvalue");
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
            visitor.Generator.LocalLookupTable.Lookup(this).Generate(visitor);
        }

        public Type ResolveType()
        {
            throw new NotImplementedException();
        }

        public bool IsLValue(Visitor visitor)
        {
            return visitor.Generator.LocalLookupTable.Lookup(this).IsLValue(visitor);
        }

        public Expression AsLValue(Visitor visitor)
        {
            return visitor.Generator.LocalLookupTable.Lookup(this).AsLValue(visitor);
        }
    }

    public class FunctionCall : Expression
    {
        private Identifier callee;
        private List<Expression> arguments;

        public FunctionCall(Identifier c, IEnumerable<Expression> args)
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
            foreach (var argument in arguments)
            {
                argument.Generate(visitor);
            }
            var function = visitor.Generator.LocalLookupTable.Lookup(callee) as MethodAccess;
            if (function == null)
            {
                throw new InvalidOperationException($"callee {callee.Name} is not a method");
            }
            if (function.ObjectType == null)
            {
                function.ObjectType = visitor.Generator.CurrentType;
            }
            var method = visitor.Generator.DeclaredMethodBuilders.Lookup(function.Name);
            visitor.Generator.CurrentMethod.GetILGenerator().Emit(OpCodes.Call,method);
        }

        public Type ResolveType()
        {
            throw new NotImplementedException();
        }

        public bool IsLValue(Visitor visitor)
        {
            return false;
        }

        public Expression AsLValue(Visitor visitor)
        {
            throw new NotImplementedException("Function can't return lvalue now");
        }
    }

    public class MemberAccess : Assignable
    {
        private FieldDeclaration fieldDeclaration;

        public MemberAccess(FieldDeclaration decl)
        {
            fieldDeclaration = decl;
        }

        override public void PrettyPrint(Visitor visitor)
        {
            throw new NotImplementedException();
        }

        override public void Generate(Visitor visitor)
        {
            var field = visitor.Generator.DeclaredFieldBuilders.Lookup(fieldDeclaration.Name);
            
            if (fieldDeclaration.IsStatic)
            {
                if (isLValue)
                {
                    visitor.Generator.CurrentMethod.GetILGenerator().Emit(OpCodes.Stsfld, field);
                }
                else
                {
                    visitor.Generator.CurrentMethod.GetILGenerator().Emit(OpCodes.Ldsfld, field);
                }
            }
            else
            {
                throw new NotImplementedException("object member can't be accessed now");
            }
        }

        override public Type ResolveType()
        {
            throw new NotImplementedException();
        }
    }

    public class ParameterAccess : Assignable
    {
        public Identifier Name { get; }
        public Type Type { get; }
        public short Index { get; }

        public ParameterAccess(Identifier n, Type t,short index)
        {
            Name = n;
            Type = t;
            Index = index;
        }

        override public void PrettyPrint(Visitor visitor)
        {
            visitor.Printer.Write("__param__->{0}",Name);
        }

        public override void Generate(Visitor visitor)
        {
            var ILGenerator = visitor.Generator.CurrentMethod.GetILGenerator();
            if (isLValue)
            {
                ILGenerator.Emit(OpCodes.Starg, Index);
            }
            else
            {
                ILGenerator.Emit(OpCodes.Ldarg, Index);
            }
        }

        override public Type ResolveType()
        {
            return Type;
        }
    }

    public class MethodAccess : Expression
    {
        public TypeBuilder ObjectType { get; set; }
        public Identifier Name { get; }

        public MethodAccess(Identifier n)
        {
            ObjectType = null;
            Name = n;
        }
        public MethodAccess(TypeBuilder ot, Identifier n)
        {
            ObjectType = ot;
            Name = n;
        }

        public void PrettyPrint(Visitor visitor)
        {
            throw new NotImplementedException();
        }

        public void Generate(Visitor visitor)
        {
            throw new NotImplementedException();
        }

        public Type ResolveType()
        {
            throw new NotImplementedException();
        }

        public bool IsLValue(Visitor visitor)
        {
            throw new NotImplementedException();
        }

        public Expression AsLValue(Visitor visitor)
        {
            throw new NotImplementedException();
        }
    }

    public class AssignExpression : Expression
    {
        private Identifier target;
        private Expression value;

        public AssignExpression(Identifier t, Expression v)
        {
            target = t;
            value = v;
        }

        public void Generate(Visitor visitor)
        {
            value.Generate(visitor);
            target.AsLValue(visitor).Generate(visitor);
        }

        public void PrettyPrint(Visitor visitor)
        {
            target.PrettyPrint(visitor);
            visitor.Printer.Write(" <- ");
            value.PrettyPrint(visitor);
        }

        public Type ResolveType()
        {
            throw new NotImplementedException();
        }

        public bool IsLValue(Visitor visitor)
        {
            throw new NotImplementedException();
        }

        public Expression AsLValue(Visitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
