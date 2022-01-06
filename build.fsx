#r "paket:
nuget Fake.DotNet.Cli
nuget Fake.IO.FileSystem
nuget Fake.Api.GitHub
nuget Fake.Core.Target //"
#load ".fake/build.fsx/intellisense.fsx"
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.Api
open Octokit
open Fake.DotNet

Target.initEnvironment ()

Target.create "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "src/**/obj"
    |> Shell.cleanDirs 
)

let getOpaBinaries (client: Async<GitHubClient>) = 
  async {
    let lastRelease = client |> GitHub.getLastRelease "open-policy-agent" "opa"

    Directory.create ".binaries"
    do! lastRelease |> GitHub.downloadAssets ".binaries"
  }

Target.create "OPA-Binaries" (fun _ ->
  let client = Environment.environVar "GITHUB_TOKEN" |> GitHub.createClientWithToken
  client |> getOpaBinaries |> Async.RunSynchronously
)

Target.create "Pack" (fun _ ->
    !! "src/**/*.*proj"
    |> Seq.iter (DotNet.pack (fun ps -> { ps with Configuration = DotNet.BuildConfiguration.Release} ))
)

Target.create "All" ignore

"Clean"
  ==> "Opa-Binaries"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "All"
