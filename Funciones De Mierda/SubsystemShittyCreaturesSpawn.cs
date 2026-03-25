using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemShittyCreaturesSpawn : SubsystemCreatureSpawn
	{
		public override void InitializeCreatureTypes()
		{
			// First, initialize all original creature types
			base.InitializeCreatureTypes();

			// Helper function to check surface spawn point
			float GetSurfaceSuitability(Point3 point, float minSkyLight, float maxSkyLight)
			{
				float skyLight = m_subsystemSky.SkyLightIntensity;
				if (skyLight < minSkyLight || skyLight > maxSkyLight)
					return 0f;

				int x = point.X, y = point.Y, z = point.Z;
				if (y <= 3 || y >= 253)
					return 0f;

				int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
				int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
				int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);

				Block belowBlock = BlocksManager.Blocks[below];
				Block atBlock = BlocksManager.Blocks[at];
				Block aboveBlock = BlocksManager.Blocks[above];

				bool solidGround = (below == 2 || below == 8 || below == 7 || below == 3);
				if (!solidGround)
					return 0f;

				if (atBlock.IsCollidable_(at) || atBlock is WaterBlock)
					return 0f;

				if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock)
					return 0f;

				return 2.5f;
			}

			// Naomi - Day spawn
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Naomi", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					return GetSurfaceSuitability(point, 0.2f, 1.0f);
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "Naomi", point, 1).Count)
			});

			// Ricardo - Day spawn
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Ricardo", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					return GetSurfaceSuitability(point, 0.2f, 1.0f);
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "Ricardo", point, 1).Count)
			});

			// Brayan - Day spawn
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Brayan", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					return GetSurfaceSuitability(point, 0.2f, 1.0f);
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "Brayan", point, 1).Count)
			});

			// Tulio - Day spawn
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Tulio", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					return GetSurfaceSuitability(point, 0.2f, 1.0f);
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "Tulio", point, 1).Count)
			});

			// LaMuerteX - Night spawn
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("LaMuerteX", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					return GetSurfaceSuitability(point, 0f, 0.1f);
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "LaMuerteX", point, 1).Count)
			});

			// Paco - Día 3 (aparece solo en el día 3)
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Paco", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay3 = currentDay == 3;
						if (isDay3)
						{
							return GetSurfaceSuitability(point, 0.2f, 1.0f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "Paco", point, 1).Count)
			});

			// Barack - Día 4 de noche (aparece solo en el día 4, de noche)
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Barack", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay4 = currentDay == 4;
						if (isDay4)
						{
							return GetSurfaceSuitability(point, 0f, 0.1f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "Barack", point, 1).Count)
			});

			// ElMarihuanero - Día 4 de día (aparece solo en el día 4, de día)
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElMarihuanero", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay4 = currentDay == 4;
						if (isDay4)
						{
							return GetSurfaceSuitability(point, 0.2f, 1.0f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "ElMarihuanero", point, 1).Count)
			});

			// ElMarihuaneroMamon - Día 5 de noche (aparece solo en el día 5, de noche)
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElMarihuaneroMamon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay5 = currentDay == 5;
						if (isDay5)
						{
							return GetSurfaceSuitability(point, 0f, 0.1f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "ElMarihuaneroMamon", point, 1).Count)
			});

			// ElSenorDeLasTumbasMoradas - Día 2 de noche (aparece solo en el día 2, de noche)
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElSenorDeLasTumbasMoradas", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay2 = currentDay == 2;
						if (isDay2)
						{
							return GetSurfaceSuitability(point, 0f, 0.1f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "ElSenorDeLasTumbasMoradas", point, 1).Count)
			});

			// LiderCalavericoSupremo - Día 7 de noche (aparece solo en el día 7, de noche)
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("LiderCalavericoSupremo", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay7 = currentDay == 7;
						if (isDay7)
						{
							return GetSurfaceSuitability(point, 0f, 0.1f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "LiderCalavericoSupremo", point, 1).Count)
			});

			// FumadorQuimico - Día 8 de noche
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("FumadorQuimico", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay8 = currentDay == 8;
						if (isDay8)
						{
							return GetSurfaceSuitability(point, 0f, 0.1f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "FumadorQuimico", point, 1).Count)
			});

			// ClaudeSpeed - Día 14 de día
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ClaudeSpeed", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay14 = currentDay == 14;
						if (isDay14)
						{
							return GetSurfaceSuitability(point, 0.2f, 1.0f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "ClaudeSpeed", point, 1).Count)
			});

			// TommyVercetti - Día 15 de noche
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("TommyVercetti", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay15 = currentDay == 15;
						if (isDay15)
						{
							return GetSurfaceSuitability(point, 0f, 0.1f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "TommyVercetti", point, 1).Count)
			});

			// Conker - Día 10, cualquier hora
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Conker", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay10 = currentDay == 10;
						if (isDay10)
						{
							return GetSurfaceSuitability(point, 0f, 1.0f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "Conker", point, 1).Count)
			});

			// Beavis - Día 9 de día (junto a Butt-Head)
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Beavis", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay9 = currentDay == 9;
						if (isDay9)
						{
							return GetSurfaceSuitability(point, 0.2f, 1.0f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "Beavis", point, 1).Count)
			});

			// Butt-Head - Día 9 de día (junto a Beavis)
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ButtHead", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay9 = currentDay == 9;
						if (isDay9)
						{
							return GetSurfaceSuitability(point, 0.2f, 1.0f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "ButtHead", point, 1).Count)
			});

			// HombreLava - Día 16 de noche, aparece en lava (50%) o terreno normal (5%)
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("HombreLava", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay16 = currentDay == 16;
						if (isDay16)
						{
							// Verificar si está en lava
							int x = point.X, y = point.Y, z = point.Z;
							int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
							bool isLava = (at == 90 || at == 91); // LavaBlock

							// En lava: 50% de idoneidad, en terreno normal: 5%
							if (isLava)
							{
								return GetSurfaceSuitability(point, 0f, 0.1f) * 0.5f;
							}
							else
							{
								return GetSurfaceSuitability(point, 0f, 0.1f) * 0.05f;
							}
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "HombreLava", point, 1).Count)
			});

			// HombreAgua - Día 16 de noche, aparece en agua/costas/playas
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("HombreAgua", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay16 = currentDay == 16;
						if (isDay16)
						{
							int x = point.X, y = point.Y, z = point.Z;
							int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
							int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);

							// Verificar si está en agua o cerca de costa
							bool isWater = (at == 8 || at == 9); // WaterBlock
							bool nearWater = false;

							// Verificar alrededor para costas/playas
							for (int dx = -2; dx <= 2; dx++)
							{
								for (int dz = -2; dz <= 2; dz++)
								{
									int neighbor = m_subsystemTerrain.Terrain.GetCellContentsFast(x + dx, y, z + dz);
									if (neighbor == 8 || neighbor == 9)
									{
										nearWater = true;
										break;
									}
								}
								if (nearWater) break;
							}

							if (isWater || nearWater)
							{
								return GetSurfaceSuitability(point, 0f, 0.1f);
							}
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "HombreAgua", point, 1).Count)
			});

			// ElArquero - Día 5 de noche, solo va
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElArquero", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay5 = currentDay == 5;
						if (isDay5)
						{
							return GetSurfaceSuitability(point, 0f, 0.1f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "ElArquero", point, 1).Count)
			});

			// ArqueroPrisionero - Día 6 de noche, solo va
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ArqueroPrisionero", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay6 = currentDay == 6;
						if (isDay6)
						{
							return GetSurfaceSuitability(point, 0f, 0.1f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "ArqueroPrisionero", point, 1).Count)
			});

			// BallestadoraMusculosa - Día 11 de día, sola va
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("BallestadoraMusculosa", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay11 = currentDay == 11;
						if (isDay11)
						{
							return GetSurfaceSuitability(point, 0.2f, 1.0f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "BallestadoraMusculosa", point, 1).Count)
			});

			// ElBallestador - Día 10 de noche, solo va
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElBallestador", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay10 = currentDay == 10;
						if (isDay10)
						{
							return GetSurfaceSuitability(point, 0f, 0.1f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "ElBallestador", point, 1).Count)
			});

			// HeadcrabBloodyHuman - Día 20 de noche, solo va
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("HeadcrabBloodyHuman", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay20 = currentDay == 20;
						if (isDay20)
						{
							return GetSurfaceSuitability(point, 0f, 0.1f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "HeadcrabBloodyHuman", point, 1).Count)
			});

			// AladinaCorrupta - Día 25, cualquier hora, solo va
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("AladinaCorrupta", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay25 = currentDay == 25;
						if (isDay25)
						{
							return GetSurfaceSuitability(point, 0f, 1.0f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "AladinaCorrupta", point, 1).Count)
			});

			// WalterZombie - Día 17 de noche, solo va
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("WalterZombie", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (timeOfDay != null)
					{
						double currentDay = Math.Floor(timeOfDay.Day) + 1;
						bool isDay17 = currentDay == 17;
						if (isDay17)
						{
							return GetSurfaceSuitability(point, 0f, 0.1f);
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "WalterZombie", point, 1).Count)
			});
		}
	}
}
