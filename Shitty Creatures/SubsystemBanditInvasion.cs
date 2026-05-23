using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    public class SubsystemBanditInvasion : Subsystem, IUpdateable
    {
        private SubsystemGameInfo m_subsystemGameInfo;
        private SubsystemTimeOfDay m_subsystemTimeOfDay;
        private SubsystemSky m_subsystemSky;
        private SubsystemTerrain m_subsystemTerrain;
        private SubsystemBodies m_subsystemBodies;
        private SubsystemPlayers m_subsystemPlayers;
        private Random m_random = new Random();

        private List<BanditSpawnData> m_bandits = new List<BanditSpawnData>();
        private float m_totalProbabilitySum;

        private bool m_acceptedWar;
        private bool m_invasionActive;
        private bool m_invasionStarted;
        private bool m_invasionCompleted;

        private float m_spawnTimer;
        private float m_spawnInterval = 3.0f;
        private const int MaxBanditsPerArea = 8;
        private const int MaxGlobalBandits = 35;
        private const int MaxSpawnsPerFrame = 2;

        private bool m_wasNight;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;
        public bool IsInvasionActive => m_invasionActive;

		public void AcceptWar()
		{
			// Si la guerra ya está completada (amanecer), reiniciamos el estado para permitir una nueva guerra
			if (m_invasionCompleted)
			{
				m_invasionCompleted = false;
				m_invasionActive = false;
				m_invasionStarted = false;
				m_acceptedWar = true;
				m_spawnTimer = 0f;
				return;
			}

			// Si no está completada y aún no se había aceptado, simplemente activamos la aceptación
			if (!m_acceptedWar)
			{
				m_acceptedWar = true;
			}
		}

		private void SetAllBanditsDrugTraffickerMode(bool enabled)
		{
			foreach (var body in m_subsystemBodies.Bodies)
			{
				var banditChase = body.Entity.FindComponent<ComponentBanditChaseBehavior>();
				if (banditChase != null)
				{
					banditChase.IsDrugTraffickerMode = enabled;
					// Al desactivar, forzar a que dejen de atacar inmediatamente
					if (!enabled)
					{
						banditChase.StopAttack();
					}
					// También al activar, reiniciamos su ataque para que busquen al jugador con la nueva prioridad
					else
					{
						banditChase.StopAttack();
					}
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);

			LoadBanditsFromXml();

			m_acceptedWar = valuesDictionary.GetValue<bool>("AcceptedWar", false);
			m_invasionCompleted = valuesDictionary.GetValue<bool>("InvasionCompleted", false);
			m_wasNight = IsNightTime();

			// Si la invasión estaba activa al cargar (por guardado durante la noche), restaurar modo narcotraficante
			if (m_acceptedWar && !m_invasionCompleted && m_wasNight)
			{
				m_invasionActive = true;
				m_invasionStarted = true;
				SetAllBanditsDrugTraffickerMode(true);
			}
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			valuesDictionary.SetValue("AcceptedWar", m_acceptedWar);
			valuesDictionary.SetValue("InvasionCompleted", m_invasionCompleted);
		}

		public void CancelWar()
		{
			if (m_invasionCompleted) return;

			m_acceptedWar = false;

			// Si la invasión estaba activa, detenerla y restaurar bandidos a modo normal
			if (m_invasionActive)
			{
				m_invasionActive = false;
				m_invasionStarted = false; // Permitir reiniciar si se vuelve a aceptar en la misma noche
				m_spawnTimer = 0f;
				SetAllBanditsDrugTraffickerMode(false);
			}
		}

		private void LoadBanditsFromXml()
		{
			XElement root = null;
			try
			{
				root = ContentManager.Get<XElement>("Waves/BanditInvasion");
			}
			catch (Exception ex)
			{
				Log.Error($"Error cargando BanditInvasion.xml: {ex.Message}");
			}

			if (root == null)
			{
				m_bandits.Add(new BanditSpawnData("Bandit1", 2, 0.45f));
				m_bandits.Add(new BanditSpawnData("Bandit2", 2, 0.40f));
				m_bandits.Add(new BanditSpawnData("Bandit3", 1, 0.25f));
				m_bandits.Add(new BanditSpawnData("Bandit7", 3, 0.55f));
				m_bandits.Add(new BanditSpawnData("Bandit11", 2, 0.45f));
			}
			else
			{
				foreach (var element in root.Elements("Bandit"))
				{
					string name = (string)element.Attribute("name");
					int count = (int)element.Attribute("count");
					float probability = (float)element.Attribute("probability");

					if (!string.IsNullOrEmpty(name) && count > 0 && probability > 0f)
					{
						if (name != "FirearmsDealer")
							m_bandits.Add(new BanditSpawnData(name, count, probability));
					}
				}
			}

			m_totalProbabilitySum = m_bandits.Sum(b => b.Probability);
		}

		public void Update(float dt)
		{
			if (m_invasionCompleted)
				return;

			bool isNight = IsNightTime();

			// Si no hay guerra aceptada, desactivamos cualquier invasión activa
			if (!m_acceptedWar)
			{
				if (m_invasionActive)
				{
					m_invasionActive = false;
					SetAllBanditsDrugTraffickerMode(false);
				}
				m_wasNight = isNight;
				return;
			}

			// Si la guerra está aceptada y la invasión no está activa
			if (!m_invasionActive)
			{
				// Si es de noche, activamos inmediatamente (útil para reactivar tras cancelar)
				if (isNight)
				{
					m_invasionActive = true;
					m_invasionStarted = true;
					m_spawnTimer = 0f;
					SetAllBanditsDrugTraffickerMode(true);
				}
				// Si no es de noche, esperamos a que oscurezca (la transición se manejará normalmente)
			}

			// Manejar finalización al amanecer
			if (m_wasNight && !isNight && m_invasionActive)
			{
				m_invasionActive = false;
				m_invasionCompleted = true;
				SetAllBanditsDrugTraffickerMode(false);
			}

			m_wasNight = isNight;

			if (!m_invasionActive)
				return;

			// Resto del código de spawn
			int totalBandits = CountBandits();
			if (totalBandits >= MaxGlobalBandits)
				return;

			m_spawnTimer += dt;
			int spawnsThisFrame = 0;

			while (m_spawnTimer >= m_spawnInterval && spawnsThisFrame < MaxSpawnsPerFrame)
			{
				m_spawnTimer -= m_spawnInterval;
				if (TrySpawnBanditGroup())
					spawnsThisFrame++;
			}
		}

		private bool TrySpawnBanditGroup()
		{
			BanditSpawnData selected = GetRandomBandit();
			if (selected == null)
				return false;

			Vector3 spawnPos = GetValidSpawnPoint();
			if (spawnPos == Vector3.Zero)
				return false;

			Vector2 areaMin = new Vector2(spawnPos.X - 16, spawnPos.Z - 16);
			Vector2 areaMax = new Vector2(spawnPos.X + 16, spawnPos.Z + 16);
			int nearby = CountBanditsInArea(areaMin, areaMax);
			if (nearby >= MaxBanditsPerArea)
				return false;

			int spawned = 0;
			for (int i = 0; i < selected.Count && spawned < MaxSpawnsPerFrame; i++)
			{
				Vector3 offsetPos = spawnPos;
				if (i > 0)
				{
					offsetPos = GetNearbySpawnPoint(spawnPos, 3f, 8f);
					if (offsetPos == Vector3.Zero)
						break;
				}

				if (SpawnBandit(selected.Name, offsetPos))
					spawned++;
			}

			return spawned > 0;
		}

		private BanditSpawnData GetRandomBandit()
		{
			if (m_bandits.Count == 0 || m_totalProbabilitySum <= 0f)
				return null;

			float roll = m_random.Float(0f, m_totalProbabilitySum);
			float cumulative = 0f;

			foreach (var bandit in m_bandits)
			{
				cumulative += bandit.Probability;
				if (roll <= cumulative)
					return bandit;
			}

			return m_bandits.LastOrDefault();
		}

		private bool SpawnBandit(string templateName, Vector3 position)
		{
			try
			{
				Entity entity = DatabaseManager.CreateEntity(Project, templateName, true);
				var body = entity.FindComponent<ComponentBody>(true);
				body.Position = position;
				body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, m_random.Float(0f, 2f * MathF.PI));
				var creature = entity.FindComponent<ComponentCreature>(true);
				creature.ConstantSpawn = false;

				var banditChase = entity.FindComponent<ComponentBanditChaseBehavior>();
				if (banditChase != null && m_invasionActive)
				{
					banditChase.IsDrugTraffickerMode = true;
				}

				Project.AddEntity(entity);
				return true;
			}
			catch (Exception ex)
			{
				Log.Error($"Error spawning bandit {templateName}: {ex.Message}");
				return false;
			}
		}

		private Vector3 GetValidSpawnPoint()
		{
			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				Vector3 playerPos = player.ComponentBody.Position;
				for (int i = 0; i < 15; i++)
				{
					float angle = m_random.Float(0f, 2f * MathF.PI);
					float distance = m_random.Float(20f, 50f);
					int x = (int)(playerPos.X + MathF.Cos(angle) * distance);
					int z = (int)(playerPos.Z + MathF.Sin(angle) * distance);
					int y = m_random.Int(10, 246);

					Point3? spawnPoint = ProcessSpawnPoint(new Point3(x, y, z));
					if (spawnPoint.HasValue)
					{
						return new Vector3(spawnPoint.Value.X + 0.5f, spawnPoint.Value.Y + 1.1f, spawnPoint.Value.Z + 0.5f);
					}
				}
			}
			return Vector3.Zero;
		}

		private Point3? ProcessSpawnPoint(Point3 spawnPoint)
		{
			int x = spawnPoint.X;
			int num = Math.Clamp(spawnPoint.Y, 1, 254);
			int z = spawnPoint.Z;

			TerrainChunk chunkAtCell = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunkAtCell == null || chunkAtCell.State <= TerrainChunkState.InvalidPropagatedLight)
				return null;

			for (int i = 0; i < 30; i++)
			{
				Point3 pointUp = new Point3(x, num + i, z);
				if (TestSpawnPoint(pointUp))
					return pointUp;

				Point3 pointDown = new Point3(x, num - i, z);
				if (TestSpawnPoint(pointDown))
					return pointDown;
			}
			return null;
		}

		private bool TestSpawnPoint(Point3 spawnPoint)
		{
			int x = spawnPoint.X;
			int y = spawnPoint.Y;
			int z = spawnPoint.Z;

			if (y <= 3 || y >= 253)
				return false;

			int cellValueFast = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
			int cellValueFast2 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
			int cellValueFast3 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

			Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast)];
			Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast2)];
			Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast3)];

			bool belowSolid = (block.IsCollidable_(cellValueFast) || block is WaterBlock);
			bool currentEmpty = (!block2.IsCollidable_(cellValueFast2) && !(block2 is WaterBlock));
			bool aboveEmpty = (!block3.IsCollidable_(cellValueFast3) && !(block3 is WaterBlock));

			if (!belowSolid || !currentEmpty || !aboveEmpty)
				return false;

			int groundContents = Terrain.ExtractContents(cellValueFast);
			if (groundContents == 18 || groundContents == 92)
				return false;

			return true;
		}

		private Vector3 GetNearbySpawnPoint(Vector3 center, float minDist, float maxDist)
		{
			for (int i = 0; i < 5; i++)
			{
				float angle = m_random.Float(0f, 2f * MathF.PI);
				float dist = m_random.Float(minDist, maxDist);
				int x = (int)(center.X + MathF.Cos(angle) * dist);
				int z = (int)(center.Z + MathF.Sin(angle) * dist);
				int y = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
				if (y > 2 && y < 254)
				{
					Point3? validPoint = ProcessSpawnPoint(new Point3(x, y, z));
					if (validPoint.HasValue)
						return new Vector3(validPoint.Value.X + 0.5f, validPoint.Value.Y + 1.1f, validPoint.Value.Z + 0.5f);
				}
			}
			return Vector3.Zero;
		}

		private int CountBandits()
		{
			int count = 0;
			foreach (var body in m_subsystemBodies.Bodies)
			{
				var creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature != null && IsBanditTemplate(body.Entity.ValuesDictionary.DatabaseObject?.Name))
					count++;
			}
			return count;
		}

		private int CountBanditsInArea(Vector2 c1, Vector2 c2)
		{
			int count = 0;
			foreach (var body in m_subsystemBodies.Bodies)
			{
				var creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature != null && IsBanditTemplate(body.Entity.ValuesDictionary.DatabaseObject?.Name))
				{
					Vector3 pos = body.Position;
					if (pos.X >= c1.X && pos.X <= c2.X && pos.Z >= c1.Y && pos.Z <= c2.Y)
						count++;
				}
			}
			return count;
		}

		private bool IsBanditTemplate(string name)
		{
			if (string.IsNullOrEmpty(name)) return false;
			return name.StartsWith("Bandit") && name != "FirearmsDealer";
		}

		private bool IsNightTime()
		{
			if (m_subsystemGameInfo.WorldSettings.TimeOfDayMode == TimeOfDayMode.Day ||
				m_subsystemGameInfo.WorldSettings.TimeOfDayMode == TimeOfDayMode.Sunrise)
				return false;
			return m_subsystemSky.SkyLightIntensity < 0.1f;
		}

		private class BanditSpawnData
		{
			public string Name { get; }
			public int Count { get; }
			public float Probability { get; }

			public BanditSpawnData(string name, int count, float probability)
			{
				Name = name;
				Count = count;
				Probability = probability;
			}
		}
	}
}
