//
//  CharmSound.swift
//  Hangly
//
//  What a charm sounds like when it moves.
//

/// The material a charm is made of, as far as the ear is concerned.
///
/// Sounds are synthesized from these at launch, so there is nothing to bundle and
/// every charm of the same material sounds like the same family without sounding
/// identical in context: volume follows how hard the charm was swung.
enum CharmSound: String, CaseIterable, Sendable {
    /// A struck bell: a clear fundamental over inharmonic partials, ringing out.
    case bell
    /// Painted wood or clay: a short, dry knock.
    case wood
    /// Glass or enamel: a bright, brief tink.
    case glass
    /// Iron or brass: a clink with a little sustain.
    case metal
    /// Fabric, straw or fruit: barely a thud.
    case soft
}

extension Charm {
    /// Most things on a cord are soft. Charms that ring or knock say so.
    var sound: CharmSound { .soft }
}
