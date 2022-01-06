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

let ghClient = 
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
  let maybeRelease = ghClient |> needRelease |> Async.RunSynchronously
  
  if maybeRelease.IsNone then
    Trace.logfn "Up to date. Nothing left to do."

  maybeRelease

Target.create "clean" (fun _ ->
    !! "src/**/bin"
    ++ "src/**/obj"
    |> Shell.cleanDirs
)


Target.create "opa-binaries" (fun _ ->

  let getOpaBinaries () = 
    async {
      Directory.create ".binaries"
      let release =
        match newRelease with
        | Some r -> r |> async.Return
        | None -> ghClient |> GitHub.getReleaseByTag owner repoName (Environment.environVar "PKGVER")

      do! release |> GitHub.downloadAssets ".binaries"
    }

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

  ghClient
  |> GitHub.createRelease owner repoName srcTagName settings
  |> Async.RunSynchronously
  |> ignore
)

Target.create "pack" (fun _ ->
    let pkgVer = 
      match Environment.environVarOrNone "PKGVER" with
      | None -> "0.0.0-dev"
      | Some v -> v[1..]
   
    !! "src/**/*.*proj"
    |> Seq.iter (DotNet.pack (fun ps -> 
      { 
        ps with 
          Configuration = DotNet.BuildConfiguration.Release
          MSBuildParams = {
            ps.MSBuildParams with
              Properties = [ "VersionPrefix", pkgVer ]
          }
      } ))
)

Target.create "nuget-release" <| fun _ ->
  for pkg in !! "src/**/*.nupkg" do

    pkg |> DotNet.nugetPush (fun ps -> {
        ps with
          PushParams = { ps.PushParams with
                          ApiKey = Some (Environment.environVar "NUGET_KEY")
                          Source = Some ("https://api.nuget.org/v3/index.json")
          }
      }
    )

"clean"
  ==> "opa-binaries"
  ==> "pack"
  ==> "nuget-release"

"clean"
  =?> ("gh-release", newRelease.IsSome)

let ctx = Target.WithContext.runOrDefault "clean"
Target.updateBuildStatus ctx
Target.raiseIfError ctx
