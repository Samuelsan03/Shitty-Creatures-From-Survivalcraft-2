using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemShittyCreaturesSpawn : Subsystem, IUpdateable
	{
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemSpawn m_subsystemSpawn;
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemTime m_subsystemTime;
		public SubsystemTimeOfDay m_subsystemTimeOfDay;
		public SubsystemSky m_subsystemSky;
		public SubsystemSeasons m_subsystemSeasons;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemGameWidgets m_subsystemViews;
		public Random m_random = new Random();

		public List<SpawnChunk> m_newSpawnChunks = new List<SpawnChunk>();
		public List<SpawnChunk> m_spawnChunks = new List<SpawnChunk>();

		public static int m_totalLimit = 26;
		public static int m_areaLimit = 3;
		public static int m_areaRadius = 16;
		public static int m_totalLimitConstant = 6;
		public static int m_totalLimitConstantChallenging = 12;
		public static int m_areaLimitConstant = 4;
		public static int m_areaRadiusConstant = 42;

		public List<CreatureType> m_creatureTypes = new List<CreatureType>();

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public class CreatureType
		{
			public string Name;
			public SpawnLocationType SpawnLocationType;
			public bool RandomSpawn;
			public bool ConstantSpawn;
			public Func<CreatureType, Point3, float> SpawnSuitabilityFunction;
			public Func<CreatureType, Point3, int> SpawnFunction;

			public CreatureType() { }

			public CreatureType(string name, SpawnLocationType spawnLocationType, bool randomSpawn, bool constantSpawn)
			{
				Name = name;
				SpawnLocationType = spawnLocationType;
				RandomSpawn = randomSpawn;
				ConstantSpawn = constantSpawn;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemSpawn = Project.FindSubsystem<SubsystemSpawn>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemSeasons = Project.FindSubsystem<SubsystemSeasons>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemViews = Project.FindSubsystem<SubsystemGameWidgets>(true);

			m_subsystemSpawn.SpawningChunk += delegate (SpawnChunk chunk)
			{
				m_spawnChunks.Add(chunk);
				if (!chunk.IsSpawned)
				{
					m_newSpawnChunks.Add(chunk);
				}
			};

			InitializeCreatureTypes();
		}

		public virtual void InitializeCreatureTypes()
		{
			// Naomi (cualquier hora, sin día límite)
			m_creatureTypes.Add(new CreatureType("Naomi", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					// Sin restricción de día
					float time = m_subsystemTimeOfDay.TimeOfDay;
					// Aparece en cualquier hora del día
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Naomi", point, 1).Count)
			});

			// Naomi Constant
			m_creatureTypes.Add(new CreatureType("Naomi Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					// Sin restricción de día
					float time = m_subsystemTimeOfDay.TimeOfDay;
					// Aparece en cualquier hora del día
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Naomi", point, 1).Count)
			});

			// Ricardo (cualquier hora, sin día límite)
			m_creatureTypes.Add(new CreatureType("Ricardo", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					// Sin restricción de día
					float time = m_subsystemTimeOfDay.TimeOfDay;
					// Aparece en cualquier hora del día
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Ricardo", point, 1).Count)
			});

			// Ricardo Constant
			m_creatureTypes.Add(new CreatureType("Ricardo Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					// Sin restricción de día
					float time = m_subsystemTimeOfDay.TimeOfDay;
					// Aparece en cualquier hora del día
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Ricardo", point, 1).Count)
			});

			// Brayan (cualquier hora, sin día límite)
			m_creatureTypes.Add(new CreatureType("Brayan", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					// Sin restricción de día
					float time = m_subsystemTimeOfDay.TimeOfDay;
					// Aparece en cualquier hora del día
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Brayan", point, 1).Count)
			});

			// Brayan Constant
			m_creatureTypes.Add(new CreatureType("Brayan Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					// Sin restricción de día
					float time = m_subsystemTimeOfDay.TimeOfDay;
					// Aparece en cualquier hora del día
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Brayan", point, 1).Count)
			});

			// Tulio (cualquier hora, sin día límite)
			m_creatureTypes.Add(new CreatureType("Tulio", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					// Sin restricción de día
					float time = m_subsystemTimeOfDay.TimeOfDay;
					// Aparece en cualquier hora del día
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Tulio", point, 1).Count)
			});

			// Tulio Constant
			m_creatureTypes.Add(new CreatureType("Tulio Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					// Sin restricción de día
					float time = m_subsystemTimeOfDay.TimeOfDay;
					// Aparece en cualquier hora del día
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Tulio", point, 1).Count)
			});

			// LaMuerteX (noche, sin día límite)
			m_creatureTypes.Add(new CreatureType("LaMuerteX", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					// Sin restricción de día
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 1f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "LaMuerteX", point, 1).Count)
			});

			// LaMuerteX Constant
			m_creatureTypes.Add(new CreatureType("LaMuerteX Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					// Sin restricción de día
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 2f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "LaMuerteX", point, 1).Count)
			});

			// Barack (día 3, cualquier hora)
			m_creatureTypes.Add(new CreatureType("Barack", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 3) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Barack", point, 1).Count)
			});

			// Barack Constant
			m_creatureTypes.Add(new CreatureType("Barack Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 3) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Barack", point, 1).Count)
			});

			// Paco (día 3, cualquier hora)
			m_creatureTypes.Add(new CreatureType("Paco", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 3) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Paco", point, 1).Count)
			});

			// Paco Constant
			m_creatureTypes.Add(new CreatureType("Paco Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 3) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Paco", point, 1).Count)
			});

			// ElMarihuanero (día 4, cualquier hora)
			m_creatureTypes.Add(new CreatureType("ElMarihuanero", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 4) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ElMarihuanero", point, 1).Count)
			});

			// ElMarihuanero Constant
			m_creatureTypes.Add(new CreatureType("ElMarihuanero Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 4) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ElMarihuanero", point, 1).Count)
			});

			// ElMarihuaneroMamon (día 5, cualquier hora)
			m_creatureTypes.Add(new CreatureType("ElMarihuaneroMamon", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 5) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ElMarihuaneroMamon", point, 1).Count)
			});

			// ElMarihuaneroMamon Constant
			m_creatureTypes.Add(new CreatureType("ElMarihuaneroMamon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 5) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ElMarihuaneroMamon", point, 1).Count)
			});

			// LiderCalavericoSupremo (día 9, cualquier hora)
			m_creatureTypes.Add(new CreatureType("LiderCalavericoSupremo", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 9) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "LiderCalavericoSupremo", point, 1).Count)
			});

			// LiderCalavericoSupremo Constant
			m_creatureTypes.Add(new CreatureType("LiderCalavericoSupremo Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 9) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "LiderCalavericoSupremo", point, 1).Count)
			});

			// Sparkster (normal)
			m_creatureTypes.Add(new CreatureType("Sparkster", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 2) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time < m_subsystemTimeOfDay.DawnStart || time >= m_subsystemTimeOfDay.DuskStart) return 0f;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Sparkster", point, 1).Count)
			});

			// Constant Sparkster
			m_creatureTypes.Add(new CreatureType("Sparkster Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 2) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time < m_subsystemTimeOfDay.DawnStart || time >= m_subsystemTimeOfDay.DuskStart) return 0f;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Sparkster", point, 1).Count)
			});

			// ElSenorDeLasTumbasMoradas (normal)
			m_creatureTypes.Add(new CreatureType("ElSenorDeLasTumbasMoradas", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 2) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 1f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ElSenorDeLasTumbasMoradas", point, 1).Count)
			});

			// Constant ElSenorDeLasTumbasMoradas
			m_creatureTypes.Add(new CreatureType("ElSenorDeLasTumbasMoradas Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 2) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 2f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ElSenorDeLasTumbasMoradas", point, 1).Count)
			});

			// ElArquero (día 5, noche)
			m_creatureTypes.Add(new CreatureType("ElArquero", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 5) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 1f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ElArquero", point, 1).Count)
			});

			// ElArquero Constant
			m_creatureTypes.Add(new CreatureType("ElArquero Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 5) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 2f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ElArquero", point, 1).Count)
			});

			// ArqueroPrisionero (día 6, noche)
			m_creatureTypes.Add(new CreatureType("ArqueroPrisionero", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 6) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 1f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ArqueroPrisionero", point, 1).Count)
			});

			// ArqueroPrisionero Constant
			m_creatureTypes.Add(new CreatureType("ArqueroPrisionero Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 6) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 2f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ArqueroPrisionero", point, 1).Count)
			});

			// ElBallestador (día 10, noche)
			m_creatureTypes.Add(new CreatureType("ElBallestador", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 10) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 1f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ElBallestador", point, 1).Count)
			});

			// ElBallestador Constant
			m_creatureTypes.Add(new CreatureType("ElBallestador Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 10) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 2f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ElBallestador", point, 1).Count)
			});

			// BallestadoraMusculosa (día 11, noche)
			m_creatureTypes.Add(new CreatureType("BallestadoraMusculosa", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 11) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 1f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "BallestadoraMusculosa", point, 1).Count)
			});

			// BallestadoraMusculosa Constant
			m_creatureTypes.Add(new CreatureType("BallestadoraMusculosa Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 11) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 2f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "BallestadoraMusculosa", point, 1).Count)
			});

			// ClaudeSpeed (día 13, día)
			m_creatureTypes.Add(new CreatureType("ClaudeSpeed", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 13) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.DawnStart && time < m_subsystemTimeOfDay.DuskStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 1f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ClaudeSpeed", point, 1).Count)
			});

			// ClaudeSpeed Constant
			m_creatureTypes.Add(new CreatureType("ClaudeSpeed Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 13) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.DawnStart && time < m_subsystemTimeOfDay.DuskStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 2f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ClaudeSpeed", point, 1).Count)
			});

			// TommyVercetti (día 15, día)
			m_creatureTypes.Add(new CreatureType("TommyVercetti", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 15) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.DawnStart && time < m_subsystemTimeOfDay.DuskStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 1f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "TommyVercetti", point, 1).Count)
			});

			// TommyVercetti Constant
			m_creatureTypes.Add(new CreatureType("TommyVercetti Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 15) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.DawnStart && time < m_subsystemTimeOfDay.DuskStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 2f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "TommyVercetti", point, 1).Count)
			});

			// Beavis (día 11, cualquier hora)
			m_creatureTypes.Add(new CreatureType("Beavis", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 11) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Beavis", point, 1).Count)
			});

			// Beavis Constant
			m_creatureTypes.Add(new CreatureType("Beavis Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 11) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Beavis", point, 1).Count)
			});

			// Butt-Head (día 11, cualquier hora)
			m_creatureTypes.Add(new CreatureType("Butt-Head", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 11) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Butt-Head", point, 1).Count)
			});

			// Butt-Head Constant
			m_creatureTypes.Add(new CreatureType("Butt-Head Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 11) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Butt-Head", point, 1).Count)
			});

			// HombreAgua (agua con alta probabilidad, terreno normal baja probabilidad, día 11)
			m_creatureTypes.Add(new CreatureType("HombreAgua", SpawnLocationType.Water, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 11) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Verificar si está en agua
					bool inWater = (ground == 18);

					if (inWater)
					{
						// En agua: 80% de probabilidad
						if (m_random.Float() > 0.2f) return 0f;
						return 1f;
					}
					else
					{
						// En terreno normal: 10% de probabilidad
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						if (m_random.Float() > 0.1f) return 0f;
						return 0.2f;
					}
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "HombreAgua", point, 1).Count)
			});

			// HombreAgua Constant
			m_creatureTypes.Add(new CreatureType("HombreAgua Constant", SpawnLocationType.Water, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 11) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Verificar si está en agua
					bool inWater = (ground == 18);

					if (inWater)
					{
						// En agua: 80% de probabilidad
						if (m_random.Float() > 0.2f) return 0f;
						return 2f;
					}
					else
					{
						// En terreno normal: 10% de probabilidad
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						if (m_random.Float() > 0.1f) return 0f;
						return 0.4f;
					}
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "HombreAgua", point, 1).Count)
			});

			// HombreLava (día 14, magma con alta probabilidad, terreno normal baja probabilidad)
			// Nota: Asumiendo que el bloque de magma es el ID 10 (MagmaBlock)
			m_creatureTypes.Add(new CreatureType("HombreLava", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 14) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Verificar si está en magma (ID 92 para MagmaBlock)
					bool inMagma = (ground == 92);

					if (inMagma)
					{
						// En magma: 80% de probabilidad
						if (m_random.Float() > 0.2f) return 0f;
						return 1f;
					}
					else
					{
						// En terreno normal: 10% de probabilidad
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						if (m_random.Float() > 0.1f) return 0f;
						return 0.2f;
					}
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "HombreLava", point, 1).Count)
			});

			// HombreLava Constant
			m_creatureTypes.Add(new CreatureType("HombreLava Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 14) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Verificar si está en magma (ID 92 para MagmaBlock)
					bool inMagma = (ground == 92);

					if (inMagma)
					{
						// En magma: 80% de probabilidad
						if (m_random.Float() > 0.2f) return 0f;
						return 2f;
					}
					else
					{
						// En terreno normal: 10% de probabilidad
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						if (m_random.Float() > 0.1f) return 0f;
						return 0.4f;
					}
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "HombreLava", point, 1).Count)
			});

			// ElGuerrillero (día 26, noche)
			m_creatureTypes.Add(new CreatureType("ElGuerrillero", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 26) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 1f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ElGuerrillero", point, 1).Count)
			});

			// ElGuerrillero Constant
			m_creatureTypes.Add(new CreatureType("ElGuerrillero Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 26) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 2f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ElGuerrillero", point, 1).Count)
			});

			// ElGuerrilleroTenebroso (día 27, noche)
			m_creatureTypes.Add(new CreatureType("ElGuerrilleroTenebroso", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 27) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 1f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ElGuerrilleroTenebroso", point, 1).Count)
			});

			// ElGuerrilleroTenebroso Constant
			m_creatureTypes.Add(new CreatureType("ElGuerrilleroTenebroso Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 27) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 2f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ElGuerrilleroTenebroso", point, 1).Count)
			});

			// AladinaCorrupta (día 35, cualquier hora)
			m_creatureTypes.Add(new CreatureType("AladinaCorrupta", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 35) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "AladinaCorrupta", point, 1).Count)
			});

			// AladinaCorrupta Constant
			m_creatureTypes.Add(new CreatureType("AladinaCorrupta Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 35) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "AladinaCorrupta", point, 1).Count)
			});

			// Richard (día 38, cualquier hora)
			m_creatureTypes.Add(new CreatureType("Richard", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 38) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Richard", point, 1).Count)
			});

			// Richard Constant
			m_creatureTypes.Add(new CreatureType("Richard Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 38) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Richard", point, 1).Count)
			});

			// ZombieRepetidor (día 37, noche)
			m_creatureTypes.Add(new CreatureType("ZombieRepetidor", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 37) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 1f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ZombieRepetidor", point, 1).Count)
			});

			// ZombieRepetidor Constant
			m_creatureTypes.Add(new CreatureType("ZombieRepetidor Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 37) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 2f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "ZombieRepetidor", point, 1).Count)
			});

			// WalterZombie (día 17, noche)
			m_creatureTypes.Add(new CreatureType("WalterZombie", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 17) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 1f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "WalterZombie", point, 1).Count)
			});

			// WalterZombie Constant
			m_creatureTypes.Add(new CreatureType("WalterZombie Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 17) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 2f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "WalterZombie", point, 1).Count)
			});

			// Conker (día 12, día)
			m_creatureTypes.Add(new CreatureType("Conker", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 12) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.DawnStart && time < m_subsystemTimeOfDay.DuskStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 1f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Conker", point, 1).Count)
			});

			// Conker Constant
			m_creatureTypes.Add(new CreatureType("Conker Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 12) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.DawnStart && time < m_subsystemTimeOfDay.DuskStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 2f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "Conker", point, 1).Count)
			});

			// CarlJohson (día 23, noche)
			m_creatureTypes.Add(new CreatureType("CarlJohnson", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 23) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 1f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "CarlJohnson", point, 1).Count)
			});

			// CarlJohson Constant
			m_creatureTypes.Add(new CreatureType("CarlJohnson Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 23) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time >= m_subsystemTimeOfDay.NightStart || time < m_subsystemTimeOfDay.DawnStart)
					{
						int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
						// Probabilidad del 50% de aparecer
						if (m_random.Float() > 0.5f) return 0f;
						return 2f;
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "CarlJohnson", point, 1).Count)
			});

			// PirataNormal (día 6, día, solo en Verano/Primavera, cerca de costa)
			m_creatureTypes.Add(new CreatureType("PirataNormal", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					// Día mínimo 6
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 6) return 0f;

					// Solo en Verano o Primavera
					if (m_subsystemSeasons.Season != Season.Summer && m_subsystemSeasons.Season != Season.Spring) return 0f;

					// Solo de día
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time < m_subsystemTimeOfDay.DawnStart || time >= m_subsystemTimeOfDay.DuskStart) return 0f;

					// Verificar que esté cerca de costa (agua cercana)
					bool nearCoast = IsNearCoast(point);
					if (!nearCoast) return 0f;

					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;

					// Probabilidad del 50% de aparecer
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "PirataNormal", point, 1).Count)
			});

			// PirataNormal Constant
			m_creatureTypes.Add(new CreatureType("PirataNormal Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 6) return 0f;
					if (m_subsystemSeasons.Season != Season.Summer && m_subsystemSeasons.Season != Season.Spring) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time < m_subsystemTimeOfDay.DawnStart || time >= m_subsystemTimeOfDay.DuskStart) return 0f;

					bool nearCoast = IsNearCoast(point);
					if (!nearCoast) return 0f;

					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "PirataNormal", point, 1).Count)
			});

			// PirataElite (día 8, día, solo en Verano/Primavera, cerca de costa)
			m_creatureTypes.Add(new CreatureType("PirataElite", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 8) return 0f;
					if (m_subsystemSeasons.Season != Season.Summer && m_subsystemSeasons.Season != Season.Spring) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time < m_subsystemTimeOfDay.DawnStart || time >= m_subsystemTimeOfDay.DuskStart) return 0f;

					bool nearCoast = IsNearCoast(point);
					if (!nearCoast) return 0f;

					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "PirataElite", point, 1).Count)
			});

			// PirataElite Constant
			m_creatureTypes.Add(new CreatureType("PirataElite Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 8) return 0f;
					if (m_subsystemSeasons.Season != Season.Summer && m_subsystemSeasons.Season != Season.Spring) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time < m_subsystemTimeOfDay.DawnStart || time >= m_subsystemTimeOfDay.DuskStart) return 0f;

					bool nearCoast = IsNearCoast(point);
					if (!nearCoast) return 0f;

					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "PirataElite", point, 1).Count)
			});

			// PirataHostilComerciante (día 17, día, solo en Verano/Primavera, cerca de costa)
			m_creatureTypes.Add(new CreatureType("PirataHostilComerciante", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 17) return 0f;
					if (m_subsystemSeasons.Season != Season.Summer && m_subsystemSeasons.Season != Season.Spring) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time < m_subsystemTimeOfDay.DawnStart || time >= m_subsystemTimeOfDay.DuskStart) return 0f;

					bool nearCoast = IsNearCoast(point);
					if (!nearCoast) return 0f;

					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "PirataHostilComerciante", point, 1).Count)
			});

			// PirataHostilComerciante Constant
			m_creatureTypes.Add(new CreatureType("PirataHostilComerciante Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 17) return 0f;
					if (m_subsystemSeasons.Season != Season.Summer && m_subsystemSeasons.Season != Season.Spring) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time < m_subsystemTimeOfDay.DawnStart || time >= m_subsystemTimeOfDay.DuskStart) return 0f;

					bool nearCoast = IsNearCoast(point);
					if (!nearCoast) return 0f;

					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "PirataHostilComerciante", point, 1).Count)
			});

			// CapitanPirata (día 30, día, solo en Verano/Primavera, cerca de costa)
			m_creatureTypes.Add(new CreatureType("CapitanPirata", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 30) return 0f;
					if (m_subsystemSeasons.Season != Season.Summer && m_subsystemSeasons.Season != Season.Spring) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time < m_subsystemTimeOfDay.DawnStart || time >= m_subsystemTimeOfDay.DuskStart) return 0f;

					bool nearCoast = IsNearCoast(point);
					if (!nearCoast) return 0f;

					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					if (m_random.Float() > 0.5f) return 0f;
					return 1f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "CapitanPirata", point, 1).Count)
			});

			// CapitanPirata Constant
			m_creatureTypes.Add(new CreatureType("CapitanPirata Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (Math.Floor(m_subsystemTimeOfDay.Day) < 30) return 0f;
					if (m_subsystemSeasons.Season != Season.Summer && m_subsystemSeasons.Season != Season.Spring) return 0f;
					float time = m_subsystemTimeOfDay.TimeOfDay;
					if (time < m_subsystemTimeOfDay.DawnStart || time >= m_subsystemTimeOfDay.DuskStart) return 0f;

					bool nearCoast = IsNearCoast(point);
					if (!nearCoast) return 0f;

					int ground = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (ground != 2 && ground != 8 && ground != 7 && ground != 3) return 0f;
					if (m_random.Float() > 0.5f) return 0f;
					return 2f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "CapitanPirata", point, 1).Count)
			});
		}

		public virtual void Update(float dt)
		{
			if (m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
			{
				if (m_newSpawnChunks.Count > 0)
				{
					m_newSpawnChunks.RandomShuffle((int max) => m_random.Int(0, max - 1));
					foreach (SpawnChunk chunk in m_newSpawnChunks)
					{
						SpawnChunkCreatures(chunk, 10, false);
					}
					m_newSpawnChunks.Clear();
				}
				if (m_spawnChunks.Count > 0)
				{
					m_spawnChunks.RandomShuffle((int max) => m_random.Int(0, max - 1));
					foreach (SpawnChunk chunk in m_spawnChunks)
					{
						SpawnChunkCreatures(chunk, 2, true);
					}
					m_spawnChunks.Clear();
				}
				float num = (m_subsystemSeasons.Season == Season.Winter) ? 120f : 60f;
				if (m_subsystemTime.PeriodicGameTimeEvent((double)num, 2.0))
				{
					SpawnRandomCreature();
				}
			}
		}

		public virtual void SpawnRandomCreature()
		{
			if (CountCreatures(false) < m_totalLimit)
			{
				foreach (GameWidget gameWidget in m_subsystemViews.GameWidgets)
				{
					Vector2 v = new Vector2(gameWidget.ActiveCamera.ViewPosition.X, gameWidget.ActiveCamera.ViewPosition.Z);
					if (CountCreaturesInArea(v - new Vector2(68f), v + new Vector2(68f), false) >= 52)
					{
						break;
					}
					SpawnLocationType spawnLocationType = GetRandomSpawnLocationType();
					Point3? spawnPoint = GetRandomSpawnPoint(gameWidget.ActiveCamera, spawnLocationType);
					if (spawnPoint != null)
					{
						Vector2 c3 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) - new Vector2(16f);
						Vector2 c2 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) + new Vector2(16f);
						if (CountCreaturesInArea(c3, c2, false) >= 3)
						{
							break;
						}
						IEnumerable<CreatureType> enumerable = from c in m_creatureTypes
															   where c.SpawnLocationType == spawnLocationType && c.RandomSpawn
															   select c;
						IEnumerable<CreatureType> source = (enumerable as CreatureType[]) ?? enumerable.ToArray();
						IEnumerable<float> items = from c in source
												   select CalculateSpawnSuitability(c, spawnPoint.Value);
						int randomWeightedItem = GetRandomWeightedItem(items);
						if (randomWeightedItem >= 0)
						{
							CreatureType creatureType = source.ElementAt(randomWeightedItem);
							creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						}
					}
				}
			}
		}

		// Método auxiliar para verificar si un punto está cerca de la costa (agua)
		public virtual bool IsNearCoast(Point3 point)
		{
			int radius = 8; // Radio de búsqueda de agua cercana

			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dz = -radius; dz <= radius; dz++)
				{
					// Verificar bloque de agua en la superficie o cerca
					int x = point.X + dx;
					int z = point.Z + dz;
					int y = point.Y;

					// Buscar agua en la superficie
					for (int dy = -5; dy <= 5; dy++)
					{
						int checkY = Math.Clamp(y + dy, 1, 254);
						int cellContents = m_subsystemTerrain.Terrain.GetCellContentsFast(x, checkY, z);

						// Si encuentra agua (ID 18 para WaterBlock)
						if (cellContents == 18)
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		public virtual void SpawnChunkCreatures(SpawnChunk chunk, int maxAttempts, bool constantSpawn)
		{
			int num = constantSpawn ? ((m_subsystemGameInfo.WorldSettings.GameMode >= GameMode.Challenging) ? m_totalLimitConstantChallenging : m_totalLimitConstant) : m_totalLimit;
			int num2 = constantSpawn ? m_areaLimitConstant : m_areaLimit;
			float v = (float)(constantSpawn ? m_areaRadiusConstant : m_areaRadius);
			int num3 = CountCreatures(constantSpawn);
			Vector2 c3 = new Vector2((float)(chunk.Point.X * 16), (float)(chunk.Point.Y * 16)) - new Vector2(v);
			Vector2 c2 = new Vector2((float)((chunk.Point.X + 1) * 16), (float)((chunk.Point.Y + 1) * 16)) + new Vector2(v);
			int num4 = CountCreaturesInArea(c3, c2, constantSpawn);
			for (int i = 0; i < maxAttempts; i++)
			{
				if (num3 >= num || num4 >= num2)
				{
					break;
				}
				SpawnLocationType spawnLocationType = GetRandomSpawnLocationType();
				Point3? spawnPoint = GetRandomChunkSpawnPoint(chunk, spawnLocationType);
				if (spawnPoint != null)
				{
					IEnumerable<CreatureType> enumerable = from c in m_creatureTypes
														   where c.SpawnLocationType == spawnLocationType && c.ConstantSpawn == constantSpawn
														   select c;
					IEnumerable<CreatureType> source = (enumerable as CreatureType[]) ?? enumerable.ToArray();
					IEnumerable<float> items = from c in source
											   select CalculateSpawnSuitability(c, spawnPoint.Value);
					int randomWeightedItem = GetRandomWeightedItem(items);
					if (randomWeightedItem >= 0)
					{
						CreatureType creatureType = source.ElementAt(randomWeightedItem);
						int num5 = creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						num3 += num5;
						num4 += num5;
					}
				}
			}
		}

		public virtual List<Entity> SpawnCreatures(CreatureType creatureType, string templateName, Point3 point, int count)
		{
			List<Entity> list = new List<Entity>();
			int num = 0;
			while (count > 0 && num < 50)
			{
				Point3 spawnPoint = point;
				if (num > 0)
				{
					spawnPoint.X += m_random.Int(-8, 8);
					spawnPoint.Y += m_random.Int(-4, 8);
					spawnPoint.Z += m_random.Int(-8, 8);
				}
				Point3? point2 = ProcessSpawnPoint(spawnPoint, creatureType.SpawnLocationType);
				if (point2 != null && CalculateSpawnSuitability(creatureType, point2.Value) > 0f)
				{
					Vector3 position = new Vector3((float)point2.Value.X + m_random.Float(0.4f, 0.6f), (float)point2.Value.Y + 1.1f, (float)point2.Value.Z + m_random.Float(0.4f, 0.6f));
					Entity entity = SpawnCreature(templateName, position, creatureType.ConstantSpawn);
					if (entity != null)
					{
						list.Add(entity);
						count--;
					}
				}
				num++;
			}
			return list;
		}

		public virtual Entity SpawnCreature(string templateName, Vector3 position, bool constantSpawn)
		{
			try
			{
				Entity entity = DatabaseManager.CreateEntity(Project, templateName, true);
				entity.FindComponent<ComponentBody>(true).Position = position;
				entity.FindComponent<ComponentBody>(true).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, m_random.Float(0f, 6.2831855f));
				entity.FindComponent<ComponentCreature>(true).ConstantSpawn = constantSpawn;
				Project.AddEntity(entity);
				return entity;
			}
			catch (Exception ex)
			{
				Log.Error("Unable to spawn creature with template \"" + templateName + "\". Reason: " + ex.Message);
				return null;
			}
		}

		public virtual Point3? GetRandomChunkSpawnPoint(SpawnChunk chunk, SpawnLocationType spawnLocationType)
		{
			for (int i = 0; i < 5; i++)
			{
				int x = 16 * chunk.Point.X + m_random.Int(0, 15);
				int y = m_random.Int(10, 246);
				int z = 16 * chunk.Point.Y + m_random.Int(0, 15);
				Point3? result = ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
				if (result != null)
				{
					return result;
				}
			}
			return null;
		}

		public virtual Point3? GetRandomSpawnPoint(Camera camera, SpawnLocationType spawnLocationType)
		{
			for (int i = 0; i < 10; i++)
			{
				int x = Terrain.ToCell(camera.ViewPosition.X) + m_random.Sign() * m_random.Int(24, 48);
				int y = Math.Clamp(Terrain.ToCell(camera.ViewPosition.Y) + m_random.Int(-30, 30), 2, 254);
				int z = Terrain.ToCell(camera.ViewPosition.Z) + m_random.Sign() * m_random.Int(24, 48);
				Point3? result = ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
				if (result != null)
				{
					return result;
				}
			}
			return null;
		}

		public virtual Point3? ProcessSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
		{
			int x = spawnPoint.X;
			int num = Math.Clamp(spawnPoint.Y, 1, 254);
			int z = spawnPoint.Z;
			TerrainChunk chunkAtCell = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunkAtCell != null && chunkAtCell.State > TerrainChunkState.InvalidPropagatedLight)
			{
				for (int i = 0; i < 30; i++)
				{
					Point3 point = new Point3(x, num + i, z);
					if (TestSpawnPoint(point, spawnLocationType))
					{
						return point;
					}
					Point3 point2 = new Point3(x, num - i, z);
					if (TestSpawnPoint(point2, spawnLocationType))
					{
						return point2;
					}
				}
			}
			return null;
		}

		public virtual bool TestSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
		{
			int x = spawnPoint.X;
			int y = spawnPoint.Y;
			int z = spawnPoint.Z;
			if (y <= 3 || y >= 253)
			{
				return false;
			}
			switch (spawnLocationType)
			{
				case SpawnLocationType.Surface:
					{
						int cellLightFast = m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
						if (m_subsystemSky.SkyLightValue - cellLightFast > 3)
						{
							return false;
						}
						int cellValueFast = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
						int cellValueFast2 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
						int cellValueFast3 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);
						Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast)];
						Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast2)];
						Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast3)];
						return (block.IsCollidable_(cellValueFast) || block is WaterBlock) && !block2.IsCollidable_(cellValueFast2) && !(block2 is WaterBlock) && !block3.IsCollidable_(cellValueFast3) && !(block3 is WaterBlock);
					}
				case SpawnLocationType.Cave:
					{
						int cellLightFast2 = m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
						if (m_subsystemSky.SkyLightValue - cellLightFast2 < 5)
						{
							return false;
						}
						int cellValueFast4 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
						int cellValueFast5 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
						int cellValueFast6 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);
						Block block4 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast4)];
						Block block5 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast5)];
						Block block6 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast6)];
						return (block4.IsCollidable_(cellValueFast4) || block4 is WaterBlock) && !block5.IsCollidable_(cellValueFast5) && !(block5 is WaterBlock) && !block6.IsCollidable_(cellValueFast6) && !(block6 is WaterBlock);
					}
				case SpawnLocationType.Water:
					{
						int cellContentsFast = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
						int cellValueFast7 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);
						int cellValueFast8 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 2, z);
						Block block7 = BlocksManager.Blocks[Terrain.ExtractContents(cellContentsFast)];
						Block block8 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast7)];
						Block block9 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast8)];
						return block7 is WaterBlock && !block8.IsCollidable_(cellValueFast7) && !block9.IsCollidable_(cellValueFast8);
					}
				default:
					throw new InvalidOperationException("Unknown spawn location type.");
			}
		}

		public virtual float CalculateSpawnSuitability(CreatureType creatureType, Point3 spawnPoint)
		{
			float num = creatureType.SpawnSuitabilityFunction(creatureType, spawnPoint);
			if (CountCreatures(creatureType) > 8)
			{
				num *= 0.25f;
			}
			return num;
		}

		public virtual int CountCreatures(CreatureType creatureType)
		{
			int num = 0;
			foreach (ComponentBody componentBody in m_subsystemBodies.Bodies)
			{
				if (componentBody.Entity.ValuesDictionary.DatabaseObject.Name == creatureType.Name)
				{
					num++;
				}
			}
			return num;
		}

		public virtual int CountCreatures(bool constantSpawn)
		{
			int num = 0;
			foreach (ComponentBody componentBody in m_subsystemBodies.Bodies)
			{
				ComponentCreature componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
				if (componentCreature != null && componentCreature.ConstantSpawn == constantSpawn)
				{
					num++;
				}
			}
			return num;
		}

		public virtual int CountCreaturesInArea(Vector2 c1, Vector2 c2, bool constantSpawn)
		{
			int num = 0;
			foreach (ComponentBody componentBody in m_subsystemBodies.Bodies)
			{
				ComponentCreature componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
				if (componentCreature != null && componentCreature.ConstantSpawn == constantSpawn)
				{
					Vector3 position = componentBody.Position;
					if (position.X >= c1.X && position.X <= c2.X && position.Z >= c1.Y && position.Z <= c2.Y)
					{
						num++;
					}
				}
			}
			Point2 point = Terrain.ToChunk(c1);
			Point2 point2 = Terrain.ToChunk(c2);
			for (int j = point.X; j <= point2.X; j++)
			{
				for (int k = point.Y; k <= point2.Y; k++)
				{
					SpawnChunk spawnChunk = m_subsystemSpawn.GetSpawnChunk(new Point2(j, k));
					if (spawnChunk != null)
					{
						foreach (SpawnEntityData spawnEntityData in spawnChunk.SpawnsData)
						{
							if (spawnEntityData.ConstantSpawn == constantSpawn)
							{
								Vector3 position2 = spawnEntityData.Position;
								if (position2.X >= c1.X && position2.X <= c2.X && position2.Z >= c1.Y && position2.Z <= c2.Y)
								{
									num++;
								}
							}
						}
					}
				}
			}
			return num;
		}

		public virtual int GetRandomWeightedItem(IEnumerable<float> items)
		{
			float[] array = items.ToArray();
			float max = MathUtils.Max(array.Sum(), 1f);
			float num = m_random.Float(0f, max);
			int num2 = 0;
			foreach (float num3 in array)
			{
				if (num < num3)
				{
					return num2;
				}
				num -= num3;
				num2++;
			}
			return -1;
		}

		public virtual SpawnLocationType GetRandomSpawnLocationType()
		{
			float num = m_random.Float();
			if (num <= 0.3f)
			{
				return SpawnLocationType.Surface;
			}
			if (num <= 0.6f)
			{
				return SpawnLocationType.Cave;
			}
			return SpawnLocationType.Water;
		}
	}
}
