module Logger
    
let log (message : string) (result) =
    let log = sprintf "%s %A" message result
    printfn "%s" log
    result