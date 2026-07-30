namespace Melper.Data;

public class UnitsCollection
{
    public static readonly List<Unit> Units =
    [
        new()
        {
            Id = 10,
            Name = "Crawler",
            Cost = 100,
            Damage = 79,
            Health = 263,
            ReloadTime = TimeSpan.FromSeconds(0.6),
            CountInPack = 24,
            Speed = 16
        },
        new()
        {
            Id = 9,
            Name = "Fang",
            Cost = 100,
            Damage = 63,
            Health = 117,
            ReloadTime = TimeSpan.FromSeconds(1.5),
            CountInPack = 18,
            Range = 75,
            Speed = 6,
            CanAttackAir = true
        },
        new()
        {
            Id = 28,
            Name = "Hound",
            Cost = 100,
            Damage = 246,
            Health = 879,
            ReloadTime = TimeSpan.FromSeconds(2.4),
            CountInPack = 5,
            Splash = 6,
            Range = 70,
            Speed = 10
        },
        new()
        {
            Id = 30,
            Name = "Void eye",
            Cost = 100,
            Damage = 995,
            Health = 1522,
            ReloadTime = TimeSpan.FromSeconds(3.3),
            CountInPack = 3,
            Range = 100,
            Speed = 8,
            CanAttackAirWithTech = true,
            CalculateSalvoMode = true
        },
        new()
        {
            Id = 15,
            Name = "Arclight",
            Cost = 100,
            Damage = 365,
            Health = 4813,
            ReloadTime = TimeSpan.FromSeconds(0.9),
            CountInPack = 1,
            Splash = 7,
            Range = 95,
            Speed = 7,
            CanAttackAirWithTech = true
        },
        new()
        {
            Id = 2,
            Name = "Marksman",
            Cost = 100,
            Damage = 2329,
            Health = 1622,
            ReloadTime = TimeSpan.FromSeconds(3.1),
            CountInPack = 1,
            Range = 140,
            Speed = 8,
            CanAttackAir = true
        },
        new()
        {
            Id = 7,
            Name = "Mustang",
            Cost = 200,
            Damage = 36,
            Health = 343,
            ReloadTime = TimeSpan.FromSeconds(0.4),
            CountInPack = 12,
            Range = 95,
            Speed = 16,
            CanAttackAir = true
        },
        new()
        {
            Id = 13,
            Name = "Sledgehammer",
            Cost = 200,
            Damage = 608,
            Health = 3478,
            ReloadTime = TimeSpan.FromSeconds(4.5),
            CountInPack = 5,
            Splash = 5,
            Range = 95,
            Speed = 7,
            CalculateSalvoMode = true
        },
        new()
        {
            Id = 12,
            Name = "Stormcaller",
            Cost = 200,
            Damage = 772,
            Health = 1149,
            ProjectilesPerShotOverride = 4,
            CountBreakpointsForSingleProjectile = true,
            ReloadTime = TimeSpan.FromSeconds(6.6),
            CountInPack = 4,
            Splash = 5.5m,
            Range = 180,
            Speed = 6
        },
        new()
        {
            Id = 8,
            Name = "Steel Ball",
            Cost = 200,
            Damage = 2605,
            Health = 4571,
            ReloadTime = TimeSpan.FromSeconds(0.2),
            CountInPack = 4,
            Range = 45,
            Speed = 16
        },
        new()
        {
            Id = 24,
            Name = "Tarantula",
            Cost = 200,
            Damage = 496,
            Health = 14773,
            ReloadTime = TimeSpan.FromSeconds(0.6),
            CountInPack = 1,
            Splash = 5,
            Range = 80,
            Speed = 8,
            CanAttackAirWithTech = true
        },
        new()
        {
            Id = 21,
            Name = "Sabertooth",
            Cost = 200,
            Damage = 6881,
            Health = 14886,
            ReloadTime = TimeSpan.FromSeconds(3.2),
            CountInPack = 1,
            Splash = 5,
            Range = 95,
            Speed = 8
        },
        new()
        {
            Id = 5,
            Name = "Rhino",
            Cost = 200,
            Damage = 3560,
            Health = 19297,
            ReloadTime = TimeSpan.FromSeconds(0.9),
            CountInPack = 1,
            Splash = 6,
            Speed = 16
        },
        new()
        {
            Id = 14,
            Name = "Hacker",
            Cost = 200,
            Damage = 600,
            Health = 3249,
            ReloadTime = TimeSpan.FromSeconds(0.3),
            CountInPack = 1,
            Range = 110,
            Speed = 8
        },
        new()
        {
            Id = 6,
            Name = "Wasp",
            Cost = 200,
            Damage = 202,
            Health = 311,
            ReloadTime = TimeSpan.FromSeconds(1.4),
            CountInPack = 12,
            Range = 50,
            Speed = 16,
            CanAttackAir = true,
            IsAir = true
        },
        new()
        {
            Id = 16,
            Name = "Phoenix",
            Cost = 200,
            Damage = 2814,
            Health = 1491,
            ReloadTime = TimeSpan.FromSeconds(3.4),
            CountInPack = 2,
            Range = 120,
            Speed = 16,
            CanAttackAir = true,
            IsAir = true,
            CalculateSalvoMode = true
        },
        new()
        {
            Id = 25,
            Name = "Phantom Ray",
            Cost = 200,
            Damage = 1087,
            Health = 3412,
            ProjectilesPerShotOverride = 2,
            ReloadTime = TimeSpan.FromSeconds(3),
            CountInPack = 3,
            Splash = 3,
            Range = 65,
            Speed = 16,
            CanAttackAir = true,
            IsAir = true,
            CalculateSalvoMode = true
        },
        new()
        {
            Id = 18,
            Name = "Wraith",
            Cost = 300,
            Damage = 381,
            Health = 14115,
            ProjectilesPerShotOverride = 4,
            CountBreakpointsForSingleProjectile = true,
            ReloadTime = TimeSpan.FromSeconds(1.6),
            CountInPack = 1,
            Splash = 8,
            Range = 60,
            Speed = 10,
            CanAttackAir = true,
            IsAir = true
        },
        new()
        {
            Id = 19,
            Name = "Scorpion",
            Cost = 300,
            Damage = 10650,
            Health = 18632,
            ReloadTime = TimeSpan.FromSeconds(4.5),
            CountInPack = 1,
            Splash = 15,
            Range = 100,
            Speed = 7
        },
        new()
        {
            Id = 26,
            Name = "Farseer",
            Cost = 300,
            Damage = 1348,
            Health = 11991,
            ProjectilesPerShotOverride = 2,
            ReloadTime = TimeSpan.FromSeconds(2),
            CountInPack = 1,
            Splash = 8,
            Range = 125,
            Speed = 16,
            CanAttackAir = true
        },
        new()
        {
            Id = 3,
            Name = "Vulcan",
            Cost = 400,
            Damage = 75,
            Health = 30279,
            ReloadTime = TimeSpan.FromSeconds(0.1),
            CountInPack = 1,
            Splash = 15,
            Range = 95,
            Speed = 6
        },
        new()
        {
            Id = 4,
            Name = "Melting Point",
            Cost = 400,
            Damage = 7952,
            Health = 30907,
            ReloadTime = TimeSpan.FromSeconds(0.2),
            CountInPack = 1,
            Splash = 3,
            Range = 115,
            Speed = 6,
            CanAttackAir = true
        },
        new()
        {
            Id = 1,
            Name = "Fortress",
            Cost = 400,
            Damage = 6524,
            Health = 43938,
            ReloadTime = TimeSpan.FromSeconds(1.8),
            CountInPack = 1,
            Splash = 5,
            Range = 100,
            Speed = 6
        },
        new()
        {
            Id = 23,
            Name = "Sandworm",
            Cost = 400,
            Damage = 9726,
            Health = 48645,
            ReloadTime = TimeSpan.FromSeconds(2.5),
            CountInPack = 1,
            Splash = 12,
            Speed = 16,
            CanAttackAirWithTech = true
        },
        new()
        {
            Id = 27,
            Name = "Raiden",
            Cost = 400,
            Damage = 5027,
            Health = 16065,
            HitsPerAttackOverride = 3,
            ReloadTime = TimeSpan.FromSeconds(4.6),
            CountInPack = 1,
            Range = 110,
            Speed = 10,
            CanAttackAir = true,
            IsAir = true
        },
        new()
        {
            Id = 11,
            Name = "Overlord",
            Cost = 500,
            Damage = 4742,
            Health = 21339,
            ProjectilesPerShotOverride = 4,
            ReloadTime = TimeSpan.FromSeconds(4.6),
            CountInPack = 1,
            Splash = 7,
            Range = 120,
            Speed = 10,
            CanAttackAir = true,
            IsAir = true
        },
        new()
        {
            Id = 17,
            Name = "War Factory",
            Cost = 800,
            Damage = 7520,
            Health = 113593,
            ProjectilesPerShotOverride = 2,
            CountBreakpointsForSingleProjectile = true,
            ReloadTime = TimeSpan.FromSeconds(1.8),
            CountInPack = 1,
            Splash = 4.5m,
            Range = 100,
            Speed = 6
        },
        new()
        {
            Id = 29,
            Name = "Abyss",
            Cost = 800,
            Damage = 3859,
            Health = 66955,
            ReloadTime = TimeSpan.FromSeconds(4),
            CountInPack = 1,
            Range = 100,
            Speed = 10,
            CanAttackAir = true,
            IsAir = true
        },
        new()
        {
            Id = 2002,
            Name = "Mountain",
            Cost = 800,
            Damage = 5899,
            Health = 136657,
            ProjectilesPerShotOverride = 4,
            CountBreakpointsForSingleProjectile = true,
            ReloadTime = TimeSpan.FromSeconds(2),
            CountInPack = 1,
            Splash = 5,
            Range = 100,
            Speed = 6,
            CanAttackAirWithTech = true
        },
        new()
        {
            Id = 20,
            Name = "Fire Badger",
            Cost = 200,
            Damage = 27,
            Health = 4222,
            ReloadTime = TimeSpan.FromSeconds(0.1),
            CountInPack = 3,
            Splash = 7,
            Range = 75,
            Speed = 9
        },
        new()
        {
            Id = 22,
            Name = "Typhoon",
            Cost = 300,
            Damage = 88,
            Health = 9529,
            ReloadTime = TimeSpan.FromSeconds(0.2),
            CountInPack = 2,
            Splash = 5,
            Range = 100,
            Speed = 9,
            CanAttackAir = true
        },
        new()
        {
            Id = 31,
            Name = "Vortex",
            Cost = 100,
            Damage = 1309,
            Health = 7425,
            ReloadTime = TimeSpan.FromSeconds(1.5),
            CountInPack = 1,
            Range = 85,
            Speed = 8
        },
    ];
}