//
//  CharmKind.swift
//  Hangly
//
//  The closed set of charms that ship in the app.
//

import Foundation

/// Identity of a built-in charm.
enum CharmKind: String, CaseIterable, Codable, Sendable, Identifiable {
    // The classics.
    case circle
    case camera
    case star
    case heart
    case diamond

    // The Hangly collection.
    case nazar
    case hamsa
    case nimbuMirchi
    case ghanta
    case drishtiBommai
    case panchangJie
    case daruma
    case manekiNeko
    case horseshoe
    case scarab
    case himmeli
    case dreamCatcher

    // The seasonal packs.
    case snowflake
    case bell
    case candyCane
    case pumpkin
    case ghost
    case bat
    case diya
    case lotus
    case lantern
    case firework
    case luckyCoin

    // The Marvel collection.
    case spiderMan
    case captainAmericaShield
    case ironManHelmet
    case thorHammer
    case hulkFist
    case spiderManSwinging

    // The DC collection.
    case batmanSymbol
    case supermanShield
    case wonderWomanEmblem
    case shazamLightning
    case greenLanternRing

    // The Tamil Spiritual collection.
    case vel
    case vinayagarCoin
    case omSymbol
    case karuppuStatue
    case templeBell

    // The BTS collection.
    case btsMemberOne
    case btsMemberTwo
    case btsMemberThree
    case btsMemberFour
    case btsMemberFive
    case btsMemberSix
    case btsMemberSeven

    // The Football Legends collection.
    case ronaldoJersey
    case messiJersey
    case neymarJersey
    case realMadridCrest
    case fcBarcelonaCrest

    // The Music Legends collection.
    case billieEilish
    case xxxtentacion
    case michaelJackson
    case taylorSwift
    case juiceWrld
    case theWeeknd

    // The Friends collection.
    case rachelGreen
    case monicaGeller
    case rossGeller
    case joeyTribbiani
    case chandlerBing
    case phoebeBuffay

    // The Breaking Bad collection.
    case walterWhite
    case jessePinkman
    case saulGoodman
    case gusFring
    case mikeEhrmantraut
    case heisenberg
    case rv

    // The Stranger Things collection.
    case eleven
    case mikeWheeler
    case dustinHenderson
    case lucasSinclair
    case willByers
    case demogorgon

    var id: String { rawValue }
}

/// The two long lookups, held apart from the declaration so that the list of
/// charms stays something you can read in one screen.
extension CharmKind {
    /// Must match the `name` in `CharmLibrary.json`; a test enforces it.
    var displayName: String {
        switch self {
        case .circle: "Bead"
        case .camera: "Camera"
        case .star: "Star"
        case .heart: "Heart"
        case .diamond: "Diamond"
        case .nazar: "Nazar boncuğu"
        case .hamsa: "Hamsa"
        case .nimbuMirchi: "Nimbu-mirchi"
        case .ghanta: "Ghanta"
        case .drishtiBommai: "Drishti bommai"
        case .panchangJie: "Pánchángjié"
        case .daruma: "Daruma"
        case .manekiNeko: "Maneki-neko"
        case .horseshoe: "Horseshoe"
        case .scarab: "Scarab"
        case .himmeli: "Himmeli"
        case .dreamCatcher: "Dream Catcher"
        case .snowflake: "Snowflake"
        case .bell: "Bell"
        case .candyCane: "Candy Cane"
        case .pumpkin: "Pumpkin"
        case .ghost: "Ghost"
        case .bat: "Bat"
        case .diya: "Diya"
        case .lotus: "Lotus"
        case .lantern: "Lantern"
        case .firework: "Firework"
        case .luckyCoin: "Lucky Coin"
        case .spiderMan: "Spider-Man"
        case .captainAmericaShield: "Captain America Shield"
        case .ironManHelmet: "Iron Man Helmet"
        case .thorHammer: "Thor Hammer"
        case .hulkFist: "Hulk Fist"
        case .spiderManSwinging: "Spider-Man Swinging"
        case .batmanSymbol: "Batman Symbol"
        case .supermanShield: "Superman Shield"
        case .wonderWomanEmblem: "Wonder Woman Emblem"
        case .shazamLightning: "Shazam Lightning"
        case .greenLanternRing: "Green Lantern Ring"
        case .vel: "Vel"
        case .vinayagarCoin: "Vinayagar Coin"
        case .omSymbol: "OM Symbol"
        case .karuppuStatue: "Karuppu Statue"
        case .templeBell: "Temple Bell"
        case .btsMemberOne: "RM"
        case .btsMemberTwo: "Jin"
        case .btsMemberThree: "SUGA"
        case .btsMemberFour: "j-hope"
        case .btsMemberFive: "Jimin"
        case .btsMemberSix: "V"
        case .btsMemberSeven: "Jungkook"
        case .ronaldoJersey: "Ronaldo 7"
        case .messiJersey: "Messi 10"
        case .neymarJersey: "Neymar 10"
        case .realMadridCrest: "Real Madrid"
        case .fcBarcelonaCrest: "FC Barcelona"
        case .billieEilish: "Billie Eilish"
        case .xxxtentacion: "XXXTentacion"
        case .michaelJackson: "Michael Jackson"
        case .taylorSwift: "Taylor Swift"
        case .juiceWrld: "Juice WRLD"
        case .theWeeknd: "The Weeknd"
        case .rachelGreen: "Rachel Green"
        case .monicaGeller: "Monica Geller"
        case .rossGeller: "Ross Geller"
        case .joeyTribbiani: "Joey Tribbiani"
        case .chandlerBing: "Chandler Bing"
        case .phoebeBuffay: "Phoebe Buffay"
        case .walterWhite: "Walter White"
        case .jessePinkman: "Jesse Pinkman"
        case .saulGoodman: "Saul Goodman"
        case .gusFring: "Gus Fring"
        case .mikeEhrmantraut: "Mike Ehrmantraut"
        case .heisenberg: "Heisenberg"
        case .rv: "RV"
        case .eleven: "Eleven"
        case .mikeWheeler: "Mike Wheeler"
        case .dustinHenderson: "Dustin Henderson"
        case .lucasSinclair: "Lucas Sinclair"
        case .willByers: "Will Byers"
        case .demogorgon: "Demogorgon"
        }
    }

