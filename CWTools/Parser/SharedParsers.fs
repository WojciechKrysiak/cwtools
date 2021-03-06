﻿namespace CWTools.Parser
open FParsec
open Microsoft.FSharp.Compiler.Range
open Types

module internal SharedParsers = 
    let (<!>) (p: Parser<_,_>) label : Parser<_,_> =
        fun stream ->
            eprintfn "%A: Entering %s" stream.Position label
            let reply = p stream
            eprintfn "%A: Leaving %s (%A)" stream.Position label reply.Status
            reply

    let betweenL (popen: Parser<_,_>) (pclose: Parser<_,_>) (p: Parser<_,_>) label =
        let expectedLabel = expected label
        let notClosedError (pos: FParsec.Position) =
            messageError (sprintf "The %s opened at %s was not closed."
                                label (pos.ToString()))
        fun (stream: CharStream<_>) ->
        // The following code might look a bit complicated, but that's mainly
        // because we manually apply three parsers in sequence and have to merge
        // the errors when they refer to the same parser state.
        let state0 = stream.State
        let reply1 = popen stream
        if reply1.Status = Ok then
            let stateTag1 = stream.StateTag
            let reply2 = p stream
            let error2 = if stateTag1 <> stream.StateTag then reply2.Error
                         else mergeErrors reply1.Error reply2.Error
            if reply2.Status = Ok then
                let stateTag2 = stream.StateTag
                let reply3 = pclose stream
                let error3 = if stateTag2 <> stream.StateTag then reply3.Error
                             else mergeErrors error2 reply3.Error
                if reply3.Status = Ok then
                    Reply(Ok, reply2.Result, error3)
                else
                    Reply(reply3.Status,
                        mergeErrors error3 (notClosedError (state0.GetPosition(stream))))
            else
                Reply(reply2.Status, reply2.Error)
        else
            let error = if state0.Tag <> stream.StateTag then reply1.Error
                        else expectedLabel
            Reply(reply1.Status, error)

    // Sets of chars
    // =======
    let whitespaceTextChars = " \t\r\n"
    let norseChars =['ö';'ð';'æ';'ó';'ä';'Þ';'Å';'Ö']
    let isidchar = fun c -> isLetter c || isDigit c || isAnyOf ['_'; ':'; '@'; '.'; '\"'; '-'; '''; '['; ']'; '!'; '<'; '>'] c
    let isvaluechar = fun c -> isLetter c || isDigit c || isAnyOf (['_'; '.'; '-'; ':'; '\''; '['; ']'; '@';'''; '+'; '`'; '%'; '/'; '!'; ','; '<'; '>'; '?'] @ ['š'; 'Š'; '’']) c


    // Utility parsers
    // =======
    let ws = spaces <?> "whitespace"
    let str s = pstring s .>> ws <?> ("string " + s)
    let strSkip s = skipString s .>> ws <?> ("skip string " + s)
    let ch c = pchar c .>> ws <?> ("char " + string c)
    let chSkip c = skipChar c .>> ws <?> ("skip char " + string c)
    let clause inner = betweenL (chSkip '{' <?> "opening brace") (skipChar '}' <?> "closing brace") inner "clause"
    let quotedCharSnippet = many1Satisfy (fun c -> c <> '\\' && c <> '"')
    let escapedChar = (pstring "\\\"" <|> pstring "\\") |>> string

    // Base types
    // =======
    let operator = choiceL [pchar '='; pchar '>'; pchar '<'] "operator 1" >>. optional (chSkip '=' <?> "operator 2") .>> ws <?> "operator"
    let operatorLookahead = choice [chSkip '='; chSkip '>'; chSkip '<'] <?> "operator 1"
    let comment = skipChar '#' >>. restOfLine true .>> ws |>> string <?> "comment"

    let key = (many1SatisfyL isidchar "id character") .>> ws |>> (fun s -> System.String.Intern(s)) |>> Key <?> "id"
    let keyQ =  between (ch '"') (ch '"') (manyStrings (quotedCharSnippet <|> escapedChar)) .>> ws |>> (fun s -> "\""+s+"\"") |>> Key <?> "quoted key"

    let valueS = (many1SatisfyL isvaluechar "value character") |>> string |>> (fun s -> System.String.Intern(s)) |>> String <?> "string"

    let valueQ = between (ch '"') (ch '"') (manyStrings (quotedCharSnippet <|> escapedChar)) |>> QString <?> "quoted string"

    // let valueB = ( (skipString "yes") .>> nextCharSatisfiesNot (isvaluechar)  |>> (fun _ -> Bool(true))) <|>
    //                 ((skipString "no") .>> nextCharSatisfiesNot (isvaluechar)  |>> (fun _ -> Bool(false)))
    let valueBYes = skipString "yes" .>> nextCharSatisfiesNot (isvaluechar) |>> (fun _ -> Bool(true))
    let valueBNo = skipString "no" .>> nextCharSatisfiesNot (isvaluechar) |>> (fun _ -> Bool(false))

    let valueI = pint64 .>> nextCharSatisfiesNot (isvaluechar) |>> int |>> Int
    let valueF = pfloat .>> nextCharSatisfiesNot (isvaluechar) |>> float |>> Float

    let hsv3 = clause (pipe3 ((valueF .>> ws) .>> ws) (valueF .>> ws) (valueF .>> ws) (fun a b c -> Clause [Statement.Value a;Statement.Value b; Statement.Value c]))
    let hsv4 = clause (pipe4 (valueF .>> ws) (valueF .>> ws) (valueF .>> ws) (valueF .>> ws) (fun a b c d -> Clause [Statement.Value a;Statement.Value b; Statement.Value c; Statement.Value d]))
    let hsvI =
        clause (pipe4 (valueF .>> ws) (valueF .>> ws) (valueF .>> ws) (opt (valueF .>> ws))
            (fun a b c d ->
            match (a, b, c, d) with
            | (a, b, c, (Some d)) -> Clause [Statement.Value a;Statement.Value b; Statement.Value c; Statement.Value d]
            | (a, b, c, None) -> Clause [Statement.Value a;Statement.Value b; Statement.Value c;]))
    let hsv = strSkip "hsv" >>. hsvI .>> ws
    let rgbI = clause (pipe4 (valueI .>> ws) (valueI .>> ws) (valueI .>> ws) (opt (valueI .>> ws))
            (fun a b c d ->
            match (a, b, c, d) with
            | (a, b, c, (Some d)) -> Clause [Statement.Value a;Statement.Value b; Statement.Value c; Statement.Value d]
            | (a, b, c, None) -> Clause [Statement.Value a;Statement.Value b; Statement.Value c;]))


    let rgb3 = clause (pipe3 (valueI .>> ws) (valueI .>> ws) (valueI .>> ws) (fun a b c -> Clause [Statement.Value a;Statement.Value b; Statement.Value c]))
    let rgb4 = clause (pipe4 (valueI .>> ws) (valueI .>> ws) (valueI .>> ws) (valueI .>> ws) (fun a b c d -> Clause [Statement.Value a;Statement.Value b; Statement.Value c; Statement.Value d]))
    let rgb = strSkip "rgb" >>. rgbI .>> ws

    // Complex types
    // =======

    // Recursive types
    let keyvalue, keyvalueimpl = createParserForwardedToRef()
    let value, valueimpl = createParserForwardedToRef()
    let statement = comment |>> Comment <|> (attempt (((value .>> ws)) .>> notFollowedBy operatorLookahead |>> Value)) <|> (keyvalue) <?> "statement"
    let valueBlock = clause (many1 ((value .>> ws |>> Value) <|> (comment |>> Comment))) |>> Clause <?> "value clause"

    let valueClause = clause (many statement) |>> Clause <?> "statement clause"

    let valueCustom : Parser<Value, unit> =
        let vcP = attempt valueClause
        let vbP = attempt valueBlock
        let iP = attempt valueI
        let fP = attempt valueF
        let byP = attempt valueBYes <|> valueS
        let bnP = attempt valueBNo <|> valueS
        fun (stream: CharStream<_>) ->
            match stream.Peek() with
            | '{' ->
                let vc = (vcP stream)
                if vc.Status = Ok then vc else
                    let vb = (vbP stream)
                    if vb.Status <> Ok then valueClause stream else vb
                // let vb = (attempt valueBlock stream)
                // if vb.Status = Ok then vb else valueClause stream
            | '"' -> valueQ stream
            | x when isDigit x || x = '-' ->
                let i = (iP stream)
                if i.Status = Ok then i else
                    let f = (fP stream)
                    if f.Status = Ok then f else
                    valueS stream
            | _ ->
                match stream.PeekString 3, stream.PeekString 2 with
                | "rgb", _ -> rgb stream
                | "hsv", _ -> hsv stream
                | "yes", _ -> byP stream
                | _, "no" -> bnP stream
                | _ -> valueS stream
                //| _ -> choice [(attempt valueB); valueS] stream
                //choiceL [(attempt valueB); (attempt hsv); (attempt rgb); valueS] "value" stream



    valueimpl := valueCustom <?> "value"
    let getRange (start: FParsec.Position) (endp : FParsec.Position) = mkRange start.StreamName (mkPos (int start.Line) (int start.Column - 1)) (mkPos (int endp.Line) (int endp.Column - 1))
    keyvalueimpl := pipe4 (getPosition) ((keyQ <|> key) .>> operator) (value) (getPosition .>> ws) (fun start id value endp -> KeyValue(PosKeyValue(getRange start endp, KeyValueItem(id, value))))
    let alle = ws >>. many statement .>> eof |>> (fun f -> (ParsedFile f : ParsedFile))
    let valuelist = many1 ((comment |>> Comment) <|> (value .>> ws |>> Value)) .>> eof
    let statementlist = (many statement) .>> eof
    let all = ws >>. ((attempt valuelist) <|> statementlist)
