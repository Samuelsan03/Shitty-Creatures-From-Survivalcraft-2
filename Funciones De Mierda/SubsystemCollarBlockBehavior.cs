using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemCollarBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					437
				};
			}
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			BodyRaycastResult? bodyRaycastResult = componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
			if (bodyRaycastResult == null)
			{
				return false;
			}
			Entity entity = bodyRaycastResult.Value.ComponentBody.Entity;
			ComponentHealth componentHealth = entity.FindComponent<ComponentHealth>();
			if (componentHealth != null && componentHealth.Health <= 0f)
			{
				return false;
			}

			// ESPACIO PARA VERIFICAR NPC ESPECÍFICO
			// ComponentCreature componentCreature = entity.FindComponent<ComponentCreature>();
			// if (componentCreature == null || componentCreature.DisplayName != "NPC_ESPECIFICO")
			// {
			//     return false;
			// }

			string entityTemplateName = SubsystemCollarBlockBehavior.CollarVariants[this.m_random.Next(SubsystemCollarBlockBehavior.CollarVariants.Length)];
			Entity entity2 = DatabaseManager.CreateEntity(base.Project, entityTemplateName, false);
			if (entity2 == null)
			{
				return true;
			}
			ComponentBody componentBody = entity2.FindComponent<ComponentBody>(true);
			componentBody.Position = bodyRaycastResult.Value.ComponentBody.Position;
			componentBody.Rotation = bodyRaycastResult.Value.ComponentBody.Rotation;
			componentBody.Velocity = bodyRaycastResult.Value.ComponentBody.Velocity;
			entity2.FindComponent<ComponentSpawn>(true).SpawnDuration = 0f;
			base.Project.RemoveEntity(entity, true);
			base.Project.AddEntity(entity2);
			float pitch = (float)(this.m_random.NextDouble() * 0.2 - 0.1);
			this.m_subsystemAudio.PlaySound("Audio/EFECTO_DE_SONIDO", 1f, pitch, ray.Position, 1f, true);
			componentMiner.RemoveActiveTool(1);
			return true;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
		}

		private SubsystemAudio m_subsystemAudio;
		private System.Random m_random = new System.Random();

		// ESPACIO PARA VARIANTES DE COLLAR DE NPC
		private static readonly string[] CollarVariants = new string[]
		{
			"NPC_Collar_1",
			"NPC_Collar_2",
			"NPC_Collar_3",
			"NPC_Collar_4",
			"NPC_Collar_5",
			"NPC_Collar_6",
			"NPC_Collar_7"
		};
	}
}
