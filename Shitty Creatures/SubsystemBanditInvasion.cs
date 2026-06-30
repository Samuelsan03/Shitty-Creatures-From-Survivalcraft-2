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
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private Random m_random = new Random();

		public event Action InvasionCompleted;

		private List<BanditSpawnData> m_bandits = new List<BanditSpawnData>();
		private float m_totalProbabilitySum;

		private bool m_acceptedWar;
		private bool m_invasionActive;
		private bool m_invasionStarted;
		private bool m_invasionCompleted;
		private bool m_wasRejected;

		private bool m_greenNightWasActiveDuringInvasion;

		private bool m_needsInitialSync;
		private bool m_restoredFromSave;

		private const float InitialSpawnDelay = 5.0f;
		private bool m_inInitialDelay;
		private float m_initialDelayTimer;

		private static readonly HashSet<string> m_banditNames = new HashSet<string>
		{
			"Bandit1", "Bandit2", "Bandit3", "Bandit4", "Bandit5",
			"Bandit6", "Bandit7", "Bandit8", "Bandit9", "Bandit10",
			"Bandit11", "Bandit13", "Bandit14", "Bandit15", "Bandit16"
		};

		private int m_killsByPlayer;
		private bool m_bossUnlocked;
		private bool m_bossSpawnedThisWar;
		private HashSet<int> m_banditDeathCounted = new HashSet<int>();

		public bool IsWarAccepted => m_acceptedWar;
		public bool IsWarRejected => m_wasRejected;
		public bool IsWarCompleted => m_invasionCompleted;
		public bool WasGreenNightActiveDuringInvasion => m_greenNightWasActiveDuringInvasion;
		public bool IsInInitialDelay => m_inInitialDelay;
		public float RemainingInitialDelay => m_inInitialDelay ? Math.Max(0f, InitialSpawnDelay - m_initialDelayTimer) : 0f;

		private float m_spawnTimer;
		private float m_spawnInterval = 3.0f;
		private const int MaxBanditsPerArea = 8;
		private const int MaxGlobalBandits = 35;
		private const int MaxSpawnsPerFrame = 2;

		private bool m_wasEffectiveInvasionTime;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public bool IsInvasionActive => m_invasionActive;

		public void AcceptWar()
		{
			if (m_invasionCompleted)
			{
				m_invasionCompleted = false;
				m_invasionActive = false;
				m_invasionStarted = false;
				m_acceptedWar = true;
				m_wasRejected = false;
				m_spawnTimer = 0f;
				m_greenNightWasActiveDuringInvasion = false;
				m_restoredFromSave = false;
				m_wasEffectiveInvasionTime = CalculateEffectiveInvasionTime();

				m_inInitialDelay = false;
				m_initialDelayTimer = 0f;

				m_killsByPlayer = 0;
				m_bossUnlocked = false;
				m_bossSpawnedThisWar = false;

				return;
			}

			if (!m_acceptedWar)
			{
				m_acceptedWar = true;
				m_wasRejected = false;
				m_greenNightWasActiveDuringInvasion = false;
				m_restoredFromSave = false;

				m_inInitialDelay = false;
				m_initialDelayTimer = 0f;

				m_bossSpawnedThisWar = false;
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
					if (!enabled)
						banditChase.StopAttack();
				}
			}
		}

		private void SyncBanditsDrugTraffickerMode()
		{
			foreach (var body in m_subsystemBodies.Bodies)
			{
				var banditChase = body.Entity.FindComponent<ComponentBanditChaseBehavior>();
				if (banditChase != null)
				{
					banditChase.IsDrugTraffickerMode = m_invasionActive;
					if (!m_invasionActive)
						banditChase.StopAttack();
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
			m_subsystemGreenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>(false);

			LoadBanditsFromXml();

			m_acceptedWar = valuesDictionary.GetValue<bool>("AcceptedWar", false);
			m_invasionCompleted = valuesDictionary.GetValue<bool>("InvasionCompleted", false);
			m_wasRejected = valuesDictionary.GetValue<bool>("WasRejected", false);
			m_greenNightWasActiveDuringInvasion = valuesDictionary.GetValue<bool>("GreenNightWasActiveDuringInvasion", false);

			m_invasionActive = valuesDictionary.GetValue<bool>("InvasionActive", false);
			m_invasionStarted = valuesDictionary.GetValue<bool>("InvasionStarted", false);

			m_inInitialDelay = valuesDictionary.GetValue<bool>("InInitialDelay", false);
			m_initialDelayTimer = valuesDictionary.GetValue<float>("InitialDelayTimer", 0f);

			m_killsByPlayer = valuesDictionary.GetValue<int>("KillsByPlayer", 0);
			m_bossUnlocked = valuesDictionary.GetValue<bool>("BossUnlocked", false);
			m_bossSpawnedThisWar = valuesDictionary.GetValue<bool>("BossSpawnedThisWar", false);

			m_wasEffectiveInvasionTime = CalculateEffectiveInvasionTime();
			m_restoredFromSave = true;

			if (m_invasionCompleted)
			{
				m_invasionActive = false;
				m_invasionStarted = false;
				m_inInitialDelay = false;
				m_initialDelayTimer = 0f;
				m_needsInitialSync = false;
			}
			else if (m_acceptedWar && !m_invasionActive && m_wasEffectiveInvasionTime)
			{
				m_invasionActive = true;
				m_invasionStarted = true;
				m_inInitialDelay = false;
				m_initialDelayTimer = 0f;
				m_needsInitialSync = true;
			}
			else
			{
				m_needsInitialSync = m_invasionActive;
			}
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			valuesDictionary.SetValue("AcceptedWar", m_acceptedWar);
			valuesDictionary.SetValue("InvasionCompleted", m_invasionCompleted);
			valuesDictionary.SetValue("WasRejected", m_wasRejected);
			valuesDictionary.SetValue("GreenNightWasActiveDuringInvasion", m_greenNightWasActiveDuringInvasion);
			valuesDictionary.SetValue("InvasionActive", m_invasionActive);
			valuesDictionary.SetValue("InvasionStarted", m_invasionStarted);
			valuesDictionary.SetValue("InInitialDelay", m_inInitialDelay);
			valuesDictionary.SetValue("InitialDelayTimer", m_initialDelayTimer);
			valuesDictionary.SetValue("KillsByPlayer", m_killsByPlayer);
			valuesDictionary.SetValue("BossUnlocked", m_bossUnlocked);
			valuesDictionary.SetValue("BossSpawnedThisWar", m_bossSpawnedThisWar);
		}

		public void CancelWar()
		{
			if (m_invasionCompleted) return;

			m_acceptedWar = false;
			m_wasRejected = true;
			m_greenNightWasActiveDuringInvasion = false;
			m_restoredFromSave = false;

			if (m_invasionActive)
			{
				m_invasionActive = false;
				m_invasionStarted = false;
				m_spawnTimer = 0f;
				m_inInitialDelay = false;
				m_initialDelayTimer = 0f;
				SetAllBanditsDrugTraffickerMode(false);
			}

			m_wasEffectiveInvasionTime = CalculateEffectiveInvasionTime();
		}

		private bool CalculateEffectiveInvasionTime()
		{
			float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
			float middawn = m_subsystemTimeOfDay.Middawn;
			const float dawnTolerance = 0.005f;
			bool isEndMoment = Math.Abs(timeOfDay - middawn) < dawnTolerance;

			if (isEndMoment)
				return false;

			if (m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive)
				return true;

			return IsInvasionTime();
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
			if (m_needsInitialSync)
			{
				m_needsInitialSync = false;
				SyncBanditsDrugTraffickerMode();
			}

			if (m_invasionCompleted)
				return;

			bool effectiveInvasionTime = CalculateEffectiveInvasionTime();

			if (!m_acceptedWar)
			{
				if (m_invasionActive)
				{
					m_invasionActive = false;
					m_inInitialDelay = false;
					m_initialDelayTimer = 0f;
					SetAllBanditsDrugTraffickerMode(false);
				}
				m_wasEffectiveInvasionTime = effectiveInvasionTime;
				m_restoredFromSave = false;
				return;
			}

			if (!m_invasionActive)
			{
				if (effectiveInvasionTime)
				{
					m_invasionActive = true;
					m_invasionStarted = true;
					m_spawnTimer = 0f;
					SetAllBanditsDrugTraffickerMode(true);

					m_inInitialDelay = true;
					m_initialDelayTimer = 0f;

					m_bossSpawnedThisWar = false;
				}
			}

			if (m_invasionActive && m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive)
			{
				m_greenNightWasActiveDuringInvasion = true;
			}

			if (!m_restoredFromSave && m_wasEffectiveInvasionTime && !effectiveInvasionTime && m_invasionActive)
			{
				m_invasionActive = false;
				m_invasionCompleted = true;
				m_inInitialDelay = false;
				m_initialDelayTimer = 0f;
				SetAllBanditsDrugTraffickerMode(false);

				m_killsByPlayer = 0;
				m_bossUnlocked = false;
				m_bossSpawnedThisWar = false;

				InvasionCompleted?.Invoke();
			}

			m_restoredFromSave = false;
			m_wasEffectiveInvasionTime = effectiveInvasionTime;

			if (!m_invasionActive)
				return;

			if (m_inInitialDelay)
			{
				m_initialDelayTimer += dt;
				if (m_initialDelayTimer >= InitialSpawnDelay)
				{
					m_inInitialDelay = false;
					m_initialDelayTimer = 0f;
					m_spawnTimer = 0f;
				}
				else
				{
					return;
				}
			}

			// Ya no se espera a medianoche para spawnear al jefe.
			// El spawn se activa inmediatamente al alcanzar 100 muertes en IncrementKillsByPlayer().

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

		private bool IsInvasionTime()
		{
			TimeOfDayMode mode = m_subsystemGameInfo.WorldSettings.TimeOfDayMode;

			if (mode == TimeOfDayMode.Day || mode == TimeOfDayMode.Sunrise)
				return false;

			if (mode == TimeOfDayMode.Night || mode == TimeOfDayMode.Sunset)
				return true;

			if (mode == TimeOfDayMode.Changing)
			{
				float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
				float duskStart = m_subsystemTimeOfDay.DuskStart;
				float middawn = m_subsystemTimeOfDay.Middawn;

				return timeOfDay >= duskStart || timeOfDay < middawn;
			}

			return false;
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

		private void SpawnBoss()
		{
			ComponentPlayer targetPlayer = m_subsystemPlayers.ComponentPlayers.FirstOrDefault();
			if (targetPlayer == null)
				return;

			Vector3 playerPos = targetPlayer.ComponentBody.Position;

			float angle = m_random.Float(0f, 2f * MathF.PI);
			float distance = m_random.Float(25f, 50f);

			Vector2 offset2D = new Vector2(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance);
			Vector2 candidatePos2D = new Vector2(playerPos.X + offset2D.X, playerPos.Z + offset2D.Y);

			Vector3 spawnPos = FindValidSpawnPointNear(candidatePos2D, playerPos.Y);
			if (spawnPos == Vector3.Zero)
			{
				for (int i = 0; i < 5; i++)
				{
					float newDist = m_random.Float(15f, 40f);
					float newAngle = m_random.Float(0f, 2f * MathF.PI);
					Vector2 newOffset = new Vector2(MathF.Cos(newAngle) * newDist, MathF.Sin(newAngle) * newDist);
					Vector2 newCandidate = new Vector2(playerPos.X + newOffset.X, playerPos.Z + newOffset.Y);
					spawnPos = FindValidSpawnPointNear(newCandidate, playerPos.Y);
					if (spawnPos != Vector3.Zero)
						break;
				}
			}

			if (spawnPos == Vector3.Zero)
			{
				Log.Warning("[SubsystemBanditInvasion] No se pudo encontrar un punto de spawn válido para el jefe.");
				return;
			}

			try
			{
				Entity entity = DatabaseManager.CreateEntity(Project, "LaBandida", true);
				var body = entity.FindComponent<ComponentBody>(true);
				body.Position = spawnPos;
				body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, m_random.Float(0f, 2f * MathF.PI));
				var creature = entity.FindComponent<ComponentCreature>(true);
				creature.ConstantSpawn = false;

				var banditChase = entity.FindComponent<ComponentBanditChaseBehavior>();
				if (banditChase != null)
				{
					banditChase.IsDrugTraffickerMode = true;
				}

				Project.AddEntity(entity);
				Log.Information($"[SubsystemBanditInvasion] Jefe spawnado en {spawnPos}");
			}
			catch (Exception ex)
			{
				Log.Error($"Error spawning boss LaBandida: {ex.Message}");
			}
		}

		private Vector3 FindValidSpawnPointNear(Vector2 position2D, float referenceY)
		{
			int x = (int)position2D.X;
			int z = (int)position2D.Y;
			int y = (int)referenceY;

			for (int i = 0; i < 20; i++)
			{
				Point3 pointUp = new Point3(x, y + i, z);
				if (TestSpawnPoint(pointUp))
				{
					return new Vector3(pointUp.X + 0.5f, pointUp.Y + 1.1f, pointUp.Z + 0.5f);
				}

				Point3 pointDown = new Point3(x, y - i, z);
				if (TestSpawnPoint(pointDown))
				{
					return new Vector3(pointDown.X + 0.5f, pointDown.Y + 1.1f, pointDown.Z + 0.5f);
				}
			}

			for (int dx = -2; dx <= 2; dx++)
			{
				for (int dz = -2; dz <= 2; dz++)
				{
					if (dx == 0 && dz == 0) continue;
					int testX = x + dx;
					int testZ = z + dz;
					for (int i = 0; i < 20; i++)
					{
						Point3 pointUp = new Point3(testX, y + i, testZ);
						if (TestSpawnPoint(pointUp))
						{
							return new Vector3(pointUp.X + 0.5f, pointUp.Y + 1.1f, pointUp.Z + 0.5f);
						}
						Point3 pointDown = new Point3(testX, y - i, testZ);
						if (TestSpawnPoint(pointDown))
						{
							return new Vector3(pointDown.X + 0.5f, pointDown.Y + 1.1f, pointDown.Z + 0.5f);
						}
					}
				}
			}

			return Vector3.Zero;
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

			bool isValidGround = block is GrassBlock || block is DirtBlock || block is SandBlock || block is GravelBlock;

			bool currentEmpty = (!block2.IsCollidable_(cellValueFast2) && !(block2 is WaterBlock));
			bool aboveEmpty = (!block3.IsCollidable_(cellValueFast3) && !(block3 is WaterBlock));

			if (!isValidGround || !currentEmpty || !aboveEmpty)
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

		public bool IsBanditTemplate(string name)
		{
			if (string.IsNullOrEmpty(name)) return false;
			return m_banditNames.Contains(name);
		}

		public void IncrementKillsByPlayer()
		{
			m_killsByPlayer++;
			if (m_killsByPlayer >= 100 && !m_bossUnlocked && m_invasionActive && !m_bossSpawnedThisWar)
			{
				m_bossUnlocked = true;
				m_bossSpawnedThisWar = true;

				SpawnBoss();

				ComponentPlayer firstPlayer = m_subsystemPlayers.ComponentPlayers.FirstOrDefault();
				if (firstPlayer != null)
				{
					firstPlayer.ComponentGui.DisplayLargeMessage(
						"¡La líder del Cartel ha llegado!\n ¡Prepárate para la pelea final!",
						"",
						5f,
						0f
					);
				}
			}
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
