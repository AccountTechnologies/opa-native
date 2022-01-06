#r "paket:
nuget Fake.DotNet.Cli
nuget Fake.IO.FileSystem
nuget Fake.Api.GitHub
nuget Fake.Core.Target //"
#load ".fake/build.fsx/intellisense.fsx"
open Fake.Core
open Fake.DotNet
open Fake.DotNet.NuGet.NuGet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.Api
open Octokit

Target.initEnvironment ()

let ghClient () = 
  Environment.environVar "GITHUB_TOKEN" |> GitHub.createClientWithToken

let owner = "AccountTechnologies"
let repoName = "opa-server-nuget"

let needRelease (client:Async<GitHubClient>) = 
  async {
    Trace.logfn "open-policy-agent/opa:"
    let! lastSourceRelease = client |> GitHub.getLastRelease "open-policy-agent" "opa"
    Trace.logfn "AccountTechnologies/opa-server-nuget:"
    let! lastTargetRelease = client |> GitHub.getLastRelease owner repoName
    return
      if lastSourceRelease.Release.Name = lastTargetRelease.Release.Name
      then None else Some (lastSourceRelease)
  }

let newRelease =
  let maybeRelease = ghClient () |> needRelease |> Async.RunSynchronously
  
  if maybeRelease.IsNone then
    Trace.logfn "Up to date. Nothing left to do."

  maybeRelease

Target.create "clean" (fun _ ->
    !! "src/**/bin"
    ++ "src/**/obj"
    |> Shell.cleanDirs
)

let getOpaBinaries () = 
  async {
    Directory.create ".binaries"
    do! async.Return(newRelease.Value) |> GitHub.downloadAssets ".binaries"
  }

Target.create "opa-binaries" (fun _ ->

  getOpaBinaries () |> Async.RunSynchronously
)

Target.create "gh-release" (fun _ ->

  let srcTagName = newRelease.Value.Release.TagName
  let settings (ps:GitHub.CreateReleaseParams) =
    {
      ps with
        Draft = false
        Name = srcTagName
        Body = $"Packaged from: { newRelease.Value.Release.HtmlUrl }"
        Prerelease = false
    }

  ghClient()
  |> GitHub.createRelease owner repoName srcTagName settings
  |> Async.RunSynchronously
  |> ignore
)

Target.create "nuget-release" <| fun _ ->
  "src/Atech.Opa.Server" |> NuGet (fun ps ->
    {
      ps with
        AccessKey = Environment.environVar "NUGET_KEY"
    })

Target.create "all" ignore

"clean"
  =?> ("opa-binaries", newRelease.IsSome)
  =?> ("gh-release", newRelease.IsSome)
  ==> "nuget-release"
  =?> ("all", newRelease.IsSome)

let ctx = Target.WithContext.runOrDefault "all"
Target.updateBuildStatus ctx
Target.raiseIfError ctx
