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

		/// <summary>
		/// Indica si la Noche Verde estuvo activa en algún momento durante la invasión actual.
		/// Se usa para otorgar el logro 70 (Noche Verde + Guerra de Narcos).
		/// </summary>
		private bool m_greenNightWasActiveDuringInvasion;

		/// <summary>
		/// Flag para sincronizar los bandidos en el primer Update después de cargar.
		/// Necesario porque las entidades podrían no estar cargadas cuando Load() se ejecuta.
		/// </summary>
		private bool m_needsInitialSync;

		/// <summary>
		/// Flag para indicar que el estado fue restaurado desde un guardado.
		/// Evita que la lógica de transición (wasEffective → !effective) se dispare
		/// incorrectamente al cargar durante una doble guerra.
		/// </summary>
		private bool m_restoredFromSave;

		public bool IsWarAccepted => m_acceptedWar;
		public bool IsWarRejected => m_wasRejected;
		public bool IsWarCompleted => m_invasionCompleted;
		public bool WasGreenNightActiveDuringInvasion => m_greenNightWasActiveDuringInvasion;

		private float m_spawnTimer;
		private float m_spawnInterval = 3.0f;
		private const int MaxBanditsPerArea = 8;
		private const int MaxGlobalBandits = 35;
		private const int MaxSpawnsPerFrame = 2;

		/// <summary>
		/// Trackea el "tiempo efectivo de invasión" (invasion time normal + noche verde activa).
		/// La invasión solo finaliza cuando este valor pasa de true a false.
		/// </summary>
		private bool m_wasEffectiveInvasionTime;

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
				m_wasRejected = false;
				m_spawnTimer = 0f;
				m_greenNightWasActiveDuringInvasion = false;
				m_restoredFromSave = false;
				m_wasEffectiveInvasionTime = CalculateEffectiveInvasionTime();
				return;
			}

			// Si no está completada y aún no se había aceptado, activamos la aceptación
			if (!m_acceptedWar)
			{
				m_acceptedWar = true;
				m_wasRejected = false;
				m_greenNightWasActiveDuringInvasion = false;
				m_restoredFromSave = false;
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

			// Restaurar estado activo directamente del guardado
			m_invasionActive = valuesDictionary.GetValue<bool>("InvasionActive", false);
			m_invasionStarted = valuesDictionary.GetValue<bool>("InvasionStarted", false);

			// Calcular tiempo efectivo de invasión (incluye noche verde)
			m_wasEffectiveInvasionTime = CalculateEffectiveInvasionTime();

			// Marcar que restauramos desde guardado para evitar transiciones falsas
			m_restoredFromSave = true;

			// Lógica de restauración con compatibilidad hacia atrás
			if (m_invasionCompleted)
			{
				m_invasionActive = false;
				m_invasionStarted = false;
				m_needsInitialSync = false;
			}
			else if (m_acceptedWar && !m_invasionActive && m_wasEffectiveInvasionTime)
			{
				// Caso de compatibilidad: guardados antiguos que no tenían InvasionActive
				m_invasionActive = true;
				m_invasionStarted = true;
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
				SetAllBanditsDrugTraffickerMode(false);
			}

			m_wasEffectiveInvasionTime = CalculateEffectiveInvasionTime();
		}

		/// <summary>
		/// Calcula si estamos en "tiempo efectivo de invasión":
		/// - Tiempo de invasión normal (DuskStart → Middawn), O
		/// - Noche Verde activa
		/// 
		/// Ambos eventos terminan en Middawn, por lo que en una doble guerra
		/// ambos terminan exactamente al mismo tiempo.
		/// </summary>
		private bool CalculateEffectiveInvasionTime()
		{
			// Si la Noche Verde está activa, siempre es tiempo efectivo de invasión
			// Esto maneja el caso donde BanditInvasion se actualiza antes que GreenNightSky
			// en el frame de finalización
			if (m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive)
				return true;

			// Si no hay Noche Verde, usar el tiempo de invasión normal
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
			// Sincronizar bandits en el primer Update después de cargar
			if (m_needsInitialSync)
			{
				m_needsInitialSync = false;
				SyncBanditsDrugTraffickerMode();
			}

			if (m_invasionCompleted)
				return;

			bool effectiveInvasionTime = CalculateEffectiveInvasionTime();

			// Si no hay guerra aceptada, desactivamos cualquier invasión activa
			if (!m_acceptedWar)
			{
				if (m_invasionActive)
				{
					m_invasionActive = false;
					SetAllBanditsDrugTraffickerMode(false);
				}
				m_wasEffectiveInvasionTime = effectiveInvasionTime;
				m_restoredFromSave = false;
				return;
			}

			// Si la guerra está aceptada y la invasión no está activa
			if (!m_invasionActive)
			{
				if (effectiveInvasionTime)
				{
					m_invasionActive = true;
					m_invasionStarted = true;
					m_spawnTimer = 0f;
					SetAllBanditsDrugTraffickerMode(true);
				}
			}

			// Rastrear si la Noche Verde estuvo activa durante esta invasión
			if (m_invasionActive && m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive)
			{
				m_greenNightWasActiveDuringInvasion = true;
			}

			// =====================================================
			// MANEJO DE FINALIZACIÓN
			// 
			// La invasión termina cuando el tiempo efectivo pasa de true a false.
			// Como IsInvasionTime() ahora usa Middawn (igual que GreenNightSky),
			// ambos eventos terminan exactamente al mismo tiempo:
			// - Individual: en Middawn
			// - Doble guerra: en Middawn (cuando GreenNightSky desactiva IsGreenNightActive
			//   y IsInvasionTime() también devuelve false)
			// =====================================================
			if (!m_restoredFromSave && m_wasEffectiveInvasionTime && !effectiveInvasionTime && m_invasionActive)
			{
				m_invasionActive = false;
				m_invasionCompleted = true;
				SetAllBanditsDrugTraffickerMode(false);
				InvasionCompleted?.Invoke();
			}

			// Limpiar flag de restauración después del primer frame
			m_restoredFromSave = false;

			m_wasEffectiveInvasionTime = effectiveInvasionTime;

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

		/// <summary>
		/// Determina si es hora de invasión NORMAL: desde DuskStart hasta Middawn.
		/// 
		/// IMPORTANTE: Usa Middawn en lugar de DawnStart para que la invasión
		/// termine EXACTAMENTE al mismo tiempo que la Noche Verde.
		/// 
		/// SubsystemGreenNightSky termina en Middawn:
		///   bool isEndMoment = Math.Abs(timeOfDay - middawn) < dawnTolerance;
		/// 
		/// Antes usaba DawnStart, lo cual causaba que la invasión individual
		/// terminara antes que la Noche Verde.
		/// </summary>
		private bool IsInvasionTime()
		{
			TimeOfDayMode mode = m_subsystemGameInfo.WorldSettings.TimeOfDayMode;

			// Modos donde NO hay invasión
			if (mode == TimeOfDayMode.Day || mode == TimeOfDayMode.Sunrise)
				return false;

			// Modos donde SÍ hay invasión
			if (mode == TimeOfDayMode.Night || mode == TimeOfDayMode.Sunset)
				return true;

			// Modo Changing: verificar si estamos entre Dusk y Middawn
			if (mode == TimeOfDayMode.Changing)
			{
				float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
				float duskStart = m_subsystemTimeOfDay.DuskStart;
				float middawn = m_subsystemTimeOfDay.Middawn;  // CAMBIO: Middawn en lugar de DawnStart

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

		private bool IsBanditTemplate(string name)
		{
			if (string.IsNullOrEmpty(name)) return false;
			return name.StartsWith("Bandit") && name != "FirearmsDealer";
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
