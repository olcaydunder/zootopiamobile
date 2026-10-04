using UnityEngine;

public enum VehicleKind
{
    Offroad = 0,
    ATV = 1,
    Moto = 2,
    Truck = 3,
    Boat = 4,
    Heli = 5,
    Tank = 6
}

/// <summary>How each vehicle drives, how tough it is, where the driver sits.</summary>
public class VehicleDef
{
    public VehicleKind kind;
    public string name;
    public string model;      // Resources/Models/Vehicles/<model>
    public string icon;       // Resources/UI/Icons/<icon>
    public int seats;
    public float maxSpeed;    // m/s
    public float reverse;
    public float accel;
    public float turnRate;    // degrees per second at full steer
    public float health;
    public float ccRadius, ccHeight;
    public Vector3 seat;      // driver's centre, local (x right, y up, z forward)
    public bool hideDriver;
    public float camDistance;
    public float wheelRadius;
    public bool water, flying, cannon, pivotTurn;
    public Color[] paints;    // factory colours (camo replaces them)
}

public static class VehicleDefs
{
    public static readonly VehicleDef[] All =
    {
        new VehicleDef { kind = VehicleKind.Offroad, name = "Arazi Aracı", model = "Offroad", icon = "veh_offroad", seats = 4,
            maxSpeed = 23f, reverse = 7f, accel = 8f, turnRate = 80f, health = 1000f, ccRadius = 1.0f, ccHeight = 2.0f,
            seat = new Vector3(-0.4f, 1.45f, -0.2f), camDistance = 7.5f, wheelRadius = 0.42f,
            paints = new[] { new Color(0.33f, 0.37f, 0.2f), new Color(0.55f, 0.45f, 0.3f), new Color(0.2f, 0.22f, 0.25f), new Color(0.6f, 0.15f, 0.12f) } },
        new VehicleDef { kind = VehicleKind.ATV, name = "ATV", model = "ATV", icon = "veh_atv", seats = 2,
            maxSpeed = 20f, reverse = 6f, accel = 10f, turnRate = 110f, health = 500f, ccRadius = 0.7f, ccHeight = 1.4f,
            seat = new Vector3(0f, 1.35f, -0.15f), camDistance = 6f, wheelRadius = 0.32f,
            paints = new[] { new Color(0.75f, 0.12f, 0.1f), new Color(0.15f, 0.35f, 0.7f), new Color(0.2f, 0.5f, 0.2f) } },
        new VehicleDef { kind = VehicleKind.Moto, name = "Motosiklet", model = "Moto", icon = "veh_moto", seats = 2,
            maxSpeed = 26f, reverse = 4f, accel = 11f, turnRate = 100f, health = 350f, ccRadius = 0.5f, ccHeight = 1.4f,
            seat = new Vector3(0f, 1.45f, -0.2f), camDistance = 6f, wheelRadius = 0.36f,
            paints = new[] { new Color(0.15f, 0.3f, 0.75f), new Color(0.85f, 0.4f, 0.1f), new Color(0.1f, 0.1f, 0.12f) } },
        new VehicleDef { kind = VehicleKind.Truck, name = "Kamyon", model = "Truck", icon = "veh_truck", seats = 4,
            maxSpeed = 17f, reverse = 5f, accel = 5f, turnRate = 55f, health = 2200f, ccRadius = 1.4f, ccHeight = 3.0f,
            seat = new Vector3(-0.5f, 2.2f, 2.3f), hideDriver = true, camDistance = 11f, wheelRadius = 0.55f,
            paints = new[] { new Color(0.26f, 0.32f, 0.2f), new Color(0.4f, 0.36f, 0.28f) } },
        new VehicleDef { kind = VehicleKind.Boat, name = "Tekne", model = "Boat", icon = "veh_boat", seats = 4,
            maxSpeed = 19f, reverse = 5f, accel = 7f, turnRate = 70f, health = 700f, ccRadius = 1.2f, ccHeight = 2.4f,
            seat = new Vector3(0f, 1.0f, -0.6f), camDistance = 8f, water = true,
            paints = new[] { new Color(0.92f, 0.92f, 0.9f), new Color(0.2f, 0.22f, 0.25f) } },
        new VehicleDef { kind = VehicleKind.Heli, name = "Helikopter", model = "Heli", icon = "veh_heli", seats = 4,
            maxSpeed = 40f, reverse = 10f, accel = 9f, turnRate = 70f, health = 900f, ccRadius = 1.6f, ccHeight = 3.2f,
            seat = new Vector3(0f, 1.5f, 0.6f), hideDriver = true, camDistance = 13f, flying = true,
            paints = new[] { new Color(0.22f, 0.27f, 0.22f), new Color(0.15f, 0.17f, 0.2f) } },
        new VehicleDef { kind = VehicleKind.Tank, name = "Tank", model = "Tank", icon = "veh_tank", seats = 1,
            maxSpeed = 9f, reverse = 5f, accel = 4f, turnRate = 45f, health = 4000f, ccRadius = 1.7f, ccHeight = 3.4f,
            seat = new Vector3(0f, 2.2f, 0f), hideDriver = true, camDistance = 11f, cannon = true, pivotTurn = true,
            paints = new[] { new Color(0.62f, 0.55f, 0.38f), new Color(0.3f, 0.34f, 0.22f) } },
    };

    public static VehicleDef Get(VehicleKind k) { return All[(int)k]; }
}
