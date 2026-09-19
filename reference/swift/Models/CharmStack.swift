//
//  CharmStack.swift
//  Hangly
//
//  The one, two or three charms threaded on the rope.
//

import Foundation

/// What hangs on the rope, ordered from the anchor down.
///
/// A stack is never empty and never longer than three. Those two facts are
/// enforced here rather than checked at every use, so nothing downstream — the
/// solver, the renderer, the menu — has to carry a "what if there are none" branch.
///
/// The order is the order you see: index zero hangs nearest the menu bar and the
/// last hangs on the end of the rope. The last one is the charm every part of the
/// app that predates this type means when it says "the charm", which is why
/// ``bottom`` exists and why it is the one the Library, the Studio and an import
/// still act on.
struct CharmStack: Equatable, Sendable {
    /// Three is the limit the rope can carry without the charms crowding each
    /// other; see `RopeConfiguration.Layout.attachments(forCharmCount:)`.
    static let maximumCount = 3

    /// Every place on the rope, whether or not it is in use. Always three long,
    /// ordered from the anchor down, with the ones in use at the end.
    ///
    /// Kept full so that changing the number of charms loses nothing: shrinking to
    /// one and back to three gives the same three charms, not three copies of the
    /// survivor. The alternative was tried and is worse than it sounds — a user who
    /// glances at a count control and puts it back where it was should not have to
    /// rebuild their rope, and any stray write of the count becomes destructive
    /// rather than merely wrong.
    private var slots: [RopeCharm]

    /// How many of them hang, one to three.
    private(set) var count: Int

    /// The places actually on the rope, from the anchor down.
    var places: [RopeCharm] { Array(slots.suffix(count)) }

    /// The charms actually on the rope, from the anchor down.
    var charms: [CharmID] { places.map(\.charm) }

    /// How large each of them is drawn, in the same order.
    var sizes: [Double] { places.map(\.size) }

    init(_ charms: [CharmID]) {
        self.init(places: charms.map { RopeCharm($0) })
    }

    init(_ charm: CharmID) {
        self.init([charm])
    }

    init(places: [RopeCharm]) {
        let trimmed = Array(places.prefix(Self.maximumCount))
        let active = trimmed.isEmpty ? [RopeCharm(OverlaySettings.fallbackCharm)] : trimmed
        count = active.count
        // The unused places above start as copies of the topmost charm, so growing
        // into them gives something recognisable rather than something arbitrary.
        slots = Array(repeating: active[0], count: Self.maximumCount - active.count) + active
    }

    /// Restores a stack including the places not currently in use.
    /// - Parameters:
    ///   - slots: Every place, from the anchor down. Padded or trimmed to three.
    ///   - count: How many hang.
    init(slots: [RopeCharm], count: Int) {
        let trimmed = Array(slots.suffix(Self.maximumCount))
        let filled = trimmed.isEmpty ? [RopeCharm(OverlaySettings.fallbackCharm)] : trimmed
        let padding = Array(repeating: filled[0], count: Self.maximumCount - filled.count)
        self.slots = padding + filled
        self.count = count.clamped(to: 1...Self.maximumCount)
    }

    /// Every place on the rope, in use or not, so that the ones put away can be
    /// stored and come back at the next launch rather than only within a session.
    var storedSlots: [RopeCharm] { slots }

    /// The charm on the end of the rope.
    var bottom: CharmID {
        get { slots[slots.count - 1].charm }
        set { slots[slots.count - 1].charm = newValue }
    }

    subscript(slot: Int) -> CharmID {
        get { charms[slot.clamped(to: 0...(count - 1))] }
        // Writing a charm into a place leaves that place's size alone: the size
        // describes the composition, and changing your mind about which charm is
        // in the middle is not a decision to make the middle large again.
        set { withPlace(slot) { $0.charm = newValue } }
    }

    /// How large the charm in this place is drawn, relative to its own artwork.
    func size(at slot: Int) -> Double {
        places[slot.clamped(to: 0...(count - 1))].size
    }

    mutating func setSize(_ size: Double, at slot: Int) {
        withPlace(slot) { $0.size = size.clamped(to: RopeCharm.sizeRange) }
    }

    /// Edits one hanging place by its position on the rope, ignoring anything out
    /// of range rather than trapping: these indices come from a interface that can
    /// be a frame behind the stack it is drawing.
    private mutating func withPlace(_ slot: Int, _ edit: (inout RopeCharm) -> Void) {
        let index = slots.count - count + slot
        guard slots.indices.contains(index), slot >= 0, slot < count else { return }
        edit(&slots[index])
    }

    /// Grows or shrinks the stack, keeping every charm already chosen.
    ///
    /// Shrinking hides places from the top, so the charm on the end of the rope —
    /// the one being looked at — is the one that stays. Growing brings back exactly
    /// what was hidden.
    mutating func setCount(_ newCount: Int) {
        count = newCount.clamped(to: 1...Self.maximumCount)
    }

    /// Replaces every occurrence of a charm, used when an import is deleted.
    mutating func replace(_ id: CharmID, with replacement: CharmID) {
        // Every place, hanging or not: a deleted import must not come back when the
        // count grows again.
        for index in slots.indices where slots[index].charm == id {
            slots[index].charm = replacement
        }
    }

    func contains(_ id: CharmID) -> Bool {
        charms.contains(id)
    }

    /// Puts a charm on the rope in a new place below the ones already there, up to
    /// the maximum. The count is never set: it is what this leaves behind.
    mutating func add(_ id: CharmID) {
        guard count < Self.maximumCount else { return }
        var places = places
        places.append(RopeCharm(id))
        self = CharmStack(places: places)
    }

    /// Takes one place off the rope. The last charm cannot be removed — a rope with
    /// nothing on it is not a thing this app can draw — so this leaves it.
    mutating func remove(at slot: Int) {
        guard count > 1, slot >= 0, slot < count else { return }
        var places = places
        places.remove(at: slot)
        self = CharmStack(places: places)
    }

    /// Moves a place up or down the rope, which is the only thing reordering means.
    mutating func move(from source: Int, to destination: Int) {
        guard source != destination, source >= 0, source < count else { return }
        var places = places
        let moved = places.remove(at: source)
        places.insert(moved, at: destination.clamped(to: 0...places.count))
        self = CharmStack(places: places)
    }

    /// How this slot is described in the interface.
    ///
    /// Named by where it hangs rather than by its number, because "Middle" is a
    /// place on a rope and "Charm 2" is a row in a list.
    static func slotName(_ slot: Int, of total: Int) -> String {
        guard total > 1 else { return "Charm" }
        switch slot {
        case 0: return "Top"
        case total - 1: return "Bottom"
        default: return "Middle"
        }
    }
}
