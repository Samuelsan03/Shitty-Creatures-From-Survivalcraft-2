using System;
using Engine;
using Game;

namespace Game
{
	/// <summary>
	/// Generador de terreno personalizado que añade árboles frutales, arbustos de arándanos y sandías.
	/// </summary>
	public class ShittyTerrainContentsGenerator24 : TerrainContentsGenerator24
	{
		private Random m_fruitTreeRandom;
		private Random m_cropsRandom;

		public ShittyTerrainContentsGenerator24(SubsystemTerrain subsystemTerrain)
			: base(subsystemTerrain)
		{
			m_fruitTreeRandom = new Random(m_seed);
			m_cropsRandom = new Random(m_seed + 7777);
			this.ChunkGenerationStep4.Add(new ChunkGenerationStep(450, GenerateBlueberryBushes));
			this.ChunkGenerationStep4.Add(new ChunkGenerationStep(550, GenerateWatermelons));
		}

		private void GenerateBlueberryBushes(TerrainChunk chunk)
		{
			if (!TGExtras) return;

			// Obtener índice del bloque de arándanos usando typeof()
			int blueberryBlockIndex = BlocksManager.GetBlockIndex(typeof(BlueberryBushBlock), throwIfNotFound: false, mustBeInSameType: true);
			if (blueberryBlockIndex < 0) return; // Si no existe el bloque, salir

			int blueberryValue = Terrain.MakeBlockValue(blueberryBlockIndex);

			Random random = new Random(m_seed + chunk.Coords.X * 3943 + chunk.Coords.Y * 1991);

			int attempts = random.Int(0, 4);
			for (int attempt = 0; attempt < attempts; attempt++)
			{
				int x = random.Int(2, 13);
				int z = random.Int(2, 13);
				int temperature = chunk.GetTemperatureFast(x, z);
				int humidity = chunk.GetHumidityFast(x, z);

				// Condiciones: clima templado-húmedo (similar a arbustos reales)
				if (temperature < 4 || temperature > 12 || humidity < 5) continue;

				int y = chunk.CalculateTopmostCellHeight(x, z);
				if (y < 66) continue;

				int ground = chunk.GetCellContentsFast(x, y, z);
				// Solo sobre césped (2) o tierra (8)
				if (ground != 2 && ground != 8) continue;

				// Verificar espacio encima
				if (chunk.GetCellContentsFast(x, y + 1, z) != 0) continue;

				// Colocar el arbusto
				chunk.SetCellValueFast(x, y + 1, z, blueberryValue);
			}
		}

		private void GenerateWatermelons(TerrainChunk chunk)
		{
			if (!TGExtras) return;

			// Obtenemos el índice del bloque de sandía por su nombre (evitamos números)
			int watermelonBlockIndex = BlocksManager.GetBlockIndex("WatermelonBlock", throwIfNotFound: false);
			if (watermelonBlockIndex < 0) return; // si no existe, salimos

			// Valor de sandía madura (tamaño 7)
			int watermelonValue = Terrain.MakeBlockValue(watermelonBlockIndex, 0,
				BaseWatermelonBlock.SetSize(BaseWatermelonBlock.SetIsDead(0, false), 7));

			Random random = new Random(m_seed + chunk.Coords.X * 3943 + chunk.Coords.Y * 1495);

			int attempts = random.Int(0, 2);
			for (int attempt = 0; attempt < attempts; attempt++)
			{
				int x = random.Int(2, 13);
				int z = random.Int(2, 13);
				int temperature = chunk.GetTemperatureFast(x, z);
				int humidity = chunk.GetHumidityFast(x, z);

				// Sandías prefieren climas cálidos y húmedos
				if (temperature < 8 || temperature > 15 || humidity < 8) continue;

				int y = chunk.CalculateTopmostCellHeight(x, z);
				if (y < 66) continue;

				int ground = chunk.GetCellContentsFast(x, y, z);
				if (ground != 2 && ground != 8) continue;

				// Espacio para la sandía (puede ser un bloque de ancho)
				bool hasSpace = true;
				for (int dx = -1; dx <= 1; dx++)
					for (int dz = -1; dz <= 1; dz++)
					{
						if (chunk.GetCellContentsFast(x + dx, y + 1, z + dz) != 0)
						{
							hasSpace = false;
							break;
						}
					}
				if (!hasSpace) continue;

				// Colocar la sandía
				chunk.SetCellValueFast(x, y + 1, z, watermelonValue);
			}
		}

