using Godot;

namespace BallFightGame;

/// <summary>
/// Procedural city generator. Lays out a grid of streets with buildings
/// of varying heights placed on city blocks. Creates a flat ground plane
/// with simple box-shaped buildings.
/// </summary>
public partial class CityGenerator : Node3D
{
    [Export] public float CitySize       { get; set; } = 120f;
    [Export] public float BlockSize      { get; set; } = 20f;
    [Export] public float StreetWidth    { get; set; } = 8f;
    [Export] public float MinBuildingH   { get; set; } = 5f;
    [Export] public float MaxBuildingH   { get; set; } = 25f;
    [Export] public float BuildingMargin { get; set; } = 1.5f;

    // Materials
    private static readonly StandardMaterial3D RoadMaterial = new()
    {
        AlbedoColor = new Color(0.2f, 0.2f, 0.22f),
        Roughness = 0.95f,
    };

    private static readonly StandardMaterial3D SidewalkMaterial = new()
    {
        AlbedoColor = new Color(0.55f, 0.55f, 0.5f),
        Roughness = 0.9f,
    };

    // Building color palette
    private static readonly Color[] BuildingColors =
    {
        new(0.6f, 0.58f, 0.55f),   // concrete grey
        new(0.55f, 0.4f, 0.3f),    // brown brick
        new(0.4f, 0.42f, 0.45f),   // dark grey
        new(0.7f, 0.65f, 0.55f),   // sandstone
        new(0.5f, 0.35f, 0.3f),    // red brick
        new(0.35f, 0.38f, 0.42f),  // slate
    };

    public override void _Ready()
    {
        GenerateCity();
    }

    private void GenerateCity()
    {
        float halfCity = CitySize / 2f;
        float cellSize = BlockSize + StreetWidth;
        int gridCount = Mathf.FloorToInt(CitySize / cellSize);
        float gridStart = -gridCount * cellSize / 2f;

        GD.Print($"[CityGenerator] Generating {gridCount}x{gridCount} city blocks");

        for (int gz = 0; gz < gridCount; gz++)
        {
            for (int gx = 0; gx < gridCount; gx++)
            {
                float cx = gridStart + gx * cellSize + cellSize / 2f;
                float cz = gridStart + gz * cellSize + cellSize / 2f;

                // Skip the center block so the player has a spawn area
                if (Mathf.Abs(cx) < cellSize && Mathf.Abs(cz) < cellSize)
                    continue;

                PlaceBuilding(cx, cz);
            }
        }

        // Place road lines along the grid
        PlaceRoads(gridCount, gridStart, cellSize);
    }

    private void PlaceBuilding(float cx, float cz)
    {
        // Building footprint must stay within the block bounds
        float maxFootprint = BlockSize - BuildingMargin * 2f;
        float bw = (float)GD.RandRange(maxFootprint * 0.5f, maxFootprint);
        float bd = (float)GD.RandRange(maxFootprint * 0.5f, maxFootprint);
        float bh = (float)GD.RandRange(MinBuildingH, MaxBuildingH);

        // Clamp offset so the building never bleeds into the street
        float maxOx = (maxFootprint - bw) / 2f;
        float maxOz = (maxFootprint - bd) / 2f;
        float ox = (float)GD.RandRange(-maxOx, maxOx);
        float oz = (float)GD.RandRange(-maxOz, maxOz);

        var color = BuildingColors[(int)(GD.Randi() % BuildingColors.Length)];
        var mat = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.85f,
        };

        var body = new StaticBody3D
        {
            CollisionLayer = 1,
            CollisionMask = 0,
        };

