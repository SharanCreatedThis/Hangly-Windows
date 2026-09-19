//
//  SeasonalPack.swift
//  Hangly
//
//  The charms that belong to a time of year.
//

import Foundation

/// A day in the year, without a year attached.
///
/// Seasons recur, so the thing that defines them is a month and a day rather than a
/// date. Comparing two of these is comparing where in the ring of the year they sit,
/// which is why ``SeasonWindow`` can wrap past December without a special case.
struct MonthDay: Codable, Equatable, Hashable, Sendable, Comparable {
    var month: Int
    var day: Int

    init(_ month: Int, _ day: Int) {
        self.month = month.clamped(to: 1...12)
        self.day = day.clamped(to: 1...31)
    }

    /// Position in the year, counting from the first of January.
    var ordinal: Int { (month * 31) + day }

    static func < (lhs: MonthDay, rhs: MonthDay) -> Bool {
        lhs.ordinal < rhs.ordinal
    }

    static func of(_ date: Date, calendar: Calendar = .current) -> MonthDay {
        let parts = calendar.dateComponents([.month, .day], from: date)
        return MonthDay(parts.month ?? 1, parts.day ?? 1)
    }
}

/// A stretch of the year, which may run past the end of it.
struct SeasonWindow: Codable, Equatable, Hashable, Sendable {
    var start: MonthDay
    var end: MonthDay

    init(_ start: MonthDay, _ end: MonthDay) {
        self.start = start
        self.end = end
    }

    /// Whether a day falls inside. A window whose end comes before its start wraps
    /// through New Year's Eve rather than being empty, which is the only sensible
    /// reading of "the twenty-seventh of December to the sixth of January".
    func contains(_ day: MonthDay) -> Bool {
        if start <= end {
            return day >= start && day <= end
        }
        return day >= start || day <= end
    }

    func contains(_ date: Date, calendar: Calendar = .current) -> Bool {
        contains(MonthDay.of(date, calendar: calendar))
    }
}

/// A set of charms that belongs to a time of year.
enum SeasonalPack: String, CaseIterable, Codable, Sendable, Identifiable {
    case halloween
    case diwali
    case christmas
    case newYear

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .halloween: "Halloween"
        case .diwali: "Diwali"
        case .christmas: "Christmas"
        case .newYear: "New Year"
        }
    }

    var symbolName: String {
        switch self {
        case .halloween: "moon.stars.fill"
        case .diwali: "flame.fill"
        case .christmas: "snowflake"
        case .newYear: "sparkles"
        }
    }

    /// The charms in the pack, in the order they hang when the whole pack is worn:
    /// the smallest at the top, the one the season is about on the end.
    var charms: [CharmKind] {
        switch self {
        case .halloween: [.bat, .ghost, .pumpkin]
        case .diwali: [.lotus, .lantern, .diya]
        case .christmas: [.snowflake, .candyCane, .bell]
        case .newYear: [.firework, .luckyCoin]
        }
    }

    /// When the pack comes round, for the packs whose date is fixed.
    ///
    /// Christmas stops on Boxing Day and New Year takes over the next morning, so
    /// the two never both claim a day — which matters, because a day claimed twice
    /// would be a day where which charm you got depended on the order of an enum.
    ///
    /// Diwali is not here: it moves with the lunar calendar, a month either side of
    /// where it fell last year, so no fixed window could be right two years running.
    /// It is carried in settings instead, where it can be corrected.
    var fixedWindow: SeasonWindow? {
        switch self {
        case .halloween: SeasonWindow(MonthDay(10, 1), MonthDay(10, 31))
        case .christmas: SeasonWindow(MonthDay(12, 1), MonthDay(12, 26))
        case .newYear: SeasonWindow(MonthDay(12, 27), MonthDay(1, 6))
        case .diwali: nil
        }
    }

    /// A window that is somewhere near right for the years just ahead, and wrong
    /// eventually — which is why it can be edited. Diwali fell in early November in
    /// 2026; a week around it is the most a charm needs to know.
    static let defaultDiwaliWindow = SeasonWindow(MonthDay(11, 5), MonthDay(11, 11))

    /// Which pack, if any, belongs to a date.
    static func active(on date: Date, diwali: SeasonWindow, calendar: Calendar = .current) -> SeasonalPack? {
        let day = MonthDay.of(date, calendar: calendar)
        // Diwali first: it is the one the user set by hand, so where it overlaps a
        // fixed window it is the deliberate answer rather than the default one.
        if diwali.contains(day) { return .diwali }
        return allCases.first { $0.fixedWindow?.contains(day) == true }
    }

    /// The pack a charm belongs to, if any. Drives the Library's grouping.
    static func containing(_ kind: CharmKind) -> SeasonalPack? {
        allCases.first { $0.charms.contains(kind) }
    }
}
