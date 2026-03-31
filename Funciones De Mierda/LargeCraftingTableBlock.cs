using System;
using Engine;

namespace Game
{
	public class LargeCraftingTableBlock : NewCubeBlock
	{
		public static int Index = 417; // Índice único

		public LargeCraftingTableBlock() : base("Textures/ShittyCreaturesTextures") { }

		public override int GetFaceTextureSlot(int face, int value)
		{
			return face == 4 ? 3 : 4; // Cara superior diferente
		}

		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale, Color.White, GetFaceTextureSlot(5, value), m_texture);
		}
	}
}
