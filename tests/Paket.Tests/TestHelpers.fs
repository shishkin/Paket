﻿module Paket.TestHelpers

open Paket
open System

let DictionaryDiscovery(graph : seq<string * string * (string * VersionRange) list>) = 
    { new IDiscovery with
          
          member __.GetDirectDependencies(sourceType, source, package, version) = 
              async { 
                  return graph
                         |> Seq.filter (fun (p, v, _) -> p = package && v = version)
                         |> Seq.map (fun (_, _, d) -> d)
                         |> Seq.head
                         |> List.map (fun (p, v) -> 
                                { Name = p
                                  VersionRange = v
                                  SourceType = sourceType
                                  Source = source })
              }
          
          member __.GetVersions(sourceType, source, package) = 
              async { 
                  return graph
                         |> Seq.filter (fun (p, _, _) -> p = package)
                         |> Seq.map (fun (_, v, _) -> v)
              } }

let resolve graph (dependencies: (string * VersionRange) seq) =
    let packages = dependencies |> Seq.map (fun (n,v) -> { Name = n; VersionRange = v; SourceType = ""; Source = ""})
    Resolver.Resolve(DictionaryDiscovery graph, packages).ResolvedVersionMap

let getVersion resolved =
    match resolved with
    | ResolvedVersion.Resolved x ->
        match x.Referenced.VersionRange with
        | Exactly v -> v

let getDefiningPackage resolved =
    match resolved with
    | ResolvedVersion.Resolved (FromPackage x) -> x.Defining.Name

let getDefiningVersion resolved =
    match resolved with
    | ResolvedVersion.Resolved (FromPackage x) -> 
        match x.Defining.VersionRange with
        | Exactly v -> v

let getSourceType resolved =
    match resolved with
    | ResolvedVersion.Resolved x -> x.Referenced.SourceType

let getSource resolved =
    match resolved with
    | ResolvedVersion.Resolved x -> x.Referenced.Source


let normalizeLineEndings (text:string) = text.Replace("\r\n","\n").Replace("\r","\n").Replace("\n",Environment.NewLine)