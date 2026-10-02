//
//  CharmKind.swift
//  Hangly
//
//  The closed set of charms that ship in the app.
//

import Foundation

/// Identity of a built-in charm.
///
/// The raw values are what a rope and a favourite are saved as, so a case is
/// never renamed. Its name is in `CharmKind+Names.swift` and its menu symbol in
/// `CharmKind+Symbols.swift`.
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

    // The Spirituality collection (once Tamil Spiritual).
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

    // The Marvel collection, added in 2.2.
    case spiderManGwen
    case eyeOfAgamotto
    case stormbreaker
    case doctorDoom
    case deadpoolWolverine

    // The Stranger Things collection, added in 2.2.
    case maxMayfield
    case steveHarrington

    // The Football Legends collection, added in 2.2.
    case ronaldoPortugal
    case ronaldoBicycleKick
    case ballonDor
    case championsLeagueTrophy

    // The Spirituality collection, added in 2.2.
    case rudraksha
    case shivaLingam
    case buddha
    case hanuman
    case kaaba
    case greenDome
    case crescentAndStar
    case allahPendant
    case cross
    case dove
    case prayingAngel
    case sacredHeart

    // The One Piece collection.
    case luffy
    case zoro
    case nami
    case usopp
    case sanji
    case chopper
    case robin
    case ace

    // The Harry Potter collection.
    case harryPotter
    case hermioneGranger
    case ronWeasley
    case albusDumbledore
    case lordVoldemort
    case hedwig
    case goldenSnitch
    case deathlyHallows

    // The Ben 10 collection.
    case benTennyson
    case fourArms
    case heatblast
    case xlr8
    case diamondhead
    case cannonbolt
    case upgrade
    case wildmutt
    case omnitrix

    // The Attack on Titan collection.
    case erenYeager
    case mikasaAckerman
    case leviAckerman
    case arminArlert
    case colossalTitan
    case attackTitan
    case surveyCorpsEmblem
    case cadetCorpsEmblem

    // The Naruto collection.
    case narutoUzumaki
    case sasukeUchiha
    case kakashiHatake
    case itachiUchiha
    case minatoNamikaze
    case kurama
    case akatsukiCloud
    case sharingan

    // The Game of Thrones collection.
    case jonSnow
    case daenerysTargaryen
    case tyrionLannister
    case nightKing
    case houseStark
    case houseTargaryen
    case houseLannister
    case ironThrone

    // The Air Jordan collection.
    case airJordan1Chicago
    case airJordan1Bred
    case airJordan4FireRed
    case airJordan11Concord
    case airJordan3
    case airJordan6Carmine
    case airJordan5
    case offWhiteJordan1

    // The Pokémon collection.
    case pikachu
    case charmander
    case squirtle
    case bulbasaur
    case charizard
    case dragonair
    case mewtwo
    case umbreon
    case ashAndPikachu
    case pokeBall

    var id: String { rawValue }
}
