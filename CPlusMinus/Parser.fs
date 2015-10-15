﻿module Parser

open FParsec;
open System;
open System.Reflection;
open System.Reflection.Emit;
open AST;
open System.Collections.Generic

type Parser<'a> = Parser<'a,unit>

//identifier
//identifier = { letter }
//identifierList = identifier, [".", identifier]
let identifier:Parser<_> = parse{
    let! id = many1Satisfy isLetter .>> spaces
    return new Identifier(id) 
}
let identifierList:Parser<_> =
    sepBy1 identifier (skipChar '.' .>> spaces)

//literals
let naturalIntegerLiteral:Parser<_> = pint32 .>> spaces |>> fun i -> new Literal<System.Int32>(i)
let floatLiteral:Parser<_> = pfloat .>> spaces |>> fun f -> new Literal<System.Double>(f)
let stringLiteral:Parser<_> = parse{
    do! skipChar '"'
    let! str = manyChars (noneOf "\"")
    do! skipChar '"'
    return new Identifier(str)
}

//Expression
//expression = addExpression
//addExpression = mulExpression | addExpression, "+", mulExpression | addExpression, "-", mulExpression
//mulExpression = unaryExpression | mulExpression, "*", unaryExpression | mulExpression, "/", unaryExpression
//unaryExpression = naturalIntegerLiteral
//callExpression = identifierList, expressionList
//expressionList = "(", expressionList, ",", expression, ")"

let expression, expressionRef = createParserForwardedToRef ()
let statement, statementRef = createParserForwardedToRef ()

let mulExpression:Parser<_> = 
    let anyLiteral = (floatLiteral |>> fun lit -> lit :> Expression) <|> (naturalIntegerLiteral |>> fun lit -> lit :> Expression)
    let term = 
        between (skipChar '(' .>> spaces) (skipChar ')' .>> spaces) expression
    let unaryExpr = anyLiteral <|> (identifier |>> fun id -> id :> Expression) <|> term
    let mulOperator = skipChar '*' .>> spaces >>% fun lhs rhs -> new BinaryExpression(BinaryExpression.ExpressionType.Multiply,lhs,rhs) :> Expression
    let divOperator = skipChar '/' .>> spaces >>% fun lhs rhs -> new BinaryExpression(BinaryExpression.ExpressionType.Divide,lhs,rhs) :> Expression
    let operator = mulOperator <|> divOperator
    chainl1 unaryExpr operator <?> "multiply expression"

let addExpression:Parser<_> =
    let addOperator = skipChar '+' .>> spaces >>% fun lhs rhs -> new BinaryExpression(BinaryExpression.ExpressionType.Add,lhs,rhs) :> Expression
    let subOperator = skipChar '-' .>> spaces >>% fun lhs rhs -> new BinaryExpression(BinaryExpression.ExpressionType.Subtract,lhs,rhs) :> Expression
    let operator = addOperator <|> subOperator
    chainl1 mulExpression operator <?> "add expression"

let expressionList:Parser<_> = 
    let delimiter = skipChar ',' >>. spaces >>% (fun x xs -> List.concat [x;xs])
    let expressionListBody = chainl (expression |>> (fun expr -> [expr])) delimiter []
    (skipChar '(' >>. spaces >>. expressionListBody .>> skipChar ')' .>> spaces)
    
let callExpression:Parser<_> = parse{
    let! callee = identifier
    let! args = expressionList
    return new FunctionCall(callee,args)
}

do expressionRef := 
    choice [
        attempt (callExpression |>> fun call -> call :> Expression)
        addExpression
    ] <?> "expression"

//Declaration
//variableDeclarlationStatement = "let", identifier, ":", identifier, "=", initializer, ";"
//functionDeclarlationStatement = "fn", identifier, "(", parameterList, ")", "=", statement
//initializer = expression
//parameterList = parameterList, parameter | parameter
let initializer = expression
let parameter = parse{
    let! id = identifier .>> pchar ':' .>> spaces
    let! t = identifierList
    return new VariableDeclaration(id,t);
}
let letKeyword:Parser<_> = skipString "let" .>> spaces
let fnKeyword:Parser<_> = skipString "fn" .>> spaces
let classKeyword:Parser<_> = skipString "class" .>> spaces
let variableDeclarlationStatement:Parser<_> = parse{
    let! name = letKeyword >>. identifier .>> skipChar ':' .>> spaces
    let! t = identifierList
    let initializerParser = skipChar '=' >>. spaces >>. initializer .>> pchar ';' .>> spaces
    let! initializer = opt initializerParser
    match initializer with
    | Some(initializer) -> return new VariableDeclaration(name,t,initializer)
    | None -> return new VariableDeclaration(name,t,null)
}
let parameterList:Parser<_> = sepBy parameter (skipChar ',' .>> spaces)
let functionDeclarationStatement:Parser<_> = parse{
    let! name = fnKeyword >>. identifier
    let! param = between (skipChar '(' .>> spaces) (skipChar ')' .>> spaces) parameterList
    let! body = skipChar '=' >>. spaces >>. statement
    return new MethodDeclaration(name,param,body);
}
let classDeclarationStatement:Parser<_> =
    let mutable variables:VariableDeclaration list = []
    let mutable methods:MethodDeclaration list = []
    let anyDeclaration = (functionDeclarationStatement |>> (fun f -> methods <- f::methods)) <|> (variableDeclarlationStatement |>> (fun v -> variables <- v::variables))
    ((classKeyword >>. identifier) 
    .>> (pchar '{' >>. spaces >>. (many anyDeclaration) .>> pchar '}' .>> spaces))
    |>> (fun id -> new ClassDeclaration(id,variables,methods))

