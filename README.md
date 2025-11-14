# Système de Génération Procédurale sur Unity à travers une grid

Système unity modulaire avec génération procédurale par algo facile d'utilisation.

---

## Sommaire

1. [Le Moteur](#le-moteur)
   - [ProceduralGridGenerator](#proceduralgridgenerator)
   - [Grid & Cell](#grid--cell)
   - [Fonctionnement Global](#fonctionnement-global)
2. [Les Algorithmes](#les-algorithmes)
   - [Simple Room Placement](#1-simple-room-placement)
   - [Binary Space Partitioning (BSP)](#2-binary-space-partitioning-bsp)
   - [Cellular Automata](#3-cellular-automata)
   - [Noise Generator](#4-noise-generator)
3. [Créer Son Algorithme](#créer-son-algorithme)

---

## Le Moteur

### ProceduralGridGenerator

C'est de là que tout est fixé pour avoir une bonne visualisation.

```csharp
public class ProceduralGridGenerator : BaseGridGenerator
{
    [SerializeField] private ProceduralGenerationMethod _generationMethod;
    [SerializeField] private int _seed = 1234;
    [SerializeField] private int _stepDelay = 500;

    public override void GenerateGrid()
    {
        // 1. Créer une grille vide
        base.GenerateGrid();
        
        // 2. Initialiser l'algorithme en fonction d'une seed pour avoir du replay en terme de random
        _generationMethod.Initialize(this, new RandomService(_seed));
        
        // 3. Démarrer la génération
        await _generationMethod.Generate();
    }
}
```

**Les paramêtres à connaitre :**
- `_generationMethod` : L'algorithme à utiliser sous la forme de ScriptableObject
- `_seed` : Une graine aléatoire pour avoir des résultats random reproduisible
- `_stepDelay` : Délai entre chaque étape surtout pour la visualisation en temps réel

### Grid & Cell

La grid est un tableau 2d qui contient toutes les cellules qui vont servir à la visualisation.

```csharp
public class Grid
{
    private readonly Cell[,] _gridArray;
    
    public int Width { get; }
    public int Lenght { get; }
    public float CellSize { get; }
    
    // Récupérer une cellule par leurs coordonnées
    public bool TryGetCellByCoordinates(int x, int y, out Cell foundCell)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Lenght)
        {
            foundCell = _gridArray[x, y];
            return true;
        }
        foundCell = null;
        return false;
    }
}
```

La cell représente une case individuelle elle peut contenir un objet et connait sa position donc facilement récuperable.

```csharp
public class Cell
{
    public Vector2Int Coordinates { get; }
    public bool ContainObject => _object != null;
    public GridObject GridObject => _object.Item1;
    
    public Vector3 GetCenterPosition(Vector3 originPosition)
    {
        return new Vector3(Coordinates.x + _size / 2, 0, 
                          Coordinates.y + _size / 2) * _size + originPosition;
    }
}
```

### Fonctionnement Global

Tous les algorithmes héritent de ProceduralGenerationMethood qui fournit des plein de méthodes utilitaires :

```csharp
public abstract class ProceduralGenerationMethod : ScriptableObject
{
    protected VTools.Grid.Grid Grid { get; }
    protected RandomService RandomService { get; }
    
    // Méthode à implémenter par chaque algorithme obligatoirement
    protected abstract UniTask ApplyGeneration(CancellationToken cancellationToken);
    
    // Helper pour placer une tile
    protected void AddTileToCell(Cell cell, string tileName, bool overrideExisting)
    {
        var template = ScriptableObjectDatabase.GetScriptableObject<GridObjectTemplate>(tileName);
        GridGenerator.AddGridObjectToCell(cell, template, overrideExisting);
    }
    
    // Helper pour vérifier si une salle peut être placée
    protected bool CanPlaceRoom(RectInt room, int spacing)
    {
        // Vérifie qu'aucune autre salle n'occupe déjà cet espace
        for (int ix = xMin; ix < xMax; ix++)
            for (int iy = yMin; iy < yMax; iy++)
                if (cell.ContainObject && cell.GridObject.Template.Name == "Room")
                    return false;
        return true;
    }
}
```

**Tiles disponibles :** `Room`, `Corridor`, `Grass`, `Water`, `Rock`, `Sand`

---

## Les Algorithmes

### 1. Simple Room Placement

**Le concept principal :** Place des salles rectangulaires de manière aléatoire sur la grid

#### Le fonctionnement est tel que

```csharp
protected override async UniTask ApplyGeneration(CancellationToken cancellationToken)
{
    var roomCenters = new List<Vector2Int>();
    
    // Première partie : Placement aléatoire
    while (roomsPlaced < _maxRooms && attempts <= maxAttempts)
    {
        // Générer une salle aléatoire
        int width = RandomService.Range(_tileMinWidth, _tileMaxWidth + 1);
        int height = RandomService.Range(_tileMinHeight, _tileMaxHeight + 1);
        RectInt room = new RectInt(x, y, width, height);
        
        // Check si elle peut être placer pour éviter l'overlap
        if (CanPlaceRoom(room, spacing: 1))
        {
            PlaceRoom(room);
            roomCenters.Add(room.GetCenter());
        }
    }
    
    // On va ensuite venir au couloir pour les connecter
    for (int i = 1; i < roomCenters.Count; i++)
    {
        CreateCorridor(roomCenters[i - 1], roomCenters[i]);
    }
}
```

**La création des couloirs*
On a vu que les rooms se connecte avec des couloirs voici comment c'est fait
```csharp
void CreateCorridor(Vector2Int from, Vector2Int to)
{
    //On voit pour avoir 2 points le point d'origine et l'arrivée on va prendre le x de l'origine et l'incrémenter ou le soustraire afin d'avoir le x de l'arrivée
    int x = from.x, y = from.y;
    
    while (x != to.x)
    {
        x += x < to.x ? 1 : -1;
        AddTileToCell(cell, CORRIDOR_TILE_NAME, true);
    }
    
    // On fait le même chose sur l'axe Y on peut faire un random pour ne pas forcément avoir le meme pattern faire le x ou y en premier
    while (y != to.y)
    {
        y += y < to.y ? 1 : -1;
        AddTileToCell(cell, CORRIDOR_TILE_NAME, true);
    }
}
```

#### Avantages
- Très rapide et simple à configurer
- Possibilités variées à chaque génération
- Peut faire gagner pas mal de temps pour du prototypage

#### Inconvénients
- Les salles ne sont pas forcément tout le temps connectées avec cette logique
- Sans réglementation la distribution peut être inégale
- Possibilité d'échouer de placer toutes les tiles

**C'est donc très bien pour :** Prototype de donjon, de salle, avoir un rendu visuel très vite

---

### 2. Binary Space Partitioning (BSP)

**Principe :** Découpe récursivement la grid en plusieurs salles pour avoir des "nodes" on va ensuite pouvoir placer des rooms à l'intérieur ce qui permettrait d'avoir quelque chose d'ordonnée

#### Comment ça marche

```csharp
protected override async UniTask ApplyGeneration(CancellationToken cancellationToken)
{
    // On construit l'arbre de découpage
    var root = new BSPNode(new RectInt(0, 0, Grid.Width, Grid.Lenght));
    root.RecursiveSplit(RandomService, _minRoomSize, _maxRooms, ref splitCount);
    
    // On va récupérer les feuilles qui sont les derniers enfants de l'algorithme donc qui n'auront pas de child supplémentaire (comme un arbre)
    var leaves = new List<BSPNode>();
    root.ReturnLeaves(leaves);
    
    // Placer une salle dans chaque feuille
    foreach (var leaf in leaves)
    {
        leaf.CreateRoomTemplate(RandomService, _minRoomSize, _maxRoomSize, _spacing);
        PlaceRoom(leaf.Room);
    }
    
    // On connecte les salles
    root.ConnectRooms(this);
}
```

**Le nœud BSP qui est du découpage récursif :**
```csharp
public class BSPNode
{
    public RectInt Bounds { get; }      // Zone rectangulaire
    public BSPNode Child1, Child2;      // Enfants résultant de la découpe
    public RectInt Room { get; }        //  La salle générée
    
    public void RecursiveSplit(RandomService random, int minRoomSize, 
                               int maxRooms, ref int splitCount)
    {
        if (splitCount >= maxRooms) return;
        
        // Déterminer la direction de découpe (horizontal ou vertical)
        bool splitHorizontal = random.Chance(0.5f);
        
        // Pour favoriser les découpes équilibrées on va réglementer couper verticalement si c'est trop large etc
        if (Bounds.width / Bounds.height >= 1.25f)
            splitHorizontal = false;  
        
        // Effectuer la découpe
        if (splitHorizontal)
        {
            Child1 = new BSPNode(/* moitié haute */);
            Child2 = new BSPNode(/* moitié basse */);
        }
        
        // Continuer récursivement
        Child1.RecursiveSplit(random, minRoomSize, maxRooms, ref splitCount);
        Child2.RecursiveSplit(random, minRoomSize, maxRooms, ref splitCount);
    }
}
```

**La connexion des rooms :**
```csharp
public void ConnectRooms(BSP parent)
{
    if (Child1 != null && Child2 != null)
    {
        // Connecter les centres des deux enfants
        var center1 = Child1.GetRoomCenter();
        var center2 = Child2.GetRoomCenter();
        parent.CreateCorridor(center1, center2);
        
        // Continuer récursivement jusqu'à ne plus en avoir donc jusqu'au feuilles
        Child1.ConnectRooms(parent);
        Child2.ConnectRooms(parent);
    }
}
```

#### Avantages
- Contrairement au simpleroomplacement la connéctivité des rooms est beaucoup plus constante
- Structure organisée
- Contrôle précis de la distribution sur la grid

#### Inconvénients
- Peut produire des layouts prévisibles
- Réglementation et instauration de contraintes plus complexe (souvent par le code)
- Les couloirs suivent la hiérarchie ce qui n'est pas optimal

**Idéal pour :** Layout de donjons requierant seulement des rooms et des couloirs qui les lient 

---

### 3. Cellular Automata

**Principe :** Génère des motifs avec des tiles randoms au départ

#### Comment ça marche

```csharp
protected override async UniTask ApplyGeneration(CancellationToken cancellationToken)
{
    // Initialisation aléatoire
    foreach (var cell in Grid.Cells)
    {
        bool isGrass = RandomService.Chance(_mainDensity / 100f);
        AddTileToCell(cell, isGrass ? GRASS_TILE_NAME : WATER_TILE_NAME, true);
    }
    
    // Lissage pour avoir quelque chose d'organique
    for (int iteration = 0; iteration < _iterations; iteration++)
    {
        Dictionary<Cell, string> nextState = new();
        
        // On calcule le prochain état de chaque cellule
        foreach (var cell in Grid.Cells)
        {
            int grassNeighbors = CountGrassNeighbors(cell);
            
            // La règle est si : 4+ voisins grass la tile devient aussi grass sinon water
            nextState[cell] = grassNeighbors >= 4 ? GRASS_TILE_NAME : WATER_TILE_NAME;
        }
        
        // On applique le nouvel état
        foreach (var cell in Grid.Cells)
            AddTileToCell(cell, nextState[cell], true);
    }
}
```

**Comptage des voisins (8 directions) :**
```csharp
int CountGrassNeighbors(Cell cell)
{
    int count = 0;
//toutes les positions de voisins possible pour chaque tile
    Vector2Int[] offsets = new Vector2Int[]
    {
        new(-1,-1), new(0,-1), new(1,-1),  // Haut
        new(-1, 0),            new(1, 0),  // Côtés
        new(-1, 1), new(0, 1), new(1, 1)   // Bas
    };
    
    foreach (var offset in offsets)
    {
        Vector2Int pos = cell.Coordinates + offset;
        if (Grid.TryGetCellByCoordinates(pos, out var neighbor))
            if (neighbor.GridObject?.Template.Name == GRASS_TILE_NAME)
                count++;
    }
    return count;
}
```

#### Avantages
- Génère des formes organiques et semblant naturelles
- Très simple à implémenter
- Excellent pour simuler de l'érosion naturelle par exemple
- Résultats visuellement intéressants pour des maps, et une visualisation d'un potentiel projet

#### Inconvénients
- Très peu de contrôle sur la connectivité (règle très précise)
- Peut créer des zones isolées
- Pas de concept de "salles" donc obligatoirement des terrains
- Résultats difficillement prédictible


**Idéal pour :** Grottes naturelles, terrains organiques, environnements de type mine forêt etc cela dit très mauvais pour générer des rooms comme les 2 algos précédent

---

### 4. Noise Generator

**Principe :** Utilise un bruit pour générer ensuite du terrain aléatoirement grâce à ce dernier

#### Comment ça marche

```csharp
protected override async UniTask ApplyGeneration(CancellationToken cancellationToken)
{
    // Config du générateur du bruit (voir la doc FastNoiseLite)
    FastNoiseLite noise = new FastNoiseLite();
    noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
    noise.SetFrequency(frequency);
    noise.SetSeed(RandomService.Seed);
    
    // Parcourir chaque cellule
    for (int x = 0; x < Grid.Width; x++)
    {
        for (int y = 0; y < Grid.Lenght; y++)
        {
            
            float height = noise.GetNoise(x, y);
            
            if (!Grid.TryGetCellByCoordinates(x, y, out var cell))
                continue;
            
            // Mapper l'altitude sur différents types de terrains
            if (height < highWater)
                AddTileToCell(cell, WATER_TILE_NAME, true);
            else if (height < highSand)
                AddTileToCell(cell, SAND_TILE_NAME, true);
            else if (height < highGrass)
                AddTileToCell(cell, GRASS_TILE_NAME, true);
            else
                AddTileToCell(cell, ROCK_TILE_NAME, true);
        }
    }
}
```


#### Avantages
- Résultats très naturels et organiques
- Transitions fluides entre biomes 
- Complètement déterministe selon la seed
- Excellente performance
- Facile à paramétrer et modifier

#### Inconvénients
- Pas de structures discrètes comme les bâtiments et les salles contrairement aux deux premiers algos
- Ne garantit pas de caractéristiques spécifiques
- Nécessite un ajustement fin des seuils
- Peut créer des layouts non optimaux pour le gameplay


**Idéal pour :** Terrains extérieurs, cartes de monde, îles etc

---

## Créer Son Algorithme

Le système est conçu pour être facilement extensible. Voici la structure de base :

```csharp
using Components.ProceduralGeneration;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

[CreateAssetMenu(menuName = "Procedural Generation Method/Mon Algorithme")]
public class MonAlgorithme : ProceduralGenerationMethod
{
    [Header("Paramètres")]
    [SerializeField] private int _monParametre = 10;
    
    protected override async UniTask ApplyGeneration(CancellationToken cancellationToken)
    {
        // 1. Parcourir la grille
        foreach (var cell in Grid.Cells)
        {
            // Vérifier l'annulation régulièrement
            cancellationToken.ThrowIfCancellationRequested();
            
            // 2. Logique de placement
            if (/* condition */)
            {
                AddTileToCell(cell, GRASS_TILE_NAME, true);
            }
            
            // 3. Délai pour visualisation (optionnel)
            await UniTask.Delay(GridGenerator.StepDelay, cancellationToken: cancellationToken);
        }
    }
}
```

**Méthodes utilitaires disponibles :**
```csharp
// Placer une tile
AddTileToCell(cell, "Room", overrideExisting: true);

// Vérifier si une salle peut être placée
if (CanPlaceRoom(room, spacing: 1)) { ... }

// Accéder à la grille
Grid.Width, Grid.Lenght, Grid.Cells

// Random avec seed
RandomService.Range(0, 100)
RandomService.Chance(0.5f)

// Récupérer une cellule
if (Grid.TryGetCellByCoordinates(x, y, out var cell)) { ... }
```

**Tiles disponibles :** `ROOM_TILE_NAME`, `CORRIDOR_TILE_NAME`, `GRASS_TILE_NAME`, `WATER_TILE_NAME`, `ROCK_TILE_NAME`, `SAND_TILE_NAME`

---

## Dépendances

- **UniTask** : Gestion asynchrone performante
- **FastNoiseLite** : Génération de bruit (inclus)
- **Unity Editor** : Outils d'inspection personnalisés

---

**Système développé pour la génération procédurale de niveaux Unity**
