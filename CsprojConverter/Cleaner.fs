module Cleaner

open System.IO

let private deleteAssemblyInfo projectDirectory =
    let assemblyInfoPath = Path.Combine(projectDirectory, "Properties", "AssemblyInfo.cs")
    File.Delete(assemblyInfoPath)
    
let private deletePackagesConfig projectDirectory =
    let assemblyInfoPath = Path.Combine(projectDirectory, "packages.config")
    File.Delete(assemblyInfoPath)

let cleanup (projectPath : string) =
    let projectDirectory = Path.GetDirectoryName(projectPath)
    projectDirectory |> deleteAssemblyInfo
    projectDirectory |> deletePackagesConfig