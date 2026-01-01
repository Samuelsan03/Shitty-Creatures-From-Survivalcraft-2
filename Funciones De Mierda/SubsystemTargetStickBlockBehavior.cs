using System;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemTargetStickBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					827  // Debe coincidir con TargetStickBlock.Index
                };
			}
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			object obj = componentMiner.Raycast(ray, RaycastMode.Interaction, true, true, true, null);
			if (!(obj is BodyRaycastResult))
			{
				return false;
			}
			BodyRaycastResult bodyRaycastResult = (BodyRaycastResult)obj;
			ComponentCreature componentCreature = bodyRaycastResult.ComponentBody.Entity.FindComponent<ComponentCreature>();
			if (componentCreature == null || bodyRaycastResult.ComponentBody.Entity.FindComponent<ComponentPlayer>() != null)
			{
				return false;
			}

			// Solo reproducir el sonido sin la lógica de defensa
			this.m_subsystemAudio.PlaySound("Audio/UI/Fight", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentBody.Position, 2f, true);

			return true;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
		}

		private SubsystemAudio m_subsystemAudio;
		private readonly Game.Random m_random = new Game.Random();
		private const int TargeterBlockID = 827; // Debe coincidir
	}
}
