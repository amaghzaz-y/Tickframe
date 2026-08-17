namespace Tickframe

module Registry =
    let buildTable () : Map<string, IndicatorSpec> =
        Map.empty

module IndicatorRegistry =
    let table : Map<string, IndicatorSpec> = Registry.buildTable ()

    let tryResolve (name: string) : IndicatorSpec option =
        Map.tryFind name table
        |> Option.orElseWith (fun () ->
            table |> Map.toSeq |> Seq.tryPick (fun (_, spec) ->
                if List.contains name spec.Aliases then Some spec else None))

    let resolve (name: string) : IndicatorSpec =
        match tryResolve name with
        | Some spec -> spec
        | None -> raise (DirectiveValueError $"unknown indicator '{name}'")
