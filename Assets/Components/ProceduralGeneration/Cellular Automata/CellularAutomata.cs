using Components.ProceduralGeneration;
using Cysharp.Threading.Tasks;
using Microsoft.Unity.VisualStudio.Editor;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VTools.Grid;
using VTools.RandomService;


[CreateAssetMenu(menuName = "Procedural Generation Method/Cellular automata")]
public class CellularAutomata : ProceduralGenerationMethod
{
    [Header("Main parameters")]
    [SerializeField] float _mainDensity = 80f;
    [SerializeField] int _iterations = 0;

    protected override async UniTask ApplyGeneration(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var cell in Grid.Cells)
        {
            bool isGrass = RandomService.Chance(_mainDensity / 100f);
            AddTileToCell(cell, isGrass ? GRASS_TILE_NAME : WATER_TILE_NAME, true);
        }

        await UniTask.Delay(GridGenerator.StepDelay, cancellationToken: cancellationToken);

        for (int currentIteration = 0; currentIteration < _iterations; currentIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();


            Dictionary<Cell, string> cellTypeForNextIteration = new();

            foreach (var cell in Grid.Cells)
            {
                int grassNeighborCount = CountGrassNeighbors(cell);
                string nextType = grassNeighborCount >= 4 ? GRASS_TILE_NAME : WATER_TILE_NAME;
                cellTypeForNextIteration[cell] = nextType;
            }

            foreach (var cell in Grid.Cells)
            {
                string typeToSet = cellTypeForNextIteration[cell];
                AddTileToCell(cell, typeToSet, true);
            }

            await UniTask.Delay(GridGenerator.StepDelay, cancellationToken: cancellationToken);
        }
    }

    int CountGrassNeighbors(Cell cell)
    {
        int count = 0;
        Vector2Int[] neighborOffsets = new Vector2Int[]
        {
        new(-1, -1),new(0, -1),new(1, -1),
        new(-1,  0),          new(1,  0),
        new(-1,  1),new(0,  1),new(1,  1)
        };

        foreach (var offset in neighborOffsets)
        {
            Vector2Int neighborPosition = cell.Coordinates + offset;
            if (Grid.TryGetCellByCoordinates(neighborPosition, out var neighborCell))
            {
                if (neighborCell.GridObject != null && neighborCell.GridObject.Template.Name == GRASS_TILE_NAME)
                    count++;
            }
        }
        return count;
    }
}