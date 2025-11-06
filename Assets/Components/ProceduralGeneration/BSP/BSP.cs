using Components.ProceduralGeneration;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VTools.Grid;
using VTools.RandomService;

[CreateAssetMenu(menuName = "Procedural Generation Method/BSP algo")]
public class BSP : ProceduralGenerationMethod
{
    [Header("Room parameters")]
    [SerializeField] int _maxRooms = 10;
    [SerializeField] int _minRoomSize = 5;
    [SerializeField] int _maxRoomSize = 10;
    [SerializeField] int _spacing = 2;
    [SerializeField] bool _showDebug = false;

    protected override async UniTask ApplyGeneration(CancellationToken cancellationToken)
    {
        var allGrid = new RectInt(0, 0, Grid.Width, Grid.Lenght);
        var root = new BSPNode(allGrid);


        int splitCount = 1;
        root.RecursiveSplit(RandomService, _minRoomSize, _maxRooms, ref splitCount, _maxSteps);


        var leaves = new List<BSPNode>();
        root.ReturnLeaves(leaves);

        var roomCenters = new List<Vector2Int>();
        foreach (var leaf in leaves)
        {
            leaf.CreateRoomTemplate(RandomService, _minRoomSize, _maxRoomSize, _spacing);
            PlaceRoom(leaf.Room);
            roomCenters.Add(Vector2Int.RoundToInt(leaf.Room.center));
            await UniTask.Delay(GridGenerator.StepDelay, cancellationToken: cancellationToken);
        }

        root.ConnectRooms(this);

        BuildGround();
        if (_showDebug)
        {
            DrawBSPBoundaries(root);
        }
    }
    void PlaceDebugRock(int x, int y)
    {
        if (Grid.TryGetCellByCoordinates(x, y, out var cell))
        {
            if (!cell.ContainObject ||
                (cell.GridObject.Template.Name != ROOM_TILE_NAME && cell.GridObject.Template.Name != CORRIDOR_TILE_NAME))
            {
                AddTileToCell(cell, ROCK_TILE_NAME, true);
            }
        }
    }
    void DrawBSPBoundaries(BSPNode node)
    {
        var bounds = node.Bounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            PlaceDebugRock(x, bounds.yMin);
            PlaceDebugRock(x, bounds.yMax - 1);
        }
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            PlaceDebugRock(bounds.xMin, y);
            PlaceDebugRock(bounds.xMax - 1, y);
        }
        if (node.Child1 != null) DrawBSPBoundaries(node.Child1);
        if (node.Child2 != null) DrawBSPBoundaries(node.Child2);
    }
    void PlaceRoom(RectInt room)
    {
        for (int ix = room.xMin; ix < room.xMax; ix++)
        {
            for (int iy = room.yMin; iy < room.yMax; iy++)
            {
                if (!Grid.TryGetCellByCoordinates(ix, iy, out Cell cell))
                    continue;
                AddTileToCell(cell, ROOM_TILE_NAME, true);
            }
        }
    }

    void CreateCorridor(Vector2Int from, Vector2Int to)
    {
        int x = from.x, y = from.y;


        while (x != to.x)
        {
            x += x < to.x ? 1 : -1;
            if (Grid.TryGetCellByCoordinates(x, y, out Cell cell))
                AddTileToCell(cell, CORRIDOR_TILE_NAME, true);
        }
        while (y != to.y)
        {
            y += y < to.y ? 1 : -1;
            if (Grid.TryGetCellByCoordinates(x, y, out Cell cell))
                AddTileToCell(cell, CORRIDOR_TILE_NAME, true);
        }
    }

    private void BuildGround()
    {
        var groundTemplate = VTools.ScriptableObjectDatabase.ScriptableObjectDatabase.GetScriptableObject<GridObjectTemplate>("Grass");
        for (int x = 0; x < Grid.Width; x++)
        {
            for (int z = 0; z < Grid.Lenght; z++)
            {
                if (!Grid.TryGetCellByCoordinates(x, z, out var chosenCell))
                    continue;
                GridGenerator.AddGridObjectToCell(chosenCell, groundTemplate, false);
            }
        }
    }

    public class BSPNode
    {
        public RectInt Bounds { get; }
        public BSPNode Child1, Child2;
        public RectInt Room { get; private set; }
        public bool IsLeaf => Child1 == null && Child2 == null;

        public BSPNode(RectInt bounds)
        {
            Bounds = bounds;
        }

        public void RecursiveSplit(RandomService random, int minRoomSize, int maxRooms, ref int splitCount, int maxSteps, int depth = 0)
        {
            if (splitCount >= maxRooms || depth >= maxSteps)
                return;

            if (!CanSplit(minRoomSize * 2))
                return;

            if (Split(random, minRoomSize))
            {
                splitCount++;
                Child1.RecursiveSplit(random, minRoomSize, maxRooms, ref splitCount, maxSteps, depth + 1);
                Child2.RecursiveSplit(random, minRoomSize, maxRooms, ref splitCount, maxSteps, depth + 1);
            }
        }

        public void ReturnLeaves(List<BSPNode> leaves)
        {
            if (IsLeaf)
            {
                leaves.Add(this);
            }
            else
            {
                Child1?.ReturnLeaves(leaves);
                Child2?.ReturnLeaves(leaves);
            }
        }

        public bool CanSplit(int minSize)
        {
            return Bounds.width >= minSize || Bounds.height >= minSize;
        }

        public bool Split(RandomService random, int minRoomSize)
        {
            bool splitH = random.Chance(0.5f);
            if (Bounds.width > Bounds.height && Bounds.width / Bounds.height >= 1.25f)
                splitH = false;
            else if (Bounds.height > Bounds.width && Bounds.height / Bounds.width >= 1.25f)
                splitH = true;

            int max = (splitH ? Bounds.height : Bounds.width) - minRoomSize * 2;
            if (max <= minRoomSize * 2)
                return false;

            int split = random.Range(minRoomSize, max);
            if (splitH)
            {
                Child1 = new BSPNode(new RectInt(Bounds.x, Bounds.y, Bounds.width, split));
                Child2 = new BSPNode(new RectInt(Bounds.x, Bounds.y + split, Bounds.width, Bounds.height - split));
            }
            else
            {
                Child1 = new BSPNode(new RectInt(Bounds.x, Bounds.y, split, Bounds.height));
                Child2 = new BSPNode(new RectInt(Bounds.x + split, Bounds.y, Bounds.width - split, Bounds.height));
            }
            return true;
        }

        public Vector2Int GetRoomCenter()
        {
            if (IsLeaf) return Vector2Int.RoundToInt(Room.center);
            var centers = new List<Vector2Int>();
            if (Child1 != null) centers.Add(Child1.GetRoomCenter());
            if (Child2 != null) centers.Add(Child2.GetRoomCenter());
            return centers.Count > 0 ? centers[0] : Vector2Int.zero;
        }

        public void CreateRoomTemplate(RandomService randomService, int minRoomSize, int maxRoomSize, int spacing)
        {
            int maxRoomWidth = Mathf.Min(maxRoomSize, Bounds.width - 2 * spacing);
            int maxRoomHeight = Mathf.Min(maxRoomSize, Bounds.height - 2 * spacing);

            if (maxRoomWidth < minRoomSize) maxRoomWidth = minRoomSize;
            if (maxRoomHeight < minRoomSize) maxRoomHeight = minRoomSize;

            int roomWidth = randomService.Range(minRoomSize, maxRoomWidth + 1);
            int roomHeight = randomService.Range(minRoomSize, maxRoomHeight + 1);

            int roomX = randomService.Range(Bounds.xMin + spacing, Bounds.xMax - spacing - roomWidth + 1);
            int roomY = randomService.Range(Bounds.yMin + spacing, Bounds.yMax - spacing - roomHeight + 1);

            Room = new RectInt(roomX, roomY, roomWidth, roomHeight);
        }

        public void ConnectRooms(BSP parent)
        {
            if (Child1 != null && Child2 != null)
            {
                var center1 = Child1.GetRoomCenter();
                var center2 = Child2.GetRoomCenter();
                parent.CreateCorridor(center1, center2);
                Child1.ConnectRooms(parent);
                Child2.ConnectRooms(parent);
            }
        }
    }
}



//debug window avec toutes mes nodes et leurs coordonnées