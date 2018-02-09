module DirectoryParser

open System.IO

let rec private allFiles dirs =
    if Seq.isEmpty dirs then Seq.empty else
        seq { yield! dirs |> Seq.collect (fun f -> Directory.EnumerateFiles(f, "*.csproj"))
              yield! dirs |> Seq.collect Directory.EnumerateDirectories |> allFiles }

let getProjects (repositoryPath : string) =
    Directory.EnumerateDirectories(repositoryPath) 
                            |> allFiles
                            |> Seq.toArray
