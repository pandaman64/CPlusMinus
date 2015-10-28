module Parser

open FParsec;
open System;
open System.Reflection;
open System.Reflection.Emit;
open AST;
open System.Collections.Generic

type Context = {
    Messages : string list
    Verbose : bool
}

let appendMessage message ctx = {ctx with Messages = message::ctx.Messages}
let appendMessageParser fmt = 
    let appender str = updateUserState (fun ctx -> appendMessage str ctx)
    Printf.ksprintf appender fmt
let nullContext verb = {Messages = [""];Verbose = verb}

//from fparsec page
let (<!>) (p: Parser<_,Context>) label : Parser<_,_> =
    fun stream ->
        if stream.UserState.Verbose then
            printfn "%A: Entering %s" stream.Position label
        let reply = p stream
        if stream.UserState.Verbose then
            printfn "%A: Leaving %s (%A)" stream.Position label reply.Status
        reply

type Parser<'a> = Parser<'a,Context>

//identifier
//identifier = { letter }
//identifierList = identifier, [".", identifier]
let identifier:Parser<_> = parse{
    let identifierHead c = isLetter c || c = '_'
    let identifierTail c = isLetter c || c = '_' || Char.IsDigit c
    let! id = many1Satisfy2 identifierHead identifierTail .>> spaces
    //do! appendMessageParser "parse identifier %s" id
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

let binaryOperator opString exprType =
    skipString opString .>> spaces >>% fun lhs rhs -> new BinaryExpression(exprType,lhs,rhs) :> Expression

let mulExpression:Parser<_> = 
    let anyLiteral = 
        choice[
            attempt ((naturalIntegerLiteral |>> fun lit -> lit :> Expression) .>> (notFollowedByString "."))
            (floatLiteral |>> fun lit -> lit :> Expression)
            (stringLiteral |>> fun lit -> lit :> Expression)
        ]
    let term = 
        between (skipChar '(' .>> spaces) (skipChar ')' .>> spaces) expression
    let unaryExpr = anyLiteral <|> (identifier |>> fun id -> id :> Expression) <|> term
    let mulOperator = binaryOperator "*" BinaryExpression.ExpressionType.Multiply
    let divOperator = binaryOperator "/" BinaryExpression.ExpressionType.Divide
    let operator = mulOperator <|> divOperator
    chainl1 unaryExpr operator <!> "multiply expression"

let addExpression:Parser<_> =
    let addOperator = binaryOperator "+" BinaryExpression.ExpressionType.Add
    let subOperator = binaryOperator "-" BinaryExpression.ExpressionType.Subtract
    let operator = addOperator <|> subOperator
    chainl1 mulExpression operator <!> "add expression"

let equalExpression= parse{
    let equalOperator = attempt (binaryOperator "=" BinaryExpression.ExpressionType.Equal)
    let notEqualOperaor = attempt (binaryOperator "<>" BinaryExpression.ExpressionType.NotEqual)
    let operator = equalOperator <|> notEqualOperaor
    let! op = operator
    let! rhs = addExpression
    return fun lhs -> op lhs rhs
}

let relationalExpression = parse{
    let greaterThan = binaryOperator ">" BinaryExpression.ExpressionType.GreaterThan .>>? notFollowedByString "="
    let greaterThanOrEq = binaryOperator ">=" BinaryExpression.ExpressionType.GreaterThanOrEqual
    let lessThan = binaryOperator "<" BinaryExpression.ExpressionType.LessThan .>>? notFollowedByString "="
    let lessThanOrEq = binaryOperator "<=" BinaryExpression.ExpressionType.LessThanOrEqual
    let operator = 
        choice[
            greaterThan <!> "gt expr"
            greaterThanOrEq <!> "egt expr"
            lessThan <!> "lt expr"
            lessThanOrEq <!> "elt expr"
        ]
    let! op = operator
    let! rhs = addExpression
    return fun lhs -> op lhs rhs
}
let compareExpression = parse{
    let! lhs = addExpression
    let! op = choice[
                equalExpression <!> "equal expr"
                relationalExpression <!> "rel expr"
              ] <!> "compare expression" 
    return (op lhs)
}
let logicalAndExpression =
    let operator = binaryOperator "&&" BinaryExpression.ExpressionType.LogicalAnd
    chainl1 compareExpression operator <!> "logical and expression"

let logicalOrExpression =
    let operator = binaryOperator "||" BinaryExpression.ExpressionType.LogicalOr
    chainl1 logicalAndExpression operator <!> "logical or expression"

let expressionList:Parser<_> = 
    let delimiter = skipChar ',' >>. spaces >>% (fun x xs -> List.concat [x;xs])
    let expressionListBody = chainl (expression |>> (fun expr -> [expr])) delimiter []
    (skipChar '(' >>. spaces >>. expressionListBody .>> skipChar ')' .>> spaces)
    
let callExpression:Parser<_> = parse{
    let! callee = identifier
    let! args = expressionList
    return new FunctionCall(callee,args)
}

let assignExpression:Parser<_> = parse{
    let! target = identifier
    do! skipString "<-" .>> spaces
    let! value = expression
    return new AssignExpression(target,value)
}

do expressionRef := 
    choice [
        attempt (callExpression |>> fun call -> call :> Expression)
        attempt (assignExpression |>> fun assign -> assign :> Expression)
        attempt logicalOrExpression
        addExpression
    ] <!> "expression"

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
let letKeyword:Parser<_> = skipString "let" .>> spaces1
let fnKeyword:Parser<_> = skipString "fn" .>> spaces1
let classKeyword:Parser<_> = skipString "class" .>> spaces1
let optionalKeyword keyword = opt (pstring keyword) .>> spaces1
let pinit = 
    let initializerParser = skipChar '=' >>. spaces >>. initializer .>> pchar ';' .>> spaces1
    opt initializerParser
let parameterDeclaration = parse{
    let! name = (attempt letKeyword) >>. identifier .>> skipChar ':' .>> spaces
    let! t = identifierList
    return new ParameterDeclaration(name,t)
}
let fieldDeclaration isStatic = parse{
    let! field = parameterDeclaration
    let! initializer = pinit
    match initializer with
    | Some(initializer) -> return new FieldDeclaration(field.Name,field.Type,initializer,isStatic)
    | None -> return new FieldDeclaration(field.Name,field.Type,null,isStatic)
}
let variableDeclarlationStatement:Parser<_> = parse{
    let! name = letKeyword >>. identifier .>> skipChar ':' .>> spaces
    let! t = identifierList
    let! initializer = pinit
    match initializer with
    | Some(initializer) -> return new VariableDeclaration(name,t,initializer)
    | None -> return new VariableDeclaration(name,t,null)
}
let parameterList:Parser<_> = sepBy parameter (skipChar ',' .>> spaces)
let functionDeclarationStatement isStatic :Parser<_> = parse{
    let! name = (attempt fnKeyword) >>. identifier
    let! param = between (skipChar '(' .>> spaces) (skipChar ')' .>> spaces) parameterList
    let! returnType = skipChar ':' >>. spaces >>. identifierList
    let! body = skipChar '=' >>. spaces >>. statement
    return new MethodDeclaration(name,param,body,returnType,isStatic);
}
let classDeclarationStatement:Parser<_> = parse{
    let either left right x = 
        match x with
        | Choice1Of2(y) -> left y
        | Choice2Of2(y) -> right y
    let left x (l,r) = (x::l,r)
    let right x (l,r) = (l,x::r)
    let folder s e = either left right e s
    let partitionEithers = List.fold folder ([],[])
    //let mutable fields:FieldDeclaration list = []
    //let mutable methods:MethodDeclaration list = []
    //let anyDeclaration = attempt (functionDeclarationStatement |>> (fun f -> methods <- f::methods)) 
                            //<|> (fieldDeclaration |>> (fun v -> fields <- v::fields))
    let! id = classKeyword >>. identifier
    let anyDeclaration = parse{
        let! isStatic = optionalKeyword "static"
        let! declaration =  (functionDeclarationStatement isStatic.IsSome |>> Choice1Of2)
                        <|> (fieldDeclaration isStatic.IsSome |>> Choice2Of2)
        return declaration
    }
    let! declarations = pchar '{' >>. spaces >>. (many anyDeclaration) .>> pchar '}' .>> spaces
    let (methods,fields) = partitionEithers declarations
    //((classKeyword >>. identifier) 
    //.>> (pchar '{' >>. spaces >>. (many anyDeclaration) .>> pchar '}' .>> spaces))
    //|>> (fun id -> new ClassDeclaration(id,fields,methods))
    return new ClassDeclaration(id,fields,methods)
}

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
        return new ReturnStatement(expr.Expr)
    }
    parser <?> "return statement"

let singleStatement = 
    let asStatement = fun stmt -> stmt :> Statement
    choice [
        variableDeclarlationStatement |>> asStatement
        //functionDeclarationStatement |>> asStatement
        //classDeclarationStatement |>> asStatement
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

let runAnyParser str parser verb =
    match runParserOnString parser (nullContext verb) "" str with
    | Success(result,state,_) -> (result,state.Messages |> List.toSeq)
    | Failure(_) as failure ->
        //let messages = ErrorMessageList.ToSortedArray(err.Messages)
        //raise (new CompileError(err.Position.Line,err.Position.Column,Array.map (fun x -> x.ToString()) messages))
        failwith (sprintf "%A" failure)
let runCompareParser str verb =
    runAnyParser str logicalOrExpression verb
let runParser str verbose = 
    runAnyParser str language verbose
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