		// ==================== ÁRBOLES FRUTALES ====================
		public override void GenerateTrees(TerrainChunk chunk)
		{
			base.GenerateTrees(chunk);
			GenerateFruitTrees(chunk);
		}

		private void GenerateFruitTrees(TerrainChunk chunk)
		{
			Terrain terrain = m_subsystemTerrain.Terrain;
			int chunkX = chunk.Coords.X;
			int chunkZ = chunk.Coords.Y;

			Random chunkRandom = new Random(m_seed + chunkX * 3943 + chunkZ);

			float forestDensity = CalculateForestDensity(chunkX * 16 + 8, chunkZ * 16 + 8);
			int attempts = Math.Max(2, (int)(3f * forestDensity));
			int planted = 0;

			for (int attempt = 0; attempt < attempts * 8 && planted < attempts; attempt++)
			{
				int x = chunkX * 16 + chunkRandom.Int(2, 13);
				int z = chunkZ * 16 + chunkRandom.Int(2, 13);
				int y = terrain.CalculateTopmostCellHeight(x, z);

				if (y < 66) continue;

				int ground = terrain.GetCellContentsFast(x, y, z);
				if (ground != 2 && ground != 8) continue; // solo césped o tierra

				// Verificar espacio básico
				bool hasSpace = true;
				for (int dy = 0; dy < 8; dy++)
				{
					for (int dx = -2; dx <= 2; dx++)
					{
						for (int dz = -2; dz <= 2; dz++)
						{
							int c = terrain.GetCellContentsFast(x + dx, y + 1 + dy, z + dz);
							if (c != 0 && c != 18) // agua permitida, otros bloques no
							{
								hasSpace = false;
								break;
							}
						}
						if (!hasSpace) break;
					}
					if (!hasSpace) break;
				}
				if (!hasSpace) continue;

				int realTemp = terrain.GetTemperature(x, z);
				int realHum = terrain.GetHumidity(x, z);

				// Solo usar temperatura base, sin ajuste de altura complicado
				ShittyTreeType? treeType = ShittyPlantsManager.GenerateRandomTreeType(
					chunkRandom, realTemp, realHum, y, 0.5f);

				if (treeType != null)
				{
					var brushes = ShittyPlantsManager.GetTreeBrushes(treeType.Value);
					if (brushes.Count > 0)
					{
						TerrainBrush brush = brushes[chunkRandom.Int(0, brushes.Count - 1)];
						brush.PaintFast(chunk, x, y + 1, z);
						chunk.AddBrushPaint(x, y + 1, z, brush);

						// --- AÑADIR FRUTOS ---
						float fruitDensity = ShittyPlantsManager.CalculateFruitDensity(
							treeType.Value, realTemp, realHum, y);
						ShittyPlantsManager.AttachFruitsToTreeFast(
							chunk, x, y + 1, z, brush, treeType.Value, chunkRandom, fruitDensity);

						planted++;
					}
				}
			}
		}

		private bool IsSpaceForTree(Terrain terrain, int x, int y, int z)
		{
			for (int dy = 0; dy < 8; dy++)
				if (terrain.GetCellContentsFast(x, y + dy, z) != 0 ||
					terrain.GetCellContentsFast(x + 1, y + dy, z) != 0 ||
					terrain.GetCellContentsFast(x - 1, y + dy, z) != 0 ||
					terrain.GetCellContentsFast(x, y + dy, z + 1) != 0 ||
					terrain.GetCellContentsFast(x, y + dy, z - 1) != 0)
					return false;
			return true;
		}
	}
}
