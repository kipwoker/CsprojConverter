open Converter
open Cleaner
open DirectoryParser
open Logger
open System
open System.IO
open System.Text

let parseParameters (args : string[]) =
    match args |> Array.toList with
    | repositoryFolder::packagesFolder::projectTypes::_ -> (Some repositoryFolder, Some packagesFolder, Some projectTypes) |> log "Parsed"
    | _ -> (None, None, None)

let processProject (projectPath : string) (packagesFolder : string) =
    let packageConfigPath = Path.Combine(Path.GetDirectoryName(projectPath), "packages.config")
    let packageConfig =
        if (File.Exists(packageConfigPath))
        then
            Some(OldPackageConfig.Load(packageConfigPath))
        else
            None
    let project = OldCsProj.Load(projectPath)
    let output = sprintf "%A" (buildNewCsProj project packageConfig packagesFolder)
    File.WriteAllText(projectPath, output, Encoding.UTF8)
    projectPath |> cleanup

let processRepository (repositoryFolder : string) (packagesFolder : string) (excludeProjectTypes : string[]) =
    let projects = repositoryFolder
                    |> getProjects
                    |> Array.filter (fun projectPath ->
                        let fileText = File.ReadAllText(projectPath).ToLower()
                        let existsIgnoredProjects =
                            excludeProjectTypes
                            |> Array.map (fun projectType -> projectType.ToLower())
                            |> Array.exists (fun projectType -> fileText.Contains(projectType))
                        existsIgnoredProjects |> not)

    projects.Length |> log "Projects for handle" |> ignore
    projects
    |> Array.iter (fun projectPath ->
        printf "Handle %s ..." projectPath
        processProject projectPath packagesFolder
        printfn "DONE"
        )

[<EntryPoint>]
let main args =
    let (repositoryFolder, packagesFolder, excludeProjectTypesString) = parseParameters args

    let excludeProjectTypes =
        match excludeProjectTypesString with
        | Some projectTypes -> projectTypes.Split( [| ' '; ';'; '.' |] )
        | None -> Array.empty

    match (repositoryFolder, packagesFolder, excludeProjectTypes) with
    | (Some repositoryFolder', Some packagesFolder', excludeProjectTypes') -> processRepository repositoryFolder' packagesFolder' excludeProjectTypes'
    | _ -> printfn "%s" "Wrong args"

    printfn "%s" "done"
    0 // return an integer exit code
