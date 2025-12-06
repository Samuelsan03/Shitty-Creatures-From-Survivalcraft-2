using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureCollect : Component, IUpdateable
	{
		public UpdateOrder UpdateOrder
		{
			get
			{
				return 0;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			this.subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.subsystemPickables = base.Project.FindSubsystem<SubsystemPickables>(true);
			this.m_audio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.componentCreature = base.Entity.FindComponent<ComponentCreature>();
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);

			// Obtener ComponentModel aquí para no buscarlo cada frame
			this.componentModel = base.Entity.FindComponent<ComponentModel>();

			this.Activate = valuesDictionary.GetValue<bool>("Activate");
			this.CanOpenInventory = valuesDictionary.GetValue<bool>("CanOpenInventory");
			this.DetectionDistance = valuesDictionary.GetValue<float>("DetectionDistance");
			this.SpecificItems = valuesDictionary.GetValue<string>("SpecificItems");
			this.IgnoreOrAcept = valuesDictionary.GetValue<bool>("IgnoreOrAcept");

			string[] array = this.SpecificItems.Split(',', StringSplitOptions.None);
			foreach (string text in array)
			{
				bool flag = string.IsNullOrWhiteSpace(text);
				if (!flag)
				{
					int num;
					bool flag2 = int.TryParse(text, out num);
					if (flag2)
					{
						bool flag3 = num > 0 && num < BlocksManager.Blocks.Length;
						if (flag3)
						{
							this.m_specificItemsSet.Add(num);
						}
					}
					else
					{
						Type type = Type.GetType("Game." + text);
						Block block = BlocksManager.GetBlock(type, false, true);
						bool flag4 = block != null;
						if (flag4)
						{
							this.m_specificItemsSet.Add(block.BlockIndex);
						}
					}
				}
			}
		}

		public void Update(float dt)
		{
			IInventory inventory = this.m_componentMiner.Inventory;
			ComponentInventory componentInventory = base.Entity.FindComponent<ComponentInventory>();
			ComponentChaseBehavior componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>();

			// --- CONTROL DE ANIMACIÓN DE MANO ---
			if (m_handAnimationTimer > 0f)
			{
				m_handAnimationTimer -= dt;
				// Interpolación senoidal para movimiento suave (sube y baja)
				float handLift = MathUtils.Sin(m_handAnimationTimer * 12f) * 0.15f;
				ApplyHandAnimation(handLift);
			}
			else if (m_handAnimationTimer <= 0f && m_handAnimationTimer != -1f)
			{
				// Restaurar posición original
				ResetHandAnimation();
				m_handAnimationTimer = -1f;
			}

			bool flag = !this.Activate;
			if (!flag)
			{
				foreach (Pickable pickable in this.subsystemPickables.Pickables)
				{
					int item = Terrain.ExtractContents(pickable.Value);
					bool flag2 = this.m_specificItemsSet.Contains(item);
					bool flag3 = this.m_specificItemsSet.Count > 0;
					if (flag3)
					{
						bool flag4 = this.IgnoreOrAcept && !flag2;
						if (flag4)
						{
							continue;
						}
						bool flag5 = !this.IgnoreOrAcept && flag2;
						if (flag5)
						{
							continue;
						}
					}
					TerrainChunk chunkAtCell = this.subsystemTerrain.Terrain.GetChunkAtCell(Terrain.ToCell(pickable.Position.X), Terrain.ToCell(pickable.Position.Z));
					bool flag6 = componentInventory != null && chunkAtCell != null && pickable.FlyToPosition == null && (double)this.componentCreature.ComponentHealth.Health > 0.0 && componentChaseBehavior.Target == null;
					if (flag6)
					{
						Vector3 vector = this.componentCreature.ComponentBody.Position + new Vector3(0f, 0.8f, 0f);
						float num = (vector - pickable.Position).LengthSquared();
						float num2 = this.DetectionDistance * this.DetectionDistance;
						bool flag7 = num < num2;
						if (flag7)
						{
							for (int i = 0; i < inventory.SlotsCount; i++)
							{
								int num3 = ComponentInventoryBase.FindAcquireSlotForItem(inventory, pickable.Value);
								bool flag8 = num3 >= 0;
								if (flag8)
								{
									this.m_componentPathfinding.SetDestination(new Vector3?(pickable.Position), 3f, 3.75f, 20, true, false, false, null);
									break;
								}
							}
						}
						bool flag9 = (double)num < 4.0;
						if (flag9)
						{
							for (int j = 0; j < inventory.SlotsCount; j++)
							{
								int num4 = ComponentInventoryBase.FindAcquireSlotForItem(inventory, pickable.Value);
								bool flag10 = num4 >= 0;
								if (flag10)
								{
									pickable.ToRemove = true;
									pickable.FlyToPosition = new Vector3?(vector);
									pickable.Count = ComponentInventoryBase.AcquireItems(componentInventory, pickable.Value, pickable.Count);
									this.m_audio.PlaySound("Audio/PickableCollected", 1f, -0.4f, vector, 6f, false);

									// --- ACTIVAR ANIMACIÓN DE MANO ---
									m_handAnimationTimer = 0.5f; // Duración de 0.5 segundos
									break;
								}
							}
						}
					}
				}
			}
		}

		// --- NUEVOS MÉTODOS PARA ANIMACIÓN DE MANO ---
		private void ApplyHandAnimation(float liftAmount)
		{
			if (componentModel?.Model == null) return;

			// Buscar el hueso de la mano derecha
			ModelBone handBone = FindBone("RightHand", "Hand.R", "Right_Hand", "hand_r", "HandR");
			if (handBone != null)
			{
				// Guardar la transformación original si no la tenemos
				if (m_originalHandTransform == Matrix.Zero && componentModel.GetBoneTransform(handBone.Index).HasValue)
				{
					m_originalHandTransform = componentModel.GetBoneTransform(handBone.Index).Value;
				}

				// Aplicar rotación local a la mano
				Matrix handRotation = Matrix.CreateRotationX(liftAmount);
				Matrix newTransform = handRotation * m_originalHandTransform;
				componentModel.SetBoneTransform(handBone.Index, newTransform);

				// Buscar y animar también el hueso del antebrazo para un movimiento más natural
				ModelBone forearmBone = FindBone("RightForeArm", "ForeArm.R", "Right_ForeArm", "forearm_r", "ForeArmR");
				if (forearmBone != null)
				{
					// Guardar la transformación original si no la tenemos
					if (m_originalForearmTransform == Matrix.Zero && componentModel.GetBoneTransform(forearmBone.Index).HasValue)
					{
						m_originalForearmTransform = componentModel.GetBoneTransform(forearmBone.Index).Value;
					}

					// Aplicar menos rotación al antebrazo
					Matrix forearmRotation = Matrix.CreateRotationX(liftAmount * 0.3f);
					Matrix newForearmTransform = forearmRotation * m_originalForearmTransform;
					componentModel.SetBoneTransform(forearmBone.Index, newForearmTransform);
				}

				// Marcar que el modelo necesita ser animado
				componentModel.Animated = true;
			}
		}

		private void ResetHandAnimation()
		{
			if (componentModel?.Model == null) return;

			// Restaurar transformaciones originales
			ModelBone handBone = FindBone("RightHand", "Hand.R", "Right_Hand", "hand_r", "HandR");
			if (handBone != null && m_originalHandTransform != Matrix.Zero)
			{
				componentModel.SetBoneTransform(handBone.Index, m_originalHandTransform);
			}

			ModelBone forearmBone = FindBone("RightForeArm", "ForeArm.R", "Right_ForeArm", "forearm_r", "ForeArmR");
			if (forearmBone != null && m_originalForearmTransform != Matrix.Zero)
			{
				componentModel.SetBoneTransform(forearmBone.Index, m_originalForearmTransform);
			}

			if (handBone != null || forearmBone != null)
			{
				componentModel.Animated = true;
			}
		}

		private ModelBone FindBone(params string[] boneNames)
		{
			if (componentModel?.Model == null) return null;

			foreach (string boneName in boneNames)
			{
				ModelBone bone = componentModel.Model.FindBone(boneName);
				if (bone != null)
				{
					return bone;
				}
			}
			return null;
		}

		public SubsystemTerrain subsystemTerrain;
		public SubsystemPickables subsystemPickables;
		public ComponentCreature componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		public ComponentMiner m_componentMiner;
		public bool Activate;
		public bool CanOpenInventory;
		public float DetectionDistance;
		public string SpecificItems;
		public bool IgnoreOrAcept;
		public SubsystemAudio m_audio;
		private HashSet<int> m_specificItemsSet = new HashSet<int>();

		// --- NUEVAS VARIABLES PARA ANIMACIÓN ---
		private ComponentModel componentModel;
		private float m_handAnimationTimer = -1f; // -1 = inactivo
		private Matrix m_originalHandTransform = Matrix.Zero;
		private Matrix m_originalForearmTransform = Matrix.Zero;
	}
}
