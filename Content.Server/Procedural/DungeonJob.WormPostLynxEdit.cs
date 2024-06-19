using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Shared.Procedural;
using Content.Shared.Procedural.PostGeneration;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Procedural;

public sealed partial class DungeonJob
{
    /// <summary>
    /// Tries to connect rooms via worm-like corridors.
    /// </summary>
    private async Task PostGen(WormCorridorLynxEditPostGen gen, Dungeon dungeon, EntityUid gridUid, MapGridComponent grid, Random random)
    {
        var networks = new List<(Vector2i Start, HashSet<Vector2i> Network)>();

        // List of places to start from.
        var worm = new ValueList<Vector2i>();
        var startAngles = new Dictionary<Vector2i, Angle>();

        foreach (var room in dungeon.Rooms)
        {
            foreach (var entrance in room.Entrances)
            {
                var network = new HashSet<Vector2i> { entrance };
                networks.Add((entrance, network));
                // Instead of subtracting room.Center, we add it to invert the direction so that it generates inside the building for emulated destruction.
                // Could probably just set it to entrance by itself but whatever, right, I'm no genius. All I said before is just total speculation. - Lynx

                //FUCK IT WE DO A FULL 180 HAHAHHA
                // Point away from the room to start with.
                startAngles.Add(entrance, (entrance + grid.TileSizeHalfVector + room.Center).ToAngle() - 180);
            }
        }

        // There's a lot of ways to handle this, e.g. pathfinding towards each room
        // For simplicity we'll go through each entrance randomly and generate worms from it
        // then as a final step we will connect all of their networks.
        random.Shuffle(networks);

        for (var i = 0; i < gen.Count; i++)
        {
            // Find a random network to worm from.
            var startIndex = (i % networks.Count);
            var startPos = networks[startIndex].Start;
            var position = startPos + grid.TileSizeHalfVector;

            var remainingLength = gen.Length;
            worm.Clear();
            var angle = startAngles[startPos];

            for (var x = remainingLength; x >= 0; x--)
            {
                position += angle.ToVec();
                angle += random.NextAngle(-gen.MaxAngleChange, gen.MaxAngleChange);
                var roundedPos = position.Floored();

                // Won't need this if we are wanting it to actually destroy the tiles!- Lynx
                /* Check if the tile doesn't overlap something it shouldn't
                if (dungeon.RoomTiles.Contains(roundedPos) ||
                    dungeon.RoomExteriorTiles.Contains(roundedPos))
                {
                    continue;
                }*/

                worm.Add(roundedPos);
            }

            // Uhh yeah.
            if (worm.Count == 0)
            {
                continue;
            }

            // Find a random part on the existing worm to start.
            var value = random.Pick(worm);
            networks[startIndex].Network.UnionWith(worm);
            startAngles[value] = random.NextAngle();
        }

        // Now to ensure they all connect we'll pathfind each network to one another
        // Simple BFS pathfinder
        var main = networks[0];

        var frontier = new PriorityQueue<Vector2i, float>();
        var cameFrom = new Dictionary<Vector2i, Vector2i>();
        var costSoFar = new Dictionary<Vector2i, float>();

        // How many times we try to patch the networks together
        var attempts = 3;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            // Skip index 0
            for (var i = networks.Count - 1; i > 0; i--)
            {
                cameFrom.Clear();
                frontier.Clear();
                costSoFar.Clear();

                var targetNode = random.Pick(main.Network);

                var other = networks[i];
                var startNode = other.Network.First();
                frontier.Enqueue(startNode, 0f);
                costSoFar[startNode] = 0f;
                var count = 0;

                await SuspendIfOutOfTime();
                if (!ValidateResume())
                    return;

                while (frontier.TryDequeue(out var node, out _) && count < gen.PathLimit)
                {
                    count++;

                    // Found
                    if (main.Network.Contains(node))
                    {
                        // found, rebuild
                        frontier.Clear();
                        main.Network.Add(node);
                        main.Network.UnionWith(other.Network);
                        var target = node;

                        // Rebuild
                        while (cameFrom.TryGetValue(target, out var source))
                        {
                            target = source;
                            main.Network.Add(target);
                        }

                        networks.RemoveSwap(i);
                        continue;
                    }

                    for (var x = -1; x <= 1; x++)
                    {
                        for (var y = -1; y <= 1; y++)
                        {
                            if (x == 0 && y == 0)
                                continue;

                            var neighbor = node + new Vector2i(x, y);

                            // Exclude room tiles.
                            if (dungeon.RoomTiles.Contains(neighbor))
                            // Remove the exterior tiles, we want it to generate right next to it as the preset itself is
                            // mean to have custom built destruction.
                            //dungeon.RoomExteriorTiles.Contains(neighbor))
                            {
                                continue;
                            }

                            var tileCost = (neighbor - node).Length;
                            var gScore = costSoFar[node] + tileCost;

                            if (costSoFar.TryGetValue(neighbor, out var nextValue) && gScore >= nextValue)
                            {
                                continue;
                            }

                            cameFrom[neighbor] = node;
                            costSoFar[neighbor] = gScore;
                            var hScore = (targetNode - neighbor).Length + gScore;

                            frontier.Enqueue(neighbor, hScore);
                        }
                    }
                }
            }
        }

        WidenCorridor(dungeon, gen.Width, main.Network);
        dungeon.CorridorTiles.UnionWith(main.Network);
        BuildCorridorExterior(dungeon);

        var tiles = new List<(Vector2i Index, Tile Tile)>();
        var tileDef = _prototype.Index(gen.Tile);

        foreach (var tile in dungeon.CorridorTiles)
        {
            tiles.Add((tile, _tile.GetVariantTile(tileDef, random)));
        }

        foreach (var tile in dungeon.CorridorExteriorTiles)
        {
            tiles.Add((tile, _tile.GetVariantTile(tileDef, random)));
        }

        _maps.SetTiles(_gridUid, _grid, tiles);
    }
}
