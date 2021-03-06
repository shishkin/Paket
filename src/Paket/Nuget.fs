﻿module Paket.Nuget

open System
open System.Net
open System.Xml
open Newtonsoft.Json

let private get (url : string) = 
    async { 
        use client = new WebClient()
        try 
            return! client.AsyncDownloadString(Uri(url))
        with exn -> 
            // TODO: Handle HTTP 404 errors gracefully and return an empty string to indicate there is no content.
            return ""
    }


/// Gets versions of the given package.
let getAllVersions nugetURL package = 
    async { 
        let! raw = sprintf "%s/package-versions/%s" nugetURL package |> get
        if raw = "" then return Seq.empty
        else return JsonConvert.DeserializeObject<string []>(raw) |> Array.toSeq
    }

let parseVersionRange (text:string) = 
    if text = "" then Latest else
    if text.StartsWith "[" then
        if text.EndsWith "]" then 
            Exactly (text.Replace("[","").Replace("]",""))
        else
            let parts = text.Replace("[","").Replace(")","").Split ','
            Between(parts.[0],parts.[1])
    else AtLeast text

/// Gets all dependencies of the given package version.
let getDependencies nugetURL package version = 
    async { 
        // TODO: this is a very very naive implementation
        let! raw = sprintf "%s/Packages(Id='%s',Version='%s')/Dependencies" nugetURL package version |> get
        let doc = XmlDocument()
        doc.LoadXml raw
        let manager = new XmlNamespaceManager(doc.NameTable)
        manager.AddNamespace("ns", "http://www.w3.org/2005/Atom")
        manager.AddNamespace("d", "http://schemas.microsoft.com/ado/2007/08/dataservices")
        manager.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata")
        let packages = 
            seq { 
                for node in doc.SelectNodes("//d:Dependencies", manager) do
                    yield node.InnerText
            }
            |> Seq.head
            |> fun s -> s.Split([| '|' |], System.StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun d -> d.Split ':')
            |> Array.filter (fun d -> Array.isEmpty d
                                      |> not && d.[0] <> "")
            |> Array.map (fun a -> 
                   a.[0], 
                   if a.Length > 1 then a.[1]
                   else "")
            |> Array.map (fun (name, version) -> 
                   { Name = name
                     // TODO: Parse nuget version ranges - see http://docs.nuget.org/docs/reference/versioning
                     VersionRange = parseVersionRange version
                     SourceType = "nuget"
                     Source = nugetURL })
            |> Array.toList
        return packages
    }

let NugetDiscovery = 
    { new IDiscovery with
          member __.GetDirectDependencies(sourceType, source, package, version) = 
              if sourceType <> "nuget" then failwithf "invalid sourceType %s" sourceType
              getDependencies source package version
          
          member __.GetVersions(sourceType, source, package) = 
              if sourceType <> "nuget" then failwithf "invalid sourceType %s" sourceType
              getAllVersions source package }