//statement = singleStatement | compoundStatement
//expressionStatement = expression, ";"
//singleStatement = declarationStatement | expressionStatement
//compoundStatement = "{", singleStatement*, "}"
//ifStatement = "if", "(", expression, ")", statement, ("elif", "(", expression, ")", statement)*, ["else", statement]
let expressionStatement = 
    expression .>> skipChar ';' .>> spaces 
    |>> fun expr -> new ExpressionStatement(expr) 
    <?> "expression statement"
let ifStatement = parse{
    let elifParser = 
        tuple2 (skipString "elif" >>. spaces >>. skipChar '(' >>. spaces >>. expression .>> skipChar ')' .>> spaces)
               statement
    let elseParser = skipString "else" >>. statement
    let! cond = (skipString "if" >>. spaces >>. skipChar '(' >>. spaces >>. expression .>> skipChar ')' .>> spaces)
    let! thenStmt = statement
    let! elifStmt = many elifParser
    let! elseStmt = elseParser
    return new IfStatement(cond,thenStmt,elseStmt,elifStmt)
    //return new ExpressionStatement(new Literal<System.String>("why cant use ifStatement"))
}
let returnStatement = 
    let parser = parse{
        do! skipString "return" .>> spaces
        let! expr = expressionStatement
        return new ReturnStatement(expr)
    }
    parser <?> "return statement"

let singleStatement = 
    let asStatement = fun stmt -> stmt :> Statement
    choice [
        variableDeclarlationStatement |>> asStatement
        functionDeclarationStatement |>> asStatement
        classDeclarationStatement |>> asStatement
        returnStatement |>> asStatement
        ifStatement |>> asStatement
        expressionStatement |>> asStatement
    ] <?> "single statement"
let openBracket = skipChar '{' .>> spaces
let closeBracket = skipChar '}' .>> spaces
let compoundStatement =
    between openBracket closeBracket (many singleStatement)
    |>> fun stmts -> new CompoundStatement(stmts) :> Statement
    <?> "compound statement"
do statementRef := singleStatement <|> compoundStatement <?> "statement"

let namespaceDeclaration = parse{
    let namespaceKeyword = skipString "namespace" .>> spaces
    let! name = namespaceKeyword >>. identifier
    let! classes = between openBracket closeBracket (many classDeclarationStatement)
    return new NamespaceDeclaration(name,classes)
}

let language = spaces >>. namespaceDeclaration

type CompileError(line:int64,column:int64,message:string[]) =
    inherit Exception()
    override this.ToString() = (String.concat "\n" message) + "\n" + base.ToString()

let runParser str = 
    match run language str with
    | Success(result,_,_) -> result
    | Failure(_,err,_) ->
        //let messages = ErrorMessageList.ToSortedArray(err.Messages)
        //raise (new CompileError(err.Position.Line,err.Position.Column,Array.map (fun x -> x.ToString()) messages))
        failwith (err.ToString())
(*
[<EntryPoint>]
let main argv = 
    let CPMAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("CPlusMinusAssembly"),AssemblyBuilderAccess.RunAndSave);
    let CPMModule = CPMAssembly.DefineDynamicModule("CPlusMinusModule","CPlusMinusModule.mod");
    let CPMType = CPMModule.DefineType("CPlusMinusType",TypeAttributes.Public);
    let CPMMethod = CPMType.DefineMethod("Main",MethodAttributes.Static ||| MethodAttributes.Public,typeof<System.Void>,List.toArray [typeof<System.String[]>]);
    let Generator = CPMMethod.GetILGenerator();
    let fileName = "test.cpm"
    let ast = runParserOnFile statement () fileName Text.Encoding.UTF8
    let ast = run language "
        if(x+y){2;}elif(x){x;}else{3;}
    "
    match ast with
    | Success(result,_,_) -> printfn "%A" result
    | Failure(_,err,_) -> printfn "%A" err
    (* match ast with
    | Success(result,_,_) -> 
        let visitor = new CodeGenerator(CPMModule,CPMType,CPMMethod,Generator)
        do
            result.Visit(visitor)
            Generator.Emit(OpCodes.Ret);
            ignore (CPMType.CreateType());
            CPMAssembly.SetEntryPoint(CPMMethod);
            CPMAssembly.Save("a.exe");
            printfn ""
    | Failure(_,err,_) -> printfn "%A" err *)
    0 // 整数の終了コードを返します
*)