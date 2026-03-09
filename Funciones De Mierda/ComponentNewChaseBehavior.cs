using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewChaseBehavior : ComponentChaseBehavior, IUpdateable
	{
		// Referencias a subsistemas y componentes
		private ComponentNewHerdBehavior m_newHerdBehavior;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemGreenNightSky m_greenNightSky;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemParticles m_subsystemParticles;
		private IInventory m_inventory;
		private ComponentMiner m_componentMiner;
		private new Random m_random = new Random();

		// Configuración de defensa proactiva
		public float ProactiveDefenseRange { get; set; } = 20f;
		public float ProactiveDefenseInterval { get; set; } = 2f;
		public float GreenNightDefenseRangeMultiplier { get; set; } = 1.5f;
		public float GreenNightDefenseInterval { get; set; } = 1f;

		// Nuevo parámetro: habilidad para usar armas a distancia
		public bool CanUseRangedWeapons { get; set; }

		// Estado para combate a distancia
		private double m_rangedAttackStartTime;
		private double m_nextRangedAttackTime;
		private bool m_isAiming;
		private float m_aimProgress; // 0-1, usado para arco/ballesta
		private MusketBlock.LoadState m_musketLoadState;
		private bool m_musketHammerState;
		private ArrowBlock.ArrowType? m_loadedArrowType;

		private double m_nextProactiveCheckTime;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Obtener componentes y subsistemas necesarios
			m_newHerdBehavior = Entity.FindComponent<ComponentNewHerdBehavior>();
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_greenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>();
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_inventory = Entity.FindComponent<ComponentInventory>(); // Asumimos que tiene inventario
			m_componentMiner = Entity.FindComponent<ComponentMiner>();

			// Cargar el nuevo parámetro desde la base de datos
			CanUseRangedWeapons = valuesDictionary.GetValue<bool>("CanUseRangedWeapons");

			// Si la criatura pertenece a la manada del jugador, ajustar comportamiento por defecto
			if (IsPlayerHerd())
			{
				AttacksPlayer = false;
				AttacksNonPlayerCreature = true;
				m_autoChaseMask = CreatureCategory.LandPredator | CreatureCategory.LandOther |
								  CreatureCategory.WaterPredator | CreatureCategory.WaterOther |
								  CreatureCategory.Bird;
				m_chaseWhenAttackedProbability = 1f;
				m_chaseOnTouchProbability = 1f;
			}
		}

		public override void Update(float dt)
		{
			base.Update(dt);

			// Defensa proactiva solo para criaturas de la manada del jugador
			if (m_newHerdBehavior != null && IsPlayerHerd())
			{
				bool isGreenNight = m_greenNightSky != null && m_greenNightSky.IsGreenNightActive;
				float checkInterval = isGreenNight ? GreenNightDefenseInterval : ProactiveDefenseInterval;
				float range = isGreenNight ? ProactiveDefenseRange * GreenNightDefenseRangeMultiplier : ProactiveDefenseRange;

				if (m_subsystemTime.GameTime >= m_nextProactiveCheckTime)
				{
					m_nextProactiveCheckTime = m_subsystemTime.GameTime + checkInterval;
					PerformProactiveDefense(range, isGreenNight);
				}
			}

			// Manejar combate a distancia si está habilitado y hay un objetivo
			if (CanUseRangedWeapons)
			{
				HandleRangedCombat(dt);
			}
		}

		/// <summary>
		/// Lógica de combate a distancia: utiliza el arma del slot activo, apunta, dispara y recarga.
		/// </summary>
		private void HandleRangedCombat(float dt)
		{
			// Si no hay objetivo o la importancia es baja, dejar de apuntar
			if (m_target == null || m_importanceLevel <= 0f)
			{
				if (m_isAiming)
				{
					m_isAiming = false;
					UpdateAimAnimations(false, null);
				}
				return;
			}

			// Obtener el arma actual del slot activo
			int activeWeaponValue = m_componentMiner != null ? m_componentMiner.ActiveBlockValue : 0;

			// Si el arma actual no es un arma a distancia válida, no hacer nada
			if (!IsValidRangedWeapon(activeWeaponValue))
			{
				if (m_isAiming)
				{
					m_isAiming = false;
					UpdateAimAnimations(false, GetBlockFromValue(activeWeaponValue));
				}
				return;
			}

			// Verificar si el objetivo está dentro del alcance
			float range = 30f; // Rango máximo de disparo
			float distToTarget = Vector3.Distance(m_componentCreature.ComponentBody.Position, m_target.ComponentBody.Position);
			if (distToTarget > range)
			{
				if (m_isAiming)
				{
					m_isAiming = false;
					UpdateAimAnimations(false, GetBlockFromValue(activeWeaponValue));
				}
				return;
			}

			// Verificar línea de visión (raycast simple)
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = m_target.ComponentBody.BoundingBox.Center();
			var raycastResult = m_subsystemTerrain.Raycast(eyePos, targetPos, false, true, (int value, float d) => true);
			if (raycastResult.HasValue && raycastResult.Value.Distance < distToTarget)
			{
				if (m_isAiming)
				{
					m_isAiming = false;
					UpdateAimAnimations(false, GetBlockFromValue(activeWeaponValue));
				}
				return; // Hay obstáculo
			}

			// Obtener el tipo de arma
			Block block = GetBlockFromValue(activeWeaponValue);
			if (block == null) return;

			// Actualizar animaciones de apuntado según el estado actual
			UpdateAimAnimations(m_isAiming, block);

			// Si estamos en período de espera entre disparos, no hacer nada
			if (m_subsystemTime.GameTime < m_nextRangedAttackTime) return;

			// Comportamiento según el tipo de arma
			if (block is BowBlock)
			{
				HandleBowCombat(activeWeaponValue, dt);
			}
			else if (block is CrossbowBlock)
			{
				HandleCrossbowCombat(activeWeaponValue, dt);
			}
			else if (block is MusketBlock)
			{
				HandleMusketCombat(activeWeaponValue, dt);
			}
		}

		private Block GetBlockFromValue(int value)
		{
			if (value == 0) return null;
			int contents = Terrain.ExtractContents(value);
			return BlocksManager.Blocks[contents];
		}

		private void HandleBowCombat(int weaponValue, float dt)
		{
			int data = Terrain.ExtractData(weaponValue);
			int draw = BowBlock.GetDraw(data);
			ArrowBlock.ArrowType? arrowType = BowBlock.GetArrowType(data);

			// Si no hay flecha cargada, "cargar" una automáticamente
			if (arrowType == null)
			{
				arrowType = ArrowBlock.ArrowType.WoodenArrow;
				data = BowBlock.SetArrowType(data, arrowType);
				UpdateWeaponValue(weaponValue, data);
			}

			if (!m_isAiming)
			{
				// Empezar a apuntar
				m_isAiming = true;
				m_rangedAttackStartTime = m_subsystemTime.GameTime;
				m_aimProgress = 0f;
				UpdateAimAnimations(true, GetBlockFromValue(weaponValue));
			}

			// Incrementar el draw mientras se apunta
			float aimTime = (float)(m_subsystemTime.GameTime - m_rangedAttackStartTime);
			int newDraw = MathUtils.Min((int)(aimTime * 10f), 15);
			if (newDraw > draw)
			{
				draw = newDraw;
				data = BowBlock.SetDraw(data, draw);
				UpdateWeaponValue(weaponValue, data);
			}

			// Disparar cuando draw >= 15 o después de un tiempo mínimo
			if (draw >= 15 || aimTime >= 2.0f)
			{
				if (m_target != null)
				{
					FireBow(arrowType.Value, draw);
					// Desgastar el arma según el draw
					int damage = (draw >= 15) ? 2 : ((draw >= 4) ? 1 : 0);
					if (damage > 0 && m_componentMiner != null)
					{
						m_componentMiner.DamageActiveTool(damage);
					}
				}
				// Reiniciar estado
				m_isAiming = false;
				UpdateAimAnimations(false, GetBlockFromValue(weaponValue));
				m_nextRangedAttackTime = m_subsystemTime.GameTime + 1.0f;
				// Poner draw a 0 y quitar flecha
				data = BowBlock.SetDraw(data, 0);
				data = BowBlock.SetArrowType(data, null);
				UpdateWeaponValue(weaponValue, data);
			}
		}

		private void HandleCrossbowCombat(int weaponValue, float dt)
		{
			int data = Terrain.ExtractData(weaponValue);
			int draw = CrossbowBlock.GetDraw(data);
			ArrowBlock.ArrowType? arrowType = CrossbowBlock.GetArrowType(data);

			if (arrowType == null)
			{
				arrowType = ArrowBlock.ArrowType.IronBolt;
				data = CrossbowBlock.SetArrowType(data, arrowType);
				UpdateWeaponValue(weaponValue, data);
			}

			if (!m_isAiming)
			{
				m_isAiming = true;
				m_rangedAttackStartTime = m_subsystemTime.GameTime;
				UpdateAimAnimations(true, GetBlockFromValue(weaponValue));
			}

			float aimTime = (float)(m_subsystemTime.GameTime - m_rangedAttackStartTime);
			if (draw < 15)
			{
				int newDraw = MathUtils.Min((int)(aimTime * 5f), 15);
				if (newDraw > draw)
				{
					draw = newDraw;
					data = CrossbowBlock.SetDraw(data, draw);
					UpdateWeaponValue(weaponValue, data);
				}
			}

			if (draw == 15)
			{
				if (m_target != null)
				{
					FireCrossbow(arrowType.Value);
					if (m_componentMiner != null)
					{
						m_componentMiner.DamageActiveTool(1);
					}
				}
				m_isAiming = false;
				UpdateAimAnimations(false, GetBlockFromValue(weaponValue));
				m_nextRangedAttackTime = m_subsystemTime.GameTime + 1.5f;
				data = CrossbowBlock.SetDraw(data, 0);
				data = CrossbowBlock.SetArrowType(data, null);
				UpdateWeaponValue(weaponValue, data);
			}
		}

		private void HandleMusketCombat(int weaponValue, float dt)
		{
			int data = Terrain.ExtractData(weaponValue);
			MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
			bool hammerState = MusketBlock.GetHammerState(data);
			BulletBlock.BulletType? bulletType = MusketBlock.GetBulletType(data);

			if (!m_isAiming)
			{
				m_isAiming = true;
				m_rangedAttackStartTime = m_subsystemTime.GameTime;
				UpdateAimAnimations(true, GetBlockFromValue(weaponValue));
				// Amartillar el martillo si no lo está
				if (!hammerState)
				{
					hammerState = true;
					data = MusketBlock.SetHammerState(data, true);
					UpdateWeaponValue(weaponValue, data);
					m_subsystemAudio.PlaySound("Audio/HammerCock", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 3f, false);
				}
			}

			float aimTime = (float)(m_subsystemTime.GameTime - m_rangedAttackStartTime);

			// Simular carga automática
			if (loadState != MusketBlock.LoadState.Loaded)
			{
				if (loadState == MusketBlock.LoadState.Empty && aimTime > 1.0f)
				{
					loadState = MusketBlock.LoadState.Gunpowder;
					m_rangedAttackStartTime = m_subsystemTime.GameTime;
				}
				else if (loadState == MusketBlock.LoadState.Gunpowder && aimTime > 1.0f)
				{
					loadState = MusketBlock.LoadState.Wad;
					m_rangedAttackStartTime = m_subsystemTime.GameTime;
				}
				else if (loadState == MusketBlock.LoadState.Wad && aimTime > 1.0f)
				{
					loadState = MusketBlock.LoadState.Loaded;
					bulletType = BulletBlock.BulletType.MusketBall; // Por defecto, bala de mosquete
					m_rangedAttackStartTime = m_subsystemTime.GameTime;
				}

				data = MusketBlock.SetLoadState(data, loadState);
				data = MusketBlock.SetBulletType(data, bulletType);
				UpdateWeaponValue(weaponValue, data);
			}

			if (loadState == MusketBlock.LoadState.Loaded && hammerState && aimTime > 1.5f)
			{
				if (m_target != null)
				{
					FireMusket(bulletType.Value);
					if (m_componentMiner != null)
					{
						m_componentMiner.DamageActiveTool(1);
					}
				}
				m_isAiming = false;
				UpdateAimAnimations(false, GetBlockFromValue(weaponValue));
				m_nextRangedAttackTime = m_subsystemTime.GameTime + 2.0f;
				data = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Empty);
				data = MusketBlock.SetHammerState(data, false);
				data = MusketBlock.SetBulletType(data, null);
				UpdateWeaponValue(weaponValue, data);
				m_subsystemAudio.PlaySound("Audio/HammerRelease", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 3f, false);
			}
		}

		private void FireBow(ArrowBlock.ArrowType arrowType, int draw)
		{
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = m_target.ComponentBody.BoundingBox.Center();
			Vector3 direction = Vector3.Normalize(targetPos - eyePos);

			float num2 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
			float inaccuracy = (m_componentCreature.ComponentBody.IsCrouching ? 0.02f : 0.04f) + 0.25f * MathUtils.Saturate((float)(m_subsystemTime.GameTime - m_rangedAttackStartTime - 2.1f) / 5f);
			Vector3 noise = new Vector3(
				SimplexNoise.OctavedNoise(num2, 2f, 3, 2f, 0.5f, false),
				SimplexNoise.OctavedNoise(num2 + 100f, 2f, 3, 2f, 0.5f, false),
				SimplexNoise.OctavedNoise(num2 + 200f, 2f, 3, 2f, 0.5f, false)
			) * inaccuracy;
			direction = Vector3.Normalize(direction + noise);

			float speed = MathUtils.Lerp(0f, 28f, MathF.Pow((float)draw / 15f, 0.75f));
			int projectileValue = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<ArrowBlock>(), 0, ArrowBlock.SetArrowType(0, arrowType));
			Vector3 velocity = m_componentCreature.ComponentBody.Velocity + direction * speed;

			m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
			m_subsystemAudio.PlaySound("Audio/Bow", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 3f, true);
		}

		private void FireCrossbow(ArrowBlock.ArrowType arrowType)
		{
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = m_target.ComponentBody.BoundingBox.Center();
			Vector3 direction = Vector3.Normalize(targetPos - eyePos);

			float num2 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
			float inaccuracy = (m_componentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f) + 0.15f * MathUtils.Saturate((float)(m_subsystemTime.GameTime - m_rangedAttackStartTime - 2.5f) / 6f);
			Vector3 noise = new Vector3(
				SimplexNoise.OctavedNoise(num2, 2f, 3, 2f, 0.5f, false),
				SimplexNoise.OctavedNoise(num2 + 100f, 2f, 3, 2f, 0.5f, false),
				SimplexNoise.OctavedNoise(num2 + 200f, 2f, 3, 2f, 0.5f, false)
			) * inaccuracy;
			direction = Vector3.Normalize(direction + noise);

			float speed = 38f;
			int projectileValue = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<ArrowBlock>(), 0, ArrowBlock.SetArrowType(0, arrowType));
			Vector3 velocity = m_componentCreature.ComponentBody.Velocity + direction * speed;

			m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
			m_subsystemAudio.PlaySound("Audio/Bow", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 3f, true);
		}

		private void FireMusket(BulletBlock.BulletType bulletType)
		{
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = m_target.ComponentBody.BoundingBox.Center();
			Vector3 direction = Vector3.Normalize(targetPos - eyePos);

			float num2 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
			float inaccuracy = (m_componentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f) + 0.2f * MathUtils.Saturate((float)(m_subsystemTime.GameTime - m_rangedAttackStartTime - 2.5f) / 6f);
			Vector3 noise = new Vector3(
				SimplexNoise.OctavedNoise(num2, 2f, 3, 2f, 0.5f, false),
				SimplexNoise.OctavedNoise(num2 + 100f, 2f, 3, 2f, 0.5f, false),
				SimplexNoise.OctavedNoise(num2 + 200f, 2f, 3, 2f, 0.5f, false)
			) * inaccuracy;
			direction = Vector3.Normalize(direction + noise);

			// Determinar número de proyectiles y dispersión según el tipo de bala
			int projectileCount = 1;
			float speed = 80f;
			float spreadX = 0f;
			float spreadY = 0f;
			float spreadZ = 0f;
			BulletBlock.BulletType actualBulletType = bulletType;

			if (bulletType == BulletBlock.BulletType.Buckshot)
			{
				projectileCount = 8;
				spreadX = 0.04f;
				spreadY = 0.04f;
				spreadZ = 0.25f;
				actualBulletType = BulletBlock.BulletType.BuckshotBall; // Los perdigones usan BuckshotBall
			}
			else if (bulletType == BulletBlock.BulletType.BuckshotBall)
			{
				projectileCount = 1;
				spreadX = 0.06f;
				spreadY = 0.06f;
				spreadZ = 0f;
				actualBulletType = BulletBlock.BulletType.BuckshotBall;
			}
			else // MusketBall
			{
				projectileCount = 1;
				spreadX = 0f;
				spreadY = 0f;
				spreadZ = 0f;
				actualBulletType = BulletBlock.BulletType.MusketBall;
			}

			Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
			Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));

			for (int i = 0; i < projectileCount; i++)
			{
				Vector3 spread = new Vector3(
					m_random.Float(-spreadX, spreadX),
					m_random.Float(-spreadY, spreadY),
					m_random.Float(-spreadZ, spreadZ)
				);
				Vector3 finalDirection = Vector3.Normalize(direction + spread);
				Vector3 velocity = m_componentCreature.ComponentBody.Velocity + finalDirection * speed;

				int projectileValue = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<BulletBlock>(), 0, BulletBlock.SetBulletType(0, actualBulletType));
				m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
			}

			// Humo
			Vector3 smokePos = eyePos + 0.3f * direction;
			m_subsystemParticles.AddParticleSystem(new GunSmokeParticleSystem(m_subsystemTerrain, smokePos, direction), false);

			// Sonido
			m_subsystemAudio.PlaySound("Audio/MusketFire", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, true);

			// Retroceso
			m_componentCreature.ComponentBody.ApplyImpulse(-4f * direction);
		}

		/// <summary>
		/// Actualiza el valor del arma en el inventario (slot activo) con los nuevos datos.
		/// </summary>
		private void UpdateWeaponValue(int oldValue, int newData)
		{
			if (m_componentMiner == null || m_componentMiner.Inventory == null) return;
			int activeSlot = m_componentMiner.Inventory.ActiveSlotIndex;
			int newValue = Terrain.MakeBlockValue(Terrain.ExtractContents(oldValue), 0, newData);
			m_componentMiner.Inventory.RemoveSlotItems(activeSlot, 1);
			m_componentMiner.Inventory.AddSlotItems(activeSlot, newValue, 1);
		}

		/// <summary>
		/// Actualiza las animaciones del modelo según el estado de apuntado y el arma.
		/// </summary>
		private void UpdateAimAnimations(bool aiming, Block block)
		{
			var model = m_componentCreature.ComponentCreatureModel;
			if (aiming && block != null)
			{
				if (block is BowBlock)
				{
					model.AimHandAngleOrder = 1.2f;
					model.InHandItemOffsetOrder = Vector3.Zero;
					model.InHandItemRotationOrder = new Vector3(0f, -0.2f, 0f);
				}
				else if (block is CrossbowBlock)
				{
					model.AimHandAngleOrder = 1.3f;
					model.InHandItemOffsetOrder = new Vector3(-0.08f, -0.1f, 0.07f);
					model.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);
				}
				else if (block is MusketBlock)
				{
					model.AimHandAngleOrder = 1.4f;
					model.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					model.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
				}
			}
			else
			{
				model.AimHandAngleOrder = 0f;
				model.InHandItemOffsetOrder = Vector3.Zero;
				model.InHandItemRotationOrder = Vector3.Zero;
			}
		}

		/// <summary>
		/// Verifica si un valor de bloque corresponde a un arma a distancia soportada.
		/// </summary>
		private bool IsValidRangedWeapon(int value)
		{
			if (value == 0) return false;
			int contents = Terrain.ExtractContents(value);
			return contents == BlocksManager.GetBlockIndex<BowBlock>() ||
				   contents == BlocksManager.GetBlockIndex<CrossbowBlock>() ||
				   contents == BlocksManager.GetBlockIndex<MusketBlock>();
		}

		/// <summary>
		/// Determina si la criatura pertenece a la manada del jugador (nombre "player" o "guardian").
		/// </summary>
		private bool IsPlayerHerd()
		{
			if (m_newHerdBehavior == null) return false;
			string herdName = m_newHerdBehavior.HerdName;
			if (string.IsNullOrEmpty(herdName)) return false;
			return herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
				   herdName.IndexOf("guardian", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private void PerformProactiveDefense(float range, bool isGreenNight)
		{
			// Código existente sin cambios
			if (m_target != null && m_importanceLevel > 0f)
				return;

			var players = m_subsystemPlayers.ComponentPlayers;
			if (players.Count == 0)
				return;

			Vector3 myPos = m_componentCreature.ComponentBody.Position;
			ComponentPlayer nearestPlayer = null;
			float minDistToPlayer = float.MaxValue;
			foreach (var player in players)
			{
				if (player.ComponentHealth.Health <= 0f) continue;
				float dist = Vector3.Distance(myPos, player.ComponentBody.Position);
				if (dist < minDistToPlayer)
				{
					minDistToPlayer = dist;
					nearestPlayer = player;
				}
			}
			if (nearestPlayer == null)
				return;

			if (minDistToPlayer > range * 2)
				return;

			Vector3 playerPos = nearestPlayer.ComponentBody.Position;
			ComponentCreature bestThreat = null;
			float bestScore = 0f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(playerPos.X, playerPos.Z), range, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature candidate = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (candidate == null || candidate == m_componentCreature) continue;
				if (candidate.ComponentHealth.Health <= 0f) continue;

				if (!m_newHerdBehavior.ShouldAttackCreature(candidate))
					continue;

				float distToPlayer = Vector3.Distance(playerPos, candidate.ComponentBody.Position);
				if (distToPlayer > range) continue;

				float score = range - distToPlayer;

				ComponentChaseBehavior candidateChase = candidate.Entity.FindComponent<ComponentChaseBehavior>();
				if (candidateChase != null && candidateChase.Target != null)
				{
					ComponentCreature target = candidateChase.Target;
					if (target != null)
					{
						bool isTargetPlayer = target.Entity.FindComponent<ComponentPlayer>() != null;
						bool isTargetAlly = m_newHerdBehavior.IsSameHerdOrGuardian(target);
						if (isTargetPlayer || isTargetAlly)
						{
							score *= 2f;
						}
					}
				}

				if (isGreenNight && candidate.Entity.FindComponent<ComponentZombieChaseBehavior>() != null)
				{
					score *= 5f;
				}

				if (score > bestScore)
				{
					bestScore = score;
					bestThreat = candidate;
				}
			}

			if (bestThreat != null)
			{
				Attack(bestThreat, range, 60f, true);
				m_newHerdBehavior?.CallNearbyCreaturesHelp(bestThreat, range, 30f, true, true);
			}
		}

		public void RespondToCommandImmediately(ComponentCreature target)
		{
			if (target == null || m_newHerdBehavior == null || !m_newHerdBehavior.CanAttackCreature(target))
				return;

			Attack(target, 30f, 45f, true);
			m_newHerdBehavior.CallNearbyCreaturesHelp(target, 20f, 30f, false, true);
		}

		public override void Attack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (m_newHerdBehavior != null && !m_newHerdBehavior.CanAttackCreature(target))
				return;

			base.Attack(target, maxRange, maxChaseTime, isPersistent);
		}

		public override float ScoreTarget(ComponentCreature creature)
		{
			if (m_newHerdBehavior != null && !m_newHerdBehavior.CanAttackCreature(creature))
				return 0f;

			return base.ScoreTarget(creature);
		}

		public override ComponentCreature FindTarget()
		{
			ComponentCreature baseTarget = base.FindTarget();
			if (baseTarget != null && m_newHerdBehavior != null && !m_newHerdBehavior.CanAttackCreature(baseTarget))
				return null;

			return baseTarget;
		}
	}
}
