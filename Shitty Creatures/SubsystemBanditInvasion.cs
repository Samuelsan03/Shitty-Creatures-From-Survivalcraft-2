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
		private SubsystemBodies m_subsystemBodies;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private SubsystemZombiesSpawn m_subsystemZombiesSpawn;
		private Random m_random = new Random();

		public event Action InvasionCompleted;

		private List<BanditSpawnData> m_bandits = new List<BanditSpawnData>();

		private bool m_acceptedWar;
		private bool m_invasionActive;
		private bool m_invasionStarted;
		private bool m_invasionCompleted;
		private bool m_wasRejected;

		private bool m_greenNightWasActiveDuringInvasion;
		private bool m_bossPendingForMidnight;

		private bool m_needsInitialSync;
		private bool m_restoredFromSave;
		private bool m_wasEffectiveInvasionTime;

		private const float BossSpawnDelay = 5.0f;
		private bool m_inBossDelay;
		private float m_bossDelayTimer;

		private bool m_bossSpawnedThisWar;

		// Spawn activo de invasión
		private const float InvasionSpawnInterval = 2.5f;
		private float m_invasionSpawnTimer;
		private const int MaxInvasionBandits = 15;

		private static readonly HashSet<string> m_banditNames = new HashSet<string>
		{
			"Bandit1", "Bandit2", "Bandit3", "Bandit4", "Bandit5",
			"Bandit6", "Bandit7", "Bandit8", "Bandit9", "Bandit10",
			"Bandit11", "Bandit12", "Bandit13", "Bandit14", "Bandit15",
			"Bandit16", "Bandit17", "FirearmsDealer"
		};

		public bool IsWarAccepted => m_acceptedWar;
		public bool IsWarRejected => m_wasRejected;
		public bool IsWarCompleted => m_invasionCompleted;
		public bool WasGreenNightActiveDuringInvasion => m_greenNightWasActiveDuringInvasion;
		public bool IsInBossDelay => m_inBossDelay;
		public float RemainingBossDelay => m_inBossDelay ? Math.Max(0f, BossSpawnDelay - m_bossDelayTimer) : 0f;
		public bool BossPendingForMidnight => m_bossPendingForMidnight;
		public bool BossSpawnedThisWar => m_bossSpawnedThisWar;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public bool IsInvasionActive => m_invasionActive;

		public float GetBanditProbability(string name)
		{
			var data = m_bandits.FirstOrDefault(b => b.Name == name);
			return data != null ? data.Probability : 1.0f;
		}

		public void AcceptWar()
		{
			if (m_invasionCompleted)
			{
				m_invasionCompleted = false;
				m_invasionActive = false;
				m_invasionStarted = false;
				m_acceptedWar = true;
				m_wasRejected = false;
				m_greenNightWasActiveDuringInvasion = false;
				m_bossPendingForMidnight = false;
				m_inBossDelay = false;
				m_bossDelayTimer = 0f;
				m_bossSpawnedThisWar = false;
				m_needsInitialSync = false;
				m_restoredFromSave = false;
				m_invasionSpawnTimer = 0f;
				m_wasEffectiveInvasionTime = CalculateEffectiveInvasionTime();
				return;
			}

			if (!m_acceptedWar)
			{
				m_acceptedWar = true;
				m_wasRejected = false;
				m_greenNightWasActiveDuringInvasion = false;
				m_bossPendingForMidnight = false;
				m_inBossDelay = false;
				m_bossDelayTimer = 0f;
				m_bossSpawnedThisWar = false;
				m_restoredFromSave = false;
				m_invasionSpawnTimer = 0f;
				m_wasEffectiveInvasionTime = CalculateEffectiveInvasionTime();
			}
		}

		public void SpawnBossNow()
		{
			if (!m_bossSpawnedThisWar && m_invasionActive)
			{
				SpawnBoss();
				m_bossSpawnedThisWar = true;
				m_bossPendingForMidnight = false;
				m_inBossDelay = false;
				m_bossDelayTimer = 0f;
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
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemGreenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>(false);
			m_subsystemZombiesSpawn = Project.FindSubsystem<SubsystemZombiesSpawn>(true);

			LoadBanditsFromXml();

			m_acceptedWar = valuesDictionary.GetValue<bool>("AcceptedWar", false);
			m_invasionCompleted = valuesDictionary.GetValue<bool>("InvasionCompleted", false);
			m_wasRejected = valuesDictionary.GetValue<bool>("WasRejected", false);
			m_greenNightWasActiveDuringInvasion = valuesDictionary.GetValue<bool>("GreenNightWasActiveDuringInvasion", false);
			m_invasionActive = valuesDictionary.GetValue<bool>("InvasionActive", false);
			m_invasionStarted = valuesDictionary.GetValue<bool>("InvasionStarted", false);
			m_inBossDelay = valuesDictionary.GetValue<bool>("InBossDelay", false);
			m_bossDelayTimer = valuesDictionary.GetValue<float>("BossDelayTimer", 0f);
			m_bossSpawnedThisWar = valuesDictionary.GetValue<bool>("BossSpawnedThisWar", false);
			m_bossPendingForMidnight = valuesDictionary.GetValue<bool>("BossPendingForMidnight", false);

			m_wasEffectiveInvasionTime = CalculateEffectiveInvasionTime();
			m_restoredFromSave = true;

			if (m_invasionCompleted)
			{
				m_invasionActive = false;
				m_invasionStarted = false;
				m_inBossDelay = false;
				m_bossDelayTimer = 0f;
				m_needsInitialSync = false;
				m_bossPendingForMidnight = false;
			}
			else if (m_acceptedWar && !m_invasionActive && m_wasEffectiveInvasionTime)
			{
				m_invasionActive = true;
				m_invasionStarted = true;
				m_inBossDelay = false;
				m_bossDelayTimer = 0f;
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
			valuesDictionary.SetValue("InBossDelay", m_inBossDelay);
			valuesDictionary.SetValue("BossDelayTimer", m_bossDelayTimer);
			valuesDictionary.SetValue("BossSpawnedThisWar", m_bossSpawnedThisWar);
			valuesDictionary.SetValue("BossPendingForMidnight", m_bossPendingForMidnight);
		}

		public void CancelWar()
		{
			if (m_invasionCompleted) return;

			m_acceptedWar = false;
			m_wasRejected = true;
			m_greenNightWasActiveDuringInvasion = false;
			m_restoredFromSave = false;
			m_bossPendingForMidnight = false;

			if (m_invasionActive)
			{
				m_invasionActive = false;
				m_invasionStarted = false;
				m_inBossDelay = false;
				m_bossDelayTimer = 0f;
				m_invasionSpawnTimer = 0f;
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
				m_bandits.Add(new BanditSpawnData("Bandit1", 0.45f));
				m_bandits.Add(new BanditSpawnData("Bandit2", 0.40f));
				m_bandits.Add(new BanditSpawnData("Bandit3", 0.25f));
				m_bandits.Add(new BanditSpawnData("Bandit8", 0.55f));
				m_bandits.Add(new BanditSpawnData("Bandit9", 0.45f));
			}
			else
			{
				foreach (var element in root.Elements("Bandit"))
				{
					string name = (string)element.Attribute("name");
					float probability = (float)element.Attribute("probability");

					if (!string.IsNullOrEmpty(name) && probability > 0f)
					{
						m_bandits.Add(new BanditSpawnData(name, probability));
					}
				}
			}
		}

		private int CountInvasionBandits()
		{
			int count = 0;
			foreach (var body in m_subsystemBodies.Bodies)
			{
				if (body.Entity?.ValuesDictionary?.DatabaseObject?.Name != null)
				{
					string name = body.Entity.ValuesDictionary.DatabaseObject.Name;
					if (m_banditNames.Contains(name))
					{
						count++;
					}
				}
			}
			return count;
		}

		private void SpawnInvasionBandit()
		{
			if (m_bandits.Count == 0) return;

			ComponentPlayer targetPlayer = m_subsystemPlayers.ComponentPlayers.FirstOrDefault();
			if (targetPlayer == null) return;

			// Seleccionar bandido aleatorio basado en probabilidad del XML
			float totalWeight = 0f;
			for (int i = 0; i < m_bandits.Count; i++)
			{
				totalWeight += m_bandits[i].Probability;
			}

			float roll = m_random.Float(0f, totalWeight);
			float cumulative = 0f;
			string selectedName = null;
			for (int i = 0; i < m_bandits.Count; i++)
			{
				cumulative += m_bandits[i].Probability;
				if (roll < cumulative)
				{
					selectedName = m_bandits[i].Name;
					break;
				}
			}
			if (selectedName == null) return;

			Vector3 playerPos = targetPlayer.ComponentBody.Position;
			SubsystemTerrain terrain = Project.FindSubsystem<SubsystemTerrain>(true);

			// Intentar encontrar punto de spawn válido
			for (int attempt = 0; attempt < 5; attempt++)
			{
				float angle = m_random.Float(0f, 2f * MathF.PI);
				float distance = m_random.Float(25f, 45f);
				int targetX = (int)(playerPos.X + MathF.Cos(angle) * distance);
				int targetZ = (int)(playerPos.Z + MathF.Sin(angle) * distance);

				TerrainChunk chunk = terrain.Terrain.GetChunkAtCell(targetX, targetZ);
				if (chunk == null || chunk.State <= TerrainChunkState.InvalidPropagatedLight)
					continue;

				int topY = terrain.Terrain.GetTopHeight(targetX, targetZ);

				if (topY > 3 && topY < 253)
				{
					int below = terrain.Terrain.GetCellValueFast(targetX, topY - 1, targetZ);
					int current = terrain.Terrain.GetCellValueFast(targetX, topY, targetZ);
					int above = terrain.Terrain.GetCellValueFast(targetX, topY + 1, targetZ);

					Block blockBelow = BlocksManager.Blocks[Terrain.ExtractContents(below)];
					Block blockCurrent = BlocksManager.Blocks[Terrain.ExtractContents(current)];
					Block blockAbove = BlocksManager.Blocks[Terrain.ExtractContents(above)];

					bool validGround = blockBelow is GrassBlock || blockBelow is DirtBlock || blockBelow is SandBlock || blockBelow is GravelBlock;
					bool currentEmpty = !blockCurrent.IsCollidable_(current) && !(blockCurrent is WaterBlock);
					bool aboveEmpty = !blockAbove.IsCollidable_(above) && !(blockAbove is WaterBlock);

					if (validGround && currentEmpty && aboveEmpty)
					{
						try
						{
							Entity entity = DatabaseManager.CreateEntity(Project, selectedName, true);
							var body = entity.FindComponent<ComponentBody>(true);
							body.Position = new Vector3(targetX + 0.5f, topY + 1.1f, targetZ + 0.5f);
							body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, m_random.Float(0f, 2f * MathF.PI));
							var creature = entity.FindComponent<ComponentCreature>(true);
							creature.ConstantSpawn = false;

							var banditChase = entity.FindComponent<ComponentBanditChaseBehavior>();
							if (banditChase != null)
							{
								banditChase.IsDrugTraffickerMode = true;
							}

							Project.AddEntity(entity);
						}
						catch (Exception ex)
						{
							Log.Error($"Error spawning invasion bandit {selectedName}: {ex.Message}");
						}
						return;
					}
				}
			}
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
					m_inBossDelay = false;
					m_bossDelayTimer = 0f;
					m_invasionSpawnTimer = 0f;
					SetAllBanditsDrugTraffickerMode(false);
				}
				m_wasEffectiveInvasionTime = effectiveInvasionTime;
				m_restoredFromSave = false;
				m_bossPendingForMidnight = false;
				return;
			}

			if (!m_invasionActive)
			{
				if (effectiveInvasionTime)
				{
					m_invasionActive = true;
					m_invasionStarted = true;
					m_invasionSpawnTimer = 0f;
					SetAllBanditsDrugTraffickerMode(true);

					m_inBossDelay = true;
					m_bossDelayTimer = 0f;
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
				m_inBossDelay = false;
				m_bossDelayTimer = 0f;
				m_invasionSpawnTimer = 0f;
				SetAllBanditsDrugTraffickerMode(false);
				m_bossPendingForMidnight = false;
				m_bossSpawnedThisWar = false;

				InvasionCompleted?.Invoke();
			}

			m_restoredFromSave = false;
			m_wasEffectiveInvasionTime = effectiveInvasionTime;

			if (!m_invasionActive)
				return;

			// SPAWN ACTIVO DE INVASIÓN
			m_invasionSpawnTimer += dt;
			if (m_invasionSpawnTimer >= InvasionSpawnInterval)
			{
				m_invasionSpawnTimer -= InvasionSpawnInterval;

				if (CountInvasionBandits() < MaxInvasionBandits)
				{
					SpawnInvasionBandit();
				}
			}

			// Lógica del jefe: solo aparece durante noche verde
			if (m_inBossDelay && !m_bossSpawnedThisWar)
			{
				m_bossDelayTimer += dt;
				if (m_bossDelayTimer >= BossSpawnDelay)
				{
					m_inBossDelay = false;
					m_bossDelayTimer = 0f;

					bool isGreenNightActive = m_subsystemGreenNightSky != null &&
											   m_subsystemGreenNightSky.GreenNightEnabled &&
											   m_subsystemGreenNightSky.IsGreenNightActive;

					if (isGreenNightActive)
					{
						if (m_subsystemZombiesSpawn != null && m_subsystemZombiesSpawn.IsFinalWave)
						{
							m_bossPendingForMidnight = true;
						}
						else
						{
							SpawnBoss();
							m_bossSpawnedThisWar = true;
						}
					}
				}
			}
		}

		private void SpawnBoss()
		{
			ComponentPlayer targetPlayer = m_subsystemPlayers.ComponentPlayers.FirstOrDefault();
			if (targetPlayer == null)
				return;

			Vector3 playerPos = targetPlayer.ComponentBody.Position;
			SubsystemTerrain terrain = Project.FindSubsystem<SubsystemTerrain>(true);

			Vector3 spawnPos = FindValidBossSpawnPoint(terrain, playerPos);
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

				string largeMessage = LanguageControl.Get("SubsystemBanditInvasion", 0);
				foreach (var player in m_subsystemPlayers.ComponentPlayers)
				{
					player.ComponentGui.DisplayLargeMessage(largeMessage, "", 5f, 0f);
				}
			}
			catch (Exception ex)
			{
				Log.Error($"Error spawning boss LaBandida: {ex.Message}");
			}
		}

		private Vector3 FindValidBossSpawnPoint(SubsystemTerrain terrain, Vector3 playerPos)
		{
			for (int attempt = 0; attempt < 10; attempt++)
			{
				float angle = m_random.Float(0f, 2f * MathF.PI);
				float distance = m_random.Float(25f, 50f);

				int targetX = (int)(playerPos.X + MathF.Cos(angle) * distance);
				int targetZ = (int)(playerPos.Z + MathF.Sin(angle) * distance);

				Vector3 pos = FindValidSpawnAtColumn(terrain, targetX, targetZ, playerPos.Y);
				if (pos != Vector3.Zero) return pos;
			}

			for (int attempt = 0; attempt < 20; attempt++)
			{
				float angle = m_random.Float(0f, 2f * MathF.PI);
				float distance = m_random.Float(15f, 60f);

				int targetX = (int)(playerPos.X + MathF.Cos(angle) * distance);
				int targetZ = (int)(playerPos.Z + MathF.Sin(angle) * distance);

				Vector3 pos = FindValidSpawnAtColumn(terrain, targetX, targetZ, playerPos.Y);
				if (pos != Vector3.Zero) return pos;
			}

			return Vector3.Zero;
		}

		private Vector3 FindValidSpawnAtColumn(SubsystemTerrain terrain, int x, int z, float referenceY)
		{
			int baseY = (int)referenceY;

			for (int i = 0; i < 30; i++)
			{
				int yUp = baseY + i;
				if (yUp > 3 && yUp < 253 && IsValidSpawnBlock(terrain, x, yUp, z))
					return new Vector3(x + 0.5f, yUp + 1.1f, z + 0.5f);

				int yDown = baseY - i;
				if (yDown > 3 && yDown < 253 && IsValidSpawnBlock(terrain, x, yDown, z))
					return new Vector3(x + 0.5f, yDown + 1.1f, z + 0.5f);
			}

			int topY = terrain.Terrain.GetTopHeight(x, z);
			if (topY > 3 && topY < 253)
			{
				for (int i = 0; i < 10; i++)
				{
					int y = topY - i;
					if (y > 3 && IsValidSpawnBlock(terrain, x, y, z))
						return new Vector3(x + 0.5f, y + 1.1f, z + 0.5f);
				}
			}

			return Vector3.Zero;
		}

		private bool IsValidSpawnBlock(SubsystemTerrain terrain, int x, int y, int z)
		{
			int below = terrain.Terrain.GetCellValueFast(x, y - 1, z);
			int current = terrain.Terrain.GetCellValueFast(x, y, z);
			int above = terrain.Terrain.GetCellValueFast(x, y + 1, z);

			Block blockBelow = BlocksManager.Blocks[Terrain.ExtractContents(below)];
			Block blockCurrent = BlocksManager.Blocks[Terrain.ExtractContents(current)];
			Block blockAbove = BlocksManager.Blocks[Terrain.ExtractContents(above)];

			bool validGround = blockBelow is GrassBlock || blockBelow is DirtBlock || blockBelow is SandBlock || blockBelow is GravelBlock;
			bool currentEmpty = !blockCurrent.IsCollidable_(current) && !(blockCurrent is WaterBlock);
			bool aboveEmpty = !blockAbove.IsCollidable_(above) && !(blockAbove is WaterBlock);

			return validGround && currentEmpty && aboveEmpty;
		}

		public bool IsBanditTemplate(string name)
		{
			if (string.IsNullOrEmpty(name)) return false;
			return m_banditNames.Contains(name);
		}

		private class BanditSpawnData
		{
			public string Name { get; }
			public float Probability { get; }

			public BanditSpawnData(string name, float probability)
			{
				Name = name;
				Probability = probability;
			}
		}
	}
}
