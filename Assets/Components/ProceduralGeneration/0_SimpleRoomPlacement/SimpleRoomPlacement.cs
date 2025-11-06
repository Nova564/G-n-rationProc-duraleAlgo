using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VTools.Grid;
using VTools.ScriptableObjectDatabase;
using VTools.Utility;

namespace Components.ProceduralGeneration.SimpleRoomPlacement
{
    [CreateAssetMenu(menuName = "Procedural Generation Method/Simple Room Placement")]
    public class SimpleRoomPlacement : ProceduralGenerationMethod
    {
        [Header("Room Parameters")]
        [SerializeField] private int _maxRooms = 10;
        [Header("Tile Parameters")]
        [SerializeField] private int _tileMinWidth = 5;
        [SerializeField] private int _tileMaxWidth = 5;
        [SerializeField] private int _tileMinHeight = 5;
        [SerializeField] private int _tileMaxHeight = 5;

        protected override async UniTask ApplyGeneration(CancellationToken cancellationToken)
        {
            int roomsPlaced = 0;
            int attempts = 0;
            int maxAttempts = _maxRooms * 10;
            var roomCenters = new List<Vector2Int>();

            while (roomsPlaced < _maxRooms && attempts <= maxAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int width = RandomService.Range(_tileMinWidth, _tileMaxWidth + 1);
                int height = RandomService.Range(_tileMinHeight, _tileMaxHeight + 1);
                int x = RandomService.Range(0, Grid.Width - width);
                int y = RandomService.Range(0, Grid.Lenght - height);

                RectInt room = new RectInt(x, y, width, height);

                if (CanPlaceRoom(room, 1))
                {
                    PlaceRoom(room);

                    var center = room.GetCenter();
                    roomCenters.Add(center);
                    roomsPlaced++;
                }
                attempts++;
                await UniTask.Delay(GridGenerator.StepDelay, cancellationToken: cancellationToken);
            }

            for (int i = 1; i < roomCenters.Count; i++)
            {
                CreateCorridor(roomCenters[i - 1], roomCenters[i]);
            }

            BuildGround();
        }

        void CreateCorridor(Vector2Int from, Vector2Int to)
        {
            int x = from.x;
            int y = from.y;

            while (x != to.x)
            {
                x += x < to.x ? 1 : -1;
                if (Grid.TryGetCellByCoordinates(x, y, out Cell cell))
                {
                    AddTileToCell(cell, CORRIDOR_TILE_NAME, true);
                }
            }
            while (y != to.y)
            {
                y += y < to.y ? 1 : -1;
                if (Grid.TryGetCellByCoordinates(x, y, out Cell cell))
                {
                    AddTileToCell(cell, CORRIDOR_TILE_NAME, true);
                }
            }
        }

        void PlaceRoom(RectInt room)
        {
            for (int ix = room.xMin; ix < room.xMin + room.width; ix++)
            {
                for (int iy = room.yMin; iy < room.yMin + room.height; iy++)
                {
                    if (!Grid.TryGetCellByCoordinates(ix, iy, out Cell cell))
                        continue;

                    AddTileToCell(cell, ROOM_TILE_NAME, true);
                }
            }
        }

        private void BuildGround()
        {
            var groundTemplate = ScriptableObjectDatabase.GetScriptableObject<GridObjectTemplate>("Grass");
            for (int x = 0; x < Grid.Width; x++)
            {
                for (int z = 0; z < Grid.Lenght; z++)
                {
                    if (!Grid.TryGetCellByCoordinates(x, z, out var chosenCell))
                    {
                        Debug.LogError($"Unable to get cell on coordinates : ({x}, {z})");
                        continue;
                    }
                    GridGenerator.AddGridObjectToCell(chosenCell, groundTemplate, false);
                }
            }
        }
    }
}