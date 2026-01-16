using System;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieChaseBehavior : ComponentBehavior, IUpdateable
	{
		public ComponentCreature Target
		{
			get
			{
				return this.m_target;
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override float ImportanceLevel
		{
			get
			{
				return this.m_importanceLevel;
			}
		}

		// Propiedades específicas para zombis
		public bool AttacksAllCategories { get; set; } = true;
		public bool AttacksSameHerd { get; set; } = false; // Los zombis NO atacan a su propia manada
		public string ZombieHerdName { get; set; } = "Zombie";

		public virtual void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent, bool isRetaliation = false)
		{
			bool suppressed = this.Suppressed;
			if (!suppressed)
			{
				// Verificar si es del mismo rebaño zombi
				ComponentZombieHerdBehavior thisHerd = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
				if (thisHerd != null && !string.IsNullOrEmpty(thisHerd.HerdName))
				{
					ComponentZombieHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (targetHerd != null && targetHerd.HerdName == thisHerd.HerdName)
					{
						if (!this.AttacksSameHerd)
						{
							return; // No atacar a miembros del mismo rebaño
						}
					}
				}

				// VERIFICACIÓN DE MODO DE JUEGO: Solo atacar en Survival, Challenging o Cruel (o si es retaliación)
				bool isPlayer = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
				if (isPlayer && !isRetaliation) // Solo aplicar restricción si NO es retaliación
				{
					GameMode currentGameMode = this.m_subsystemGameInfo.WorldSettings.GameMode;
					if (currentGameMode == GameMode.Creative || currentGameMode == GameMode.Harmless)
					{
						return; // No atacar jugadores en Creative o Harmless (solo si no es retaliación)
					}
				}

				this.m_target = componentCreature;
				this.m_nextUpdateTime = 0.0;
				this.m_range = maxRange;
				this.m_chaseTime = maxChaseTime;
				this.m_isPersistent = isPersistent;
				this.m_importanceLevel = (isPersistent ? this.ImportanceLevelPersistent : this.ImportanceLevelNonPersistent);

				// CORRECCIÓN: Asegurar que se active el comportamiento de persecución
				this.IsActive = true;
				this.m_stateMachine.TransitionTo("Chasing");

				// CORRECCIÓN: Actualizar inmediatamente el estado de persecución
				if (this.m_target != null && this.m_componentPathfinding != null)
				{
					this.m_componentPathfinding.Stop();
					this.UpdateChasingStateImmediately();
				}
			}
		}

		// CORRECCIÓN: Añadir método faltante para actualizar persecución inmediata
		private void UpdateChasingStateImmediately()
		{
			if (this.m_target == null || !this.IsActive)
				return;
			Vector3 targetPosition = this.m_target.ComponentBody.Position;
			this.m_componentPathfinding.SetDestination(new Vector3?(targetPosition), 1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
			this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
		}

		public virtual void StopAttack()
		{
			this.m_stateMachine.TransitionTo("LookingForTarget");
			this.IsActive = false;
			this.m_target = null;
			this.m_nextUpdateTime = 0.0;
			this.m_range = 0f;
			this.m_chaseTime = 0f;
			this.m_isPersistent = false;
			this.m_importanceLevel = 0f;
		}

		public virtual void Update(float dt)
		{
			bool suppressed = this.Suppressed;
			if (suppressed)
			{
				this.StopAttack();
			}
			this.m_autoChaseSuppressionTime -= dt;

			// Lógica de armas de distancia para zombis
			bool hasRangedWeapon = this.HasActiveRangedWeaponComponent();
			if (hasRangedWeapon && this.m_target != null)
			{
				this.UpdateRangedWeaponLogic(dt);
			}

			bool flag = this.IsActive && this.m_target != null;
			if (flag)
			{
				this.m_chaseTime -= dt;
				this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);

				// Lógica de ataque con armas de distancia
				if (this.m_attackMode != AttackMode.OnlyHand)
				{
					float num2;
					ComponentBody hitBody = this.GetHitBody1(this.m_target.ComponentBody, out num2);
					bool flag3 = hitBody != null && num2 > 5f && this.FindAimTool(this.m_componentMiner);
					if (flag3)
					{
						Vector3 vector = Vector3.Normalize(this.m_target.ComponentCreatureModel.EyePosition - this.m_componentCreature.ComponentCreatureModel.EyePosition);
						this.m_chaseTime = Math.Max(this.m_chaseTime, this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f);
						bool flag4 = num2 >= this.m_attackRange.X && num2 <= this.m_attackRange.Y;
						if (flag4)
						{
							this.ProcessRangedAttack(vector, num2);
							return;
						}
					}
				}

				// Lógica de ataque cuerpo a cuerpo
				bool flag11 = this.IsTargetInAttackRange(this.m_target.ComponentBody);
				if (flag11)
				{
					this.m_componentCreatureModel.AttackOrder = true;
					bool flag12 = this.m_attackMode != AttackMode.OnlyHand && !this.HasActiveRangedWeaponComponent();
					if (flag12)
					{
						this.FindHitTool(this.m_componentMiner);
					}
				}
				bool isAttackHitMoment = this.m_componentCreatureModel.IsAttackHitMoment;
				if (isAttackHitMoment)
				{
					Vector3 hitPoint;
					ComponentBody hitBody2 = this.GetHitBody(this.m_target.ComponentBody, out hitPoint);
					bool flag13 = hitBody2 != null;
					if (flag13)
					{
						float x = this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f;
						this.m_chaseTime = MathUtils.Max(this.m_chaseTime, x);
						this.m_componentMiner.Hit(hitBody2, hitPoint, this.m_componentCreature.ComponentBody.Matrix.Forward);
						this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
					}
				}
			}
			bool flag14 = this.m_subsystemTime.GameTime >= this.m_nextUpdateTime;
			if (flag14)
			{
				this.m_dt = this.m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(this.m_subsystemTime.GameTime - this.m_nextUpdateTime), 0.1f);
				this.m_nextUpdateTime = this.m_subsystemTime.GameTime + (double)this.m_dt;
				this.m_stateMachine.Update();
			}
		}

		private void ProcessRangedAttack(Vector3 direction, float distance)
		{
			float num = (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative) ? 2.5f : 3f;
			bool flag5 = (double)Vector3.Dot(this.m_componentCreature.ComponentBody.Matrix.Forward, direction) > 0.8;
			bool flag6 = !flag5;
			if (flag6)
			{
				this.m_componentPathfinding.SetDestination(new Vector3?(this.m_target.ComponentCreatureModel.EyePosition), 1f, 1f, 0, false, true, false, null);
			}
			else
			{
				this.m_componentPathfinding.Destination = null;
			}
			string category = BlocksManager.Blocks[Terrain.ExtractContents(this.m_componentMiner.ActiveBlockValue)].GetCategory(this.m_componentMiner.ActiveBlockValue);
			bool flag7 = this.m_subsystemTime.GameTime - this.m_lastActionTime > (double)num;
			if (flag7)
			{
				bool flag8 = this.m_componentMiner.Use(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, direction));
				if (flag8)
				{
					this.m_lastActionTime = this.m_subsystemTime.GameTime;
				}
				else
				{
					bool flag9 = flag5;
					if (flag9)
					{
						this.m_componentMiner.Aim(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, direction), AimState.Completed);
						this.m_lastActionTime = this.m_subsystemTime.GameTime;
					}
				}
			}
			else
			{
				bool flag10 = flag5;
				if (flag10)
				{
					this.m_componentMiner.Aim(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, direction), AimState.InProgress);
				}
			}
		}

		private void UpdateRangedWeaponLogic(float dt)
		{
			if (this.m_target == null || !this.IsActive)
				return;

			bool hasRangedWeapon = this.HasActiveRangedWeaponComponent();
			if (!hasRangedWeapon)
			{
				this.FindAimTool(this.m_componentMiner);
				hasRangedWeapon = this.HasActiveRangedWeaponComponent();
			}

			// *** MODIFICACIÓN PARA FLAMETHROWER ***
			// Verificar específicamente si el lanzallamas necesita recarga
			if (this.m_componentMiner != null && this.m_componentMiner.ActiveBlockValue != 0)
			{
				int blockId = Terrain.ExtractContents(this.m_componentMiner.ActiveBlockValue);
				if (blockId == BlocksManager.GetBlockIndex(typeof(FlameThrowerBlock), true, false))
				{
					int data = Terrain.ExtractData(this.m_componentMiner.ActiveBlockValue);
					int loadCount = FlameThrowerBlock.GetLoadCount(this.m_componentMiner.ActiveBlockValue);
					FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
					FlameBulletBlock.FlameBulletType? bulletType = FlameThrowerBlock.GetBulletType(data);

					// DETECCIÓN ESPECIAL: Si el lanzallamas está vacío o necesita inicialización
					if ((loadState == FlameThrowerBlock.LoadState.Empty || loadCount <= 0) &&
						!bulletType.HasValue)
					{
						// Esto indica que es la primera vez que la criatura usa este lanzallamas
						// Forzar recarga con tipo aleatorio
						this.FindAimTool(this.m_componentMiner);
						hasRangedWeapon = this.HasActiveRangedWeaponComponent();
					}
					else if (loadState == FlameThrowerBlock.LoadState.Empty || loadCount <= 0)
					{
						// Solo recarga normal (mantiene tipo existente)
						this.FindAimTool(this.m_componentMiner);
						hasRangedWeapon = this.HasActiveRangedWeaponComponent();
					}
				}
			}
			// *** FIN DE MODIFICACIÓN ***

			if (hasRangedWeapon && this.m_target != null)
			{
				float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_target.ComponentBody.Position);

				// MEJORA DE PUNTERÍA: Cálculo más preciso
				Vector3 eyePosition = this.m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPosition = this.m_target.ComponentCreatureModel.EyePosition;

				// Ajuste vertical basado en distancia
				float verticalAdjustment = MathUtils.Lerp(1f, 3f, distance / 25f);
				Vector3 adjustedTargetPosition = targetPosition + new Vector3(0f, verticalAdjustment, 0f);

				Vector3 direction = Vector3.Normalize(adjustedTargetPosition - eyePosition);

				// Ajustar dirección para armas horizontales (excepto arco)
				int activeBlockValue = this.m_componentMiner.ActiveBlockValue;
				if (activeBlockValue != 0)
				{
					Block activeBlock = BlocksManager.Blocks[Terrain.ExtractContents(activeBlockValue)];

					// Para armas que deben estar horizontales, ajustar la dirección
					if (activeBlock is MusketBlock || activeBlock is CrossbowBlock ||
						activeBlock is RepeatCrossbowBlock || activeBlock is FlameThrowerBlock ||
						activeBlock is ItemsLauncherBlock)
					{
						// Reducir componente vertical para mantener el arma más horizontal
						direction = new Vector3(direction.X, direction.Y * 0.3f, direction.Z);
						direction = Vector3.Normalize(direction);
					}
				}

				if (distance >= this.m_attackRange.X && distance <= this.m_attackRange.Y)
				{
					float rayDistance;
					ComponentBody hitBody = this.GetHitBody1(this.m_target.ComponentBody, out rayDistance);

					// MEJORA: Tolerancia aumentada para puntería más precisa
					if (hitBody != null && Math.Abs(rayDistance - distance) < 3f)
					{
						float actionDelay = (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative) ? 2.5f : 3f;
						if (this.m_subsystemTime.GameTime - this.m_lastActionTime > actionDelay)
						{
							// Puntería más recta y precisa
							Vector3 preciseDirection = Vector3.Normalize(targetPosition - eyePosition);

							// Ajustar dirección final basada en el tipo de arma
							if (activeBlockValue != 0)
							{
								Block activeBlock = BlocksManager.Blocks[Terrain.ExtractContents(activeBlockValue)];
								if (activeBlock is MusketBlock || activeBlock is CrossbowBlock ||
									activeBlock is RepeatCrossbowBlock || activeBlock is FlameThrowerBlock ||
									activeBlock is ItemsLauncherBlock)
								{
									preciseDirection = new Vector3(preciseDirection.X, preciseDirection.Y * 0.4f, preciseDirection.Z);
									preciseDirection = Vector3.Normalize(preciseDirection);
								}
							}

							preciseDirection = preciseDirection + this.m_random.Vector3(0.02f);
							preciseDirection = Vector3.Normalize(preciseDirection);

							this.m_componentMiner.Aim(new Ray3(eyePosition, preciseDirection), AimState.Completed);
							this.m_lastActionTime = this.m_subsystemTime.GameTime;
							this.m_chaseTime = Math.Max(this.m_chaseTime, this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f);
						}
						else
						{
							this.m_componentMiner.Aim(new Ray3(eyePosition, direction), AimState.InProgress);
						}
						this.m_componentPathfinding.Destination = null;
					}
					else
					{
						this.m_componentPathfinding.SetDestination(
							new Vector3?(this.m_target.ComponentBody.Position),
							1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
					}
				}
				else if (distance > this.m_attackRange.Y)
				{
					this.m_componentPathfinding.SetDestination(
						new Vector3?(this.m_target.ComponentBody.Position),
						1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
				}
				else if (distance < this.m_attackRange.X)
				{
					Vector3 retreatDirection = Vector3.Normalize(this.m_componentCreature.ComponentBody.Position - this.m_target.ComponentBody.Position);
					Vector3 retreatPosition = this.m_componentCreature.ComponentBody.Position + retreatDirection * 3f;
					this.m_componentPathfinding.SetDestination(new Vector3?(retreatPosition), 1f, 1f, 0, false, true, false, null);
				}
			}
		}

		// RESTAURADO: Método para verificar si hay un arma de fuego activa
		private bool IsFirearmActive()
		{
			if (this.m_componentMiner == null || this.m_componentMiner.ActiveBlockValue == 0)
				return false;
			int blockId = Terrain.ExtractContents(this.m_componentMiner.ActiveBlockValue);
			return blockId == BlocksManager.GetBlockIndex(typeof(AKBlock), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(G3Block), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(Izh43Block), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(M4Block), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(Mac10Block), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(SWM500Block), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(UziBlock), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(AUGBlock), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(P90Block), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(SCARBlock), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(RevolverBlock), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(FamasBlock), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(AA12Block), true, false) ||
		blockId == BlocksManager.GetBlockIndex(typeof(M249Block), true, false) ||
		blockId == BlocksManager.GetBlockIndex(typeof(NewG3Block), true, false) ||
		blockId == BlocksManager.GetBlockIndex(typeof(MP5SSDBlock), true, false) ||
		blockId == BlocksManager.GetBlockIndex(typeof(MendozaBlock), true, false) ||
		blockId == BlocksManager.GetBlockIndex(typeof(GrozaBlock), true, false);
		}

		// RESTAURADO: Método para verificar si un bloque es un arma de fuego
		private bool IsFirearmBlock(int blockValue)
		{
			int blockId = Terrain.ExtractContents(blockValue);
			return blockId == BlocksManager.GetBlockIndex(typeof(AKBlock), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(G3Block), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(Izh43Block), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(M4Block), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(Mac10Block), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(SWM500Block), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(UziBlock), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(AUGBlock), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(P90Block), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(SCARBlock), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(RevolverBlock), true, false) ||
				   blockId == BlocksManager.GetBlockIndex(typeof(FamasBlock), true, false) ||
				blockId == BlocksManager.GetBlockIndex(typeof(AA12Block), true, false) ||
		blockId == BlocksManager.GetBlockIndex(typeof(M249Block), true, false) ||
		blockId == BlocksManager.GetBlockIndex(typeof(NewG3Block), true, false) ||
		blockId == BlocksManager.GetBlockIndex(typeof(MP5SSDBlock), true, false) ||
		blockId == BlocksManager.GetBlockIndex(typeof(MendozaBlock), true, false) ||
		blockId == BlocksManager.GetBlockIndex(typeof(GrozaBlock), true, false);
		}

		// CORREGIDO: Método para verificar si hay un arma de distancia activa (incluye armas de fuego)
		private bool HasActiveRangedWeaponComponent()
		{
			// Primero verificar armas de fuego
			if (this.IsFirearmActive())
				return true;

			// Luego verificar otras armas de distancia
			ComponentMusketShooterBehavior musket = base.Entity.FindComponent<ComponentMusketShooterBehavior>();
			ComponentBowShooterBehavior bow = base.Entity.FindComponent<ComponentBowShooterBehavior>();
			ComponentCrossbowShooterBehavior crossbow = base.Entity.FindComponent<ComponentCrossbowShooterBehavior>();
			ComponentFlameThrowerShooterBehavior flamethrower = base.Entity.FindComponent<ComponentFlameThrowerShooterBehavior>();
			ComponentItemsLauncherShooterBehavior launcher = base.Entity.FindComponent<ComponentItemsLauncherShooterBehavior>();

			return (musket != null && musket.IsActive && this.m_target != null) ||
				   (bow != null && bow.IsActive && this.m_target != null) ||
				   (crossbow != null && crossbow.IsActive && this.m_target != null) ||
				   (flamethrower != null && flamethrower.IsActive && this.m_target != null) ||
				   (launcher != null && launcher.IsActive && this.m_target != null);
		}

		// RESTAURADO: Método para obtener número de balas de un arma de fuego
		private int GetFirearmBulletNum(int slotValue)
		{
			int blockId = Terrain.ExtractContents(slotValue);
			int data = Terrain.ExtractData(slotValue);
			if (blockId == BlocksManager.GetBlockIndex(typeof(AKBlock), true, false)) return AKBlock.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(G3Block), true, false)) return G3Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(Izh43Block), true, false)) return Izh43Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(M4Block), true, false)) return M4Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(Mac10Block), true, false)) return Mac10Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false)) return MinigunBlock.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false)) return SPAS12Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(SWM500Block), true, false)) return SWM500Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(UziBlock), true, false)) return UziBlock.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(AUGBlock), true, false)) return AUGBlock.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(P90Block), true, false)) return P90Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(SCARBlock), true, false)) return SCARBlock.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(RevolverBlock), true, false)) return RevolverBlock.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(FamasBlock), true, false)) return FamasBlock.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(AA12Block), true, false)) return AA12Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(M249Block), true, false)) return M249Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(NewG3Block), true, false)) return NewG3Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(MP5SSDBlock), true, false)) return MP5SSDBlock.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(MendozaBlock), true, false)) return MendozaBlock.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(GrozaBlock), true, false)) return GrozaBlock.GetBulletNum(data);
			return 0;
		}

		// RESTAURADO: Método para recargar un arma de fuego
		private void ReloadFirearm(ComponentMiner componentMiner, int slotIndex, int slotValue)
		{
			int blockId = Terrain.ExtractContents(slotValue);
			int data = Terrain.ExtractData(slotValue);
			int maxCapacity = 0;
			if (blockId == BlocksManager.GetBlockIndex(typeof(AKBlock), true, false)) maxCapacity = 30;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(G3Block), true, false)) maxCapacity = 30;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(Izh43Block), true, false)) maxCapacity = 2;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(M4Block), true, false)) maxCapacity = 22;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(Mac10Block), true, false)) maxCapacity = 30;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false)) maxCapacity = 100;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false)) maxCapacity = 8;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(SWM500Block), true, false)) maxCapacity = 5;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(UziBlock), true, false)) maxCapacity = 30;
			// Nuevas armas añadidas
			else if (blockId == BlocksManager.GetBlockIndex(typeof(AUGBlock), true, false)) maxCapacity = 30;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(P90Block), true, false)) maxCapacity = 50;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(SCARBlock), true, false)) maxCapacity = 30;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(RevolverBlock), true, false)) maxCapacity = 6;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(FamasBlock), true, false)) maxCapacity = 30;

			if (maxCapacity > 0)
			{
				if (blockId == BlocksManager.GetBlockIndex(typeof(AKBlock), true, false)) data = AKBlock.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(G3Block), true, false)) data = G3Block.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(Izh43Block), true, false)) data = Izh43Block.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(M4Block), true, false)) data = M4Block.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(Mac10Block), true, false)) data = Mac10Block.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false)) data = MinigunBlock.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false)) data = SPAS12Block.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(SWM500Block), true, false)) data = SWM500Block.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(UziBlock), true, false)) data = UziBlock.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(AUGBlock), true, false)) data = AUGBlock.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(P90Block), true, false)) data = P90Block.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(SCARBlock), true, false)) data = SCARBlock.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(RevolverBlock), true, false)) data = RevolverBlock.SetBulletNum(data, maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(FamasBlock), true, false)) data = FamasBlock.SetBulletNum(data, maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(AA12Block), true, false)) data = AA12Block.SetBulletNum(data, maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(M249Block), true, false)) data = M249Block.SetBulletNum(data, maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(NewG3Block), true, false)) data = NewG3Block.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(MP5SSDBlock), true, false)) data = MP5SSDBlock.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(MendozaBlock), true, false)) data = MendozaBlock.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(GrozaBlock), true, false)) data = GrozaBlock.SetBulletNum(maxCapacity);

				int value2 = Terrain.MakeBlockValue(blockId, 0, data);
				componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				componentMiner.Inventory.AddSlotItems(slotIndex, value2, 1);

				if (this.m_subsystemAudio != null)
				{
					this.m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, this.m_random.Float(-0.1f, 0.1f), this.m_componentCreature.ComponentCreatureModel.EyePosition, 5f, true);
				}
			}
		}

		// Métodos auxiliares con implementaciones completas
		public ComponentBody GetHitBody1(ComponentBody target, out float distance)
		{
			distance = 0f;
			if (target == null || this.m_subsystemBodies == null)
			{
				return null;
			}

			Vector3 eyePosition = this.m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 v = Vector3.Normalize(target.BoundingBox.Center() - eyePosition);
			BodyRaycastResult? bodyRaycastResult = this.m_subsystemBodies.Raycast(eyePosition, eyePosition + v * this.m_attackRange.Y, 0.35f,
				(ComponentBody body, float dist) => body.Entity != base.Entity && !body.IsChildOfBody(this.m_componentCreature.ComponentBody) && !this.m_componentCreature.ComponentBody.IsChildOfBody(body));

			TerrainRaycastResult? terrainRaycastResult = null;
			if (this.m_componentMiner != null && this.m_componentMiner.m_subsystemTerrain != null)
			{
				terrainRaycastResult = this.m_componentMiner.m_subsystemTerrain.Raycast(eyePosition, eyePosition + v * this.m_attackRange.Y, true, true,
					(int value, float dist) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable);
			}

			distance = ((bodyRaycastResult != null) ? bodyRaycastResult.GetValueOrDefault().Distance : float.PositiveInfinity);

			if (this.m_componentMiner.Inventory != null && bodyRaycastResult != null)
			{
				if (terrainRaycastResult != null && (double)terrainRaycastResult.Value.Distance < (double)bodyRaycastResult.Value.Distance)
				{
					return null;
				}

				if (bodyRaycastResult.Value.ComponentBody == target ||
					bodyRaycastResult.Value.ComponentBody.IsChildOfBody(target) ||
					target.IsChildOfBody(bodyRaycastResult.Value.ComponentBody) ||
					target.StandingOnBody == bodyRaycastResult.Value.ComponentBody)
				{
					return bodyRaycastResult.Value.ComponentBody;
				}
			}

			return null;
		}

		private TerrainRaycastResult? PickTerrain(Vector3 position, Vector3 direction, float reach)
		{
			if (this.m_componentMiner == null || this.m_componentMiner.m_subsystemTerrain == null)
			{
				return null;
			}

			direction = Vector3.Normalize(direction);
			Vector3 creaturePosition = this.m_componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
			Vector3 end = position + direction * reach;

			return this.m_componentMiner.m_subsystemTerrain.Raycast(position, end, true, true,
				(int value, float distance) => (double)Vector3.Distance(position + distance * direction, creaturePosition) <= (double)reach &&
											   BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable);
		}

		private BodyRaycastResult? PickBody(Vector3 position, Vector3 direction, float reach)
		{
			if (this.m_subsystemBodies == null)
			{
				return null;
			}

			direction = Vector3.Normalize(direction);
			Vector3 creaturePosition = this.m_componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
			Vector3 end = position + direction * reach;

			return this.m_subsystemBodies.Raycast(position, end, 0.35f,
				(ComponentBody body, float distance) => (double)Vector3.Distance(position + distance * direction, creaturePosition) <= (double)reach &&
														body.Entity != this.Entity &&
														!body.IsChildOfBody(this.m_componentMiner.ComponentCreature.ComponentBody) &&
														!this.m_componentMiner.ComponentCreature.ComponentBody.IsChildOfBody(body));
		}

		// RESTAURADO: Método para encontrar herramienta de puntería (incluye armas de fuego)
		public bool FindAimTool(ComponentMiner componentMiner)
		{
			if (componentMiner.Inventory == null)
			{
				return false;
			}

			int activeSlotIndex = componentMiner.Inventory.ActiveSlotIndex;
			int activeBlockValue = componentMiner.ActiveBlockValue;
			int num = Terrain.ExtractContents(activeBlockValue);
			Block block = BlocksManager.Blocks[num];

			// RESTAURADO: Verificar armas de fuego
			bool isFirearm = this.IsFirearmActive();

			if (block.IsAimable_(activeBlockValue) || isFirearm)
			{
				// *** MODIFICACIÓN PARA FLAMETHROWER ***
				if (block is FlameThrowerBlock)
				{
					int data = Terrain.ExtractData(activeBlockValue);
					FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
					int loadCount = FlameThrowerBlock.GetLoadCount(activeBlockValue);
					FlameBulletBlock.FlameBulletType? bulletType = FlameThrowerBlock.GetBulletType(data);

					// Verificar si necesita inicialización (primera vez)
					bool needsInitialization = (loadState == FlameThrowerBlock.LoadState.Empty || loadCount <= 0) &&
											  !bulletType.HasValue;

					if (needsInitialization || !this.IsReady(activeBlockValue))
					{
						// Si necesita inicialización o no está listo, recargar
						this.HandleComplexAimTool(componentMiner, activeSlotIndex);
					}
					return true;
				}
				else
				{
					bool flag4 = this.IsAimToolNeedToReady(componentMiner, activeSlotIndex);
					if (flag4)
					{
						this.HandleComplexAimTool(componentMiner, activeSlotIndex);
					}
					return true;
				}
			}

			for (int i = 0; i < componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = componentMiner.Inventory.GetSlotValue(i);
				int num2 = Terrain.ExtractContents(slotValue);
				Block block2 = BlocksManager.Blocks[num2];

				// RESTAURADO: Incluir armas de fuego en la búsqueda
				bool flag6 = (block2.IsAimable_(slotValue) || this.IsFirearmBlock(slotValue)) &&
							(!(block2 is FlameThrowerBlock) || this.IsReady(slotValue));

				if (flag6)
				{
					componentMiner.Inventory.ActiveSlotIndex = i;
					return true;
				}
			}

			return false;
		}

		public bool IsReady(int slotValue)
		{
			int data = Terrain.ExtractData(slotValue);
			// Modificación: Mismo comportamiento que NewChaseBehavior
			return !(BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is FlameThrowerBlock) || 
				   (FlameThrowerBlock.GetLoadState(data) == FlameThrowerBlock.LoadState.Loaded && 
					FlameThrowerBlock.GetBulletType(data) != null);
		}

		// RESTAURADO: Método para verificar si una herramienta de puntería necesita prepararse (incluye armas de fuego)
		public bool IsAimToolNeedToReady(ComponentMiner componentMiner, int slotIndex)
		{
			int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];

			// RESTAURADO: ItemsLauncher siempre está listo para criaturas
			if (block is ItemsLauncherBlock) return false;

			// *** MODIFICACIÓN PARA FLAMETHROWER ***
			if (block is FlameThrowerBlock)
			{
				return !this.IsReady(slotValue);
			}

			if (!(block is BowBlock))
			{
				if (!(block is CrossbowBlock))
				{
					if (!(block is RepeatCrossbowBlock))
					{
						if (!(block is MusketBlock))
						{
							// RESTAURADO: Lógica para armas de fuego
							if (this.IsFirearmBlock(slotValue))
							{
								int bulletNum = this.GetFirearmBulletNum(slotValue);
								if (bulletNum <= 0)
								{
									this.ReloadFirearm(componentMiner, slotIndex, slotValue);
									return true;
								}
								return false;
							}
							return false;
						}

						// RESTAURADO: Lógica para Mosquete
						bool flag5 = MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded && MusketBlock.GetBulletType(data) != null;
						if (flag5) return false;
					}
					else
					{
						// RESTAURADO: Lógica para Ballesta repetidora
						bool flag6 = RepeatCrossbowBlock.GetDraw(data) >= 15 && RepeatCrossbowBlock.GetArrowType(data) != null;
						if (flag6) return false;
					}
				}
				else
				{
					// RESTAURADO: Lógica para Ballesta
					bool flag7 = CrossbowBlock.GetDraw(data) >= 15 && CrossbowBlock.GetArrowType(data) != null;
					if (flag7) return false;
				}
			}
			else
			{
				// RESTAURADO: Lógica para Arco
				bool flag8 = BowBlock.GetDraw(data) >= 15 && BowBlock.GetArrowType(data) != null;
				if (flag8) return false;
			}
			return true;
		}

		// RESTAURADO: Método para manejar herramientas de puntería complejas (incluye armas de fuego)
		public void HandleComplexAimTool(ComponentMiner componentMiner, int slotIndex)
		{
			int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			int num2 = Terrain.ExtractContents(slotValue);
			Block block = BlocksManager.Blocks[num2];

			// *** MODIFICACIÓN PARA FLAMETHROWER ***
			// Manejo especial para FlameThrowerBlock
			if (block is FlameThrowerBlock)
			{
				// Obtener el tipo de bala actual y el estado de carga
				FlameBulletBlock.FlameBulletType? currentType = FlameThrowerBlock.GetBulletType(data);
				FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
				int loadCount = FlameThrowerBlock.GetLoadCount(slotValue);

				// Determinar el tipo de bala
				FlameBulletBlock.FlameBulletType bulletType;

				// SOLO ASIGNAR NUEVO TIPO SI ESTÁ VACÍO Y NO TIENE TIPO DEFINIDO
				if (currentType.HasValue)
				{
					// Si ya tiene un tipo, mantenerlo siempre
					bulletType = currentType.Value;
				}
				else if (loadState == FlameThrowerBlock.LoadState.Empty || loadCount <= 0)
				{
					// Solo si está realmente vacío y sin tipo definido, asignar ALEATORIO (50/50)
					bulletType = (this.m_random.Bool()) ?
						FlameBulletBlock.FlameBulletType.Flame :
						FlameBulletBlock.FlameBulletType.Poison;
				}
				else
				{
					// Si tiene carga pero no tiene tipo (caso raro), asignar aleatorio
					bulletType = (this.m_random.Bool()) ?
						FlameBulletBlock.FlameBulletType.Flame :
						FlameBulletBlock.FlameBulletType.Poison;
				}

				// Establecer tipo de bala, estado cargado y carga completa (15)
				data = FlameThrowerBlock.SetBulletType(data, bulletType);
				data = FlameThrowerBlock.SetLoadState(data, FlameThrowerBlock.LoadState.Loaded);
				int newValue = Terrain.ReplaceData(slotValue, data);
				newValue = FlameThrowerBlock.SetLoadCount(newValue, 15);

				// Actualizar el inventario
				componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				componentMiner.Inventory.AddSlotItems(slotIndex, newValue, 1);

				return;
			}

			// RESTAURADO: Lógica para armas de fuego
			if (this.IsFirearmBlock(slotValue))
			{
				int bulletNum = this.GetFirearmBulletNum(slotValue);
				if (bulletNum <= 0)
				{
					this.ReloadFirearm(componentMiner, slotIndex, slotValue);
				}
				return;
			}

			if (!(block is ItemsLauncherBlock))
			{
				if (!(block is BowBlock))
				{
					if (!(block is CrossbowBlock))
					{
						if (!(block is RepeatCrossbowBlock))
						{
							if (block is MusketBlock)
							{
								data = MusketBlock.SetLoadState(MusketBlock.SetBulletType(0, new BulletBlock.BulletType?(BulletBlock.BulletType.MusketBall)), MusketBlock.LoadState.Loaded);
							}
						}
						else
						{
							float randomValue = this.m_random.Float(0f, 1f);
							RepeatArrowBlock.ArrowType value;
							if (randomValue < 0.166f) value = RepeatArrowBlock.ArrowType.ExplosiveArrow;
							else if (randomValue < 0.332f) value = RepeatArrowBlock.ArrowType.PoisonArrow;
							else if (randomValue < 0.498f) value = RepeatArrowBlock.ArrowType.CopperArrow;
							else if (randomValue < 0.664f) value = RepeatArrowBlock.ArrowType.DiamondArrow;
							else if (randomValue < 0.83f) value = RepeatArrowBlock.ArrowType.SeriousPoisonArrow;
							else value = RepeatArrowBlock.ArrowType.IronArrow;

							data = RepeatCrossbowBlock.SetArrowType(data, new RepeatArrowBlock.ArrowType?(value));
							data = RepeatCrossbowBlock.SetDraw(data, 15);
						}
					}
					else
					{
						float randomValue = this.m_random.Float(0f, 1f);
						ArrowBlock.ArrowType value;
						if (randomValue < 0.333f) value = ArrowBlock.ArrowType.IronBolt;
						else if (randomValue < 0.666f) value = ArrowBlock.ArrowType.DiamondBolt;
						else value = ArrowBlock.ArrowType.ExplosiveBolt;

						data = CrossbowBlock.SetArrowType(0, new ArrowBlock.ArrowType?(value));
						data = CrossbowBlock.SetDraw(data, 15);
					}
				}
				else
				{
					float randomValue = this.m_random.Float(0f, 1f);
					ArrowBlock.ArrowType value;
					if (randomValue < 0.166f) value = ArrowBlock.ArrowType.WoodenArrow;
					else if (randomValue < 0.332f) value = ArrowBlock.ArrowType.StoneArrow;
					else if (randomValue < 0.498f) value = ArrowBlock.ArrowType.IronArrow;
					else if (randomValue < 0.664f) value = ArrowBlock.ArrowType.DiamondArrow;
					else if (randomValue < 0.83f) value = ArrowBlock.ArrowType.FireArrow;
					else value = ArrowBlock.ArrowType.CopperArrow;

					data = BowBlock.SetArrowType(0, new ArrowBlock.ArrowType?(value));
					data = BowBlock.SetDraw(data, 15);
				}

				int value2 = Terrain.MakeBlockValue(num2, 0, data);
				componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				componentMiner.Inventory.AddSlotItems(slotIndex, value2, 1);
			}
		}

		public bool FindHitTool(ComponentMiner componentMiner)
		{
			int activeBlockValue = componentMiner.ActiveBlockValue;
			if (componentMiner.Inventory == null)
			{
				return false;
			}

			if (BlocksManager.Blocks[Terrain.ExtractContents(activeBlockValue)].GetMeleePower(activeBlockValue) > 1f)
			{
				return true;
			}

			float num = 1f;
			int activeSlotIndex = 0;
			for (int i = 0; i < 6; i++)
			{
				int slotValue = componentMiner.Inventory.GetSlotValue(i);
				float meleePower = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)].GetMeleePower(slotValue);
				if (meleePower > num)
				{
					num = meleePower;
					activeSlotIndex = i;
				}
			}

			if (num > 1f)
			{
				componentMiner.Inventory.ActiveSlotIndex = activeSlotIndex;
				return true;
			}

			return false;
		}

		public virtual bool IsTargetInAttackRange(ComponentBody target)
		{
			if (this.IsBodyInAttackRange(target))
			{
				return true;
			}

			BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox boundingBox2 = target.BoundingBox;
			Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
			Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max) - v;
			float num = vector.Length();
			Vector3 v2 = vector / num;
			float num2 = 0.5f * (boundingBox.Max.X - boundingBox.Min.X + boundingBox2.Max.X - boundingBox2.Min.X);
			float num3 = 0.5f * (boundingBox.Max.Y - boundingBox.Min.Y + boundingBox2.Max.Y - boundingBox2.Min.Y);

			if (MathF.Abs(vector.Y) < num3 * 0.99f)
			{
				if (num < num2 + 0.99f && Vector3.Dot(v2, this.m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
				{
					return true;
				}
			}
			else
			{
				if (num < num3 + 0.3f && MathF.Abs(Vector3.Dot(v2, Vector3.UnitY)) > 0.8f)
				{
					return true;
				}
			}

			return ((target.ParentBody != null && this.IsTargetInAttackRange(target.ParentBody)) ||
					(this.AllowAttackingStandingOnBody && target.StandingOnBody != null &&
					 target.StandingOnBody.Position.Y < target.Position.Y && this.IsTargetInAttackRange(target.StandingOnBody)));
		}

		public virtual bool IsBodyInAttackRange(ComponentBody target)
		{
			BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox boundingBox2 = target.BoundingBox;
			Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
			Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max) - v;
			float num = vector.Length();
			Vector3 v2 = vector / num;
			float num2 = 0.5f * (boundingBox.Max.X - boundingBox.Min.X + boundingBox2.Max.X - boundingBox2.Min.X);
			float num3 = 0.5f * (boundingBox.Max.Y - boundingBox.Min.Y + boundingBox2.Max.Y - boundingBox2.Min.Y);

			if (MathF.Abs(vector.Y) < num3 * 0.99f)
			{
				if (num < num2 + 0.99f && Vector3.Dot(v2, this.m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
				{
					return true;
				}
			}
			else
			{
				if (num < num3 + 0.3f && MathF.Abs(Vector3.Dot(v2, Vector3.UnitY)) > 0.8f)
				{
					return true;
				}
			}

			return false;
		}

		public virtual ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			hitPoint = Vector3.Zero;
			Vector3 vector = this.m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 v = target.BoundingBox.Center();
			Ray3 ray = new Ray3(vector, Vector3.Normalize(v - vector));
			BodyRaycastResult? bodyRaycastResult = this.m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);

			if (bodyRaycastResult != null && bodyRaycastResult.Value.Distance < this.MaxAttackRange &&
				(bodyRaycastResult.Value.ComponentBody == target ||
				 bodyRaycastResult.Value.ComponentBody.IsChildOfBody(target) ||
				 target.IsChildOfBody(bodyRaycastResult.Value.ComponentBody) ||
				 (target.StandingOnBody == bodyRaycastResult.Value.ComponentBody && this.AllowAttackingStandingOnBody)))
			{
				hitPoint = bodyRaycastResult.Value.HitPoint();
				return bodyRaycastResult.Value.ComponentBody;
			}

			return null;
		}

		public virtual ComponentCreature FindTarget()
		{
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentCreature result = null;
			float num = 0f;
			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), this.m_range, this.m_componentBodies);

			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentCreature componentCreature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (componentCreature != null)
				{
					// Verificar si no es del mismo rebaño zombi
					bool canAttack = true;
					ComponentZombieHerdBehavior thisHerd = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (thisHerd != null && !string.IsNullOrEmpty(thisHerd.HerdName))
					{
						ComponentZombieHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentZombieHerdBehavior>();
						if (targetHerd != null && targetHerd.HerdName == thisHerd.HerdName)
						{
							canAttack = this.AttacksSameHerd; // Solo atacar si AttacksSameHerd es true
						}
					}

					// Verificar modo de juego para jugadores (ataques no provocados)
					bool isPlayer = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
					if (isPlayer)
					{
						GameMode currentGameMode = this.m_subsystemGameInfo.WorldSettings.GameMode;
						if (currentGameMode == GameMode.Creative || currentGameMode == GameMode.Harmless)
						{
							canAttack = false; // No atacar jugadores en Creative o Harmless (solo ataques no provocados)
						}
					}

					if (canAttack)
					{
						float num2 = this.ScoreTarget(componentCreature);
						if (num2 > num)
						{
							num = num2;
							result = componentCreature;
						}
					}
				}
			}
			return result;
		}

		public virtual float ScoreTarget(ComponentCreature componentCreature)
		{
			float result = 0f;

			// Los zombis atacan a todos (jugadores y criaturas) sin importar la categoría
			bool isPlayer = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
			bool canAttack = true;

			// Verificar si es del mismo rebaño zombi
			ComponentZombieHerdBehavior thisHerd = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
			if (thisHerd != null && !string.IsNullOrEmpty(thisHerd.HerdName))
			{
				ComponentZombieHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentZombieHerdBehavior>();
				if (targetHerd != null && targetHerd.HerdName == thisHerd.HerdName)
				{
					canAttack = this.AttacksSameHerd; // Solo atacar si AttacksSameHerd es true
				}
			}

			// Verificar modo de juego para jugadores (ataques no provocados)
			if (isPlayer)
			{
				GameMode currentGameMode = this.m_subsystemGameInfo.WorldSettings.GameMode;
				if (currentGameMode == GameMode.Creative || currentGameMode == GameMode.Harmless)
				{
					canAttack = false; // No atacar jugadores en Creative o Harmless (solo ataques no provocados)
				}
			}

			bool flag6 = componentCreature != this.m_componentCreature &&
						 canAttack &&
						 componentCreature.Entity.IsAddedToProject &&
						 componentCreature.ComponentHealth.Health > 0f;

			if (flag6)
			{
				float num = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, componentCreature.ComponentBody.Position);
				if (num < this.m_range)
				{
					result = this.m_range - num;
				}
			}
			return result;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Cargar subsistemas y componentes
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			this.m_subsystemCreatureSpawn = base.Project.FindSubsystem<SubsystemCreatureSpawn>(true);

			// AÑADIDO: Inicializar SubsystemAudio - asegúrate de que el subsistema existe
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>();

			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			this.m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			this.m_componentFeedBehavior = base.Entity.FindComponent<ComponentRandomFeedBehavior>();
			this.m_componentCreatureModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			this.m_componentFactors = base.Entity.FindComponent<ComponentFactors>(true);
			this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);

			// Configuración específica de zombis
			string attackModeString = valuesDictionary.GetValue<string>("AttackMode", "Default");
			if (Enum.TryParse<AttackMode>(attackModeString, true, out AttackMode parsedAttackMode))
			{
				this.m_attackMode = parsedAttackMode;
			}
			else
			{
				this.m_attackMode = AttackMode.Default;
			}

			this.m_attackRange = valuesDictionary.GetValue<Vector2>("AttackRange", new Vector2(2f, 15f));
			this.m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
			this.m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
			this.m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
			this.m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");

			// Los zombis atacan a todas las categorías por defecto
			this.m_autoChaseMask = CreatureCategory.LandPredator | CreatureCategory.LandOther |
								   CreatureCategory.WaterPredator | CreatureCategory.WaterOther |
								   CreatureCategory.Bird;

			// CORRECCIÓN: Cargar probabilidades del XML en lugar de valores fijos
			this.m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability", 0);
			this.m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability", 0f);
			this.m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability", 0f);

			// Obtener el nombre del rebaño zombi del ComponentHerdBehavior
			ComponentZombieHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
			if (herdBehavior != null)
			{
				this.ZombieHerdName = herdBehavior.HerdName;
			}

			// Configurar eventos y máquina de estados
			this.SetupEventHooks();
			this.SetupStateMachine();
		}

		private void SetupEventHooks()
		{
			ComponentBody componentBody = this.m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.Combine(componentBody.CollidedWithBody, new Action<ComponentBody>(delegate (ComponentBody body)
			{
				bool flag = this.m_target == null && this.m_autoChaseSuppressionTime <= 0f && this.m_random.Float(0f, 1f) < this.m_chaseOnTouchProbability;
				if (flag)
				{
					ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						bool flag3 = this.m_subsystemPlayers.IsPlayer(body.Entity);
						bool flag4 = this.m_autoChaseMask > (CreatureCategory)0;

						// Verificar si es del mismo rebaño
						bool canAttack = true;
						ComponentZombieHerdBehavior thisHerd = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
						if (thisHerd != null && !string.IsNullOrEmpty(thisHerd.HerdName))
						{
							ComponentZombieHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentZombieHerdBehavior>();
							if (targetHerd != null && targetHerd.HerdName == thisHerd.HerdName)
							{
								canAttack = this.AttacksSameHerd;
							}
						}

						// Verificar modo de juego para jugadores (contacto)
						if (flag3)
						{
							GameMode currentGameMode = this.m_subsystemGameInfo.WorldSettings.GameMode;
							// NO atacar por contacto en Creative/Harmless
							if (currentGameMode == GameMode.Creative || currentGameMode == GameMode.Harmless)
							{
								canAttack = false;
							}
						}

						bool flag7 = canAttack && ((this.AttacksPlayer && flag3 && this.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
												   (this.AttacksNonPlayerCreature && !flag3 && flag4));
						if (flag7)
						{
							this.Attack(componentCreature, this.ChaseRangeOnTouch, this.ChaseTimeOnTouch, false);
						}
					}
				}
			}));

			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(delegate (Injury injury)
			{
				bool flag = injury.Attacker == null || this.m_random.Float(0f, 1f) >= this.m_chaseWhenAttackedProbability;
				if (!flag)
				{
					float maxRange = this.ChaseRangeOnAttacked ?? 30f;
					float maxChaseTime = this.ChaseTimeOnAttacked ?? 60f;
					bool isPersistent = this.ChasePersistentOnAttacked ?? true;

					// Permitir retaliación incluso en Creative/Harmless
					// NO verificar modo de juego para retaliación

					// Verificar si el atacante es del mismo rebaño
					bool canAttack = true;
					ComponentZombieHerdBehavior thisHerd = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (thisHerd != null && !string.IsNullOrEmpty(thisHerd.HerdName))
					{
						ComponentZombieHerdBehavior attackerHerd = injury.Attacker.Entity.FindComponent<ComponentZombieHerdBehavior>();
						if (attackerHerd != null && attackerHerd.HerdName == thisHerd.HerdName)
						{
							canAttack = this.AttacksSameHerd;
						}
					}

					if (canAttack)
					{
						// Pasar true como isRetaliation para permitir ataque en Creative/Harmless
						this.Attack(injury.Attacker, maxRange, maxChaseTime, isPersistent, true);
					}
				}
			}));
		}

		private void SetupStateMachine()
		{
			this.m_stateMachine.AddState("LookingForTarget", delegate
			{
				this.m_importanceLevel = 0f;
				this.m_target = null;
			}, delegate
			{
				if (this.IsActive)
				{
					this.m_stateMachine.TransitionTo("Chasing");
				}
				else
				{
					bool flag = !this.Suppressed && this.m_autoChaseSuppressionTime <= 0f &&
							   (this.m_target == null || this.ScoreTarget(this.m_target) <= 0f) &&
							   this.m_componentCreature.ComponentHealth.Health > this.MinHealthToAttackActively;
					if (flag)
					{
						this.m_range = ((this.m_subsystemSky.SkyLightIntensity < 0.2f) ? this.m_nightChaseRange : this.m_dayChaseRange);
						this.m_range *= this.m_componentFactors.GetOtherFactorResult("ChaseRange", false, false);
						ComponentCreature componentCreature = this.FindTarget();
						if (componentCreature != null)
						{
							this.m_targetInRangeTime += this.m_dt;
						}
						else
						{
							this.m_targetInRangeTime = 0f;
						}
						if (this.m_targetInRangeTime > this.TargetInRangeTimeToChase)
						{
							bool flag4 = this.m_subsystemSky.SkyLightIntensity >= 0.1f;
							float maxRange = flag4 ? (this.m_dayChaseRange + 6f) : (this.m_nightChaseRange + 6f);
							float maxChaseTime = flag4 ? (this.m_dayChaseTime * this.m_random.Float(0.75f, 1f)) :
													   (this.m_nightChaseTime * this.m_random.Float(0.75f, 1f));
							this.Attack(componentCreature, maxRange, maxChaseTime, !flag4);
						}
					}
				}
			}, null);

			this.m_stateMachine.AddState("RandomMoving", delegate
			{
				this.m_componentPathfinding.SetDestination(new Vector3?(this.m_componentCreature.ComponentBody.Position + new Vector3(6f * this.m_random.Float(-1f, 1f), 0f, 6f * this.m_random.Float(-1f, 1f))), 1f, 1f, 0, false, true, false, null);
			}, delegate
			{
				bool flag = !this.m_componentPathfinding.IsStuck || this.m_componentPathfinding.Destination == null;
				if (flag)
				{
					this.m_stateMachine.TransitionTo("Chasing");
				}
				bool flag2 = !this.IsActive;
				if (flag2)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}
			}, delegate
			{
				this.m_componentPathfinding.Stop();
			});

			this.m_stateMachine.AddState("Chasing", delegate
			{
				this.m_subsystemNoise.MakeNoise(this.m_componentCreature.ComponentBody, 0.25f, 6f);
				bool playIdleSoundWhenStartToChase = this.PlayIdleSoundWhenStartToChase;
				if (playIdleSoundWhenStartToChase)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				}
				this.m_nextUpdateTime = 0.0;
			}, delegate
			{
				bool flag = !this.IsActive;
				if (flag)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}
				else
				{
					bool flag2 = this.m_chaseTime <= 0f;
					if (flag2)
					{
						this.m_autoChaseSuppressionTime = this.m_random.Float(10f, 60f);
						this.m_importanceLevel = 0f;
					}
					else
					{
						bool flag3 = this.m_target == null;
						if (flag3)
						{
							this.m_importanceLevel = 0f;
						}
						else
						{
							bool flag4 = this.m_target.ComponentHealth.Health <= 0f;
							if (flag4)
							{
								bool flag5 = this.m_componentFeedBehavior != null;
								if (flag5)
								{
									this.m_subsystemTime.QueueGameTimeDelayedExecution(this.m_subsystemTime.GameTime + (double)this.m_random.Float(1f, 3f), delegate
									{
										bool flag16 = this.m_target != null;
										if (flag16)
										{
											this.m_componentFeedBehavior.Feed(this.m_target.ComponentBody.Position);
										}
									});
								}
								this.m_importanceLevel = 0f;
							}
							else
							{
								bool flag6 = !this.m_isPersistent && this.m_componentPathfinding.IsStuck;
								if (flag6)
								{
									this.m_importanceLevel = 0f;
								}
								else
								{
									bool flag7 = this.m_isPersistent && this.m_componentPathfinding.IsStuck;
									if (flag7)
									{
										this.m_stateMachine.TransitionTo("RandomMoving");
									}
									else
									{
										bool flag8 = this.ScoreTarget(this.m_target) <= 0f;
										if (flag8)
										{
											this.m_targetUnsuitableTime += this.m_dt;
										}
										else
										{
											this.m_targetUnsuitableTime = 0f;
										}
										bool flag9 = this.m_targetUnsuitableTime > 3f;
										if (flag9)
										{
											this.m_importanceLevel = 0f;
										}
										else
										{
											int maxPathfindingPositions = 0;
											bool isPersistent = this.m_isPersistent;
											if (isPersistent)
											{
												maxPathfindingPositions = ((this.m_subsystemTime.FixedTimeStep != null) ? 2000 : 500);
											}
											BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
											BoundingBox boundingBox2 = this.m_target.ComponentBody.BoundingBox;
											Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
											Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max);
											float num = Vector3.Distance(v, vector);
											float num2 = (num < 4f) ? 0.2f : 0f;

											bool hasZombieHerd = base.Entity.FindComponent<ComponentZombieHerdBehavior>() != null;
											bool flag10 = (this.m_attackMode != AttackMode.OnlyHand && num > 5f && this.FindAimTool(this.m_componentMiner)) ||
														   (hasZombieHerd && num > 5f && this.FindAimTool(this.m_componentMiner));

											if (flag10)
											{
												float num3;
												ComponentBody hitBody = this.GetHitBody1(this.m_target.ComponentBody, out num3);
												bool flag11 = hitBody != null && num3 >= this.m_attackRange.X && num3 <= this.m_attackRange.Y;
												if (flag11)
												{
													float num4 = (this.m_attackRange.X + this.m_attackRange.Y) * 0.5f;
													bool flag12 = Math.Abs(num3 - num4) > 2f;
													if (flag12)
													{
														bool flag13 = num3 > num4;
														if (flag13)
														{
															this.m_componentPathfinding.SetDestination(new Vector3?(vector + num2 * num * this.m_target.ComponentBody.Velocity), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
														}
														else
														{
															Vector3 v2 = Vector3.Normalize(this.m_componentCreature.ComponentBody.Position - this.m_target.ComponentBody.Position);
															Vector3 value = this.m_componentCreature.ComponentBody.Position + v2 * 3f;
															this.m_componentPathfinding.SetDestination(new Vector3?(value), 1f, 1f, 0, false, true, false, null);
														}
													}
													else
													{
														this.m_componentPathfinding.Destination = null;
													}
												}
												else
												{
													bool flag14 = num3 > this.m_attackRange.Y;
													if (flag14)
													{
														this.m_componentPathfinding.SetDestination(new Vector3?(vector + num2 * num * this.m_target.ComponentBody.Velocity), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
													}
												}
											}
											else
											{
												this.m_componentPathfinding.SetDestination(new Vector3?(vector + num2 * num * this.m_target.ComponentBody.Velocity), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
											}
											bool flag15 = this.PlayAngrySoundWhenChasing && this.m_random.Float(0f, 1f) < 0.33f * this.m_dt;
											if (flag15)
											{
												this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
											}
										}
									}
								}
							}
						}
					}
				}
			}, null);

			this.m_stateMachine.TransitionTo("LookingForTarget");
		}

		// Campos - NOTA: m_subsystemAudio YA está declarado aquí, no lo añadas de nuevo
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemPlayers m_subsystemPlayers;
		public SubsystemSky m_subsystemSky;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemTime m_subsystemTime;
		public SubsystemNoise m_subsystemNoise;
		public SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public ComponentMiner m_componentMiner;
		public ComponentRandomFeedBehavior m_componentFeedBehavior;
		public ComponentCreatureModel m_componentCreatureModel;
		public ComponentFactors m_componentFactors;
		public ComponentBody m_componentBody;
		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		public Random m_random = new Random();
		public StateMachine m_stateMachine = new StateMachine();
		public float m_dayChaseRange;
		public float m_nightChaseRange;
		public float m_dayChaseTime;
		public float m_nightChaseTime;
		public float m_chaseNonPlayerProbability;
		public float m_chaseWhenAttackedProbability;
		public float m_chaseOnTouchProbability;
		public CreatureCategory m_autoChaseMask;
		public float m_importanceLevel;
		public float m_targetUnsuitableTime;
		public float m_targetInRangeTime;
		public double m_nextUpdateTime;
		public double m_nextPlayerCheckTime;
		public double m_lastActionTime;
		public ComponentCreature m_target;
		public float m_dt;
		public float m_range;
		public float m_chaseTime;
		public bool m_isPersistent;
		public bool m_autoDismount = true;
		public float m_autoChaseSuppressionTime;
		private AttackMode m_attackMode = AttackMode.Default;
		private Vector2 m_attackRange = new Vector2(2f, 15f);
		public float ImportanceLevelNonPersistent = 200f;
		public float ImportanceLevelPersistent = 200f;
		public float MaxAttackRange = 1.75f;
		public bool AllowAttackingStandingOnBody = true;
		public bool JumpWhenTargetStanding = true;
		public bool AttacksPlayer = true;
		public bool AttacksNonPlayerCreature = true;
		public float ChaseRangeOnTouch = 7f;
		public float ChaseTimeOnTouch = 7f;
		public float? ChaseRangeOnAttacked;
		public float? ChaseTimeOnAttacked;
		public bool? ChasePersistentOnAttacked;
		public float MinHealthToAttackActively = 0.4f;
		public bool Suppressed;
		public bool PlayIdleSoundWhenStartToChase = true;
		public bool PlayAngrySoundWhenChasing = true;
		public float TargetInRangeTimeToChase = 3f;

		// AÑADIR m_subsystemAudio SOLO AQUÍ, en la sección de campos
		public SubsystemAudio m_subsystemAudio;

		public enum AttackMode
		{
			Default,
			OnlyHand,
			Ranged
		}
	}
}
