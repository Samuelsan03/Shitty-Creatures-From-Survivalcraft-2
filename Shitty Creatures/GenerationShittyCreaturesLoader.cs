using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using XmlUtilities;

namespace Game
{
	public class GenerationShittyCreaturesLoader : ModLoader
	{
		public override void __ModInitialize()
		{
			ModsManager.RegisterHook("InitializeCreatureTypes", this);
		}

		public override void InitializeCreatureTypes(SubsystemCreatureSpawn subsystemCreatureSpawn, List<SubsystemCreatureSpawn.CreatureType> creatureTypes)
		{
			// Subsistemas necesarios
			SubsystemTime time = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTime>(true);
			SubsystemTimeOfDay timeOfDay = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
			SubsystemGameInfo gameInfo = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemGameInfo>(true);
			SubsystemSky sky = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemSky>(true);

			// Función auxiliar: detectar si un punto está cerca del agua
			Func<Point3, bool> isNearWater = delegate (Point3 point)
			{
				float shoreDistance = subsystemCreatureSpawn.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);
				int blockUnder = Terrain.ExtractContents(subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
				return (shoreDistance >= -5f && shoreDistance <= 15f) || (BlocksManager.Blocks[blockUnder] is WaterBlock);
			};

			// Función auxiliar: aparecer grupo de 3 o 5 criaturas
			Func<SubsystemCreatureSpawn, SubsystemCreatureSpawn.CreatureType, Point3, string, int> spawnGroup = delegate (SubsystemCreatureSpawn spawnSys, SubsystemCreatureSpawn.CreatureType ct, Point3 point, string templateName)
			{
				int count = spawnSys.m_random.Int(0, 1) == 0 ? 3 : 5;
				return spawnSys.SpawnCreatures(ct, templateName, point, count).Count;
			};

			// ==========================================
			// 1. PIRATA NORMAL (día ≥ 5)
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("PirataNormal", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (timeOfDay.CalculateDay(gameInfo.TotalElapsedGameTime) < 5.0) return 0f;
					return isNearWater(point) ? 2.5f : 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnGroup(subsystemCreatureSpawn, ct, point, "PirataNormal");
				}
			});

			// ==========================================
			// 2. PIRATA ELITE (día ≥ 15)
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("PirataElite", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (timeOfDay.CalculateDay(gameInfo.TotalElapsedGameTime) < 15.0) return 0f;
					return isNearWater(point) ? 2.5f : 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnGroup(subsystemCreatureSpawn, ct, point, "PirataElite");
				}
			});

			// ==========================================
			// 3. PIRATA HOSTIL COMERCIANTE (día ≥ 35) - solo
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("PirataHostilComerciante", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (timeOfDay.CalculateDay(gameInfo.TotalElapsedGameTime) < 35.0) return 0f;
					return isNearWater(point) ? 2.5f : 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return subsystemCreatureSpawn.SpawnCreatures(ct, "PirataHostilComerciante", point, 1).Count;
				}
			});

			// ==========================================
			// 4. CAPITÁN PIRATA (día ≥ 55) - solo
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("CapitanPirata", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (timeOfDay.CalculateDay(gameInfo.TotalElapsedGameTime) < 55.0) return 0f;
					return isNearWater(point) ? 2.5f : 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return subsystemCreatureSpawn.SpawnCreatures(ct, "CapitanPirata", point, 1).Count;
				}
			});

			// ==========================================
			// 5. RAYMAN (montañas, siempre)
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Rayman", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					// 1. Solo de día
					if (sky.SkyLightIntensity < 0.4f) return 0f;

					// 2. Factor de montaña (0..1, valores altos = cadenas montañosas)
					float mountainFactor = subsystemCreatureSpawn.m_subsystemTerrain.TerrainContentsGenerator.CalculateMountainRangeFactor((float)point.X, (float)point.Z);

					// 3. Altura real del terreno en esta celda
					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);

					// 4. Si el punto está dentro de los 5 bloques superiores de la montaña
					bool nearTop = (point.Y >= topHeight - 5);

					// Umbrales elevados: mountainFactor >= 0.95 Y altura >= 120 Y cerca de la cima
					if (mountainFactor >= 0.95f && topHeight >= 120 && nearTop)
						return 5000f;   // Prioridad muy alta para que aparezca rápido
					else
						return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					// Ajustar la posición final a la cima real del terreno (evita spawn en laderas)
					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					Point3 correctedPoint = new Point3(point.X, topHeight, point.Z);
					return subsystemCreatureSpawn.SpawnCreatures(ct, "Rayman", correctedPoint, 1).Count;
				}
			});
		}

		// Los siguientes métodos no son necesarios para la generación, pero se dejan vacíos para cumplir con ModLoader
		public override void SaveSettings(XElement xElement) { }
		public override void LoadSettings(XElement xElement) { }
	}
}
