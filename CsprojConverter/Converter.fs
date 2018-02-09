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
        useVsHostingProccess)

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
                                                if propertyGroup.Condition.IsSome && propertyGroup.Condition.Value.ToLower().Contains("Debug".ToLower())
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

let convertItemGroup (source : OldCsProj.ItemGroup) (oldConfig : Option<OldPackageConfig.Packages>) (packagesFolder : string) =
    let oldConfigPackages =
        if (oldConfig.IsSome) then
            oldConfig.Value.Packages
        else
            [||]
    let references = source.References
                        |> Array.filter (fun reference ->
                            not (oldConfigPackages |> Array.exists (fun p -> reference.Include.ToLower().Contains(p.Id.ToLower()))) &&
                            not (reference.HintPath.IsSome && reference.HintPath.Value.ToLower().Contains(packagesFolder.ToLower()))
                            )
    let packages = oldConfigPackages
                    |> Array.filter (fun package -> source.References |> Array.exists (fun r -> r.Include.ToLower().Contains(package.Id.ToLower())))

    NewCsProj.ItemGroup(
        source.EmbeddedResources |> Array.map (fun r -> r |> convertEmbeddedResource),
        references |> Array.map (fun r -> r |> convertReference),
        packages |> Array.map (fun r -> r |> convertPackageReference),
        source.Nones |> Array.filter (fun r -> not (r.Include = "packages.config")) |> Array.map (fun r -> r |> convertNone),
        source.Contents |> Array.map (fun r -> r |> convertContent),
        source.ProjectReferences |> Array.map (fun r -> r |> convertProjectReference),
        source.Analyzers |> Array.map (fun r -> r |> convertAnalyzer))

let getItemGroups (oldProject : OldCsProj.Project) (oldConfig : Option<OldPackageConfig.Packages>) (packagesFolder : string) =
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

let private convertBuildEvent (target : string) (command : string) =
    let exec = NewCsProj.Exec(command)
    NewCsProj.Target(target, target + "Event", exec)


let private getTargets (oldProject : OldCsProj.Project) =
    let convertEvents (chooseEvent : (OldCsProj.PropertyGroup -> string option)) (target : string) =
        oldProject.PropertyGroups
            |> Array.filter (fun p -> (chooseEvent p).IsSome)
            |> Array.map (fun p -> convertBuildEvent target (chooseEvent p).Value)

    let postBuildEvents = convertEvents (fun p -> p.PostBuildEvent) "PostBuild"
    let preBuildEvents = convertEvents (fun p -> p.PreBuildEvent) "PreBuild"

    [preBuildEvents; postBuildEvents] |> Array.concat

let buildNewCsProj (oldProject : OldCsProj.Project) (oldConfig : Option<OldPackageConfig.Packages>) (packagesFolder : string) =
    NewCsProj
        .Project(
            getPropertyGroups oldProject,
            getItemGroups oldProject oldConfig packagesFolder,
            [| NewCsProj.Import("Microsoft.NET.Sdk", "Sdk.props"); NewCsProj.Import("Microsoft.NET.Sdk", "Sdk.targets") |],
            getTargets oldProject)