    /// SF Symbol used for the menu bar item.
    var symbolName: String {
        switch self {
        case .circle: "circle.fill"
        case .camera: "camera.fill"
        case .star: "star.fill"
        case .heart: "heart.fill"
        case .diamond: "diamond.fill"
        case .nazar: "eye.fill"
        case .hamsa: "hand.raised.fill"
        case .nimbuMirchi: "leaf.fill"
        case .ghanta: "bell.fill"
        case .drishtiBommai: "theatermasks.fill"
        case .panchangJie: "seal.fill"
        case .daruma: "face.smiling.fill"
        case .manekiNeko: "cat.fill"
        case .horseshoe: "u.circle.fill"
        case .scarab: "ant.fill"
        case .himmeli: "pyramid.fill"
        case .dreamCatcher: "circle.hexagongrid.fill"
        case .snowflake: "snowflake"
        case .bell: "bell.and.waves.left.and.right.fill"
        case .candyCane: "figure.walk.motion"
        case .pumpkin: "carrot.fill"
        case .ghost: "figure.stand"
        case .bat: "bolt.horizontal.fill"
        case .diya: "flame.fill"
        case .lotus: "camera.macro"
        case .lantern: "lightbulb.fill"
        case .firework: "sparkles"
        case .luckyCoin: "centsign.circle.fill"
        case .spiderMan: "figure.climbing"
        case .captainAmericaShield: "shield.fill"
        case .ironManHelmet: "faceid"
        case .thorHammer: "hammer.fill"
        case .hulkFist: "hand.raised.fill"
        case .spiderManSwinging: "figure.climbing"
        case .batmanSymbol: "moon.fill"
        case .supermanShield: "diamond.fill"
        case .wonderWomanEmblem: "seal.fill"
        case .shazamLightning: "bolt.fill"
        case .greenLanternRing: "circle.circle.fill"
        case .vel: "location.north.fill"
        case .vinayagarCoin: "centsign.circle.fill"
        case .omSymbol: "circle.hexagonpath.fill"
        case .karuppuStatue: "figure.stand"
        case .templeBell: "bell.circle.fill"
        case .btsMemberOne: "1.circle.fill"
        case .btsMemberTwo: "2.circle.fill"
        case .btsMemberThree: "3.circle.fill"
        case .btsMemberFour: "4.circle.fill"
        case .btsMemberFive: "5.circle.fill"
        case .btsMemberSix: "6.circle.fill"
        case .btsMemberSeven: "7.circle.fill"
        case .ronaldoJersey: "tshirt.fill"
        case .messiJersey: "tshirt.fill"
        case .neymarJersey: "tshirt.fill"
        case .realMadridCrest: "crown.fill"
        case .fcBarcelonaCrest: "shield.fill"
        case .billieEilish: "music.mic"
        case .xxxtentacion: "music.note"
        case .michaelJackson: "music.note.list"
        case .taylorSwift: "guitars.fill"
        case .juiceWrld: "headphones"
        case .theWeeknd: "music.quarternote.3"
        case .rachelGreen: "cup.and.saucer.fill"
        case .monicaGeller: "fork.knife"
        case .rossGeller: "book.fill"
        case .joeyTribbiani: "hand.thumbsup.fill"
        case .chandlerBing: "briefcase.fill"
        case .phoebeBuffay: "guitars.fill"
        case .walterWhite: "testtube.2"
        case .jessePinkman: "flame.fill"
        case .saulGoodman: "doc.text.fill"
        case .gusFring: "fork.knife.circle.fill"
        case .mikeEhrmantraut: "shield.lefthalf.filled"
        case .heisenberg: "eyeglasses"
        case .rv: "bus.fill"
        case .eleven: "bolt.fill"
        case .mikeWheeler: "antenna.radiowaves.left.and.right"
        case .dustinHenderson: "radio.fill"
        case .lucasSinclair: "scope"
        case .willByers: "lightbulb.fill"
        case .demogorgon: "hurricane"
        }
    }
}
