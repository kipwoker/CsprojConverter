module Converter

#if INTERACTIVE
#r "bin/Debug/FSharp.Data.dll"
#r "System.Xml.Linq.dll"
#endif

open FSharp.Data

type OldCsProj = XmlProvider<"templates\\old.csproj">
type NewCsProj = XmlProvider<"templates\\new.csproj">
type OldPackageConfig = XmlProvider<"templates\\oldPackage.config">

let convert<'TIn, 'TOut> (source : Option<'TIn>) (converter : ('TIn -> 'TOut)) =
    match source with
    | None -> None
    | Some source' -> source' |> converter |> Some

let convertTargetFramework (old: string) : string = old.Replace("v", "net").Replace(".", "")

let convertConfiguration (old : OldCsProj.Configuration) : NewCsProj.Configuration =
    NewCsProj.Configuration(old.Condition, old.Value)

let convertPlatform (old : OldCsProj.Platform) : NewCsProj.Platform =
    NewCsProj.Platform(old.Condition, old.Value)

let convertPropertyGroup (source : OldCsProj.PropertyGroup) (useVsHostingProccess: Option<bool>) (runPostBuildEvent: Option<string>) =
    NewCsProj.PropertyGroup(
        source.Condition,
        source.OutputType,
        source.TargetFrameworkVersion |> convert <| convertTargetFramework,
        source.Configuration |> convert <| convertConfiguration,
        source.Platform |> convert <| convertPlatform,
        source.ProjectGuid,
        source.RootNamespace,
        source.AssemblyName,
        source.FileAlignment,
        runPostBuildEvent,
        source.TreatWarningsAsErrors,
        source.AutoGenerateBindingRedirects,
        source.AppDesignerFolder,
        source.PlatformTarget,
        source.DebugSymbols,
        source.DebugType,
        source.Optimize,
        source.OutputPath,
        source.DefineConstants,
        source.ErrorReport,
        source.WarningLevel,
        source.Prefer32Bit,
        useVsHostingProccess,
        source.PostBuildEvent)

let getPropertyGroups (oldProject : OldCsProj.Project) =
    let propertyGroupsList = oldProject.PropertyGroups |> Array.toList
    match propertyGroupsList with
    | [] -> [||]
    | headPropertyGroup::tailPropertyGroups -> 
        let newHeadPropertyGroup = convertPropertyGroup headPropertyGroup None ("OnOutputUpdated" |> Some)
        let newTailPropertyGroups = tailPropertyGroups
                                    |> List.map (
                                        fun propertyGroup -> 
                                            let useVsHostingProccess = 
                                                if propertyGroup.Condition.IsSome && propertyGroup.Condition.Value.Contains("Debug") 
                                                then (Some true) 
                                                else None
                                            convertPropertyGroup propertyGroup useVsHostingProccess None
                                    )
        newHeadPropertyGroup::newTailPropertyGroups
        |> List.filter (fun propertyGroup -> propertyGroup.XElement.HasElements) 
        |> List.toArray

let convertEmbeddedResource (source : OldCsProj.EmbeddedResource) : NewCsProj.EmbeddedResource =
    NewCsProj.EmbeddedResource(source.Include)

let convertNone (source : OldCsProj.None) : NewCsProj.None =
    NewCsProj.None(source.Include, source.CopyToOutputDirectory, source.SubType)

let convertReference (source : OldCsProj.Reference) : NewCsProj.Reference =
    NewCsProj.Reference(source.Include, source.HintPath, source.Private, source.SpecificVersion)

let convertPackageReference (source : OldPackageConfig.Package) : NewCsProj.PackageReference =
    NewCsProj.PackageReference(source.Id, source.Version) 

let convertContent (source : OldCsProj.Content) : NewCsProj.Content =
    NewCsProj.Content(source.Include, source.Link, source.CopyToOutputDirectory, source.SubType)

let convertProjectReference (source : OldCsProj.ProjectReference) : NewCsProj.ProjectReference =
    NewCsProj.ProjectReference(source.Include)

let convertAnalyzer (source : OldCsProj.Analyzer) : NewCsProj.Analyzer =
    NewCsProj.Analyzer(source.Include)

let convertItemGroup (source : OldCsProj.ItemGroup) (oldConfig : OldPackageConfig.Packages) (packagesFolder : string) = 
    let references = source.References 
                        |> Array.filter (fun reference ->
                            not (oldConfig.Packages |> Array.exists (fun p -> reference.Include.Contains(p.Id))) &&
                            not (reference.HintPath.IsSome && reference.HintPath.Value.Contains(packagesFolder))
                            )
    let packages = oldConfig.Packages 
                    |> Array.filter (fun package -> source.References |> Array.exists (fun r -> r.Include.Contains(package.Id))) 
    
    NewCsProj.ItemGroup(
        source.EmbeddedResources |> Array.map (fun r -> r |> convertEmbeddedResource),
        references |> Array.map (fun r -> r |> convertReference),
        packages |> Array.map (fun r -> r |> convertPackageReference),
        source.Nones |> Array.filter (fun r -> not (r.Include = "packages.config")) |> Array.map (fun r -> r |> convertNone),
        source.Contents |> Array.map (fun r -> r |> convertContent),
        source.ProjectReferences |> Array.map (fun r -> r |> convertProjectReference),
        source.Analyzers |> Array.map (fun r -> r |> convertAnalyzer))

let getItemGroups (oldProject : OldCsProj.Project) (oldConfig : OldPackageConfig.Packages) (packagesFolder : string) =
    oldProject.ItemGroups
    |> Array.map (fun itemGroup -> convertItemGroup itemGroup oldConfig packagesFolder)
    |> Array.filter (fun itemGroup ->
                        ( 
                            itemGroup.Analyzers |> Array.isEmpty &&
                            itemGroup.Contents |> Array.isEmpty &&
                            itemGroup.References |> Array.isEmpty &&
                            itemGroup.EmbeddedResources |> Array.isEmpty &&
                            itemGroup.Nones |> Array.isEmpty &&
                            itemGroup.PackageReferences |> Array.isEmpty &&
                            itemGroup.ProjectReferences |> Array.isEmpty
                        )
                        |> not
                    )


let buildNewCsProj (oldProject : OldCsProj.Project) (oldConfig : OldPackageConfig.Packages) (packagesFolder : string) = 
    NewCsProj
        .Project(
            getPropertyGroups oldProject, 
            getItemGroups oldProject oldConfig packagesFolder,
            [| NewCsProj.Import("Microsoft.NET.Sdk", "Sdk.props"); NewCsProj.Import("Microsoft.NET.Sdk", "Sdk.targets") |])