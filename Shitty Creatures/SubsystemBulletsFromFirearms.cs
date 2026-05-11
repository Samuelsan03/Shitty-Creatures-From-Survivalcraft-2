using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemBulletsFromFirearms : SubsystemBlockBehavior
	{
		int[] m_handledBlocks = Array.Empty<int>();

		public override int[] HandledBlocks => m_handledBlocks;

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			// Siempre suena el ricochet
			if (cellFace != null)
			{
				m_subsystemAudio.PlayRandomSound(
					"Audio/Ricochets",
					1f,
					m_random.Float(-0.2f, 0.2f),
					new Vector3(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z),
					8f,
					true);
			}
			return true; // La bala se destruye, no rebota
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);

			var indices = new List<int>();
			Type[] bulletTypes = new Type[]
			{
				typeof(NuevaBala), typeof(NuevaBala2), typeof(NuevaBala3),
				typeof(NuevaBala4), typeof(NuevaBala5), typeof(NuevaBala6)
			};

			foreach (Block block in BlocksManager.Blocks)
			{
				if (block != null && Array.IndexOf(bulletTypes, block.GetType()) >= 0)
					indices.Add(block.BlockIndex);
			}
			m_handledBlocks = indices.ToArray();
		}

		SubsystemAudio m_subsystemAudio;
		Random m_random = new Random();
	}
}
