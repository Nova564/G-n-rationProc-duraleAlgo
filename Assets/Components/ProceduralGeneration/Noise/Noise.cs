using Components.ProceduralGeneration;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

[CreateAssetMenu(menuName = "Procedural Generation Method/Noise Generator")]
public class NoiseGenerator : ProceduralGenerationMethod
{
    [Header("LAYER")]
    [Range(-1f, 1f)] public float highWater;
    [Range(-1f, 1f)] public float highSand;
    [Range(-1f, 1f)] public float highGrass;
    [Range(-1f, 1f)] public float highRock;

    [Header("NOISE PARAMETER")]
    [Range(0, 1f)] public float frequency;

    protected override async UniTask ApplyGeneration(CancellationToken cancellationToken)
    {
        // Create and configure FastNoise object
        FastNoiseLite noise = new FastNoiseLite();
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);

        // Gather noise data
        float[,] noiseData = new float[Grid.Width, Grid.Lenght];

        noise.SetFrequency(frequency);
        noise.SetSeed(RandomService.Seed);

        for (int x = 0; x < Grid.Width; x++)
        {
            for (int y = 0; y < Grid.Lenght; y++)
            {
                noiseData[x, y] = noise.GetNoise(x, y);
                if (!Grid.TryGetCellByCoordinates(x, y, out var cell))
                    continue;
                float high = noise.GetNoise(x, y);
                bool hasplace = false;
                if (high < highWater)
                {
                    AddTileToCell(cell, WATER_TILE_NAME, true);
                    hasplace = true;
                }
                if (high < highSand && hasplace == false)
                {
                    AddTileToCell(cell, SAND_TILE_NAME, true);
                    hasplace = true;
                }
                if (high < highGrass && hasplace == false)
                {
                    AddTileToCell(cell, GRASS_TILE_NAME, true);
                    hasplace = true;
                }
                if (high < highRock && hasplace == false)
                {
                    AddTileToCell(cell, ROCK_TILE_NAME, true);
                    hasplace = true;
                }
            }
        }
    }
}