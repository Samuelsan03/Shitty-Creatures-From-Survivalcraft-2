using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemCandyBlockBehavior : SubsystemBlockBehavior, IUpdateable
	{
		public class VomitState
		{
			public bool Active;
			public CandyBlock.CandyType CandyType;
			public ParticleSystemBase ParticleSystem;
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { CandyBlock.Index };
			}
		}

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemSoundMaterials m_subsystemSoundMaterials;
		private SubsystemTime m_subsystemTime;

		private Dictionary<ComponentPlayer, VomitState> m_states = new Dictionary<ComponentPlayer, VomitState>();
		private List<ComponentPlayer> m_toRemove = new List<ComponentPlayer>();

		public VomitState GetVomitState(ComponentPlayer player)
		{
			if (player == null)
			{
				return null;
			}
			VomitState vomitState;
			if (!m_states.TryGetValue(player, out vomitState))
			{
				vomitState = new VomitState();
				m_states[player] = vomitState;
			}
			return vomitState;
		}

		public void GrantVomitBreath(ComponentPlayer player, CandyBlock.CandyType type)
		{
			if (player == null)
			{
				return;
			}
			VomitState vomitState = GetVomitState(player);
			vomitState.Active = true;
			vomitState.CandyType = type;
		}

		public void RemoveVomitBreath(ComponentPlayer player)
		{
			if (player == null)
			{
				return;
			}
			VomitState vomitState = GetVomitState(player);
			SetStopped(vomitState.ParticleSystem, true);
			vomitState.ParticleSystem = null;
			vomitState.Active = false;
		}

		private bool GetStopped(ParticleSystemBase ps)
		{
			if (ps is FireVomitParticleSystem fire) return fire.IsStopped;
			if (ps is PoisonVomitParticleSystem poison) return poison.IsStopped;
			if (ps is FrozenVomitParticleSystem frozen) return frozen.IsStopped;
			if (ps is BloodVomitParticleSystem blood) return blood.IsStopped;
			return true;
		}

		private void SetStopped(ParticleSystemBase ps, bool value)
		{
			if (ps is FireVomitParticleSystem fire) fire.IsStopped = value;
			else if (ps is PoisonVomitParticleSystem poison) poison.IsStopped = value;
			else if (ps is FrozenVomitParticleSystem frozen) frozen.IsStopped = value;
			else if (ps is BloodVomitParticleSystem blood) blood.IsStopped = value;
		}

		private ParticleSystemBase CreateParticleSystem(CandyBlock.CandyType type, ComponentCreature creature)
		{
			switch (type)
			{
				case CandyBlock.CandyType.FireCandy:
					return new FireVomitParticleSystem(m_subsystemTerrain, m_subsystemBodies, m_subsystemSoundMaterials, m_subsystemTime, creature);
				case CandyBlock.CandyType.PoisonCandy:
					return new PoisonVomitParticleSystem(m_subsystemTerrain, m_subsystemBodies, m_subsystemSoundMaterials, m_subsystemTime, m_subsystemParticles, creature);
				case CandyBlock.CandyType.FrozenCandy:
					return new FrozenVomitParticleSystem(m_subsystemTerrain, m_subsystemBodies, m_subsystemSoundMaterials, m_subsystemTime, creature);
				case CandyBlock.CandyType.BloodCandy:
					return new BloodVomitParticleSystem(m_subsystemTerrain, m_subsystemBodies, m_subsystemSoundMaterials, m_subsystemTime, m_subsystemParticles, creature);
				default:
					return new FireVomitParticleSystem(m_subsystemTerrain, m_subsystemBodies, m_subsystemSoundMaterials, m_subsystemTime, creature);
			}
		}

		private Vector3 GetEyePosition(ComponentPlayer player)
		{
			ComponentCreatureModel componentCreatureModel = player.Entity.FindComponent<ComponentCreatureModel>();
			if (componentCreatureModel != null)
			{
				return componentCreatureModel.EyePosition;
			}
			return player.ComponentBody.Position + new Vector3(0f, player.ComponentBody.StanceBoxSize.Y * 0.9f, 0f);
		}

		public void HandleEat(ComponentVitalStats vitalStats, ref int value, ref bool skipVanilla, out bool eatSuccess)
		{
			eatSuccess = false;
			if (Terrain.ExtractContents(value) != CandyBlock.Index)
			{
				return;
			}

			CandyBlock.CandyType candyType = CandyBlock.GetCandyType(Terrain.ExtractData(value));
			ComponentPlayer componentPlayer = vitalStats.m_componentPlayer;

			if (componentPlayer != null)
			{
				// Otorgar el aliento de vomito
				GrantVomitBreath(componentPlayer, candyType);

				// Reproducir sonido de comer (ya que saltamos vanilla)
				SubsystemAudio subsystemAudio = vitalStats.Project.FindSubsystem<SubsystemAudio>(false);
				if (subsystemAudio != null)
				{
					subsystemAudio.PlayRandomSound(
						"Audio/Creatures/HumanEat",
						1f,
						-0.1f,
						componentPlayer.ComponentBody.Position,
						2f,
						0f
					);
				}
			}

			// ✅ IMPORTANTE: Saltar toda la lógica vanilla
			// Esto evita:
			// - Añadir nutrición (Food)
			// - Añadir saciedad (m_satiation)
			// - Verificar probabilidad de enfermedad
			// - Mensajes de "estás lleno" o "te puede sentar mal"
			skipVanilla = true;
			eatSuccess = true;
		}

		public void HandleAim(ComponentPlayer player, bool isAiming, ref bool flag, ref float timeIntervalAim, bool skipVanilla, out bool outSkipVanilla)
		{
			outSkipVanilla = skipVanilla;
			if (player == null)
			{
				return;
			}
			VomitState vomitState = GetVomitState(player);
			if (!vomitState.Active)
			{
				return;
			}
			Camera activeCamera = player.GameWidget?.ActiveCamera;
			if (isAiming && activeCamera != null)
			{
				outSkipVanilla = true;
				flag = true;
				timeIntervalAim = 0.1f;
				player.ComponentAimingSights.ShowAimingSights(activeCamera.ViewPosition, activeCamera.ViewDirection);

				if (vomitState.ParticleSystem == null || !m_subsystemParticles.ContainsParticleSystem(vomitState.ParticleSystem))
				{
					ComponentCreature componentCreature = player.Entity.FindComponent<ComponentCreature>();
					if (componentCreature == null)
					{
						return;
					}
					vomitState.ParticleSystem = CreateParticleSystem(vomitState.CandyType, componentCreature);
					m_subsystemParticles.AddParticleSystem(vomitState.ParticleSystem, false);
				}

				SetParticleSystemDirection(vomitState.ParticleSystem, activeCamera.ViewPosition, activeCamera.ViewDirection);
				SetStopped(vomitState.ParticleSystem, false);
			}
			else
			{
				SetStopped(vomitState.ParticleSystem, true);
				RemoveVomitBreath(player);
			}
		}

		private void SetParticleSystemDirection(ParticleSystemBase particleSystem, Vector3 position, Vector3 direction)
		{
			if (particleSystem is FireVomitParticleSystem fireSystem)
			{
				fireSystem.Position = position;
				fireSystem.Direction = direction;
			}
			else if (particleSystem is PoisonVomitParticleSystem poisonSystem)
			{
				poisonSystem.Position = position;
				poisonSystem.Direction = direction;
			}
			else if (particleSystem is FrozenVomitParticleSystem frozenSystem)
			{
				frozenSystem.Position = position;
				frozenSystem.Direction = direction;
			}
			else if (particleSystem is BloodVomitParticleSystem bloodSystem)
			{
				bloodSystem.Position = position;
				bloodSystem.Direction = direction;
			}
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			if (componentMiner == null)
			{
				return false;
			}
			ComponentPlayer player = componentMiner.ComponentPlayer;
			if (player == null)
			{
				return false;
			}
			ComponentVitalStats vitalStats = player.ComponentVitalStats;
			if (vitalStats == null)
			{
				return false;
			}
			int slotIndex = componentMiner.Inventory.ActiveSlotIndex;
			int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
			if (Terrain.ExtractContents(slotValue) != CandyBlock.Index)
			{
				return false;
			}
			if (vitalStats.Eat(slotValue))
			{
				componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				componentMiner.Poke(false);
				return true;
			}
			return false;
		}

		public virtual void Update(float dt)
		{
			m_toRemove.Clear();
			foreach (KeyValuePair<ComponentPlayer, VomitState> keyValuePair in m_states)
			{
				ComponentPlayer key = keyValuePair.Key;
				VomitState value = keyValuePair.Value;
				if (!value.Active || key == null || !key.IsAddedToProject)
				{
					m_toRemove.Add(key);
				}
			}
			foreach (ComponentPlayer componentPlayer in m_toRemove)
			{
				if (m_states[componentPlayer].ParticleSystem != null && m_subsystemParticles != null)
				{
					m_subsystemParticles.RemoveParticleSystem(m_states[componentPlayer].ParticleSystem, false);
				}
				m_states.Remove(componentPlayer);
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemSoundMaterials = base.Project.FindSubsystem<SubsystemSoundMaterials>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
		}

		public override void Dispose()
		{
			foreach (KeyValuePair<ComponentPlayer, VomitState> keyValuePair in m_states)
			{
				if (keyValuePair.Value.ParticleSystem != null && m_subsystemParticles != null)
				{
					m_subsystemParticles.RemoveParticleSystem(keyValuePair.Value.ParticleSystem, false);
				}
			}
			m_states.Clear();
			base.Dispose();
		}
	}
}