        var size = new Vector3(bw, bh, bd);
        var shape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = size },
        };
        body.AddChild(shape);

        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size, Material = mat },
        };
        body.AddChild(mesh);

        // Add window lines (dark horizontal strips) for visual detail
        AddWindowStrips(body, size, bh, color);

        // Add doorway on a random side (ground-floor opening)
        AddDoorway(body, size, bh, mat);

        AddChild(body);
        body.GlobalPosition = new Vector3(cx + ox, bh / 2f, cz + oz);
    }

    /// <summary>
    /// Carve a doorway into the ground floor of a building on one random side.
    /// Uses CSG-style approach: cover the wall with the building material,
    /// leaving a dark rectangular opening the player can roll through.
    /// </summary>
    private void AddDoorway(StaticBody3D building, Vector3 size, float height, StandardMaterial3D wallMat)
    {
        float doorW = 2.0f;   // wide enough for the player ball
        float doorH = 2.5f;   // tall enough to roll through
        float doorD = 0.15f;  // thin slab depth

        if (height < doorH + 1f) return; // building too short for a door

        var doorMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.08f, 0.06f, 0.06f),
            Roughness = 0.95f,
        };

        // Pick a random side (0=+X, 1=-X, 2=+Z, 3=-Z)
        int side = (int)(GD.Randi() % 4);
        Vector3 doorPos;
        Vector3 doorSize;

        switch (side)
        {
            case 0: // +X wall
                doorPos = new Vector3(size.X / 2f, -height / 2f + doorH / 2f, 0);
                doorSize = new Vector3(doorD, doorH, doorW);
                break;
            case 1: // -X wall
                doorPos = new Vector3(-size.X / 2f, -height / 2f + doorH / 2f, 0);
                doorSize = new Vector3(doorD, doorH, doorW);
                break;
            case 2: // +Z wall
                doorPos = new Vector3(0, -height / 2f + doorH / 2f, size.Z / 2f);
                doorSize = new Vector3(doorW, doorH, doorD);
                break;
            default: // -Z wall
                doorPos = new Vector3(0, -height / 2f + doorH / 2f, -size.Z / 2f);
                doorSize = new Vector3(doorW, doorH, doorD);
                break;
        }

        // Dark rectangle representing the door opening
        var doorMesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = doorSize, Material = doorMat },
            Position = doorPos,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        building.AddChild(doorMesh);

        // Hollow out the collision: replace the single box with walls that
        // leave the doorway open. We do this by adding a concave trimesh
        // collision override just for the doorway area.
        // Simpler approach: add a hole in the collision by using an Area3D
        // that disables collision in the doorway region.
        // Simplest approach that works: carve a passage using a second
        // StaticBody with no collision in the doorway area.
        // 
        // For now: we punch a passable hole by adding a doorway-sized
        // CollisionShape with disabled=true won't work — instead we
        // reshape the building collision into an L or U.
        // 
        // Practical approach: split the bottom floor collision into segments
        // that leave the door gap open.
        AddDoorwayCollisionGap(building, size, height, doorW, doorH, side);
    }

    private void AddDoorwayCollisionGap(StaticBody3D building, Vector3 bSize, float bHeight,
        float doorW, float doorH, int side)
    {
        // Remove the original full-box collision shape.
        CollisionShape3D? origShape = null;
        foreach (var child in building.GetChildren())
        {
            if (child is CollisionShape3D cs)
            {
                origShape = cs;
                break;
            }
        }
        if (origShape == null) return;
        building.RemoveChild(origShape);
        origShape.QueueFree();

        // Upper portion (above door): full width
        // Local Y origin is at the building center (0). Bottom = -bHeight/2, Top = +bHeight/2.
        // Door occupies bottom: from -bHeight/2 to -bHeight/2 + doorH.
        // Upper portion: from -bHeight/2 + doorH to +bHeight/2.
        float upperH = bHeight - doorH;
        if (upperH > 0.1f)
        {
            float upperCenterY = -bHeight / 2f + doorH + upperH / 2f;
            var upperShape = new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(bSize.X, upperH, bSize.Z) },
                Position = new Vector3(0, upperCenterY, 0),
            };
            building.AddChild(upperShape);
        }

        // Lower portion: two segments flanking the door
        bool doorOnX = side is 0 or 1;
        float wallLen = doorOnX ? bSize.Z : bSize.X;
        float segLen = (wallLen - doorW) / 2f;
        float lowerCenterY = -bHeight / 2f + doorH / 2f;

        if (segLen > 0.1f)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 segSize, segPos;
                if (doorOnX)
                {
                    segSize = new Vector3(bSize.X, doorH, segLen);
                    segPos = new Vector3(0, lowerCenterY, s * (doorW / 2f + segLen / 2f));
                }
                else
                {
                    segSize = new Vector3(segLen, doorH, bSize.Z);
                    segPos = new Vector3(s * (doorW / 2f + segLen / 2f), lowerCenterY, 0);
                }

                var segShape = new CollisionShape3D
                {
                    Shape = new BoxShape3D { Size = segSize },
                    Position = segPos,
                };
                building.AddChild(segShape);
            }
        }
    }

    private void AddWindowStrips(StaticBody3D building, Vector3 size, float height, Color baseColor)
    {
        var windowColor = baseColor * 0.5f;
        windowColor.A = 1f;
        var windowMat = new StandardMaterial3D
        {
            AlbedoColor = windowColor,
            Roughness = 0.3f,
            Metallic = 0.2f,
        };

        float floorH = 3.5f;
        int floors = Mathf.FloorToInt(height / floorH);

        for (int f = 0; f < floors; f++)
        {
            float stripY = (f * floorH + floorH * 0.6f) - height / 2f;
            float stripH = floorH * 0.25f;

            // Front and back window strips
            for (int side = -1; side <= 1; side += 2)
            {
                var strip = new MeshInstance3D
                {
                    Mesh = new BoxMesh
                    {
                        Size = new Vector3(size.X * 0.92f, stripH, 0.05f),
                        Material = windowMat,
                    },
                    Position = new Vector3(0, stripY, side * (size.Z / 2f + 0.03f)),
                };
                building.AddChild(strip);
            }

            // Left and right window strips
            for (int side = -1; side <= 1; side += 2)
            {
                var strip = new MeshInstance3D
                {
                    Mesh = new BoxMesh
                    {
                        Size = new Vector3(0.05f, stripH, size.Z * 0.92f),
                        Material = windowMat,
                    },
                    Position = new Vector3(side * (size.X / 2f + 0.03f), stripY, 0),
                };
                building.AddChild(strip);
            }
        }
    }

    private void PlaceRoads(int gridCount, float gridStart, float cellSize)
    {
        // Horizontal and vertical road strips
        for (int i = 0; i <= gridCount; i++)
        {
            float pos = gridStart + i * cellSize - StreetWidth / 2f + cellSize / 2f;

            // East-West road
            var hRoad = new MeshInstance3D
            {
                Mesh = new BoxMesh
                {
                    Size = new Vector3(CitySize, 0.02f, StreetWidth),
                    Material = RoadMaterial,
                },
            };
            AddChild(hRoad);
            hRoad.GlobalPosition = new Vector3(0, 0.01f, pos);

            // North-South road
            var vRoad = new MeshInstance3D
            {
                Mesh = new BoxMesh
                {
                    Size = new Vector3(StreetWidth, 0.02f, CitySize),
                    Material = RoadMaterial,
                },
            };
            AddChild(vRoad);
            vRoad.GlobalPosition = new Vector3(pos, 0.01f, 0);
        }
    }
}
