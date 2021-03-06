namespace CWTools.Common

open System
open System.ComponentModel.Design
module STLConstants =
    /// Blackninja9939: Country, leader, galatic object, planet, ship, fleet, pop, ambient object, army, tile, species, pop faction, sector and alliance
    /// Blackninja9939: War and Megastructure are scopes too
    type Scope =
        |Country
        |Leader
        |GalacticObject
        |Planet
        |Ship
        |Fleet
        |Pop
        |AmbientObject
        |Army
        |Tile
        |Species
        |PopFaction
        |Sector
        |Alliance
        |War
        |Megastructure
        |Any
        |Design
        |Starbase
        |Star
        |InvalidScope
        override x.ToString() =
            match x with
            |GalacticObject -> "System"
            |AmbientObject -> "Ambient object"
            |PopFaction -> "Pop faction"
            |Any -> "Any/Unknown"
            |x -> sprintf "%A" x

    let allScopes = [
            Country;
            Leader;
            GalacticObject;
            Planet;
            Ship;
            Fleet;
            Pop;
            AmbientObject;
            Army;
            Tile
            Species;
            PopFaction;
            Sector;
            Alliance;
            War;
            Design;
            Starbase;
            Megastructure;
            Star;
            ]
    let allScopesSet = allScopes |> Set.ofList
    let parseScope =
        (fun (x : string) ->
        x.ToLower()
        |>
            function
            |"country" -> Scope.Country
            |"leader" -> Scope.Leader
            |"galacticobject"
            |"system"
            |"galactic_object" -> Scope.GalacticObject
            |"planet" -> Scope.Planet
            |"ship" -> Scope.Ship
            |"fleet" -> Scope.Fleet
            |"pop" -> Scope.Pop
            |"ambientobject"
            |"ambient_object" -> Scope.AmbientObject
            |"army" -> Scope.Army
            |"tile" -> Scope.Tile
            |"species" -> Scope.Species
            |"popfaction"
            |"pop_faction" -> Scope.PopFaction
            |"sector" -> Scope.Sector
            |"alliance" -> Scope.Alliance
            |"war" -> Scope.War
            |"megastructure" -> Scope.Megastructure
            |"design" -> Scope.Design
            |"starbase" -> Scope.Starbase
            |"star" -> Scope.Star
            |"any" -> Scope.Any
            |"all" -> Scope.Any
            |"no_scope" -> Scope.Any
            |x -> failwith ("unexpected scope" + x.ToString()))

    let parseScopes =
        function
        |"all" -> allScopes
        |x -> [parseScope x]

    type EffectType = |Effect |Trigger |Both
    type Effect internal (name, scopes, effectType) =
        member val Name : string = name
        member val Scopes : Scope list = scopes
        member this.ScopesSet = this.Scopes |> Set.ofList
        member val Type : EffectType = effectType
        override x.Equals(y) =
            match y with
            | :? Effect as y -> x.Name = y.Name && x.Scopes = y.Scopes && x.Type = y.Type
            |_ -> false
        interface System.IComparable with
            member x.CompareTo yobj =
                match yobj with
                | :? Effect as y ->
                    let r1 = x.Name.CompareTo(y.Name)
                    if r1 = 0 then 0 else List.compareWith compare x.Scopes y.Scopes
                | _ -> invalidArg "yobj" ("cannot compare values of different types" + yobj.GetType().ToString())
        override x.ToString() = sprintf "%s: %A" x.Name x.Scopes


    type ScriptedEffect(name, scopes, effectType, comments, globals, settargets, usedtargets) =
        inherit Effect(name, scopes, effectType)
        member val Comments : string = comments
        member val GlobalEventTargets : string list = globals
        member val SavedEventTargets : string list = settargets
        member val UsedEventTargets : string list = usedtargets
        override x.Equals(y) =
            match y with
            | :? ScriptedEffect as y -> x.Name = y.Name && x.Scopes = y.Scopes && x.Type = y.Type
            |_ -> false
        interface System.IComparable with
            member x.CompareTo yobj =
                match yobj with
                | :? Effect as y -> x.Name.CompareTo(y.Name)
                | _ -> invalidArg "yobj" "cannot compare values of different types"

    type DocEffect(name, scopes, effectType, desc, usage) =
        inherit Effect(name, scopes, effectType)
        member val Desc : string = desc
        member val Usage : string = usage
        override x.Equals(y) =
            match y with
            | :? DocEffect as y -> x.Name = y.Name && x.Scopes = y.Scopes && x.Type = y.Type && x.Desc = y.Desc && x.Usage = y.Usage
            |_ -> false
        interface System.IComparable with
            member x.CompareTo yobj =
                match yobj with
                | :? Effect as y -> x.Name.CompareTo(y.Name)
                | _ -> invalidArg "yobj" "cannot compare values of different types"
        new(rawEffect : RawEffect, effectType) =
            let scopes = rawEffect.scopes |> List.collect parseScopes
            DocEffect(rawEffect.name, scopes, effectType, rawEffect.desc, rawEffect.usage)
    type ScopedEffect(name, scopes, inner, effectType, desc, usage, isScopeChange, ignoreChildren) =
        inherit DocEffect(name, scopes, effectType, desc, usage)
        member val InnerScope : Scope -> Scope = inner
        member val IsScopeChange : bool = isScopeChange
        member val IgnoreChildren : string list = ignoreChildren
        new(de : DocEffect, inner : Scope -> Scope, isScopeChange, ignoreChildren) =
            ScopedEffect(de.Name, de.Scopes, inner, de.Type, de.Desc, de.Usage, isScopeChange, ignoreChildren)
        new(de : DocEffect, inner : Scope) =
            ScopedEffect(de.Name, de.Scopes, (fun _ -> inner), de.Type, de.Desc, de.Usage, true, [])
        new(name, scopes, inner, effectType, desc, usage) =
            ScopedEffect(name, scopes, (fun _ -> inner), effectType, desc, usage, true, [])
    type ModifierCategory =
        |Pop
        |Science
        |Country
        |Army
        |Leader
        |Planet
        |PopFaction
        |ShipSize
        |Ship
        |Tile
        |Megastructure
        |PlanetClass
        |Starbase
        |Any
    type RawStaticModifier =
        {
            num : int
            tag : string
            name : string
        }
    type RawModifier =
        {
            tag : string
            category : int
        }

    type Modifier =
        {
            tag : string
            categories : ModifierCategory list
            /// Is this a core modifier or a static modifier?
            core : bool
        }
    let createModifier (raw : RawModifier) =
        let category =
            match raw.category with
            | 2 -> ModifierCategory.Pop
            | 64 -> ModifierCategory.Science
            | 256 -> ModifierCategory.Country
            | 512 -> ModifierCategory.Army
            | 1024 -> ModifierCategory.Leader
            | 2048 -> ModifierCategory.Planet
            | 8192 -> ModifierCategory.PopFaction
            | 16496 -> ModifierCategory.ShipSize
            | 16508 -> ModifierCategory.Ship
            | 32768 -> ModifierCategory.Tile
            | 65536 -> ModifierCategory.Megastructure
            | 131072 -> ModifierCategory.PlanetClass
            | 262144 -> ModifierCategory.Starbase
            |_ -> ModifierCategory.Any
        { tag = raw.tag; categories = [category]; core = true}

    type EntityType =
    |Agenda = 1
    |AmbientObjects = 2
    |Anomalies = 3
    |Armies = 4
    |ArmyAttachments = 5
    |AscensionPerks = 6
    |Attitudes = 7
    |BombardmentStances = 8
    |BuildablePops = 9
    |BuildingTags = 10
    |Buildings = 11
    |ButtonEffects = 12
    |Bypass = 13
    |CasusBelli = 14
    |Colors = 15
    |ComponentFlags = 16
    |ComponentSets = 17
    |ComponentTags = 18
    |ComponentTemplates = 19
    |CountryCustomization = 20
    |CountryTypes = 21
    |Deposits = 22
    |DiploPhrases = 23
    |DiplomaticActions =24
    |Edicts = 25
    |Ethics = 26
    |EventChains = 27
    |FallenEmpires = 28
    |GameRules = 29
    |GlobalShipDesigns = 30
    |Governments = 31
    |Authorities = 90
    |Civics = 32
    |GraphicalCulture = 33
    |Mandates = 34
    |MapModes = 35
    |Megastructures = 36
    |NameLists = 37
    |NotificationModifiers = 38
    |ObservationStationMissions = 39
    |OnActions = 40
    |OpinionModifiers = 41
    |Personalities = 42
    |PlanetClasses =43
    |PlanetModifiers = 44
    |Policies = 45
    |PopFactionTypes = 46
    |PrecursorCivilizations = 47
    |ScriptedEffects = 48
    |ScriptedLoc = 49
    |ScriptedTriggers = 50
    |ScriptedVariables = 51
    |SectionTemplates = 52
    |SectorTypes = 53
    |ShipBehaviors = 54
    |ShipSizes = 55
    |SolarSystemInitializers = 56
    |SpecialProjects = 57
    |SpeciesArchetypes = 58
    |SpeciesClasses = 59
    |SpeciesNames = 60
    |SpeciesRights = 61
    |StarClasses = 62
    |StarbaseBuilding = 63
    |StarbaseLevels = 64
    |StarbaseModules = 65
    |StarbaseTypes = 66
    |SpaceportModules = 67
    |StartScreenMessages =68
    |StaticModifiers = 69
    |StrategicResources = 70
    |Subjects = 71
    |SystemTypes = 72
    |Technology = 73
    |Terraform = 74
    |TileBlockers = 75
    |TraditionCategories =76
    |Traditions = 77
    |Traits = 78
    |TriggeredModifiers = 79
    |WarDemandCounters = 80
    |WarDemandTypes = 81
    |WarGoals = 82
    |Events = 83
    |MapGalaxy = 84
    |MapSetupScenarios = 85
    |PrescriptedCountries = 86
    |Interface = 87
    |GfxGfx = 88
    |Other = 89
    |GfxAsset = 90

