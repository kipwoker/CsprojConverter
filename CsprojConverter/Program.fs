open Converter
open System.IO
open System.Text    

[<EntryPoint>]
let main args = 
    let packagesFolder = 
        if args.Length >= 1 then
            args.[0]
        else
            "packages"
    
    let packageConfig = 
        if args.Length >= 2 then
            OldPackageConfig.Load(args.[1])
        else 
            OldPackageConfig.GetSample()
    
    let project = 
        if args.Length >= 3 then
            OldCsProj.Load(args.[2])
        else 
            OldCsProj.GetSample()
            
    let output = sprintf "%A" (buildNewCsProj project packageConfig packagesFolder)
    File.WriteAllText("output.csproj", output, Encoding.UTF8)
    printfn "%s" "done"
    0 // return an integer exit code
