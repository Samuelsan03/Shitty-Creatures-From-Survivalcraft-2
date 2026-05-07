using System;
using Engine;

namespace Game
{
	/// <summary>
	/// Generador de terreno modificado para incluir árboles de manzano adicionales.
	/// No reemplaza los árboles vanilla, solo añade manzanos.
	/// </summary>
	public class ShittyTerrainContentsGenerator : TerrainContentsGenerator24
	{
		public ShittyTerrainContentsGenerator(SubsystemTerrain subsystemTerrain) : base(subsystemTerrain)
		{
		}

		/// <summary>
		/// Primero genera árboles vanilla, luego añade manzanos extra.
		/// </summary>
		public override void GenerateTrees(TerrainChunk chunk)
		{
			// 1. Árboles normales del juego (robles, abedules, pinos, etc.)
			base.GenerateTrees(chunk);

			// 2. Añadir árboles frutales (manzano, peral, naranjo, cerezo)
			if (!TGExtras) return;

			Terrain terrain = m_subsystemTerrain.Terrain;
			int chunkX = chunk.Coords.X;
			int chunkZ = chunk.Coords.Y;

			Random random = new Random(m_seed + chunkX + 3943 * chunkZ + 12345);
			int humidity = CalculateHumidity(chunkX * 16, chunkZ * 16);
			int temperature = CalculateTemperature(chunkX * 16, chunkZ * 16);
			float forestDensity = CalculateForestDensity(chunkX * 16, chunkZ * 16);

			// Cantidad de árboles frutales a añadir (aproximadamente la mitad de la densidad normal)
			int targetFruitTrees = (int)(3f * forestDensity);
			int attempts = 0;
			int planted = 0;

			while (attempts < 36 && planted < targetFruitTrees)
			{
				int x = chunkX * 16 + random.Int(2, 13);
				int z = chunkZ * 16 + random.Int(2, 13);
				int y = terrain.CalculateTopmostCellHeight(x, z);

				if (y >= 66)
				{
					int ground = terrain.GetCellContentsFast(x, y, z);
					if (ground == 2 || ground == 8) // césped o tierra
					{
						y++; // posición de plantación

						// Espacio libre alrededor
						if (!BlocksManager.Blocks[terrain.GetCellContentsFast(x + 1, y, z)].IsCollidable &&
							!BlocksManager.Blocks[terrain.GetCellContentsFast(x - 1, y, z)].IsCollidable &&
							!BlocksManager.Blocks[terrain.GetCellContentsFast(x, y, z + 1)].IsCollidable &&
							!BlocksManager.Blocks[terrain.GetCellContentsFast(x, y, z - 1)].IsCollidable)
						{
							// Elegir tipo de árbol frutal según probabilidad
							ShittyTreeType? fruitTreeType = ShittyPlantsManager.GenerateRandomFruitTreeType(random, temperature, humidity, y);
							if (fruitTreeType != null)
							{
								var brushes = ShittyPlantsManager.GetTreeBrushes(fruitTreeType.Value);
								if (brushes.Count > 0)
								{
									TerrainBrush brush = brushes[random.Int(brushes.Count)];
									brush.PaintFast(chunk, x, y, z);
									chunk.AddBrushPaint(x, y, z, brush);
									planted++;
								}
							}
						}
					}
				}
				attempts++;
			}
		}
	}
}
